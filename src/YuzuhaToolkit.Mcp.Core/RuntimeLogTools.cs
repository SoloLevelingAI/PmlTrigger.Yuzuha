using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>CLI 和 MCP 共享的有界日志查询与导出；不执行 PML。</summary>
[McpServerToolType]
public sealed class RuntimeLogTools
{
    /// <summary>按完整会话身份、工具、结果和文本查询保留日志，最多 200 条。</summary>
    [McpServerTool, Description("Query locally retained runtime logs, filtered before the 200-entry limit. Optional PID plus process-start ticks identifies one session; old records without ticks do not match that identity. tool and phase are exact matches; text searches tool/call ID/PID/summary. Bounded retained segments only, not complete history.")]
    public string QueryRuntimeLogs(int? targetPid = null, long? targetStartTimeUtcTicks = null, string? tool = null, string? phase = null, string? text = null, int limit = 200)
        => JsonSerializer.Serialize(RuntimeReader.Query(targetPid, targetStartTimeUtcTicks, tool, phase, text, limit), WorkspaceJsonContext.Default.ActivityEntryArray);

    /// <summary>仅创建新的 JSONL 导出文件，不覆盖已有证据。</summary>
    [McpServerTool, Description("Export the same filtered, bounded runtime logs to a new local UTF-8 JSONL file. Refuses to overwrite existing evidence. Named secret fields are already redacted; PML source may contain project data.")]
    public string ExportRuntimeLogs(string outputFile, int? targetPid = null, long? targetStartTimeUtcTicks = null, string? tool = null, string? phase = null, string? text = null, int limit = 200)
    {
        var count = RuntimeReader.Export(outputFile, RuntimeReader.Query(targetPid, targetStartTimeUtcTicks, tool, phase, text, limit));
        return "Exported " + count + " retained events to " + Path.GetFullPath(outputFile);
    }
}
