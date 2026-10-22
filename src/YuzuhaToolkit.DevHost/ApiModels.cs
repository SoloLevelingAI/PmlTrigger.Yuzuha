using System.Text.Json;

namespace YuzuhaToolkit.DevHost;

/// <summary>开发宿主状态。</summary>
/// <param name="Service">当前宿主名称。</param>
/// <param name="ApiVersion">HTTP API 版本。</param>
public sealed record HealthResponse(string Service, string ApiVersion);

/// <summary>HTTP 输入校验失败。</summary>
/// <param name="Code">稳定的错误代码。</param>
/// <param name="Message">参数问题说明。</param>
public sealed record ApiError(string Code, string Message);

/// <summary>随产品嵌入的完整指南。</summary>
/// <param name="Id">稳定主题 ID。</param>
/// <param name="Title">主题标题。</param>
/// <param name="Content">完整 Markdown 正文。</param>
/// <param name="Priority">指南优先级。</param>
/// <param name="OverridePolicy">选择自定义实现时的授权规则。</param>
public sealed record GuideDocument(string Id, string Title, string Content, string Priority, string OverridePolicy);

/// <summary>调用预览的一个参数。</summary>
/// <param name="Type">string/str、bool/boolean 或 double/real/number。</param>
/// <param name="Value">JSON 标量值；对象和数组无效。</param>
public sealed record PmlParameter(string Type, JsonElement Value);

/// <summary>有序 PML 参数及不含 !!、括号的方法名。</summary>
/// <param name="MethodName">全局方法名，可使用 object.method 形式。</param>
/// <param name="Parameters">有序参数；无参调用传空数组。</param>
public sealed record PmlPreviewRequest(string MethodName, IReadOnlyList<PmlParameter> Parameters);

/// <summary>生成的调用表达式。</summary>
/// <param name="Command">PML 调用文本；需要执行时仍通过 MCP 的会话核验流程。</param>
public sealed record PmlPreviewResponse(string Command);
