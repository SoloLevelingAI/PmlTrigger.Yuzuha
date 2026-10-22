using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>原生开发入口：撤销分组、目录健康和统一 MCP 工具状态。</summary>
[McpServerToolType]
public sealed class WorkspaceTools(PmlCallTools pml)
{
    /// <summary>在当前已核验会话中执行 MARKDB，开始一个带描述的撤销分组。</summary>
    /// <param name="description">撤销列表中的描述；最多 256 字符，不含控制字符。</param>
    /// <param name="cancellationToken">取消等待，不自动重发命令。</param>
    [McpServerTool(Name = "markdb")]
    [Description("Legacy compatibility name. Updated execution automatically manages MARKDB/body/ENDMARKDB in one Host callback. Manual nested boundaries are rejected with PML_MARKDB_MANAGED; do not wrap normal calls with this tool. Never auto-retry a timeout.")]
    public Task<string> MarkDb([Description("Undo-group description; up to 256 characters.")] string description = "Yuzuha plugin development", CancellationToken cancellationToken = default)
        => pml.RunPmlCommand(BuildMarkDb(description), cancellationToken);

    /// <summary>在当前已核验会话中执行 ENDMARKDB，结束撤销分组。</summary>
    /// <param name="cancellationToken">取消等待，不自动重发命令。</param>
    [McpServerTool(Name = "endmarkdb")]
    [Description("Legacy compatibility name. Automatic Host execution owns ENDMARKDB; manual nested boundaries are rejected. Inspect failed or unknown group receipts rather than retrying this tool. Does not save work or undo edits.")]
    public Task<string> EndMarkDb(CancellationToken cancellationToken = default) => pml.RunPmlCommand("ENDMARKDB", cancellationToken);

    /// <summary>生成 MARKDB 命令并转义描述，便于无 AVEVA 的自动化检查。</summary>
    /// <param name="description">用户提供的撤销描述。</param>
    public static string BuildMarkDb(string description)
    {
        if (description.Length > 256 || description.Any(char.IsControl)) throw new ArgumentException("MARKDB 描述不能超过 256 字符或包含控制字符。");
        return "MARKDB '" + description.Replace("'", "''") + "'";
    }

    /// <summary>扫描用户指定或工作区保存的注册目录，只读统计并报告清理提醒。</summary>
    /// <param name="directory">可选的已注册 PMLLIB 目录；省略则使用启动台设置。</param>
    [McpServerTool]
    [Description("Read-only bounded scan of the registered Yuzuha PMLLIB folder. Reports file counts, sizes, large folders, development artifacts and cleanup reminders. Does not delete files, change registration or execute PML REHASH ALL.")]
    public string InspectRegisteredPmllib(string? directory = null)
    {
        var settings = WorkspaceSettings.Load();
        var root = directory ?? settings.RegisteredPmlLib;
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("请在原生启动台选择已注册 PMLLIB 目录，或提供 directory。");
        return JsonSerializer.Serialize(DirectoryInspector.Scan(root, settings.WarnFiles, settings.WarnBytes), WorkspaceJsonContext.Default.DirectoryHealth);
    }

    /// <summary>读取已启动 MCP 进程的心跳和工具状态，不创建新的 AVEVA 会话。</summary>
    [McpServerTool]
    [Description("Read local Yuzuha MCP service heartbeats, selected target PIDs, active tool calls and logging faults. Observes tools, not the AI model's private reasoning.")]
    public string GetAiRuntimeStatus() => JsonSerializer.Serialize(RuntimeReader.Statuses(), WorkspaceJsonContext.Default.RuntimeStatusArray);

    /// <summary>读取有界近期工具事件，关联开始、结果和耗时。</summary>
    /// <param name="limit">最多 200 条，默认 50。</param>
    [McpServerTool]
    [Description("Read recent local tool-call audit events including names, target PID, bounded arguments, duration and outcome. Named password/token fields are redacted. Raw PML text may contain project information.")]
    public string GetRecentToolCalls(int limit = 50) => JsonSerializer.Serialize(RuntimeReader.Recent(limit), WorkspaceJsonContext.Default.ActivityEntryArray);
}
