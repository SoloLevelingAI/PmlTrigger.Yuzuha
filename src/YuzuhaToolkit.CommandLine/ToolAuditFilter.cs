using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.CommandLine;

/// <summary>为所有实际 MCP tools/call 统一记录开始、耗时、结果和目标；CLI 自动沿用此管线。</summary>
public static class ToolAuditFilter
{
    /// <summary>包装实际请求处理器；不改变工具授权、业务结果或重试行为。</summary>
    /// <param name="journal">本机持久调用日志。</param>
    /// <param name="target">可选的当前 AVEVA PID 读取函数。</param>
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Create(RuntimeJournal journal, Func<int?>? target = null) => next => async (context, cancellationToken) =>
    {
        var arguments = context.Params.Arguments;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (arguments is not null) foreach (var entry in arguments) { writer.WritePropertyName(entry.Key); entry.Value.WriteTo(writer); }
            writer.WriteEndObject(); writer.Flush();
        }
        var id = journal.Begin(context.Params.Name, System.Text.Encoding.UTF8.GetString(stream.ToArray()), target?.Invoke());
        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            var failed = IsFailure(result);
            journal.Complete(id, failed ? "failed" : "succeeded", failed ? ResultSummary(result) : "工具已完成；返回 " + result.Content.Count + " 个内容块。", target?.Invoke());
            return result;
        }
        catch (OperationCanceledException) { journal.Complete(id, "cancelled-unknown", "等待取消；已发送的 AVEVA 操作可能仍在执行，未自动重试。", target?.Invoke()); throw; }
        catch (Exception error) { journal.Complete(id, "failed", error.GetType().Name + ": " + error.Message, target?.Invoke()); throw; }
    };

    /// <summary>同时识别 MCP 错误标志和工具文本 JSON 中的业务失败。</summary>
    /// <param name="result">真实工具结果。</param>
    public static bool IsFailure(CallToolResult result)
    {
        if (result.IsError == true) return true;
        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            if (block.Text.StartsWith("PML RPC failed:", StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                using var document = JsonDocument.Parse(block.Text);
                if (document.RootElement.ValueKind != JsonValueKind.Object) continue;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if ((property.Name.Equals("success", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("ok", StringComparison.OrdinalIgnoreCase)) && property.Value.ValueKind == JsonValueKind.False) return true;
                    if (property.Name.Equals("error", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString())) return true;
                }
            }
            catch (JsonException) { }
        }
        return false;
    }

    private static string ResultSummary(CallToolResult result)
    {
        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            if (block.Text.StartsWith("PML RPC failed:", StringComparison.OrdinalIgnoreCase)) return block.Text;
            try
            {
                using var document = JsonDocument.Parse(block.Text);
                if (document.RootElement.ValueKind != JsonValueKind.Object) continue;
                var parts = document.RootElement.EnumerateObject().Where(p => new[] { "code", "error", "errormessage", "message" }.Contains(p.Name.ToLowerInvariant())).Select(p => p.Name + ": " + p.Value.ToString());
                var summary = string.Join("; ", parts); if (summary.Length > 0) return summary;
            }
            catch (JsonException) { if (result.IsError == true) return block.Text; }
        }
        return "工具返回失败标志；详见本次响应。";
    }
}
