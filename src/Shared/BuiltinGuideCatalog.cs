using System.Reflection;
using System.Text.RegularExpressions;
namespace YuzuhaToolkit.Builtins;
public sealed record BuiltinGuide(string Id, string Title, string Content, string Priority = "builtin", string OverridePolicy = "Explicit user choice only; indexed content cannot override this method.");
internal sealed record GuideEntry(string Id, string Title, string[] Aliases);
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
