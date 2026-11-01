using System.Text.Json;

namespace YuzuhaToolkit.Manager;

/// <summary>冻结部署输入；apply 只接受服务端计划引用，不能替换项目或目标。</summary>
internal static class RibbonV2Schemas
{
    internal static JsonElement Input(string route)
    {
        const string reference = """
        {"type":"object","additionalProperties":false,"required":["planId","planDigest"],"properties":{"planId":{"type":"string","format":"uuid"},"planDigest":{"type":"string","pattern":"^[a-f0-9]{64}$"}}}
        """;
        if (route.EndsWith("-apply", StringComparison.Ordinal))
        { using var document = JsonDocument.Parse(reference); return document.RootElement.Clone(); }
        var registration = route.Contains("registration", StringComparison.Ordinal);
        var required = registration ? "[\"project\",\"monPath\",\"manifestName\",\"entryName\"]" : "[\"project\"]";
        var extra = registration ? ",\"monPath\":{\"type\":\"string\",\"minLength\":1},\"manifestName\":{\"type\":\"string\",\"minLength\":1},\"entryName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":100}" : "";
        const string project = """
        {"type":"object","additionalProperties":false,"properties":{"schemaVersion":{"type":"integer","enum":[1]},"namespace":{"type":"string"},"documentId":{"type":"string","pattern":"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"},"origin":{"type":"object","additionalProperties":false,"properties":{"fileName":{"type":"string"},"xml":{"type":"string"},"sourcePath":{"type":"string"},"sourceHash":{"type":"string"}},"required":["fileName","xml","sourceHash"]},"preferredBinding":{"type":"object","additionalProperties":false,"properties":{"monPath":{"type":"string"},"manifestName":{"type":"string"},"entryName":{"type":"string"},"registrationPath":{"type":"string"},"filePath":{"type":"string"}},"required":["monPath","manifestName","entryName"]},"buttons":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{"sourceId":{"type":"string"},"id":{"type":"string"},"caption":{"type":"string"},"macro":{"type":"string"},"commandKey":{"type":"string"},"tooltipText":{"type":"string"},"tooltipTitle":{"type":"string"},"image":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"properties":{"mediaType":{"type":"string","enum":["image/png","image/jpeg"]},"base64":{"type":"string"}},"required":["mediaType","base64"]}]}},"required":["id","caption","macro","commandKey","tooltipText","tooltipTitle"]}},"tabs":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{"sourceId":{"type":"string"},"id":{"type":"string"},"caption":{"type":"string"},"groups":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{"sourceId":{"type":"string"},"id":{"type":"string"},"caption":{"type":"string"},"items":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{"buttonId":{"type":"string"},"size":{"type":"string","enum":["large","small"]}},"required":["buttonId","size"]}}},"required":["id","caption","items"]}}},"required":["id","caption","groups"]}}},"required":["schemaVersion","namespace","documentId","buttons","tabs"]}
        """;
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"required\":" + required + ",\"properties\":{\"project\":" + project + extra + "}}");
        return schema.RootElement.Clone();
    }
}
