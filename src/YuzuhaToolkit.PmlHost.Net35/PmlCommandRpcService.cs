using System;

namespace YuzuhaToolkit.PmlHost
{
    /// <summary>核验主线程后转发 Host 身份查询与 PML 执行，保留协议失败信息。</summary>
    internal sealed class PmlCommandRpcService : IPmlCommandService
    {
        /// <summary>在主线程读取当前 Host 进程、模块、管道与启动时间。</summary>
        /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
        public HostIdentityResponse GetHostIdentity(
            HostIdentityRequest request)
        {
            PmlCommandRpcHost.AssertMainThread();
            PmlCommandMethod target = PmlCommandRpcHost.GetTarget();
            return new HostIdentityResponse
            {
                ProcessId = PmlCommandRpcHost.ProcessId,
                ProcessStartTimeUtcTicks =
                    PmlCommandRpcHost.ProcessStartTimeUtcTicks,
                Model = target.Model,
                PipeName = PmlCommandRpcHost.PipeName,
                HostFramework = "net35",
                BoundedArrayReadVersion = 1,
                HostVersion = typeof(PmlCommandMethod).Assembly
                    .GetName().Version.ToString(),
                ServerTimeUtc = DateTime.UtcNow
            };
        }

        /// <summary>在当前 AVEVA 主线程执行一次非空命令并返回 AVEVA 执行标志。</summary>
        /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
        public RunPmlCommandResponse RunPmlCommand(
            RunPmlCommandRequest request)
        {
            PmlCommandRpcHost.AssertMainThread();
            string pmlCommand = request == null ? null : request.PmlCommand;

            try
            {
                PmlCommandMethod target = PmlCommandRpcHost.GetTarget();
                if (request != null && request.ReadArray)
                {
                    if (request.ReturnList || !String.IsNullOrEmpty(request.PmlCommand) || request.DeleteGlobalVar)
                        throw new ArgumentException("ReadArray only reads an existing array; expression, ReturnList and deletion are not allowed.");
                    RunPmlCommandResponse windowResponse = CreateResponse(true, "OK", null, null);
                    windowResponse.ArrayWindow = GetRequireFromE3D.GetPmlVariableWindow(request.GlobalVar, request.StartIndex, request.MaxItems, request.MaxCharacters);
                    return windowResponse;
                }
                if (request != null && request.ReturnList)
                {
                    if (String.IsNullOrEmpty(request.GlobalVar) ||
                        request.GlobalVar.Trim().Length == 0)
                        throw new ArgumentException(
                            "GlobalVar is required when ReturnList is true.",
                            "GlobalVar");

                    RunPmlCommandResponse response = CreateResponse(
                        true, "OK", null, pmlCommand);
                    response.ResultList = target.GetPmlBfsList(
                        pmlCommand,
                        request.GlobalVar,
                        request.DeleteGlobalVar);
                    return response;
                }

                bool success = target.RunPmlCommand(pmlCommand);
                return CreateResponse(
                    success,
                    success ? "OK" : "PML_COMMAND_FAILED",
                    success ? null : "AVEVA Command.Run returned false.",
                    pmlCommand);
            }
            catch (Exception exception)
            {
                return CreateResponse(
                    false,
                    "PML_COMMAND_EXCEPTION",
                    exception.Message,
                    pmlCommand);
            }
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
                PmlCommand = pmlCommand == null ? String.Empty : pmlCommand,
                RequestId = Guid.NewGuid().ToString("N"),
                ExecutionThreadId = PmlCommandRpcHost.MainThreadId,
                ServerRuntime = Environment.Version.ToString(),
                ServerTimeUtc = DateTime.UtcNow
            };
        }
    }
}
