using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using YuzuhaToolkit.Knowledge.Search;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.DevHost;

/// <summary>开发 API：复用内置指南、PML 格式化及查询规划；每个处理器只计算返回值。</summary>
public static partial class DevelopmentEndpoints
{
    private static readonly JsonSerializerOptions GuideJson = new(JsonSerializerDefaults.Web);

    /// <summary>读取开发宿主状态。</summary>
    /// <returns>宿主名称与当前 API 版本；不表示 AVEVA 已连接。</returns>
    public static HealthResponse Health() => new("YuzuhaToolkit.DevHost", "v1");

    /// <summary>读取随 MCP 版本嵌入的完整使用指南。</summary>
    /// <param name="query">主题 ID、别名或任务描述；省略时返回全部指南。</param>
    /// <returns>包含优先级及替换策略的指南；未知主题返回空数组。</returns>
    public static IReadOnlyList<GuideDocument> Guides(string? query = null) =>
        JsonSerializer.Deserialize<List<GuideDocument>>(new BuiltinUsageTools().GetBuiltinUsage(query), GuideJson)!;

    /// <summary>生成 PML 全局方法调用文本。</summary>
    /// <param name="request">方法名及有序的带类型参数。</param>
    /// <returns>调用文本，或参数无效时的 HTTP 400；生成结果没有被执行。</returns>
    public static Results<Ok<PmlPreviewResponse>, BadRequest<ApiError>> PreviewPml(PmlPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MethodName) || !MethodNameRegex().IsMatch(request.MethodName))
            return TypedResults.BadRequest(new ApiError("invalid_method", "方法名必须是标识符或 object.method，且不包含 !! 或括号。"));
        if (request.Parameters is null || request.Parameters.Any(p => p is null || string.IsNullOrWhiteSpace(p.Type)))
            return TypedResults.BadRequest(new ApiError("invalid_parameters", "parameters 必须是数组，每个参数必须指定 type。"));
        try
        {
            var parameters = request.Parameters.Select(p => new DynamicParameter(p.Type, Normalize(p.Value)!)).ToArray();
            return TypedResults.Ok(new PmlPreviewResponse(PmlMethodSignatureGenerator.Build(request.MethodName, parameters)));
        }
        catch (Exception error) when (error is ArgumentException or FormatException or NotSupportedException)
        {
            return TypedResults.BadRequest(new ApiError("invalid_parameters", error.Message));
        }
    }

    /// <summary>预览确定性的 FTS5 查询变体。</summary>
    /// <param name="query">PML、英文或中文检索语句。</param>
    /// <returns>加权查询计划；空查询返回 HTTP 400。不会打开或创建知识库。</returns>
    public static Results<Ok<IReadOnlyList<QueryVariant>>, BadRequest<ApiError>> PlanQuery(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? TypedResults.BadRequest(new ApiError("invalid_query", "query 不能为空。"))
            : TypedResults.Ok<IReadOnlyList<QueryVariant>>(QueryPlanner.Plan(query));

    private static object? Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when double.IsFinite(value.GetDouble()) => value.GetRawText(),
        JsonValueKind.Null => null,
        _ => throw new FormatException("参数只支持有限数字、字符串、布尔值或 null。")
    };

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex MethodNameRegex();
}
