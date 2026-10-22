using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace YuzuhaToolkit.Mcp;

/// <summary>复用现有执行桥提供函数注册、权限与带类型的调用；不替代原生加载和窗体管理。</summary>
[McpServerToolType]
public sealed class PmlFunctionTools(PmlFunctionRegistry registry,PmlCallTools calls)
{
    /// <summary>登记本机函数源码及签名；默认禁止调用，重新登记也撤销许可，不加载 PML 文件。</summary>
    [McpServerTool,Description("Register one local .pmlfnc source and its scalar signature. Starts denied, including re-registration. Does not load PML or modify PMLLIB. Source identity is not proof of the version currently loaded in AVEVA.")]
    public string RegisterPmlFunction(string sourceFile,string functionName)=>JsonSerializer.Serialize(Validate(()=>registry.Register(sourceFile,functionName)),PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistration);

    /// <summary>列出本机登记函数和独立调用权限。</summary>
    [McpServerTool,Description("List registered local PML function source identities and invocation permissions. Separate from the manual function trust list.")]
    public string ListRegisteredPmlFunctions()=>JsonSerializer.Serialize(registry.List(),PmlFunctionRegistryJsonContext.Default.ListPmlFunctionRegistration);

    /// <summary>按用户授权授予或撤销注册入口的函数调用许可；不改变人工信任列表。</summary>
    [McpServerTool,Description("Explicitly grant or revoke invocation permission for one registered source revision within the user's authorized task. This is independent of the advisory trust list and does not restrict legacy raw-command tools.")]
    public string SetPmlFunctionPermission(string functionName,bool allowInvoke)=>JsonSerializer.Serialize(Validate(()=>registry.SetPermission(functionName,allowInvoke)),PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistration);

    /// <summary>核验许可、源码和类型后经现有会话桥调用一次；返回调度结果，不推断函数业务返回值。</summary>
    [McpServerTool,Description("Invoke one registered function with ordered typed scalar parameters after checking permission and unchanged source. Requires an explicitly selected AVEVA session. Returns dispatch status, not the function's business return value. No automatic retry. Verify the matching source is loaded in AVEVA before use.")]
    public Task<string> InvokePmlFunction(string functionName,List<DynamicParameterInput> parameters,CancellationToken cancellationToken=default)=>
        calls.RunPmlCommand(Validate(()=>registry.PrepareCall(functionName,parameters)),cancellationToken);

    private static T Validate<T>(Func<T> action)
    {
        try { return action(); }
        catch(Exception error) when(error is UnauthorizedAccessException or InvalidDataException or ArgumentException or KeyNotFoundException)
        {
            // Expected validation codes must survive the MCP SDK's exception sanitization.
            throw new McpException(error.Message);
        }
    }
}
