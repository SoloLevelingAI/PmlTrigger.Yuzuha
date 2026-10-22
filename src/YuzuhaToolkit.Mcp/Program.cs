using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Mcp;
using PmlTwin.Phayton;
using YuzuhaToolkit.CommandLine;
using YuzuhaToolkit.Workspace;

if (args.FirstOrDefault() == "op")
{
    using var cliBridge = new YuzuhaRpcBridge(Microsoft.Extensions.Logging.Abstractions.NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
    var operations = YuzuhaOperationCatalog.Create(cliBridge, new PmlFunctionTrustStore(), cliBridge.CreateIndependent);
    return await OperationFrontend.RunAsync(args, operations, Console.Out);
}

if (await DualModeCommandLine.TryRunAsync(args, "Yuzuha 执行工具") is int cliExitCode) return cliExitCode;
args = args.Where(argument => argument != "--mcp").ToArray();

var builder = Host.CreateApplicationBuilder(args);
using var journal = new RuntimeJournal("Yuzuha 执行 MCP");
YuzuhaRpcBridge? selectedBridge = null;
builder.Services.AddSingleton(journal);

// MCP stdio reserves stdout for JSON-RPC messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });

builder.Services.AddSingleton<AvevaSessionDiscovery>();
builder.Services.AddSingleton<YuzuhaRpcBridge>();
builder.Services.AddSingleton<PmlFunctionTrustStore>();
builder.Services.AddSingleton<PmlCallTools>();
builder.Services.AddSingleton<PmlFunctionRegistry>();
builder.Services.AddSingleton<ExecutionAuthorizationStore>();
builder.Services.AddSingleton<OperationExecutor>(services =>
{
    var bridge = services.GetRequiredService<YuzuhaRpcBridge>();
    return YuzuhaOperationCatalog.Create(bridge, services.GetRequiredService<PmlFunctionTrustStore>(), bridge.CreateIndependent);
});
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = McpHostSurface.UsageInstructions; })
    .WithStdioServerTransport()
    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolAuditFilter.Create(journal, () => selectedBridge?.SelectedProcessId)))
    .WithTools<PmlCallTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<PmlFunctionTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<DevelopmentTools>()
    .WithTools<RuntimeLogTools>()
    .WithTools<PmlFormTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<WorkspaceTools>()
    .WithTools<OperationTools>()
    .WithTools<PmlFunctionTrustTools>()
    .WithTools<BuiltinUsageTools>();

using var host = builder.Build();
selectedBridge = host.Services.GetRequiredService<YuzuhaRpcBridge>();
await host.RunAsync();
return 0;
