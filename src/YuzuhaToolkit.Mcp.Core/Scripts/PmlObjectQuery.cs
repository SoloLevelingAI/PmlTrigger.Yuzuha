using System.Text.RegularExpressions;
using System.IO;
using System.Text.Json;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>将纯数据查询条件转成已有内置遍历函数调用，供界面与其他入口复用。</summary>
public sealed record PmlObjectQuery(string Kind, string Expression)
{
    /// <summary>当前对象、指定元素或全局变量，分别保留成员路径与遍历预算。</summary>
    public static PmlObjectQuery Build(string kind, string target, string memberPath, int size, int depth)
    {
        if (size is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(size), "每层遍历上限为 1–1000。");
        if (depth is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(depth), "遍历深度为 0–10。");
        memberPath = memberPath.Trim(); target = target.Trim();
        if (memberPath.Length > 256 || memberPath.Length > 0 && !Regex.IsMatch(memberPath, @"^(?:[A-Za-z_][A-Za-z0-9_]*|[0-9]+)(?:\.(?:[A-Za-z_][A-Za-z0-9_]*|[0-9]+))*$")) throw new ArgumentException("成员路径只接受名称、索引和点号，不接受表达式。");
        var parameters = new List<DynamicParameter> { new("real", size), new("real", depth), new("string", memberPath) };
        string function;
        switch (kind)
        {
            case "current": function = "YuzuhaReadCurrentElement"; break;
            case "element":
                if (string.IsNullOrWhiteSpace(target) || target.Length > 512 || target.Any(char.IsControl)) throw new ArgumentException("请填写元素名称、完整路径或 DBREF。");
                function = "YuzuhaReadDbref"; parameters.Add(new("string", target)); break;
            case "global":
                if (target.StartsWith("!!", StringComparison.Ordinal)) target = target[2..];
                if (!Regex.IsMatch(target, @"^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("全局变量名不能包含方法或表达式；成员放在成员路径中。");
                function = "YuzuhaReadGlobal"; parameters.Add(new("string", target)); break;
            default: throw new ArgumentException("未知对象查询范围。");
        }
        return new(kind, PmlMethodSignatureGenerator.Build(function, parameters));
    }
}

/// <summary>保留现有列表工具解析的类型、路径、空值、未解析行和同次原始回执。</summary>
public sealed record PmlObjectReadReply(RunPmlCommandResponse Receipt, PmlListItem[] Items, string[] Unparsed, string Raw)
{
    /// <summary>使用已有工具的结构化结果，不另外解析 PML 原始行。</summary>
    public static PmlObjectReadReply Parse(string raw)
    {
        var receipt = WorkbenchReply.ParseRegisteredCall(raw);
        using var document = JsonDocument.Parse(raw); var root = document.RootElement;
        if (receipt.Success && !root.TryGetProperty("Items", out _)) throw new IOException("对象查询缺少列表回执，结果未知。");
        var payload = JsonSerializer.Deserialize(root, new YuzuhaJsonContext(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).PmlListToolResponse)!;
        return new(receipt, payload.Items?.ToArray() ?? [], payload.Unparsed?.ToArray() ?? [], raw);
    }
}
