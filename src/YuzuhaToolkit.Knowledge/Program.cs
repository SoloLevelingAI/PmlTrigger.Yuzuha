using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Knowledge;

if (args.Length == 2 && args[0] == "--refresh-project")
{
    var repository = new KnowledgeRepository();
    var result = repository.RefreshProject(Path.GetFullPath(args[1]));
    repository.EnsureExperience();
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result,
        KnowledgeResponseJsonContext.Default.KnowledgeBuildResult));
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio reserves stdout for JSON-RPC messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });

builder.Services.AddSingleton<KnowledgeRepository>();
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = KnowledgeUsageInstructions.Text; })
    .WithStdioServerTransport()
    .WithTools<KnowledgeTools>()
    .WithTools<KnowledgeLayerTools>();

await builder.Build().RunAsync();
return 0;
