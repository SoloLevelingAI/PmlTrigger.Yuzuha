using System.Globalization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>对已读取的本机记录应用状态、操作和文字条件；不重读日志或改变记录。</summary>
internal static class DesktopActivityFilter
{
    /// <summary>条件取交集，保持输入顺序；未知结果不并入失败或成功。</summary>
    internal static ActivityEntry[] Apply(IEnumerable<ActivityEntry> entries, string phase, string? operation, string? query)
    {
        var text = query?.Trim() ?? "";
        return entries.Where(e =>
            (phase == "all" || (phase == "unknown" ? e.Phase is "unknown" or "cancelled-unknown" : e.Phase == phase)) &&
            (operation is null || e.Tool == operation) &&
            (text.Length == 0 || new[] { e.Tool, e.CallId, e.Summary, e.Service, e.PhaseLabel,
                e.TargetPid?.ToString(CultureInfo.InvariantCulture) ?? "", e.ProcessId.ToString(CultureInfo.InvariantCulture) }
                .Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }
}
