using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Mcp;

var passed = new List<string>();
async Task Test(string name, Func<Task> action)
{
    await action(); passed.Add(name); Console.WriteLine("PASS " + name);
}
static void Assert(bool value, string message = "Assertion failed")
{ if (!value) throw new Exception(message); }
static JsonElement Json(string text) { using var d = JsonDocument.Parse(text); return d.RootElement.Clone(); }
static Task Throws(Func<Task> action) => ThrowsCore(action);
static async Task ThrowsCore(Func<Task> action)
{ try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected target rejection"); }

using var fixture = new Fixture();
var executor = fixture.Executor;
var valid = Json("""{"pmlCommand":"!!Example()"}""");
await Test("registry rejects duplicate stable IDs", () =>
{
    var descriptor = executor.Registry.Descriptors[0];
    var handler = new DelegateOperationHandler((id, _, _, _) => Task.FromResult(OperationResult.Ok(id, Json("{}"))));
    try { _ = new OperationRegistry([new(descriptor, handler), new(descriptor, handler)]); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("Duplicate was accepted");
});
await Test("unknown operation has stable exit code", async () =>
    Assert((await executor.InvokeAsync("unknown.op", Json("{}"), new())).ExitCode == 3));
await Test("missing PID blocks before connecting", async () =>
{
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(AllowExecution: true));
    Assert(result.ExitCode == 5 && fixture.Factory.Connects == 0);
});
await Test("missing authorization blocks before connecting", async () =>
{
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42));
    Assert(result.ExitCode == 4 && fixture.Factory.Connects == 0);
});
await Test("array reads require authorization", async () =>
{
    var input = Json("""{"pmlCommand":"!!Read()","globalVar":"RESULT","deleteGlobalVar":true}""");
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.ReadArray, input, new(42))).ExitCode == 4);
    Assert(fixture.Factory.Connects == 0);
});
foreach (var invalid in new[] { "null", "[]", "{}", "{\"pmlCommand\":true}",
    "{\"pmlCommand\":\" \"}", "{\"pmlCommand\":\"Q\",\"pid\":42}",
    "{\"pmlCommand\":\"Q\",\"pmlCommand\":\"DELETE\"}" })
    await Test("strict command input: " + invalid, async () =>
        Assert((await executor.InvokeAsync(YuzuhaOperationIds.Execute, Json(invalid), new(42, null, true))).ExitCode == 2));
await Test("generator escapes text and is culture independent", async () =>
{
    var old = System.Globalization.CultureInfo.CurrentCulture;
    try
    {
        System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
        var result = await executor.InvokeAsync(YuzuhaOperationIds.Generate,
            Json("""{"methodName":"Example","parameters":[{"type":"str","value":"O'Brien 中文"},{"type":"real","value":1.5},{"type":"bool","value":true}]}"""), new());
        Assert(result.Success && result.Data!.Value.GetString() == "!!Example('O''Brien 中文',1.5,TRUE)");
        Assert(fixture.Factory.Connects == 0);
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = old; }
});
await Test("generator rejects injected method names", async () =>
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Generate,
        Json("""{"methodName":"Example()\nDELETE"}"""), new())).ExitCode == 2));
await Test("generator supports qualified global object methods", async () =>
{
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Generate,
        Json("""{"methodName":"YuzuhaExecuter.ExecuteFile","parameters":[{"type":"str","value":"example.pmlmac"}]}"""), new());
    Assert(result.Success && result.Data!.Value.GetString() == "!!YuzuhaExecuter.ExecuteFile('example.pmlmac')");
});
await Test("pure operations do not bind even with explicit PID", async () =>
{
    using var f = new Fixture();
    Assert((await f.Executor.InvokeAsync(YuzuhaOperationIds.Generate, Json("""{"methodName":"Example"}"""), new(42))).Success);
    Assert((await f.Executor.InvokeAsync(YuzuhaOperationIds.Sessions, Json("{}"), new(42))).Success);
    Assert(f.Factory.Connects == 0 && f.Service.Commands == 0);
});
await Test("nested parameter contracts reject objects", async () =>
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Generate,
        Json("""{"methodName":"Example","parameters":[{"type":"str","value":{}}]}"""), new())).ExitCode == 2));
await Test("nonfinite number text is rejected", async () =>
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Generate,
        Json("""{"methodName":"Example","parameters":[{"type":"number","value":"NaN"}]}"""), new())).ExitCode == 2));
await Test("undiscovered PID cannot connect", async () =>
{
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(777, null, true))).ExitCode == 5);
    Assert(fixture.Factory.Connects == 0);
});
await Test("missing PID pipe cannot fall back", async () =>
{
    fixture.Discovery.Candidate.PipeDetected = false;
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, null, true))).ExitCode == 5);
    Assert(fixture.Factory.Connects == 0);
    fixture.Discovery.Candidate.PipeDetected = true;
});
foreach (var field in new[] { "pid", "start", "pipe", "model", "emptyModel" })
    await Test("selection rejects identity mismatch: " + field, async () =>
    {
        using var f = new Fixture();
        f.Service.Mismatch = field;
        var result = await f.Executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, "Design", true));
        Assert(result.ExitCode == 5 && f.Service.Commands == 0 && f.Factory.Disposals == 1);
    });
foreach (var field in new[] { "pid", "start", "pipe", "model" })
    await Test("execution rechecks identity: " + field, async () =>
    {
        using var f = new Fixture();
        await f.Bridge.SelectSessionAsync(42, "Design", default);
        f.Service.Mismatch = field;
        await Throws(() => f.Bridge.RunPmlCommandAsync("Q", default));
        Assert(f.Service.Commands == 0);
    });
await Test("kernel refuses identity changed after selection", async () =>
{
    using var f = new Fixture();
    f.Service.MismatchOnIdentity = 2;
    var result = await f.Executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, "Design", true));
    Assert(result.ExitCode == 5 && !result.Error!.MayHaveExecuted && f.Service.Commands == 0);
});
await Test("failed selection clears previous target", async () =>
{
    using var f = new Fixture();
    await f.Bridge.SelectSessionAsync(42, "Design", default);
    await Throws(() => f.Bridge.SelectSessionAsync(777, null, default));
    Assert(!(await f.Bridge.GetConnectionStatusAsync(default)).SessionSelected);
    await Throws(() => f.Bridge.RunPmlCommandAsync("Q", default));
    Assert(f.Service.Commands == 0);
});
await Test("one dispatch and repeated identity validation", async () =>
{
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, "Design", true));
    Assert(result.Success && fixture.Service.Commands == 1 && fixture.Service.Identities == 2);
    Assert(fixture.Factory.LastPipe == "yuzuha.pml.command.v1.pid-42" && fixture.Factory.Disposals == 1);
    Assert(result.Data!.Value.GetProperty("RequestId").GetString() == "request-1");
});
await Test("host failure retains diagnostic payload", async () =>
{
    fixture.Service.HostSuccess = false;
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, null, true));
    Assert(result.ExitCode == 6 && result.Data!.Value.GetProperty("Code").GetString() == "PML_COMMAND_FAILED");
    fixture.Service.HostSuccess = true;
});
await Test("transport failure never retries", async () =>
{
    var before = fixture.Service.Commands;
    fixture.Service.ThrowTransport = true;
    var result = await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, null, true));
    Assert(result.ExitCode == 7 && result.Error!.MayHaveExecuted && fixture.Service.Commands == before + 1);
    fixture.Service.ThrowTransport = false;
});
await Test("cancellation does not dispatch", async () =>
{
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    var before = fixture.Service.Commands;
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.Execute, valid, new(42, null, true), cancellation.Token)).ExitCode == 8);
    Assert(fixture.Service.Commands == before);
});
await Test("array summary, filtering, unparsed and cleanup request", async () =>
{
    fixture.Service.Rows = ["<STRING> '[1] name <STRING> 'Pump''", "[1] blank <STRING> ''",
        "[1] zero <REAL> 0mm", "[1] unset <DBREF> Unset", "[1] array <ARRAY> 0 Elements", "unparsed"];
    var result = await executor.InvokeAsync(YuzuhaOperationIds.ReadArray,
        Json("""{"pmlCommand":"!!Read()","globalVar":"RESULT","deleteGlobalVar":true,"includeEmpty":false}"""), new(42, null, true));
    var data = result.Data!.Value;
    Assert(result.Success && data.GetProperty("Count").GetInt32() == 2 && data.GetProperty("UnparsedCount").GetInt32() == 1);
    Assert(data.GetProperty("Summary").GetProperty("total").GetInt32() == 5);
    Assert(data.GetProperty("Items")[0].GetProperty("Value").GetString() == "Pump");
    Assert(fixture.Service.LastRequest is { ReturnList: true, DeleteGlobalVar: true, GlobalVar: "RESULT" });
});
await Test("invalid array variable cannot inject PML", async () =>
    Assert((await executor.InvokeAsync(YuzuhaOperationIds.ReadArray,
        Json("""{"pmlCommand":"!!Read()","globalVar":"A;DELETE","deleteGlobalVar":true}"""), new(42, null, true))).ExitCode == 2));
await Test("legacy tools preserve output, trust warning and selected-PID requirement", async () =>
{
    using var f = new Fixture();
    var tools = new PmlCallTools(f.Bridge, f.Trust);
    Assert((await tools.RunPmlCommand("Q", default)).StartsWith("PML RPC failed:"));
    Assert(tools.GeneratePmlCall("Example", null) == "!!Example()");
    Assert(tools.GeneratePmlCall("Example", [new() { Type = "double", Value = 1.5 }]) == "!!Example(1.5)");
    Assert(tools.GeneratePmlCall(" Example ", [new() { Type = " REAL ", Value = 1.5 }]) == "!!Example(1.5)");
    Assert(Json(await tools.GetConnectionStatus(default)).GetProperty("TargetVerified").GetBoolean() == false);
    await tools.SelectAvevaSession(42, "Design");
    f.Trust.Set("Example", "untrusted", "test-only user-confirmed entry", null);
    var data = Json(await tools.RunPmlCommand("!!Example()", default));
    Assert(data.GetProperty("Success").GetBoolean() && data.GetProperty("FunctionTrustWarning").GetString()!.Contains("UNTRUSTED"));
    Assert(f.Service.Commands == 1);
});
await Test("generic MCP adapter uses kernel gate", async () =>
{
    var tools = new OperationTools(executor);
    var result = Json(await tools.OpInvoke(YuzuhaOperationIds.Execute, valid.GetRawText(), 42));
    Assert(result.GetProperty("exitCode").GetInt32() == 4);
    Assert(Json(tools.OpList()).GetArrayLength() == 6);
});
await Test("CLI JSON output, exit code and duplicate options", async () =>
{
    using var output = new StringWriter();
    Assert(await OperationCli.RunAsync(["op", "invoke", YuzuhaOperationIds.Generate, "--input", "{bad}"], executor, output) == 2);
    Assert(Json(output.ToString()).GetProperty("error").GetProperty("code").GetString() == "INVALID_INPUT");
    output.GetStringBuilder().Clear();
    Assert(await OperationCli.RunAsync(["op", "invoke", YuzuhaOperationIds.Execute, "--pid", "42", "--pid", "43"], executor, output) == 2);
});
Console.WriteLine($"{passed.Count} regression tests passed.");

sealed class Fixture : IDisposable
{
    public FakeDiscovery Discovery { get; } = new();
    public FakeService Service { get; } = new();
    public FakeFactory Factory { get; }
    public YuzuhaRpcBridge Bridge { get; }
    public PmlFunctionTrustStore Trust { get; }
    public OperationExecutor Executor { get; }
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "phayton-tests-" + Guid.NewGuid().ToString("N"));
    public Fixture()
    {
        Factory = new(Service);
        Directory.CreateDirectory(_folder);
        var authorization = new ExecutionAuthorizationStore(Path.Combine(_folder,"authorization.json"));
        authorization.Save(new() { Mode="Development", ProcessId=42, ProcessStartTimeUtcTicks=12345, Model="Design", SourceRoot=_folder, Reason="isolated Phayton regression", BypassRules=["raw-command"], ExpiresUtc=DateTimeOffset.UtcNow.AddHours(1) });
        Bridge = new(NullLogger<YuzuhaRpcBridge>.Instance, Discovery, Factory, authorization, new PmlFunctionRegistry(Path.Combine(_folder,"functions.json")), _folder);
        Trust = new(Path.Combine(_folder, "trust.json"));
        Executor = YuzuhaOperationCatalog.Create(Bridge, Trust, Bridge.CreateIndependent);
    }
    public void Dispose()
    {
        Bridge.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}
sealed class FakeDiscovery : IAvevaSessionDiscovery
{
    public AvevaSessionCandidate Candidate { get; } = new()
    { ProcessId = 42, ProcessStartTimeUtcTicks = 12345, PipeName = "yuzuha.pml.command.v1.pid-42", PipeDetected = true };
    public AvevaSessionListResponse Discover() => new() { Count = 1, Sessions = [Candidate] };
    public AvevaSessionCandidate RequireCandidate(int processId) => processId == Candidate.ProcessId
        ? Candidate : throw new InvalidOperationException("AVEVA_SESSION_NOT_DISCOVERED");
}
sealed class FakeFactory(FakeService service) : IPmlRpcConnectionFactory
{
    public int Connects, Disposals;
    public string? LastPipe;
    public IPmlRpcConnection Connect(string pipeName) { Connects++; LastPipe = pipeName; return new Connection(this, service); }
    private sealed class Connection(FakeFactory owner, FakeService service) : IPmlRpcConnection
    { public IPmlCommandService Service => service; public void Dispose() => owner.Disposals++; }
}
sealed class FakeService : IPmlCommandService
{
    public int Commands, Identities;
    public bool HostSuccess = true, ThrowTransport;
    public string? Mismatch;
    public int MismatchOnIdentity;
    public string[]? Rows;
    public RunPmlCommandRequest? LastRequest;
    public Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Identities++;
        if (MismatchOnIdentity > 0 && Identities >= MismatchOnIdentity) Mismatch = "start";
        return Task.FromResult(new HostIdentityResponse
        { AutomaticMarkDbVersion=1, ProcessId = Mismatch == "pid" ? 43 : 42, ProcessStartTimeUtcTicks = Mismatch == "start" ? 54321 : 12345,
            PipeName = Mismatch == "pipe" ? "legacy-shared-pipe" : "yuzuha.pml.command.v1.pid-42",
            Model = Mismatch == "model" ? "Draft" : Mismatch == "emptyModel" ? "" : "Design" });
    }
    public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Commands++; LastRequest = request;
        if (ThrowTransport) throw new IOException("Simulated lost response AFTER dispatch; do not retry.");
        return Task.FromResult(new RunPmlCommandResponse { Success = HostSuccess,
            Code = HostSuccess ? "OK" : "PML_COMMAND_FAILED", ErrorMessage = HostSuccess ? null : "Host rejected",
            MarkDbStarted=request.AutomaticMarkDb, MarkDbEnded=request.AutomaticMarkDb, ExecutionStarted=true, MarkDbId="fixture-group",
            PmlCommand = request.PmlCommand, ResultList = Rows, RequestId = "request-1", ExecutionThreadId = 1,
            ServerRuntime = "fake-host", ServerTimeUtc = DateTime.UtcNow });
    }
}
