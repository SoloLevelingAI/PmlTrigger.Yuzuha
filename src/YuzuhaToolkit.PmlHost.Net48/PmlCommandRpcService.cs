using System;
using System.Threading;
using System.Threading.Tasks;

namespace YuzuhaToolkit.PmlHost.Net48;

internal sealed class PmlCommandRpcService : IPmlCommandService
{
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