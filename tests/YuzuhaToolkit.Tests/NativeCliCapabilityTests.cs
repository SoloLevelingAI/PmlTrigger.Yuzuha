using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

[Collection("UserData environment")]
public sealed class NativeCliCapabilityTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static JsonElement Input(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates()
    {
        using var fixture = new NativeFixture();
        var executor = fixture.Executor;
        var report = new List<object>();
        var failures = new List<string>();
        var visited = new HashSet<string>();
        var target = new OperationContext(42, "Design", true, true);
        long chunk = 0;
        async Task Run(string id, object input, Action<OperationResult>? verify = null)
        {
            var data = Input(input);
            executor.Registry.TryGet(id, out var entry);
            var rejected = await executor.InvokeAsync(id, Json("{\"unexpectedField\":true}"), target);
            Assert.Equal("INVALID_INPUT", rejected.Error?.Code);
            OperationResult? denied = null;
            if (entry!.Descriptor.RequiresExecution || entry.Descriptor.RequiresWrite)
            {
                var before = fixture.Service.Commands;
                denied = await executor.InvokeAsync(id, data, new(42, "Design"));
                Assert.Equal(4, denied.ExitCode);
                Assert.Equal(before, fixture.Service.Commands);
            }
            var result = await executor.InvokeAsync(id, data, target);
            visited.Add(id);
            string? assertionError = null;
            try { Assert.True(result.Success, result.Error?.Message); verify?.Invoke(result); }
            catch (Exception error) { assertionError = error.Message; failures.Add(id + ": " + error.Message); }
            report.Add(new { operationId = id, legacyTools = entry.Descriptor.LegacyTools,
                environment = entry.Descriptor.RequiresPid ? "synthetic-host-real-native-adapter" : "isolated-files-real-service",
                status = assertionError is null ? "Passed" : "Failed", assertionError, result,
                unknownFieldRejected = rejected.Error?.Code, permissionRejected = denied?.Error?.Code });
        }
        try
        {
            await Run("aveva.sessions.list", new { }, r => Assert.Equal(42, r.Data!.Value.GetProperty("Sessions")[0].GetProperty("ProcessId").GetInt32()));
            await Run("aveva.session.select", new { }, r => Assert.True(r.Data!.Value.GetProperty("TargetVerified").GetBoolean()));
            await Run("aveva.session.status", new { }, r => Assert.True(r.Data!.Value.GetProperty("TargetVerified").GetBoolean()));
            await Run("pml.call.generate", new { methodName = "NativeDemo", parameters = new[] { new { type = "string", value = "O'Brien" } } }, r => Assert.Equal("!!NativeDemo('O''Brien')", r.Data!.Value.GetString()));
            await Run("pml.command.execute", new { pmlCommand = "Q CE" }, _ => Assert.Equal("Q CE", fixture.Service.LastRequest!.PmlCommand));
            await Run("pml.array.read", new { pmlCommand = "!!NativeDemo()", globalVar = "TESTARRAY", deleteGlobalVar = false }, r => Assert.Equal(1, r.Data!.Value.GetProperty("Count").GetInt32()));
            await Run("pml.array.window", new { globalVar = "TESTARRAY", startIndex = 3, maxItems = 10, maxCharacters = 200 }, _ => Assert.True(fixture.Service.LastRequest is { ReadArray: true, StartIndex: 3, MaxItems: 10, MaxCharacters: 200, DeleteGlobalVar: false }));
            await Run("pml.forms.list", new { shownOnly = false }, _ => Assert.Equal("forms", fixture.Service.LastRequest!.FormQuery!.Kind));
            await Run("pml.gadgets.list", new { formName = "NativeForm", startIndex = 2 }, _ => Assert.Equal("NativeForm", fixture.Service.LastRequest!.FormQuery!.FormName));
            await Run("pml.gadget.read", new { formName = "NativeForm", gadgetName = "Apply" }, _ => Assert.Equal("Apply", fixture.Service.LastRequest!.FormQuery!.GadgetName));
            await Run("pml.function.register", new { sourceFile = fixture.Source, functionName = "NativeDemo" }, r => Assert.False(r.Data!.Value.GetProperty("AllowInvoke").GetBoolean()));
            await Run("pml.functions.list", new { }, r => Assert.Single(r.Data!.Value.EnumerateArray()));
            await Run("pml.function.permission", new { functionName = "NativeDemo", allowInvoke = true }, r => Assert.True(r.Data!.Value.GetProperty("AllowInvoke").GetBoolean()));
            await Run("pml.function.invoke", new { functionName = "NativeDemo", parameters = Array.Empty<object>() }, _ => Assert.Equal("!!NativeDemo()", fixture.Service.LastRequest!.PmlCommand));
            await Run("pml.undo.begin", new { description = "fixture's group" }, _ => Assert.Equal("MARKDB 'fixture''s group'", fixture.Service.LastRequest!.PmlCommand));
            await Run("pml.undo.end", new { }, _ => Assert.Equal("ENDMARKDB", fixture.Service.LastRequest!.PmlCommand));
            await Run("pml.trust.set", new { functionName = "NativeDemo", state = "untrusted", reason = "Synthetic fixture only" }, r => Assert.True(r.Data!.Value.GetProperty("ok").GetBoolean()));
            await Run("pml.trust.list", new { }, r => Assert.Equal(1, r.Data!.Value.GetProperty("untrustedCount").GetInt32()));
            await Run("builtin.usage", new { query = "current element" }, r => Assert.NotEmpty(r.Data!.Value.EnumerateArray()));
            await Run("workspace.pmllib.inspect", new { directory = fixture.PmlRoot }, r => Assert.Contains("NativeDemo", r.Data!.Value.GetRawText(), StringComparison.OrdinalIgnoreCase));
            await Run("runtime.status", new { }, r => Assert.NotEmpty(r.Data!.Value.EnumerateArray()));
            await Run("runtime.calls", new { limit = 200 }, r => Assert.NotEmpty(r.Data!.Value.EnumerateArray()));
            await Run("knowledge.database.build", new { pmlLibRoot = fixture.PmlRoot, webHelpRoot = fixture.HtmlRoot, dbName = "native-fixture" }, r => Assert.True(r.Data!.Value.GetProperty("ok").GetBoolean()));
            await Run("knowledge.database.check", new { dbPath = fixture.Database }, r => Assert.True(r.Data!.Value.GetProperty("ok").GetBoolean()));
            await Run("knowledge.databases.list", new { checkFreshness = true }, r => Assert.Single(r.Data!.Value.EnumerateArray()));
            await Run("knowledge.search", new { query = "fixturequartz", dbPath = fixture.Database, topK = 8 }, r => { Assert.NotEmpty(r.Data!.Value.GetProperty("hits").EnumerateArray()); chunk = r.Data.Value.GetProperty("hits")[0].GetProperty("chunkId").GetInt64(); });
            await Run("knowledge.chunk.read", new { chunkId = chunk, dbPath = fixture.Database }, r => Assert.Contains("fixturequartz", r.Data!.Value.GetRawText(), StringComparison.OrdinalIgnoreCase));
            await Run("knowledge.source.register", new { role = "official", name = "synthetic", pmlLibRoot = fixture.PmlRoot }, r => Assert.True(r.Data!.Value.GetProperty("ok").GetBoolean()));
            await Run("knowledge.layers.search", new { query = "fixturequartz", includeSupplemental = true }, r => Assert.NotEmpty(r.Data!.Value.EnumerateArray()));
            await Run("knowledge.experience.record", new { title = "Synthetic fixture", content = "fixturequartz check", context = "Offline adapter test; no AVEVA execution", id = "native-cli-fixture" }, r => Assert.Contains("native-cli-fixture", r.Data!.Value.GetRawText()));
            Assert.Equal(executor.Registry.Descriptors.Select(d => d.Id).Order(), visited.Order());
            Assert.Equal(30, visited.Count);
        }
        finally
        {
            WriteReport("capabilities.json", new { scope = "all-30-former-business-tools", actualAvevaExecuted = false, cases = report, failures,
                missing = executor.Registry.Descriptors.Select(d => d.Id).Except(visited).ToArray() });
        }
        Assert.Empty(failures);
    }

    [Fact]
    public async Task FileStdinAndInlineJsonUseTheSameDispatcherAndRejectAmbiguity()
    {
        using var fixture = new NativeFixture();
        var file = Path.Combine(fixture.Root, "input.json");
        const string payload = "{\"methodName\":\"NativeDemo\",\"parameters\":[{\"type\":\"string\",\"value\":\"中文 O'Brien\"}]}";
        File.WriteAllText(file, payload);
        var results = new List<JsonElement>();
        foreach (var source in new[] { new[] { "--input", payload }, new[] { "--input-file", file }, new[] { "--input-stdin" } })
        {
            using var output = new StringWriter();
            var exit = await OperationCli.RunAsync(["op", "invoke", "pml.call.generate", .. source], fixture.Executor, output, inputReader: new StringReader(payload));
            Assert.Equal(0, exit);
            results.Add(Json(output.ToString()));
        }
        Assert.All(results, result => Assert.True(JsonElement.DeepEquals(results[0], result)));
        using var rejected = new StringWriter();
        Assert.Equal(2, await OperationCli.RunAsync(["op", "invoke", "pml.call.generate", "--input", payload, "--input-file", file], fixture.Executor, rejected));
        using var duplicate = new StringWriter();
        Assert.Equal(2, await OperationCli.RunAsync(["op", "invoke", "pml.call.generate", "--input", "{\"methodName\":\"A\",\"methodName\":\"B\"}"], fixture.Executor, duplicate));
        WriteReport("json-inputs.json", new { passed = true, inline = results[0], file = results[1], stdin = results[2], ambiguousRejected = Json(rejected.ToString()), duplicateRejected = Json(duplicate.ToString()) });
    }

    [Fact]
    public async Task CliMetaCommandsDescribeEveryOperationAndNeverStartMcp()
    {
        using var fixture = new NativeFixture();
        using var list = new StringWriter();
        Assert.Equal(0, await OperationCli.RunAsync(["op", "list"], fixture.Executor, list));
        Assert.Equal(30, Json(list.ToString()).GetProperty("data").GetArrayLength());
        foreach (var item in fixture.Executor.Registry.Descriptors)
        {
            using var description = new StringWriter();
            Assert.Equal(0, await OperationCli.RunAsync(["op", "describe", item.Id], fixture.Executor, description));
            Assert.Equal(item.Id, Json(description.ToString()).GetProperty("data").GetProperty("id").GetString());
        }
        Assert.Equal(0, fixture.Service.Commands);
        WriteReport("meta-commands.json", new { passed = true, oldTools = new[] { "op_list", "op_describe", "op_invoke" }, described = fixture.Executor.Registry.Descriptors.Count, catalog = Json(list.ToString()) });
    }

    [Fact]
    public async Task ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth()
    {
        using var fixture = new NativeFixture();
        var executor = fixture.Executor;
        var records = new List<OperationResult>();
        var input = Json("{\"globalVar\":\"TESTARRAY\"}");
        records.Add(await executor.InvokeAsync("pml.array.window", input, new(AllowExecution: true)));
        records.Add(await executor.InvokeAsync("pml.array.window", input, new(42)));
        Assert.Equal(0, fixture.Service.Commands);
        fixture.Service.Protocol = 0;
        foreach (var (operation, request, code) in new[]
        {
            ("pml.array.window", input, "PML_BOUNDED_ARRAY_UNSUPPORTED"),
            ("pml.forms.list", Json("{}"), "PML_UI_HOST_UNSUPPORTED"),
            ("pml.gadgets.list", Json("{\"formName\":\"NativeForm\"}"), "PML_UI_HOST_UNSUPPORTED"),
            ("pml.gadget.read", Json("{\"formName\":\"NativeForm\",\"gadgetName\":\"Apply\"}"), "PML_UI_HOST_UNSUPPORTED")
        })
        {
            // A valid request against an old Host is a capability failure, not invalid user input.
            records.Add(await executor.InvokeAsync(operation, request, new(42, "Design", true)));
            Assert.Equal(6, records[^1].ExitCode);
            Assert.Equal(code, records[^1].Error!.Code);
            Assert.False(records[^1].Error!.MayHaveExecuted);
            Assert.Equal(0, fixture.Service.Commands);
        }
        fixture.Service.Protocol = 1;
        fixture.Service.WrongIdentity = true;
        records.Add(await executor.InvokeAsync("pml.forms.list", Json("{}"), new(42, "Design", true)));
        Assert.Equal(5, records[^1].ExitCode);
        Assert.Equal(0, fixture.Service.Commands);
        fixture.Service.WrongIdentity = false;
        fixture.Service.ThrowTransport = true;
        records.Add(await executor.InvokeAsync("pml.undo.end", Json("{}"), new(42, "Design", true)));
        Assert.False(records[^1].Success);
        Assert.True(records[^1].Error!.MayHaveExecuted);
        Assert.Equal(1, fixture.Service.Commands);
        fixture.Service.ThrowTransport = false;
        fixture.Service.HostSuccess = false;
        records.Add(await executor.InvokeAsync("pml.gadget.read", Json("{\"formName\":\"NativeForm\",\"gadgetName\":\"Apply\"}"), new(42, "Design", true)));
        Assert.Equal("HOST_FAILED", records[^1].Error!.Code);
        Assert.True(records[^1].Error!.MayHaveExecuted);
        Assert.NotNull(records[^1].Data);
        records.Add(await executor.InvokeAsync("pml.command.execute", Json("{\"pmlCommand\":\"Q CE\"}"), new(42, "Design", true)));
        Assert.Equal("HOST_FAILED", records[^1].Error!.Code);
        Assert.True(records[^1].Error!.MayHaveExecuted);
        WriteReport("failure-paths.json", new { passed = true, hostDispatches = fixture.Service.Commands, automaticRetries = 0, cases = records });
    }

    [Fact]
    public async Task FunctionAuthorizationAndSourceValidationPrecedeDiscoveryAndConnection()
    {
        using var fixture = new NativeFixture();
        fixture.Functions.Register(fixture.Source, "NativeDemo");
        const string input = "{\"functionName\":\"NativeDemo\",\"parameters\":[]}";
        async Task<JsonElement> Invoke(bool allowExecution, int expectedExit)
        {
            using var output = new StringWriter();
            string[] authorization = allowExecution ? ["--allow-execution"] : [];
            Assert.Equal(expectedExit, await OperationCli.RunAsync(
                ["op", "invoke", "pml.function.invoke", "--pid", "42", .. authorization, "--input", input],
                fixture.Executor, output));
            return Json(output.ToString());
        }

        var denied = await Invoke(true, 4);
        Assert.Contains("PML_FUNCTION_INVOKE_DENIED", denied.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Factory.Connections);
        fixture.Functions.SetPermission("NativeDemo", true);
        var unauthorized = await Invoke(false, 4);
        Assert.Equal("EXECUTION_NOT_AUTHORIZED", unauthorized.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Factory.Connections);
        Assert.Equal(0, fixture.Service.Commands);

        var allowed = await Invoke(true, 0);
        Assert.True(allowed.GetProperty("success").GetBoolean());
        Assert.True(fixture.Discovery.Calls > 0);
        Assert.Equal(1, fixture.Factory.Connections);
        Assert.Equal(1, fixture.Service.Commands);
        Assert.Equal("!!NativeDemo()", fixture.Service.LastRequest!.PmlCommand);

        var discoveries = fixture.Discovery.Calls;
        File.AppendAllText(fixture.Source, "\n-- changed after permission\n");
        var stale = await Invoke(true, 2);
        Assert.Contains("PML_FUNCTION_SOURCE_CHANGED", stale.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(discoveries, fixture.Discovery.Calls);
        Assert.Equal(1, fixture.Factory.Connections);
        Assert.Equal(1, fixture.Service.Commands);
    }

    [Fact]
    public async Task ChangedFunctionAndKnowledgeFailureDoNotBecomeSuccessfulWrappers()
    {
        using var fixture = new NativeFixture();
        fixture.Functions.Register(fixture.Source, "NativeDemo");
        fixture.Functions.SetPermission("NativeDemo", true);
        File.AppendAllText(fixture.Source, "\n-- changed after permission\n");
        var stale = await fixture.Executor.InvokeAsync("pml.function.invoke", Json("{\"functionName\":\"NativeDemo\",\"parameters\":[]}"), new(42, "Design", true));
        Assert.False(stale.Success);
        Assert.Contains("SOURCE_CHANGED", stale.Error!.Message);
        Assert.Equal(0, fixture.Service.Commands);
        var missing = await fixture.Executor.InvokeAsync("knowledge.database.check", Input(new { dbPath = Path.Combine(fixture.Root, "absent.sqlite3") }), new());
        Assert.False(missing.Success);
        Assert.NotNull(missing.Data);
        WriteReport("service-failures.json", new { passed = true, stale, missing });
    }

    private static void WriteReport(string name, object value)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        var root = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable"), "artifacts", "native-cli-migration");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class NativeFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "phayton-native-" + Guid.NewGuid().ToString("N"));
        public string PmlRoot => Path.Combine(Root, "PMLLIB");
        public string HtmlRoot => Path.Combine(Root, "WebHelp");
        public string Source => Path.Combine(PmlRoot, "NativeDemo.pmlfnc");
        public string Database => Path.Combine(Root, "knowledge", "native-fixture.sqlite3");
        public NativeService Service { get; } = new();
        public NativeDiscovery Discovery { get; } = new();
        public NativeFactory Factory { get; }
        public PmlFunctionRegistry Functions { get; }
        public OperationExecutor Executor { get; }
        private readonly string? priorState = Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR");
        private readonly YuzuhaRpcBridge bridge;
        private readonly RuntimeJournal journal;
        public NativeFixture()
        {
            Directory.CreateDirectory(PmlRoot); Directory.CreateDirectory(HtmlRoot);
            File.WriteAllText(Source, "define function !!NativeDemo() is STRING\n-- fixturequartz search evidence\nreturn 'fixturequartz'\nendfunction\n");
            File.WriteAllText(Path.Combine(HtmlRoot, "fixture.html"), "<html><head><title>Native fixture</title></head><body><h1>fixturequartz</h1><p>Native CLI fixture documentation.</p></body></html>");
            Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", Path.Combine(Root, "state"));
            Factory = new NativeFactory(Service);
            bridge = new(NullLogger<YuzuhaRpcBridge>.Instance, Discovery, Factory);
            Functions = new(Path.Combine(Root, "functions.json"));
            journal = new("native-cli-test", Path.Combine(Root, "state"));
            Executor = NativeOperationCatalog.Create(bridge, new(Path.Combine(Root, "trust.json")), bridge.CreateIndependent, Functions, new(Path.Combine(Root, "knowledge")), journal);
        }
        public void Dispose()
        {
            journal.Dispose(); bridge.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", priorState);
            Directory.Delete(Root, true);
        }
    }

    private sealed class NativeDiscovery : IAvevaSessionDiscovery
    {
        private readonly AvevaSessionCandidate candidate = new() { ProcessId = 42, ProcessStartTimeUtcTicks = 12345, PipeName = "yuzuha.pml.command.v1.pid-42", PipeDetected = true };
        public int Calls;
        public AvevaSessionListResponse Discover() { Calls++; return new() { Count = 1, Sessions = [candidate] }; }
        public AvevaSessionCandidate RequireCandidate(int processId)
        { Calls++; return processId == 42 ? candidate : throw new InvalidOperationException("AVEVA_SESSION_NOT_DISCOVERED"); }
    }
    private sealed class NativeFactory(NativeService service) : IPmlRpcConnectionFactory
    {
        public int Connections;
        public IPmlRpcConnection Connect(string pipeName) { Connections++; return new NativeConnection(service); }
        private sealed class NativeConnection(NativeService service) : IPmlRpcConnection
        { public IPmlCommandService Service => service; public void Dispose() { } }
    }
    private sealed class NativeService : IPmlCommandService
    {
        public int Commands, Protocol = 1;
        public bool WrongIdentity, ThrowTransport, HostSuccess = true;
        public RunPmlCommandRequest? LastRequest;
        public Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token) => Task.FromResult(new HostIdentityResponse
        { ProcessId = WrongIdentity ? 43 : 42, ProcessStartTimeUtcTicks = 12345, PipeName = "yuzuha.pml.command.v1.pid-42", Model = "Design", BoundedArrayReadVersion = Protocol, FormQueryVersion = Protocol });
        public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Commands++; LastRequest = request;
            if (ThrowTransport) throw new IOException("Synthetic response loss after dispatch.");
            return Task.FromResult(new RunPmlCommandResponse { Success = HostSuccess, Code = HostSuccess ? "OK" : "FIXTURE_REJECTED", ErrorMessage = HostSuccess ? null : "Synthetic Host rejection",
                PmlCommand = request.PmlCommand, ResultList = ["[1] fixture <STRING> 'fixturequartz'"], RequestId = "fixture-" + Commands, ServerRuntime = "synthetic-host", ServerTimeUtc = DateTime.UtcNow });
        }
    }
}
