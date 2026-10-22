using System.Text.Json;

namespace YuzuhaToolkit.Knowledge;

/// <summary>知识工具对外必需的少量入口；内部 JSON 上下文与实现细节不跨程序集暴露。</summary>
public static class KnowledgeJsonSurface
{
    /// <summary>把建库结果序列化为 CLI 输出文本。</summary>
    /// <param name="result">建库或刷新的结果。</param>
    /// <returns>缩进的 JSON 文本。</returns>
    public static string SerializeBuildResult(KnowledgeBuildResult result) =>
        JsonSerializer.Serialize(result, KnowledgeResponseJsonContext.Default.KnowledgeBuildResult);
}
