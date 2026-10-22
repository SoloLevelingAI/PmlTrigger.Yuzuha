using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>仅汇总现有有界工具记录，未包含已轮转日志，不推断全天或长期全量。</summary>
internal sealed record DesktopActivitySummary(int Completed, int Successful, double? AverageMilliseconds, double? SuccessPercent, IReadOnlyList<DesktopActivityDay> Days)
{
    /// <summary>完成项含失败与未知；图表按显示时区分组，空日保持零项。</summary>
    internal static DesktopActivitySummary Create(IEnumerable<ActivityEntry> entries, DateTimeOffset now)
    {
        var completed = entries.Where(item => item.Phase != "started" && item.Tool is not ("launch_aveva" or "copy_plugin")).ToArray();
        var lastDay = DateOnly.FromDateTime(now.Date); var firstDay = lastDay.AddDays(-6);
        var days = Enumerable.Range(0, 7).Select(index =>
        {
            var date = firstDay.AddDays(index); var rows = completed.Where(item => DateOnly.FromDateTime(item.TimeUtc.ToOffset(now.Offset).Date) == date).ToArray();
            return new DesktopActivityDay(date.ToString("MM/dd"), rows.Count(item => Category(item.Tool) == "query"), rows.Count(item => Category(item.Tool) == "execute"), rows.Count(item => Category(item.Tool) == "knowledge"));
        }).ToArray();
        var successful = completed.Count(item => item.Phase == "succeeded");
        return new(completed.Length, successful, completed.Length == 0 ? null : completed.Average(item => item.Milliseconds), completed.Length == 0 ? null : 100d * successful / completed.Length, days);
    }
    private static string Category(string tool) => tool.Contains("knowledge", StringComparison.OrdinalIgnoreCase) || tool.Contains("search", StringComparison.OrdinalIgnoreCase) ? "knowledge" : tool.Contains("call", StringComparison.OrdinalIgnoreCase) || tool.Contains("run_pml", StringComparison.OrdinalIgnoreCase) || tool.Contains("function", StringComparison.OrdinalIgnoreCase) ? "execute" : "query";
}

/// <summary>某个显示日内的已保留调用数；名称分类用于图表，不用于授权或结果判定。</summary>
internal sealed record DesktopActivityDay(string Label, int Query, int Execute, int Knowledge)
{
    /// <summary>本日所有已保留完成项数量，包含失败和未知。</summary>
    public int Total => Query + Execute + Knowledge;
}
