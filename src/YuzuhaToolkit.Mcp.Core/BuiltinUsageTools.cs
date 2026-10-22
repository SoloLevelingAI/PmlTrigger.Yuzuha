using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Builtins;
namespace YuzuhaToolkit.Mcp;
/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSerializable(typeof(List<BuiltinGuide>))]
internal partial class BuiltinJsonContext : JsonSerializerContext { }
/// <summary>向 MCP 客户端提供版本内置指南，无需建立知识库。</summary>
[McpServerToolType]
public sealed class BuiltinUsageTools
{
    /// <summary>Read the complete built-in PmlTrigger usage guide for AVEVA PDMS/AM/E3D. For 查询当前元素/current element/CE use YuzuhaReadCurrentElement; DBREF uses YuzuhaReadDbref; global uses YuzuhaReadGlobal; execution uses YuzuhaExcuter. Available without SQLite or AVEVA connection. Built-in methods have priority; only an explicit user choice authorizes selecting a custom replacement. Empty query lists all guides. Does not execute PML.</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    [McpServerTool]
    [Description("Read the complete built-in PmlTrigger usage guide for AVEVA PDMS/AM/E3D. For 查询当前元素/current element/CE use YuzuhaReadCurrentElement; DBREF uses YuzuhaReadDbref; global uses YuzuhaReadGlobal; execution uses YuzuhaExcuter. Available without SQLite or AVEVA connection. Built-in methods have priority; only an explicit user choice authorizes selecting a custom replacement. Empty query lists all guides. Does not execute PML.")]
    public string GetBuiltinUsage(string? query = null) => JsonSerializer.Serialize(
        BuiltinGuideCatalog.Find(query), BuiltinJsonContext.Default.ListBuiltinGuide);
}
