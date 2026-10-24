using System.Text.RegularExpressions;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>先核验 Host 有界协议能力，再发送只读取已有数组的请求；旧 Host 不降级为全量读取。</summary>
public static class BoundedArrayRequest
{
    /// <summary>校验数组名称与预算后发送一次请求；身份由会话桥核验，能力检查在任何 PML 前完成。</summary>
    public static Task<RunPmlCommandResponse> SendAsync(IPmlCommandService service,HostIdentityResponse identity,string globalVar,int startIndex,int maxItems,int maxCharacters,CancellationToken cancellationToken)
    {
        PmlArrayWindow.Validate(startIndex,maxItems,maxCharacters);
        if(string.IsNullOrEmpty(globalVar)||!Regex.IsMatch(globalVar,@"\A[A-Za-z][A-Za-z0-9_]{0,127}\z",RegexOptions.None,TimeSpan.FromSeconds(1)))throw new ArgumentException("Use a plain PML global array name without !!.",nameof(globalVar));
        if(identity.BoundedArrayReadVersion!=1)throw new HostCapabilityException("PML_BOUNDED_ARRAY_UNSUPPORTED", "update the selected NET35/NET48 Host; no command was sent.");
        return service.RunPmlCommandAsync(new RunPmlCommandRequest{ReadArray=true,GlobalVar=globalVar,StartIndex=startIndex,MaxItems=maxItems,MaxCharacters=maxCharacters},cancellationToken);
    }
}
