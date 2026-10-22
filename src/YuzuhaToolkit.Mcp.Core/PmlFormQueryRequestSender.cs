using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>验证只读查询与 Host 能力，在旧 Host 上禁止发送任何 PML 请求。</summary>
public static class PmlFormQueryRequestSender
{
    /// <summary>在身份核验后发送类型化查询；请求不携带可执行表达式或全局变量删除选项。</summary>
    public static Task<RunPmlCommandResponse> SendAsync(IPmlCommandService service, HostIdentityResponse identity,
        PmlFormQueryRequest query, CancellationToken cancellationToken)
    {
        PmlFormQuery.Validate(query);
        if (identity.FormQueryVersion != 1)
            throw new NotSupportedException("PML_UI_HOST_UNSUPPORTED: Form queries require Host protocol v1; rebuild and load the matching NET35/NET48 Host.");
        return service.RunPmlCommandAsync(new RunPmlCommandRequest { FormQuery = query }, cancellationToken);
    }
}
