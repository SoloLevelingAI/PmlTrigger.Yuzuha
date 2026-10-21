using System;
using System.Threading;
using System.Threading.Tasks;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost;

/// <summary>执行工具与 AVEVA Host 之间的身份查询和主线程命令契约。</summary>
[RpcService("yuzuha.pml-command.v1")]
public interface IPmlCommandService
{
    /// <summary>在核验后的目标 Host 主线程执行一次已授权命令；取消或超时不自动重试。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    [RpcOperation("run-pml-command")]
    Task<RunPmlCommandResponse> RunPmlCommandAsync(
        RunPmlCommandRequest request,
        CancellationToken cancellationToken);

    /// <summary>只读查询专用管道 Host 的进程、模块与启动身份，用于目标核验。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    [RpcOperation("get-host-identity")]
    Task<HostIdentityResponse> GetHostIdentityAsync(
        HostIdentityRequest request,
        CancellationToken cancellationToken);
}

/// <summary>读取当前管道 Host 身份的无参数请求。</summary>
public sealed class HostIdentityRequest
{
}

/// <summary>Host 返回的进程、启动时间、模块、管道、框架和版本身份。</summary>
public sealed class HostIdentityResponse
{
    /// <summary>有界数组读取协议版本。</summary>
    public int BoundedArrayReadVersion { get; set; }
    /// <summary>目标或发送方操作系统进程编号；不能代替启动时间核验。</summary>
    public int ProcessId { get; set; }
    /// <summary>进程启动 UTC ticks；与 PID 联合识别目标，防止 PID 重用。</summary>
    public long ProcessStartTimeUtcTicks { get; set; }
    /// <summary>Host 报告的 AVEVA 模块名称。</summary>
    public string Model { get; set; }
    /// <summary>绑定该目标 PID 的专用 Named Pipe 名称。</summary>
    public string PipeName { get; set; }
    /// <summary>Host 使用的 net35 或 net48 框架标识。</summary>
    public string HostFramework { get; set; }
    /// <summary>目标 Host 程序集版本。</summary>
    public string HostVersion { get; set; }
    /// <summary>Host 生成响应时的 UTC 时间。</summary>
    public DateTime ServerTimeUtc { get; set; }
}

/// <summary>待执行 PML 命令及可选全局数组返回和清理策略。</summary>
public sealed class RunPmlCommandRequest
{
    /// <summary>仅查询已有数组，不执行表达式。</summary>
    public bool ReadArray { get; set; }
    /// <summary>从 1 开始的索引。</summary>
    public int StartIndex { get; set; }
    /// <summary>最大读取槽位数。</summary>
    public int MaxItems { get; set; }
    /// <summary>最大返回字符数。</summary>
    public int MaxCharacters { get; set; }
    /// <summary>已生成或准备执行的完整 PML 文本。</summary>
    public string PmlCommand { get; set; }
    /// <summary>是否把表达式结果作为全局数组读取。</summary>
    public bool ReturnList { get; set; }
    /// <summary>存放结果数组的 PML 全局变量名称。</summary>
    public string GlobalVar { get; set; }
    /// <summary>成功读取后是否清理请求指定的结果全局变量。</summary>
    public bool DeleteGlobalVar { get; set; }
}

/// <summary>PML 执行状态、错误、数组结果和服务端关联信息。</summary>
public sealed class RunPmlCommandResponse
{
    /// <summary>有界数组窗口与续读信息。</summary>
    public YuzuhaToolkit.Pml.PmlArrayWindowResult ArrayWindow { get; set; }
    /// <summary>该次协议或业务操作报告的成功标志。</summary>
    public bool Success { get; set; }
    /// <summary>稳定的业务状态或失败代码。</summary>
    public string Code { get; set; }
    /// <summary>执行失败的可读原因，成功时通常为空。</summary>
    public string ErrorMessage { get; set; }
    /// <summary>已生成或准备执行的完整 PML 文本。</summary>
    public string PmlCommand { get; set; }
    /// <summary>Host 返回的完整 PML 全局数组字符串行。</summary>
    public string[] ResultList { get; set; }
    /// <summary>用于关联一个请求与响应的编号。</summary>
    public string RequestId { get; set; }
    /// <summary>真正处理 Host 请求的托管线程编号。</summary>
    public int ExecutionThreadId { get; set; }
    /// <summary>处理请求的 Host 运行时标识。</summary>
    public string ServerRuntime { get; set; }
    /// <summary>Host 生成响应时的 UTC 时间。</summary>
    public DateTime ServerTimeUtc { get; set; }
}
