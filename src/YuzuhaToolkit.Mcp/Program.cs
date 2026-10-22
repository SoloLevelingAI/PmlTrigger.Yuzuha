using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.CommandLine;
using YuzuhaToolkit.Workspace;

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
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = McpHostSurface.UsageInstructions; })
    .WithStdioServerTransport()
    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolAuditFilter.Create(journal, () => selectedBridge?.SelectedProcessId)))
    .WithTools<PmlCallTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<PmlFunctionTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<PmlFormTools>(McpHostSurface.ToolJsonOptions)
    .WithTools<WorkspaceTools>()
    .WithTools<PmlFunctionTrustTools>()
    .WithTools<BuiltinUsageTools>();

using var host = builder.Build();
selectedBridge = host.Services.GetRequiredService<YuzuhaRpcBridge>();
await host.RunAsync();
return 0;
