using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace YuzuhaToolkit.Tests;

// Former CLI-over-MCP cases now exercise the native CLI as real processes.
public sealed class DualModeCommandLineTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "phayton-cli-process-" + Guid.NewGuid().ToString("N"));
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
    private static string Input(object value) => JsonSerializer.Serialize(value);

    [Theory]
    [InlineData("builtin.usage")]
    [InlineData("knowledge.layers.search")]
    public async Task NativeCliListsBothExecutionAndKnowledgeOperations(string expected)
    {
        var result = await Run("op", "list");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Json(result.Output).GetProperty("data").EnumerateArray(), x => x.GetProperty("id").GetString() == expected);
        Assert.DoesNotContain("Application started.", result.Error);
    }

    [Fact]
    public async Task InlineFileAndStdinReturnIdenticalGeneratedPml()
    {
        var input = """{"methodName":"Test","parameters":[{"type":"string","value":"O'Brien 测试"}]}""";
        var inline = await Run("op", "invoke", "pml.call.generate", "--input", input);
        var file = await Run("op", "invoke", "pml.call.generate", "--input-file", Write("input.json", input));
        var stdin = await RunCore(["op", "invoke", "pml.call.generate", "--input-stdin"], input);
        Assert.Equal(0, inline.ExitCode); Assert.Equal(0, file.ExitCode); Assert.Equal(0, stdin.ExitCode);
        Assert.Equal("!!Test('O''Brien 测试')", Json(inline.Output).GetProperty("data").GetString());
        Assert.True(JsonElement.DeepEquals(Json(inline.Output), Json(file.Output)));
        Assert.True(JsonElement.DeepEquals(Json(inline.Output), Json(stdin.Output)));
    }

    [Fact]
    public async Task CompatibilityLaunchRejectsArgvSecretsButFileAndStdinReachAuthorization()
    {
        var input = Input(new { profileId = "does-not-exist", expectedRevision = "missing", password = "SYNTHETIC_SECRET" });
        var inline = await Run("op", "invoke", "launch.profile.start", "--input", input);
        Assert.Equal(2, inline.ExitCode);
        Assert.Equal("SECRET_INPUT_SOURCE_REQUIRED", Json(inline.Output).GetProperty("error").GetProperty("code").GetString());
        var file = await Run("op", "invoke", "launch.profile.start", "--input-file", Write("launch.json", input));
        var stdin = await RunCore(["op", "invoke", "launch.profile.start", "--input-stdin"], input);
        foreach (var result in new[] { file, stdin })
        {
            Assert.Equal(4, result.ExitCode);
            Assert.Equal("LAUNCH_NOT_AUTHORIZED", Json(result.Output).GetProperty("error").GetProperty("code").GetString());
        }
        foreach (var result in new[] { inline, file, stdin })
        { Assert.DoesNotContain("SYNTHETIC_SECRET", result.Output); Assert.DoesNotContain("SYNTHETIC_SECRET", result.Error); }
    }

    [Fact]
    public async Task CommunicationProtocolStillRejectsManagementLaunchBeforeDispatch()
    {
        var request = Input(new { protocolVersion = 1, requestId = Guid.NewGuid().ToString("N"), operationId = "launch.profile.start",
            input = new { profileId = "does-not-exist", expectedRevision = "missing", password = "SYNTHETIC_SECRET" }, context = new { allowLaunch = true } });
        var result = await RunCore(["--manager-protocol"], request);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("INVALID_PROTOCOL_REQUEST", Json(result.Output).GetProperty("result").GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("SYNTHETIC_SECRET", result.Output);
        Assert.DoesNotContain("SYNTHETIC_SECRET", result.Error);
    }

    [Theory]
    [InlineData("--mcp", 3, "MCP_DISABLED")]
    [InlineData("--cli", 2, "INVALID_INPUT")]
    public async Task RemovedTransportModesExitWithoutStartingServer(string argument, int code, string error)
    {
        var result = await Run(argument);
        Assert.Equal(code, result.ExitCode);
        Assert.Equal(error, Json(result.Output).GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("Application started.", result.Error);
    }

    [Fact]
    public async Task NativeBatchReturnsOrderedOutcomesFromOneDispatcher()
    {
        var requests = Input(new[] { new { operationId = "builtin.usage", input = (object)new { query = "connection" } }, new { operationId = "pml.call.generate", input = (object)new { methodName = "Test" } } });
        var result = await Run("op", "batch", "--input-file", Write("batch.json", requests));
        Assert.Equal(0, result.ExitCode);
        var data = Json(result.Output).GetProperty("data");
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal(2, data.GetProperty("completed").GetInt32());
        Assert.Equal("builtin.usage", data.GetProperty("results")[0].GetProperty("operationId").GetString());
        Assert.Equal("pml.call.generate", data.GetProperty("results")[1].GetProperty("operationId").GetString());
        Assert.DoesNotContain("Application started.", result.Error);
    }

    [Fact]
    public async Task NativeBatchRetainsEarlierResultsAndStopsOnUnknownOperation()
    {
        var requests = Input(new[] {
            new { operationId = "pml.call.generate", input = (object)new { methodName = "Test" } },
            new { operationId = "does.not.exist", input = (object)new { } },
            new { operationId = "pml.trust.set", input = (object)new { functionName = "Test", state = "untrusted" } } });
        var result = await Run("op", "batch", "--input-file", Write("failure.json", requests), "--allow-write");
        Assert.Equal(3, result.ExitCode);
        var data = Json(result.Output).GetProperty("data");
        Assert.Equal(2, data.GetProperty("completed").GetInt32());
        Assert.True(data.GetProperty("results")[0].GetProperty("success").GetBoolean());
        Assert.Equal("UNKNOWN_OPERATION", data.GetProperty("results")[1].GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(Path.Combine(temporary, "trust.json")));
    }

    [Fact]
    public async Task NativeBatchStopsOnTargetFailureBeforeLaterWrite()
    {
        var requests = Input(new[] { new { operationId = "aveva.session.status", input = (object)new { } }, new { operationId = "pml.trust.set", input = (object)new { functionName = "Test", state = "untrusted" } } });
        var result = await Run("op", "batch", "--input-file", Write("domain-failure.json", requests), "--allow-write");
        Assert.Equal(5, result.ExitCode);
        var data = Json(result.Output).GetProperty("data");
        Assert.Equal(1, data.GetProperty("completed").GetInt32());
        Assert.True(data.GetProperty("stoppedOnError").GetBoolean());
        Assert.False(File.Exists(Path.Combine(temporary, "trust.json")));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    public async Task InvalidJsonFailsBeforeNativeOperationDispatch(string arguments)
    {
        var result = await Run("op", "invoke", "builtin.usage", "--input", arguments);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("INVALID_INPUT", Json(result.Output).GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("Application started.", result.Error);
    }

    [Fact]
    public async Task InvalidTimeoutFailsBeforeDispatch()
    {
        var result = await Run("op", "invoke", "builtin.usage", "--timeout-seconds", "0");
        Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain("Application started.", result.Error);
    }

    [Fact]
    public async Task NativeKnowledgeUsesBuiltinContentWithoutCreatingDatabase()
    {
        var result = await Run("op", "invoke", "knowledge.layers.search", "--input", Input(new { query = "当前元素" }));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("read-current-element", Json(result.Output).GetProperty("data").GetRawText());
        Assert.False(Directory.Exists(Path.Combine(temporary, "knowledge")));
    }

    [Fact]
    public async Task NativeFormQueriesDescribeBoundsAndRequireExplicitTarget()
    {
        foreach (var id in new[] { "pml.forms.list", "pml.gadgets.list", "pml.gadget.read" })
        {
            var described = await Run("op", "describe", id);
            var data = Json(described.Output).GetProperty("data");
            Assert.True(data.GetProperty("requiresPid").GetBoolean());
            Assert.True(data.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("maxCharacters", out _));
        }
        Assert.Equal(5, (await Run("op", "invoke", "pml.forms.list")).ExitCode);
        Assert.Equal(2, (await Run("op", "invoke", "pml.gadgets.list", "--input", Input(new { formName = "Bad\nshow !!Other" }))).ExitCode);
        Assert.Equal(5, (await Run("op", "invoke", "pml.gadget.read", "--input", Input(new { formName = "Sample", gadgetName = "accept" }))).ExitCode);
    }

    [Fact]
    public async Task WorkspaceOperationsAuditActualNativeCalls()
    {
        Write("registered/example.pmlfnc", "define function !!Example()\nendfunction");
        var inspected = await Run("op", "invoke", "workspace.pmllib.inspect", "--input", Input(new { directory = Path.Combine(temporary, "registered") }));
        Assert.Equal(0, inspected.ExitCode);
        Assert.Equal(1, Json(inspected.Output).GetProperty("data").GetProperty("files").GetInt32());
        foreach (var id in new[] { "pml.undo.begin", "pml.undo.end" }) Assert.Equal(5, (await Run("op", "invoke", id)).ExitCode);
        var recent = await Run("op", "invoke", "runtime.calls", "--input", Input(new { limit = 50 }));
        foreach (var id in new[] { "pml.undo.begin", "pml.undo.end" })
            Assert.Contains(Json(recent.Output).GetProperty("data").EnumerateArray(), e => e.GetProperty("tool").GetString() == id && e.GetProperty("phase").GetString() == "failed");
        var status = await Run("op", "invoke", "runtime.status");
        Assert.Contains(Json(status.Output).GetProperty("data").EnumerateArray(), s => s.GetProperty("state").GetString() == "running");
    }

    [Fact]
    public async Task NativeFunctionPermissionAndSourceAreCheckedBeforeHostConnection()
    {
        var source = Write("Demo.pmlfnc", "define function !!Demo(!value is STRING) is STRING\nreturn !value\nendfunction");
        Assert.Equal(0, (await Run("op", "invoke", "pml.function.register", "--allow-write", "--input", Input(new { sourceFile = source, functionName = "Demo" }))).ExitCode);
        var input = Input(new { functionName = "Demo", parameters = new[] { new { type = "string", value = "test" } } });
        var denied = await Run("op", "invoke", "pml.function.invoke", "--pid", "2147483647", "--allow-execution", "--input", input);
        Assert.Equal(4, denied.ExitCode);
        Assert.Contains("PML_FUNCTION_INVOKE_DENIED", Json(denied.Output).GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(0, (await Run("op", "invoke", "pml.function.permission", "--allow-write", "--input", Input(new { functionName = "Demo", allowInvoke = true }))).ExitCode);
        Assert.Equal(5, (await Run("op", "invoke", "pml.function.invoke", "--pid", "2147483647", "--allow-execution", "--input", input)).ExitCode);
        File.AppendAllText(source, "\n-- changed source");
        var changed = await Run("op", "invoke", "pml.function.invoke", "--pid", "2147483647", "--allow-execution", "--input", input);
        Assert.Equal(2, changed.ExitCode);
        Assert.Contains("PML_FUNCTION_SOURCE_CHANGED", Json(changed.Output).GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(5, (await Run("op", "invoke", "pml.array.window", "--input", Input(new { globalVar = "DemoArray" }))).ExitCode);
    }

    private string Write(string name, string content)
    {
        var file = Path.Combine(temporary, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content); return file;
    }
    private Task<(int ExitCode, string Output, string Error)> Run(params string[] arguments) => RunCore(arguments, null);
    private async Task<(int ExitCode, string Output, string Error)> RunCore(string[] arguments, string? stdin)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false), StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        start.ArgumentList.Add(CliDll());
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var (key, value) in new Dictionary<string, string> { ["YUZUHA_STATE_DIR"] = Path.Combine(temporary, "state"), ["YUZUHA_KNOWLEDGE_DIR"] = Path.Combine(temporary, "knowledge"), ["YUZUHA_TRUST_FILE"] = Path.Combine(temporary, "trust.json"), ["YUZUHA_FUNCTION_REGISTRY"] = Path.Combine(temporary, "functions.json") }) start.Environment[key] = value;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (stdin is not null) await process.StandardInput.WriteAsync(stdin);
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output, await error);
    }
    private static string CliDll()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "YuzuhaToolkit.Development.slnx")))
                return Path.Combine(current.FullName, "src", "PmlTwin.Phayton.Cli", "bin", new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net10.0", "PmlTwin.Phayton.Cli.dll");
        throw new DirectoryNotFoundException("Yuzuha repository not found.");
    }
    public void Dispose()
    {
        var resolved = Path.GetFullPath(temporary);
        if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("phayton-cli-process-", StringComparison.Ordinal) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
