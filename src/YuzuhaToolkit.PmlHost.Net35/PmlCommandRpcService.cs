using System;

namespace YuzuhaToolkit.PmlHost
{
    internal sealed class PmlCommandRpcService : IPmlCommandService
    {
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
                HostVersion = typeof(PmlCommandMethod).Assembly
                    .GetName().Version.ToString(),
                ServerTimeUtc = DateTime.UtcNow
            };
        }

        public RunPmlCommandResponse RunPmlCommand(
            RunPmlCommandRequest request)
        {
            PmlCommandRpcHost.AssertMainThread();
            string pmlCommand = request == null ? null : request.PmlCommand;

            try
            {
                PmlCommandMethod target = PmlCommandRpcHost.GetTarget();
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
