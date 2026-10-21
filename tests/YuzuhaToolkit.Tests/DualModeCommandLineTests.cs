using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class DualModeCommandLineTests : IDisposable
{
    private readonly string _temporary = Path.Combine(Path.GetTempPath(), "yuzuha-cli-test-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("YuzuhaToolkit.Mcp", "get_builtin_usage")]
    [InlineData("YuzuhaToolkit.Knowledge", "search_knowledge_layers")]
    public async Task SameExecutableCanListItsRealMcpToolsFromCli(string server, string expected)
    {
        var outcome = await RunAsync(server, "--cli", "list");
        Assert.Equal(0, outcome.ExitCode);
        using var json = JsonDocument.Parse(outcome.Output);
        Assert.Contains(json.RootElement.GetProperty("tools").EnumerateArray(), tool => tool.GetProperty("name").GetString() == expected);
    }

    [Fact]
    public async Task CliAndMcpReturnTheSameGeneratedPml()
    {
        const string arguments = "{\"methodName\":\"Test\",\"parameters\":[{\"type\":\"string\",\"value\":\"O'Brien 测试\"}]}";
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "call", "generate_pml_call", "--arguments", arguments);
        Assert.Equal(0, outcome.ExitCode);
        using var cli = JsonDocument.Parse(outcome.Output);
        using var parameters = JsonDocument.Parse(arguments);
        await using var client = await ConnectAsync("YuzuhaToolkit.Mcp", "--mcp");
        var result = await client.CallToolAsync(new CallToolRequestParams { Name = "generate_pml_call", Arguments = parameters.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()) });
        Assert.Equal(result.Content.OfType<TextContentBlock>().Single().Text, cli.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("YuzuhaToolkit.Mcp")]
    [InlineData("YuzuhaToolkit.Knowledge")]
    public async Task DefaultAndExplicitMcpModesHaveIdenticalCatalogs(string server)
    {
        await using var implicitMode = await ConnectAsync(server);
        await using var explicitMode = await ConnectAsync(server, "--mcp");
        var first = await implicitMode.ListToolsAsync();
        var second = await explicitMode.ListToolsAsync();
        Assert.Equal(first.Select(tool => tool.Name).Order(), second.Select(tool => tool.Name).Order());
    }

    [Fact]
    public async Task BatchUsesOneMcpSessionAndReturnsOrderedOutcomes()
    {
        var file = Write("requests.json", "[{\"name\":\"get_builtin_usage\",\"arguments\":{\"query\":\"connection\"}},{\"name\":\"generate_pml_call\",\"arguments\":{\"methodName\":\"Test\",\"parameters\":[]}}]");
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "batch", "--file", file);
        Assert.Equal(0, outcome.ExitCode);
        using var json = JsonDocument.Parse(outcome.Output);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(2, json.RootElement.GetProperty("completed").GetInt32());
        Assert.Equal("get_builtin_usage", json.RootElement.GetProperty("results")[0].GetProperty("name").GetString());
        Assert.Equal("generate_pml_call", json.RootElement.GetProperty("results")[1].GetProperty("name").GetString());
        Assert.Equal(1, outcome.Error.Split("Application started.").Length - 1);
    }

    [Fact]
    public async Task BatchRetainsEarlierResultsAndDoesNotRunAfterProtocolFailure()
    {
        var file = Write("failure.json", "[{\"name\":\"generate_pml_call\",\"arguments\":{\"methodName\":\"Test\",\"parameters\":[]}},{\"name\":\"does_not_exist\",\"arguments\":{}},{\"name\":\"set_pml_function_trust\",\"arguments\":{\"functionName\":\"Test\",\"state\":\"untrusted\"}}]");
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "batch", "--file", file);
        Assert.Equal(1, outcome.ExitCode);
        using var json = JsonDocument.Parse(outcome.Output);
        Assert.Equal(2, json.RootElement.GetProperty("completed").GetInt32());
        Assert.True(json.RootElement.GetProperty("results")[1].GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.False(File.Exists(Path.Combine(_temporary, "trust.json")));
    }

    [Fact]
    public async Task DomainFailureStopsTheBatchBeforeAnyLaterWrite()
    {
        var file = Write("domain-failure.json", "[{\"name\":\"get_connection_status\",\"arguments\":{}},{\"name\":\"set_pml_function_trust\",\"arguments\":{\"functionName\":\"Test\",\"state\":\"untrusted\"}}]");
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "batch", "--file", file);
        Assert.Equal(1, outcome.ExitCode);
        using var json = JsonDocument.Parse(outcome.Output);
        Assert.Equal(1, json.RootElement.GetProperty("completed").GetInt32());
        Assert.True(json.RootElement.GetProperty("stoppedOnError").GetBoolean());
        Assert.False(File.Exists(Path.Combine(_temporary, "trust.json")));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    public async Task InvalidArgumentJsonIsRejectedBeforeStartingMcp(string arguments)
    {
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "call", "get_builtin_usage", "--arguments", arguments);
        Assert.Equal(2, outcome.ExitCode);
        Assert.DoesNotContain("Application started.", outcome.Error);
        using var json = JsonDocument.Parse(outcome.Output);
        Assert.Equal("invalid_arguments", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidTimeoutDoesNotStartTheServer()
    {
        var outcome = await RunAsync("YuzuhaToolkit.Mcp", "--cli", "list", "--timeout-seconds", "0");
        Assert.Equal(2, outcome.ExitCode);
        Assert.DoesNotContain("Application started.", outcome.Error);
    }

    [Fact]
    public async Task KnowledgeCliUsesBuiltinContentWithoutCreatingDatabase()
    {
        var outcome = await RunAsync("YuzuhaToolkit.Knowledge", "--cli", "call", "search_knowledge_layers", "--arguments", "{\"query\":\"当前元素\"}");
        Assert.Equal(0, outcome.ExitCode);
        using var result = JsonDocument.Parse(outcome.Output);
        Assert.Contains("read-current-element", result.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.False(Directory.Exists(Path.Combine(_temporary, "knowledge")));
    }

    private string Write(string name, string content)
    {
        Directory.CreateDirectory(_temporary);
        var file = Path.Combine(_temporary, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content); return file;
    }

    [Fact]
    public async Task NativeWorkspaceToolsAreCallableAndAuditRealMcpCalls()
    {
        Write("registered/example.pmlfnc", "define function !!Example()\nendfunction");
        await using var client = await ConnectAsync("YuzuhaToolkit.Mcp", "--mcp");
        var catalog = await client.ListToolsAsync();
        foreach (var name in new[] { "markdb", "endmarkdb", "inspect_registered_pmllib", "get_ai_runtime_status", "get_recent_tool_calls" }) Assert.Contains(catalog, tool => tool.Name == name);
        var inspected = await client.CallToolAsync("inspect_registered_pmllib", new Dictionary<string, object?> { ["directory"] = Path.Combine(_temporary, "registered") });
        Assert.NotEqual(true, inspected.IsError);
        using (var health = JsonDocument.Parse(inspected.Content.OfType<TextContentBlock>().Single().Text)) Assert.Equal(1, health.RootElement.GetProperty("files").GetInt32());
        foreach (var name in new[] { "markdb", "endmarkdb" })
        {
            var outcome = await client.CallToolAsync(name, new Dictionary<string, object?>());
            Assert.Contains("TARGET_NOT_SELECTED", outcome.Content.OfType<TextContentBlock>().Single().Text);
        }
        var recent = await client.CallToolAsync("get_recent_tool_calls", new Dictionary<string, object?> { ["limit"] = 50 });
        using var entries = JsonDocument.Parse(recent.Content.OfType<TextContentBlock>().Single().Text);
        foreach (var name in new[] { "markdb", "endmarkdb" })
            Assert.Contains(entries.RootElement.EnumerateArray(), entry => entry.GetProperty("tool").GetString() == name && entry.GetProperty("phase").GetString() == "failed");
        var status = await client.CallToolAsync("get_ai_runtime_status", new Dictionary<string, object?>());
        using var states = JsonDocument.Parse(status.Content.OfType<TextContentBlock>().Single().Text);
        Assert.Contains(states.RootElement.EnumerateArray(), state => state.GetProperty("state").GetString() == "running");
    }

    private Dictionary<string, string?> EnvironmentOverrides() => new()
    {
        ["YUZUHA_TOOLKIT_ROOT"] = _temporary,
        ["YUZUHA_KNOWLEDGE_DIR"] = Path.Combine(_temporary, "knowledge"),
        ["YUZUHA_TRUST_FILE"] = Path.Combine(_temporary, "trust.json"),
        ["YUZUHA_FUNCTION_REGISTRY"] = Path.Combine(_temporary, "functions.json")
    };

    [Fact]
    public async Task RegisteredFunctionPermissionIsEnforcedBeforeRealMcpDispatch()
    {
        var source=Write("Demo.pmlfnc","define function !!Demo(!value is STRING) is STRING\nreturn !value\nendfunction");
        await using var client=await ConnectAsync("YuzuhaToolkit.Mcp","--mcp");
        var registered=await client.CallToolAsync("register_pml_function",new Dictionary<string,object?>{{"sourceFile",source},{"functionName","Demo"}});
        Assert.NotEqual(true,registered.IsError);
        var arguments=new Dictionary<string,object?>{{"functionName","Demo"},{"parameters",new[]{new{type="string",value="test"}}}};
        var denied=await client.CallToolAsync("invoke_pml_function",arguments);
        Assert.True(denied.IsError);
        Assert.Contains("PML_FUNCTION_INVOKE_DENIED",denied.Content.OfType<TextContentBlock>().Single().Text);
        var allowed=await client.CallToolAsync("set_pml_function_permission",new Dictionary<string,object?>{{"functionName","Demo"},{"allowInvoke",true}});
        Assert.NotEqual(true,allowed.IsError);
        var unselected=await client.CallToolAsync("invoke_pml_function",arguments);
        Assert.Contains("TARGET_NOT_SELECTED",unselected.Content.OfType<TextContentBlock>().Single().Text);
        File.AppendAllText(source,"\n-- changed source");
        var changed=await client.CallToolAsync("invoke_pml_function",arguments);
        Assert.True(changed.IsError);
        Assert.Contains("PML_FUNCTION_SOURCE_CHANGED",changed.Content.OfType<TextContentBlock>().Single().Text);
        var bounded=await client.CallToolAsync("read_pml_array",new Dictionary<string,object?>{{"globalVar","DemoArray"}});
        Assert.True(bounded.IsError);
        Assert.Contains("TARGET_NOT_SELECTED",bounded.Content.OfType<TextContentBlock>().Single().Text);
    }

    private async Task<McpClient> ConnectAsync(string server, params string[] arguments)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "dotnet", Arguments = new[] { ServerDll(server) }.Concat(arguments).ToList(),
            EnvironmentVariables = EnvironmentOverrides(), Name = "dual-mode-test"
        });
        return await McpClient.CreateAsync(transport);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(string server, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(ServerDll(server));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var item in EnvironmentOverrides()) start.Environment[item.Key] = item.Value;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output, await error);
    }

    private static string ServerDll(string server)
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "YuzuhaToolkit.Development.slnx")))
                return Path.Combine(current.FullName, "src", server, "bin", "Release", "net10.0", "win-x64", server + ".dll");
        throw new DirectoryNotFoundException("Yuzuha repository not found.");
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_temporary);
        if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("yuzuha-cli-test-", StringComparison.Ordinal) && Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
