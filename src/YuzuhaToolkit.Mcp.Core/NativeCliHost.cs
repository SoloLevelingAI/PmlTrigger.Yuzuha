using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>独立原生 CLI 的进程入口；旧 MCP 文件名仅保留命令兼容，不启动 stdio 服务。</summary>
public static class NativeCliHost
{
    /// <summary>构建真实服务、处理取消并记录调用；所有 Host 调用仍要求显式 PID。</summary>
    public static async Task<int> RunAsync(string[] args, bool legacyMcp = false)
    {
        // JSON pipes have a stable UTF-8 contract regardless of Windows console code page.
        Console.InputEncoding = new UTF8Encoding(false, true);
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine(JsonSerializer.Serialize(OperationResult.Ok("cli.help", JsonSerializer.SerializeToElement(
                "Phayton native CLI: op list | op describe <id> | op invoke <id> | op batch [--input <json> | --input-file <path> | --input-stdin] [--pid <pid>] [--expected-model <module>] [--allow-execution] [--allow-write] [--allow-launch] [--timeout-seconds <1..3600>]. Batch accepts [{operationId,input,processId?,expectedModel?}], stops on first failure and retains earlier results. Standalone MCP is disabled. All calls use local services directly.",
                OperationJsonContext.Default.String)), OperationJsonContext.Default.OperationResult));
            return 0;
        }
        if (args.SequenceEqual(new[] { "--cli", "list" })) args = ["op", "list"];
        if (args.SequenceEqual(new[] { "--cli", "web", "open" }) || args.SequenceEqual(new[] { "--cli", "web", "open", "--no-browser" }))
        {
            var url = await HtmlAppLauncher.OpenAsync(openBrowser: !args.Contains("--no-browser"));
            Console.WriteLine(url);
            return 0;
        }
        // Retain the explicitly requested package-reference maintenance command.
        // It is a direct local call and never enables the removed MCP transport.
        if (args.Length == 2 && args[0] == "--refresh-project")
        {
            using var maintenance = new RuntimeJournal("Phayton CLI maintenance");
            var call = maintenance.Begin("knowledge.project.refresh", "{}");
            try
            {
                var repository = new KnowledgeRepository();
                var result = repository.RefreshProject(Path.GetFullPath(args[1]));
                repository.EnsureExperience();
                Console.WriteLine(KnowledgeJsonSurface.SerializeBuildResult(result));
                maintenance.Complete(call, result.Ok ? "succeeded" : "failed", "Package reference refresh completed.");
                return result.Ok ? 0 : 2;
            }
            catch (Exception error) when (error is IOException or ArgumentException or KnowledgeException)
            {
                maintenance.Complete(call, "failed", error.Message);
                Console.WriteLine(JsonSerializer.Serialize(OperationResult.Fail("knowledge.project.refresh", 2, "REFRESH_FAILED", error.Message), OperationJsonContext.Default.OperationResult));
                return 2;
            }
        }
        if (args.FirstOrDefault() != "op")
        {
            if (legacyMcp || args.Contains("--mcp"))
            {
                Console.WriteLine(JsonSerializer.Serialize(OperationResult.Fail("legacy.mcp", 3, "MCP_DISABLED",
                    "Standalone MCP is disabled. Use PmlTwin.Phayton.Cli.exe op list / op describe / op invoke with JSON, file or stdin input."), OperationJsonContext.Default.OperationResult));
                return 3;
            }
        }
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
        using var journal = new RuntimeJournal("Phayton CLI");
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            var executor = NativeOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), bridge.CreateIndependent,
                journal: journal, additional: LaunchOperationCatalog.Registrations());
            return await OperationCli.RunAsync(args, executor, Console.Out, cancellation.Token).ConfigureAwait(false);
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
