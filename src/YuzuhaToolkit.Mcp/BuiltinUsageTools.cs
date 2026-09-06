using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Builtins;
namespace YuzuhaToolkit.Mcp;
[JsonSerializable(typeof(List<BuiltinGuide>))]
internal partial class BuiltinJsonContext : JsonSerializerContext { }
[McpServerToolType]
public sealed class BuiltinUsageTools
{
    [McpServerTool]
    [Description("Read the complete built-in PmlTrigger usage guide for AVEVA PDMS/AM/E3D. For 查询当前元素/current element/CE use YuzuhaReadCurrentElement; DBREF uses YuzuhaReadDbref; global uses YuzuhaReadGlobal; execution uses YuzuhaExcuter. Available without SQLite or AVEVA connection. Built-in methods have priority; only an explicit user choice authorizes selecting a custom replacement. Empty query lists all guides. Does not execute PML.")]
    public string GetBuiltinUsage(string? query = null) => JsonSerializer.Serialize(
        BuiltinGuideCatalog.Find(query), BuiltinJsonContext.Default.ListBuiltinGuide);
}
