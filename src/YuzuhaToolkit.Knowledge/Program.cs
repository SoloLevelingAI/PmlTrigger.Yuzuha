using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.CommandLine;
using YuzuhaToolkit.Workspace;

if (await DualModeCommandLine.TryRunAsync(args, "Yuzuha 知识工具") is int cliExitCode) return cliExitCode;
args = args.Where(argument => argument != "--mcp").ToArray();

if (args.Length == 2 && args[0] == "--refresh-project")
{
    var repository = new KnowledgeRepository();
    var result = repository.RefreshProject(Path.GetFullPath(args[1]));
    repository.EnsureExperience();
    Console.WriteLine(KnowledgeJsonSurface.SerializeBuildResult(result));
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);
using var journal = new RuntimeJournal("Yuzuha 知识 MCP");

// MCP stdio reserves stdout for JSON-RPC messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });

builder.Services.AddSingleton<KnowledgeRepository>();
builder.Services
    .AddMcpServer(options => { options.ServerInstructions = KnowledgeUsageInstructions.Text; })
    .WithStdioServerTransport()
    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolAuditFilter.Create(journal)))
    .WithTools<KnowledgeTools>()
    .WithTools<KnowledgeLayerTools>();

await builder.Build().RunAsync();
return 0;
