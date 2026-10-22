namespace YuzuhaToolkit.Mcp;

/// <summary>执行 MCP 宿主所需的少量公开入口；其余实现细节仍留在 Core 内部。</summary>
public static class McpHostSurface
{
    /// <summary>MCP 初始化时提供给客户端的完整使用说明。</summary>
    public static string UsageInstructions => McpUsageInstructions.Text;

    /// <summary>工具方法共享的 AOT 友好 JSON 选项。</summary>
    public static System.Text.Json.JsonSerializerOptions ToolJsonOptions => YuzuhaToolJsonContext.Default.Options;
}
