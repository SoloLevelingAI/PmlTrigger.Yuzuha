using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>会话调用边界；前端与测试使用相同请求/响应，不依赖任何界面类型。</summary>
public interface IWorkbenchHost
{
    /// <summary>只读发现，不选择目标。</summary>
    AvevaSessionListResponse Discover();
    /// <summary>显式选择发现到的会话。</summary>
    Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token);
    /// <summary>重新核验锁定目标。</summary>
    Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token);
    /// <summary>沿用原生窗体查询。</summary>
    Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token);
    /// <summary>沿用已有数组窗口。</summary>
    Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token);
    /// <summary>沿用三种内置对象遍历函数和现有列表工具。</summary>
    Task<PmlObjectReadReply> ReadObjectAsync(PmlObjectQuery query, CancellationToken token);
    /// <summary>沿用注册函数调用和信任提示。</summary>
    Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token);
}

