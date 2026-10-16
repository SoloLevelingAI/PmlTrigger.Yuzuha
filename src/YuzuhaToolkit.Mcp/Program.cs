using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Mcp;
using PmlTwin.Phayton;

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio reserves stdout for JSON-RPC messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });

builder.Services.AddSingleton<AvevaSessionDiscovery>();
builder.Services.AddSingleton<YuzuhaRpcBridge>();
builder.Services.AddSingleton<PmlFunctionTrustStore>();
builder.Services.AddSingleton<OperationExecutor>(services =>
{
    var bridge = services.GetRequiredService<YuzuhaRpcBridge>();
    return YuzuhaOperationCatalog.Create(bridge,
        services.GetRequiredService<PmlFunctionTrustStore>(), bridge.CreateIndependent);
});
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = McpUsageInstructions.Text; })
    .WithStdioServerTransport()
    .WithTools<PmlCallTools>(YuzuhaToolJsonContext.Default.Options)
    .WithTools<OperationTools>()
    .WithTools<PmlFunctionTrustTools>()
    .WithTools<BuiltinUsageTools>();

await builder.Build().RunAsync();
return 0;
