namespace YuzuhaToolkit.Mcp;

/// <summary>目标 Host 缺少要求的协议能力；仅在任何 PML 请求发送前抛出。</summary>
/// <param name="code">可供调用方判断的稳定能力错误码。</param>
/// <param name="message">所需能力及更新提示，不表示调用参数有误。</param>
public sealed class HostCapabilityException(string code, string message) : NotSupportedException(code + ": " + message)
{
    /// <summary>目标 Host 不支持的稳定能力错误码。</summary>
    public string Code { get; } = code;
}
