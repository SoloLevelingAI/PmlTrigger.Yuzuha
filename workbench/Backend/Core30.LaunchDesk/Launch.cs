using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private static CoreData RunF01(FeatureContext context, CoreData request)
    {
        var init = context.FilePath(Read(request, "init"));
        ValidateInitialization(init);
        var planned = MonitorArguments(request, init);
        var received = Receive(planned, context.Input?.FixtureHost);
        var result = new CoreData("result", new CoreField("mode", "FixtureReceiver"),
            new CoreField("exitCode", received.ExitCode), new CoreField("pid", received.ProcessId),
            new CoreData("preview", Arguments(planned)),
            new CoreData("observed", Arguments(received.Arguments)));
        return result;
    }

    private static void ValidateInitialization(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Initialization fixture was not found.", path);
    }

    // Use the existing Monitor token order. The sixth argument carries a fixed fixture marker.
    private static string[] MonitorArguments(CoreData request, string init) =>
    [
        "PROD", Read(request, "product"), "init", init, Read(request, "project"),
        Read(request, "user") + "/fixture-only", "/" + Read(request, "mdb").TrimStart('/'),
        Read(request, "module")
    ];

    private static IEnumerable<CoreData> Arguments(string[] arguments) =>
        arguments.Select(argument => new CoreData("argument", argument));

    private static ProcessStartInfo ReceiverCommand(string[] arguments, string? fixtureHost)
    {
        if (fixtureHost is null)
            ValidateReceiverHost();
        var executable = fixtureHost ?? Environment.ProcessPath ?? throw new IOException("The fixture host path is unavailable.");
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.GetFullPath(Environment.GetCommandLineArgs()[0]));
        start.ArgumentList.Add("__receive-launch");
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return start;
    }

    private static void ValidateReceiverHost()
    {
        if (Assembly.GetEntryAssembly()?.GetName().Name is not ("Core30.Cli" or "Core30.LaunchDesk"))
            throw new InvalidOperationException("The fixture receiver requires the Core30.Cli or Core30.LaunchDesk host.");
    }

    private static ReceivedLaunch Receive(string[] expected, string? fixtureHost)
    {
        var command = ReceiverCommand(expected, fixtureHost);
        using var process = StartReceiver(command);
        var receipt = ReadReceipt(process);
        ValidateReceipt(receipt, expected);
        return receipt;
    }

    private static Process StartReceiver(ProcessStartInfo command) =>
        Process.Start(command) ?? throw new IOException("Receiver did not start.");

    private static ReceivedLaunch ReadReceipt(Process process)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        WaitForReceiver(process);
        var json = output.GetAwaiter().GetResult();
        var arguments = DecodeArguments(json);
        return new ReceivedLaunch(process.Id, process.ExitCode, arguments);
    }

    private static string[] DecodeArguments(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? throw new InvalidDataException("Missing receiver arguments.");

    // Process-lifecycle boundary. Terminate only the child receiver created for this operation.
    private static void WaitForReceiver(Process process)
    {
        if (process.WaitForExit(5_000))
            return;

        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        throw new IOException("Receiver timed out.");
    }

    private static void ValidateReceipt(ReceivedLaunch receipt, string[] expected)
    {
        if (receipt.ExitCode != 0 || !expected.SequenceEqual(receipt.Arguments, StringComparer.Ordinal))
            throw new InvalidDataException("The receiver did not preserve the Monitor argument sequence.");
    }

    private sealed record ReceivedLaunch(int ProcessId, int ExitCode, string[] Arguments);
}
