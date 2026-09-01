using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio reserves stdout for JSON-RPC messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });

builder.Services.AddSingleton<AvevaSessionDiscovery>();
builder.Services.AddSingleton<YuzuhaRpcBridge>();
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = McpUsageInstructions.Text; })
    .WithStdioServerTransport()
    .WithTools<PmlCallTools>(YuzuhaToolJsonContext.Default.Options);

await builder.Build().RunAsync();
return 0;
