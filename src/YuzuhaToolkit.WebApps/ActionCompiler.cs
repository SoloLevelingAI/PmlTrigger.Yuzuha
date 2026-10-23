using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.WebApps;

/// <summary>生成经过类型检查的独立宏和结果变量；私有文件不在网页静态目录中。</summary>
public sealed record CompiledAction(string Macro, string ResultGlobal, string SourceFile, string SourceSha256);

/// <summary>将已登记的按钮和实参变为 PML 宏，不注册 PMLCMD，也不更改旧调用。</summary>
public static class ActionCompiler
{
    /// <summary>按声明类型编码标量，拒绝多行、PML 展开符和无效 DBREF/属性名。</summary>
    public static string Literal(ParameterSpec parameter, JsonElement value)
    {
        if (parameter.Type is "real" or "integer")
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var n) || !double.IsFinite(n) ||
                parameter.Type == "integer" && Math.Truncate(n) != n ||
                parameter.Min is { } min && n < min || parameter.Max is { } max && n > max)
                throw new ArgumentException(parameter.Label + "：数值类型或范围无效。");
            return n.ToString("0.###############################", CultureInfo.InvariantCulture);
        }
        if (parameter.Type == "bool")
        {
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new ArgumentException("布尔参数无效。");
            return value.GetBoolean() ? "TRUE" : "FALSE";
        }
        if (parameter.Type is not ("string" or "dbref" or "attribute") || value.ValueKind != JsonValueKind.String)
            throw new ArgumentException("参数类型无效：" + parameter.Type);
        var text = value.GetString()!;
        if (text.Length > 2048 || text.Any(c => char.IsControl(c) || c == '$') ||
            parameter.Type == "dbref" && !Regex.IsMatch(text, "^=[0-9]+/[0-9]+$") ||
            parameter.Type == "attribute" && !Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9_]{0,79}$"))
            throw new ArgumentException(parameter.Label + "：文本格式无效。");
        // Reuse the existing scalar encoder after excluding PML preprocessor expansion.
        var call = PmlMethodSignatureGenerator.Build("Value", new DynamicParameter("string", text));
        return call[8..^1];
    }

    /// <summary>找到唯一的已部署目标源码，并生成结果数组赋值及必要的对象实例。</summary>
    public static CompiledAction Compile(ActionBinding action, IReadOnlyDictionary<string, JsonElement> values,
        string pmlRoot, string requestId)
    {
        ArgumentNullException.ThrowIfNull(values);
        CatalogStore.Validate(new(1, 1, ["test"], [new("test", "test", "test", "", "table", true, [action])]));
        if (values.Keys.Except(action.Parameters.Select(p => p.Name), StringComparer.Ordinal).Any())
            throw new ArgumentException("包含未登记的参数。");
        if (!Regex.IsMatch(requestId, "^[a-f0-9]{32}$")) throw new ArgumentException("请求标识无效。");
        var extension = action.Kind switch { "object" => ".pmlobj", "form" => ".pmlfrm", _ => ".pmlfnc" };
        var candidates = Directory.EnumerateFiles(pmlRoot, "*" + extension, SearchOption.AllDirectories)
            .Where(f => string.Equals(Path.GetFileNameWithoutExtension(f), action.Target, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length != 1) throw new ArgumentException("目标源码缺失或存在同名冲突：" + action.Target);
        var file = candidates[0];
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        var args = string.Join(",", action.Parameters.Select(p => Literal(p, values.TryGetValue(p.Name, out var v) ? v : p.Default)));
        var global = "YzWeb" + requestId[..24];
        var text = new StringBuilder();
        string expression;
        switch (action.Kind)
        {
            case "object":
                text.AppendLine("!webObject = object " + action.Target + "()");
                expression = "!webObject." + action.Method + "(" + args + ")";
                break;
            case "form" when action.Method == "show":
                expression = "show !!" + action.Target;
                break;
            case "form": expression = "!!" + action.Target + "." + action.Method + "(" + args + ")"; break;
            default: expression = "!!" + action.Target + "(" + args + ")"; break;
        }
        if (action.Result == "array") text.AppendLine("!!" + global + " = " + expression);
        else
        {
            if (action.Result == "scalar") text.AppendLine("!webValue = " + expression);
            else text.AppendLine(expression);
            text.AppendLine("!!" + global + " = object array()");
            text.AppendLine("!!" + global + ".append('[0] Result<STRING> " +
                (action.Result == "scalar" ? "' + !webValue.string())" : "Dispatched')"));
        }
        return new(text.ToString(), global, file, hash);
    }
}
