using System.Text.Json;
using System.Text.RegularExpressions;

namespace PmlTwin.Phayton;

// The documented schema subset used by built-in registrations. Rejects unknown/duplicate keys.
/// <summary>验证注册操作采用的 JSON Schema 子集，拒绝未知或重复字段。</summary>
internal static class InputContract
{
    /// <summary>按实际契约检查输入类型、必填字段、值域与结构，返回首个错误。</summary>
    public static string? Validate(JsonElement value, JsonElement schema, string path = "$")
    {
        if (schema.TryGetProperty("anyOf", out var alternatives))
            return alternatives.EnumerateArray().Any(s => Validate(value, s, path) == null)
                ? null : path + " must match one of the documented types.";
        var type = schema.GetProperty("type").GetString();
        var matches = type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n),
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };
        if (!matches) return path + " must have type " + type + ".";
        if (schema.TryGetProperty("enum", out var options) &&
            !options.EnumerateArray().Any(e => JsonElement.DeepEquals(e, value)))
            return path + " is not an allowed value.";
        if (type == "object")
        {
            var properties = schema.GetProperty("properties");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return path + "." + property.Name + " is duplicated.";
                if (!properties.TryGetProperty(property.Name, out var child))
                    return path + "." + property.Name + " is not a known parameter.";
                var error = Validate(property.Value, child, path + "." + property.Name);
                if (error != null) return error;
            }
            if (schema.TryGetProperty("required", out var required))
                foreach (var key in required.EnumerateArray())
                    if (!seen.Contains(key.GetString()!)) return path + "." + key.GetString() + " is required.";
        }
        if (type == "array")
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var error = Validate(item, schema.GetProperty("items"), path + "[" + index++ + "]");
                if (error != null) return error;
            }
        }
        if (type == "string")
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var minimum) && text.Trim().Length < minimum.GetInt32())
                return path + " must not be blank.";
            if (schema.TryGetProperty("pattern", out var pattern) &&
                !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                return path + " has an invalid format.";
        }
        return null;
    }
}
