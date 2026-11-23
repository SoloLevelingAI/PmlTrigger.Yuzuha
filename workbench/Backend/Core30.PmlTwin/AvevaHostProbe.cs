using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal interface IAvevaHostProbe
{
    CoreData ReadStatus(int pid, long startTicks);
}

/// <summary>Uses the registered Phayton CLI's read-only status operation and checks its Host identity.</summary>
internal sealed class PhaytonHostProbe(Func<string[]> findPrograms) : IAvevaHostProbe
{
    private const string Operation = "aveva.session.status";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public CoreData ReadStatus(int pid, long startTicks)
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("WindowsRequired");
        var programs = findPrograms();
        if (programs.Length != 1)
            return Unavailable(programs.Length == 0 ? "NotInstalled" : "MultipleInstallations");

        var command = new ProcessStartInfo(programs[0]) { WorkingDirectory = Path.GetDirectoryName(programs[0])! };
        foreach (var argument in new[] { "op", "invoke", Operation, "--pid", pid.ToString(CultureInfo.InvariantCulture),
            "--timeout-seconds", "12", "--input", "{}" })
            command.ArgumentList.Add(argument);
        var response = WindowsDetachedProcess.RunCaptured(command, TimeSpan.FromSeconds(20));
        return DescribeResponse(response, pid, startTicks);
    }

    private static CoreData Unavailable(string reason) => new("host", new CoreField("provider", "Phayton"),
        new CoreField("state", reason), new CoreField("targetVerified", false));

    private static CoreData DescribeResponse((int ExitCode, string Output, string Error) response, int pid, long ticks)
    {
        var envelope = JsonSerializer.Deserialize<StatusEnvelope>(response.Output, Json);
        var data = envelope?.Data;
        var host = data?.Host;
        var verified = response.ExitCode == 0 && envelope is { Success: true, OperationId: Operation }
            && data is { Connected: true, TargetVerified: true }
            && host is not null && host.ProcessId == pid && host.ProcessStartTimeUtcTicks == ticks;
        return new CoreData("host", new CoreField("provider", "Phayton"), new CoreField("state", verified ? "Verified" : "Unverified"),
            new CoreField("connected", data?.Connected == true), new CoreField("targetVerified", verified),
            new CoreField("compatible", verified && data?.Compatibility?.Compatible == true),
            new CoreField("model", host?.Model ?? ""), new CoreField("exitCode", response.ExitCode),
            new CoreData("receipt", response.Output), response.Error.Length == 0 ? null : new CoreData("stderr", response.Error));
    }

    private sealed record StatusEnvelope(string? OperationId, bool Success, StatusData? Data);
    private sealed record StatusData(bool Connected, bool TargetVerified, RemoteHost? Host, HostCompatibility? Compatibility);
    private sealed record RemoteHost(int ProcessId, long ProcessStartTimeUtcTicks, string? Model);
    private sealed record HostCompatibility(bool Compatible);
}
