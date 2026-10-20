using System;
using System.Threading;
using System.Threading.Tasks;

namespace YuzuhaToolkit.PmlHost;

/// <summary>核验主线程后转发 Host 身份查询与 PML 执行，保留协议失败信息。</summary>
internal sealed class PmlCommandRpcService : IPmlCommandService
{
    /// <summary>只读查询专用管道 Host 的进程、模块与启动身份，用于目标核验。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    public Task<HostIdentityResponse> GetHostIdentityAsync(
        HostIdentityRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PmlCommandRpcHost.AssertMainThread();
        var target = PmlCommandRpcHost.GetTarget();
        return Task.FromResult(new HostIdentityResponse
        {
            ProcessId = PmlCommandRpcHost.ProcessId,
            ProcessStartTimeUtcTicks =
                PmlCommandRpcHost.ProcessStartTimeUtcTicks,
            Model = target.Model,
            PipeName = PmlCommandRpcHost.PipeName,
            HostFramework = "net48",
            HostVersion = typeof(PmlCommandMethod).Assembly
                .GetName().Version.ToString(),
            ServerTimeUtc = DateTime.UtcNow
        });
    }

    /// <summary>在核验后的目标 Host 主线程执行一次已授权命令；取消或超时不自动重试。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    public Task<RunPmlCommandResponse> RunPmlCommandAsync(
        RunPmlCommandRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PmlCommandRpcHost.AssertMainThread();

        var pmlCommand = request == null ? null : request.PmlCommand;
        RunPmlCommandResponse response;

        try
        {
            var target = PmlCommandRpcHost.GetTarget();

            if (request != null && request.ReturnList)
            {
                if (string.IsNullOrEmpty(request.GlobalVar) ||
                    request.GlobalVar.Trim().Length == 0)
                    throw new ArgumentException(
                        "GlobalVar is required when ReturnList is true.",
                        "GlobalVar");

                var resultList = target.GetPmlBfsList(
                    pmlCommand,
                    request.GlobalVar,
                    request.DeleteGlobalVar);

                response = CreateResponse(
                    true,
                    "OK",
                    null,
                    pmlCommand);
                response.ResultList = resultList;
            }
            else
            {
                var success = target.RunPmlCommand(pmlCommand);

                response = CreateResponse(
                    success,
                    success ? "OK" : "PML_COMMAND_FAILED",
                    success ? null : "AVEVA Command.Run returned false.",
                    pmlCommand);
            }
        }
        catch (Exception exception)
        {
            response = CreateResponse(
                false,
                "PML_COMMAND_EXCEPTION",
                exception.Message,
                pmlCommand);
        }

        return Task.FromResult(response);
    }

    private static RunPmlCommandResponse CreateResponse(
        bool success,
        string code,
        string errorMessage,
        string pmlCommand)
    {
        return new RunPmlCommandResponse
        {
            Success = success,
            Code = code,
            ErrorMessage = errorMessage,
            PmlCommand = pmlCommand == null ? string.Empty : pmlCommand,
            RequestId = Guid.NewGuid().ToString("N"),
            ExecutionThreadId = PmlCommandRpcHost.MainThreadId,
            ServerRuntime = Environment.Version.ToString(),
            ServerTimeUtc = DateTime.UtcNow
        };
    }
}
