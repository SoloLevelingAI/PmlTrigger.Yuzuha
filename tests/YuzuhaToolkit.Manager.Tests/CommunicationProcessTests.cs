using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class CommunicationProcessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Yuzuha manager 通信 " + Guid.NewGuid().ToString("N"));
    private readonly string fixture;
    public CommunicationProcessTests()
    {
        Directory.CreateDirectory(root);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "ProcessFixture")))
            File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
        fixture = Path.Combine(root, "YuzuhaToolkit.Manager.ProcessFixture.dll");
    }
    private CommunicationCliClient Client(TimeSpan? timeout = null, int limit = 2 * 1024 * 1024) =>
        new(new(fixture, Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet.exe"), timeout, limit));
    private static JsonElement Input(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public async Task UnicodePathsAndStdinPreserveTargetAndSeparateDiagnostics()
    {
        var reply = await Client().InvokeAsync("pml.call.generate", Input("""{"text":"中文 O'Brien & $()"}"""),
            new(42, "Design", ExpectedProcessStartTimeUtcTicks: 1234));
        Assert.True(reply.Result.Success, reply.Result.Error?.Message);
        var data = reply.Result.Data!.Value;
        Assert.Equal("中文 O'Brien & $()", data.GetProperty("Input").GetProperty("text").GetString());
        Assert.Equal(1234, data.GetProperty("Context").GetProperty("ExpectedProcessStartTimeUtcTicks").GetInt64());
        Assert.Equal(root, data.GetProperty("workingDirectory").GetString());
    }

    [Theory]
    [InlineData("wrong-request")]
    [InlineData("wrong-version")]
    [InlineData("wrong-exit")]
    [InlineData("bad-json")]
    public async Task InvalidResponsesNeverBecomeSuccess(string mode)
    {
        var result = (await Client().InvokeAsync("pml.command.execute", JsonSerializer.SerializeToElement(new { mode }), new(AllowExecution: true))).Result;
        Assert.False(result.Success); Assert.Equal("INVALID_CLI_RESPONSE", result.Error?.Code);
        Assert.True(result.Error?.MayHaveExecuted);
    }

    [Theory]
    [InlineData("flood")]
    [InlineData("stderr-flood")]
    public async Task EitherOutputStreamHasABound(string mode)
    {
        var marker = Path.Combine(root, "dispatches.txt");
        var result = (await Client(limit: 4096).InvokeAsync("pml.command.execute", JsonSerializer.SerializeToElement(new { mode, marker }), new(AllowExecution: true))).Result;
        Assert.Equal("CLI_OUTPUT_LIMIT", result.Error?.Code); Assert.True(result.Error?.MayHaveExecuted);
        AssertDispatchedProcessStopped(marker);
    }

    [Fact]
    public async Task TimeoutDoesNotRetryDispatchedOperation()
    {
        var marker = Path.Combine(root, "dispatches.txt");
        var result = (await Client(TimeSpan.FromSeconds(8)).InvokeAsync("pml.command.execute",
            JsonSerializer.SerializeToElement(new { mode = "timeout", marker }), new(AllowExecution: true))).Result;
        Assert.Equal("CLI_TIMEOUT", result.Error?.Code); Assert.True(result.Error?.MayHaveExecuted);
        Assert.Single(File.ReadAllLines(marker));
        AssertDispatchedProcessStopped(marker);
    }

    [Fact]
    public async Task CancelledBeforeStartCannotDispatch()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = (await Client().InvokeAsync("pml.command.execute", Input("{}"), new(AllowExecution: true), cancellation.Token)).Result;
        Assert.Equal("CANCELLED", result.Error?.Code); Assert.False(result.Error?.MayHaveExecuted);
    }

    [Fact]
    public async Task ChangedExecutableAndManagementCallsAreRejectedBeforeDispatch()
    {
        var client = Client();
        var management = (await client.InvokeAsync("launch.profile.start", Input("{}"), new(AllowLaunch: true))).Result;
        Assert.Equal("MANAGEMENT_OPERATION", management.Error?.Code);
        File.AppendAllText(fixture, "changed");
        var changed = (await client.InvokeAsync("aveva.sessions.list", Input("{}"), new())).Result;
        Assert.Equal("CLI_CHANGED", changed.Error?.Code); Assert.False(changed.Error?.MayHaveExecuted);
    }

    [Fact]
    public async Task HostRejectionAndItsExitCodeAreRetained()
    {
        var result = (await Client().InvokeAsync("pml.command.execute", Input("""{"mode":"host-failed"}"""), new(AllowExecution: true))).Result;
        Assert.Equal(6, result.ExitCode); Assert.Equal("HOST_FAILED", result.Error?.Code);
    }

    [Fact]
    public async Task ManagerRejectsBarePidAndHasNoBridgeDependency()
    {
        var executor = ManagerOperationCatalog.Create(communication: () => throw new Exception("Must not create client"));
        var rejected = await executor.InvokeAsync("connection.test", Input("{}"), new(42));
        Assert.Equal("PROCESS_IDENTITY_REQUIRED", rejected.Error?.Code);
        Assert.DoesNotContain(typeof(ManagerOperationCatalog).Assembly.GetReferencedAssemblies(),
            a => (a.Name ?? "").Contains("Mcp") || (a.Name ?? "").Contains("Rpc"));
    }

    [Theory]
    [InlineData("connection.test", "aveva.session.status")]
    [InlineData("connection.select", "aveva.session.select")]
    [InlineData("connection.status", "aveva.session.status")]
    public async Task ManagerConnectionTestBuildsEvidenceFromRealChildWithoutForwardingManagementInput(string operation, string target)
    {
        var host = Path.Combine(root, "runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll");
        var bootstrap = Path.Combine(root, "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc");
        Directory.CreateDirectory(Path.GetDirectoryName(host)!); Directory.CreateDirectory(Path.GetDirectoryName(bootstrap)!);
        File.WriteAllText(host, "synthetic host never loaded"); File.WriteAllText(bootstrap, "synthetic bootstrap never executed");
        ConnectionEvidenceTests.WriteStartupFiles(root);
        var executor = ManagerOperationCatalog.Create(communication: () => Client());
        var result = await executor.InvokeAsync(operation, JsonSerializer.SerializeToElement(new { profile = "E3D2.1", runtimeRoot = root }),
            new(42, "Design", ExpectedProcessStartTimeUtcTicks: 12345));
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("green", result.Data!.Value.GetProperty("evidence").GetProperty("overall").GetProperty("state").GetString());
        Assert.Equal(target, result.Data.Value.GetProperty("communication").GetProperty("operationId").GetString());
    }

    private static void AssertDispatchedProcessStopped(string marker)
    {
        var identity = File.ReadAllLines(marker).Single().Split('|');
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(int.Parse(identity[0]));
            Assert.True(process.HasExited || process.StartTime.ToUniversalTime().Ticks != long.Parse(identity[1]),
                "The dispatched child must have exited before returning a timeout/output-limit result.");
        }
        catch (ArgumentException) { } // The process ID no longer exists.
    }

    public void Dispose()
    {
        // Windows scanners can briefly retain image handles even after process termination.
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(root, true); return; }
            catch (Exception error) when (attempt < 40 && error is IOException or UnauthorizedAccessException)
            { Thread.Sleep(100); }
        }
    }
}
