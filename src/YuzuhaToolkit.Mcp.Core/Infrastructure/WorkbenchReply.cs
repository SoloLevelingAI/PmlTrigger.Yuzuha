using System.Text.Json;

namespace YuzuhaToolkit.Mcp;

/// <summary>适配已有注册工具的 JSON 成功回执和文本传输错误。</summary>
public static class WorkbenchReply
{
    private static readonly YuzuhaJsonContext Json = new(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    /// <summary>不可解析或缺失的回执保持未知，不能误报业务失败或通过。</summary>
    public static RunPmlCommandResponse ParseRegisteredCall(string text)
    {
        if (text.StartsWith("PML RPC failed:", StringComparison.Ordinal)) throw new IOException(text);
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals("Success", StringComparison.OrdinalIgnoreCase)).Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new IOException("注册调用回执缺少明确的 Success 状态，结果未知。");
            return JsonSerializer.Deserialize(text, Json.RunPmlCommandResponse) ?? throw new IOException("注册调用缺少回执。");
        }
        catch (JsonException error) { throw new IOException("无法解析注册调用回执，执行结果未知。", error); }
    }
}

