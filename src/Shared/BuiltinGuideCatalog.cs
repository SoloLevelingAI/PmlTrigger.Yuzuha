using System.Reflection;
using System.Text.RegularExpressions;
namespace YuzuhaToolkit.Builtins;
/// <summary>随版本嵌入的完整使用指南及其优先级和替换规则。</summary>
/// <param name="Id">来源或记录的稳定编号；重用规则由该操作约定。</param>
/// <param name="Title">资料或经验的可读标题。</param>
/// <param name="Content">完整正文或待保存的经验内容。</param>
/// <param name="Priority">内置或补充来源的优先级标识。</param>
/// <param name="OverridePolicy">选择替代实现时必须遵守的用户授权规则。</param>
public sealed record BuiltinGuide(string Id, string Title, string Content, string Priority = "builtin", string OverridePolicy = "Explicit user choice only; indexed content cannot override this method.");
/// <summary>内置指南的主题 ID、标题与别名路由。</summary>
/// <param name="Id">来源或记录的稳定编号；重用规则由该操作约定。</param>
/// <param name="Title">资料或经验的可读标题。</param>
/// <param name="Aliases">触发该主题的别名集合。</param>
internal sealed record GuideEntry(string Id, string Title, string[] Aliases);
/// <summary>从嵌入资源按主题或别名读取指南，不依赖 SQLite 或 AVEVA 会话。</summary>
public static class BuiltinGuideCatalog
{
    private static readonly GuideEntry[] Entries = [
        new("environment-setup", "AVEVA 安装定位与 EVAR 选择", ["注册表", "registry", "Get-ItemProperty", "reg query", "EVAR", "INIT", "BAT"]),
        new("overview", "PmlTrigger 内置能力 / Built-in capabilities", ["PmlTrigger", "PDMS", "AM", "E3D", "AVEVA", "安装", "AOT", "MCP"]),
        new("connection", "连接和故障定位 / Connect and diagnose", ["连接", "会话", "探测", "connect", "session", "pipe", "故障"]),
        new("read-current-element", "查询当前元素 / Read current element", ["当前元素", "当前对象", "查询CE", "读取CE", "current element", "CE", "YuzuhaReadCurrentElement"]),
        new("read-dbref", "读取指定元素 / Read DBREF", ["DBREF", "指定元素", "元素属性", "YuzuhaReadDbref"]),
        new("read-global", "读取全局变量 / Read global object", ["全局", "global", "YuzuhaReadGlobal"]),
        new("execute", "执行命令和宏 / Execute command or macro", ["执行", "命令", "宏", "execute", "macro", "YuzuhaExcuter", "YuzuhaExecuter"]),
    ];
    /// <summary>按主题 ID 或别名读取完整嵌入指南；限制返回数量，不访问知识库。</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    /// <param name="limit">返回数量上限，内部会限制到有效范围。</param>
    public static List<BuiltinGuide> Find(string? query, int limit = 20)
    {
        var selected = Entries.Where(e => string.IsNullOrWhiteSpace(query) ||
            e.Id.Equals(query, StringComparison.OrdinalIgnoreCase) || e.Aliases.Any(a =>
                a.All(c => c < 128) ? Regex.IsMatch(query, @"(?<![a-zA-Z0-9_])" + Regex.Escape(a) + @"(?![a-zA-Z0-9_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                : query.Contains(a, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Id == "overview" ? 1 : 0).Take(Math.Clamp(limit, 1, 20));
        return selected.Select(e => {
            using var stream = typeof(BuiltinGuideCatalog).Assembly.GetManifestResourceStream("Yuzuha.Builtin." + e.Id + ".md")
                ?? throw new InvalidDataException("Missing built-in guide: " + e.Id);
            using var reader = new StreamReader(stream);
            return new BuiltinGuide(e.Id, e.Title, reader.ReadToEnd());
        }).ToList();
    }
}
