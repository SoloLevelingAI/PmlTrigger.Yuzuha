using System.Text.Json;
using System.Text.Json.Nodes;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>为独立 op CLI 记录与 MCP 一样的调用起止；共享操作执行器仍负责调度。</summary>
public static class OperationFrontend
{
    /// <summary>执行一次 op CLI，记录参数、目标和退出状态；不自动重试。</summary>
    public static async Task<int> RunAsync(string[] args, OperationExecutor executor, TextWriter output, CancellationToken token = default)
    {
        using var journal = new RuntimeJournal("Phayton CLI");
        var metadata = new JsonObject { ["command"] = args.ElementAtOrDefault(1), ["operation"] = args.ElementAtOrDefault(2) };
        var inputIndex = Array.IndexOf(args,"--input");
        if (inputIndex >= 0 && inputIndex+1 < args.Length)
        {
            try { metadata["input"] = JsonNode.Parse(args[inputIndex+1]); } catch(JsonException) { metadata["input"]="[invalid JSON]"; }
        }
        var pidIndex = Array.IndexOf(args,"--pid");
        int? pid = pidIndex >= 0 && pidIndex+1 < args.Length && int.TryParse(args[pidIndex+1],out var value) ? value : null;
        var id = journal.Begin("op." + args.ElementAtOrDefault(1),JsonSerializer.Serialize(metadata,YuzuhaToolJsonContext.Default.JsonObject),pid);
        try
        {
            if(journal.Snapshot().LoggingError != null) throw new IOException("PML_AUDIT_UNAVAILABLE");
            var exit = await OperationCli.RunAsync(args,executor,output,token).ConfigureAwait(false);
            journal.Complete(id,exit==0?"succeeded":exit==OperationExitCodes.Cancelled?"cancelled-unknown":"failed","exit="+exit,pid);
            return exit;
        }
        catch(Exception error) { journal.Complete(id,error is OperationCanceledException?"cancelled-unknown":"failed",error.Message,pid);throw; }
    }
}
