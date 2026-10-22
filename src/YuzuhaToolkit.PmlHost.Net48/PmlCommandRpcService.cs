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
            BoundedArrayReadVersion = 1,
            FormQueryVersion = 1,
                AutomaticMarkDbVersion = 1,
            HostVersion = typeof(PmlCommandMethod).Assembly
                .GetName().Version.ToString(),
            ServerTimeUtc = DateTime.UtcNow
        });
    }

    /// <summary>在核验后的目标 Host 主线程执行一次已授权命令；取消或超时不自动重试。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PmlCommandRpcHost.AssertMainThread();
        if (request == null) return Task.FromResult(CreateResponse(false, "PML_REQUEST_REQUIRED", "Request is null.", null));
        if (request != null && (request.FormQuery != null || request.ReadArray)) return RunCore(request, cancellationToken);
        try
        {
            if (request != null && !String.IsNullOrEmpty(request.DevelopmentMacroPath))
            {
                YuzuhaToolkit.Pml.PmlSourceIdentity.Verify(request.DevelopmentMacroPath, request.DevelopmentMacroSha256);
                request.PmlCommand = YuzuhaToolkit.Pml.PmlSourceIdentity.MacroCommand(request.DevelopmentMacroPath);
            }
            if (request != null && !request.AutomaticMarkDb && !String.IsNullOrEmpty(request.ReadOnlySourcePath))
            {
                YuzuhaToolkit.Pml.PmlSourceIdentity.Verify(request.ReadOnlySourcePath, request.ReadOnlySourceSha256);
                return RunCore(request, cancellationToken);
            }
        }
        catch (Exception error) { return Task.FromResult(CreateResponse(false, "PML_SOURCE_IDENTITY_FAILED", error.Message, request == null ? null : request.PmlCommand)); }
        var group = YuzuhaToolkit.Pml.PmlUndoGroup.Run(
            command => PmlCommandRpcHost.GetTarget().RunPmlCommand(command),
            () => RunCore(request, CancellationToken.None).GetAwaiter().GetResult());
        var response = group.Value ?? CreateResponse(false, "MARKDB_FAILED", group.Error, request.PmlCommand);
        response.MarkDbId = group.Id; response.MarkDbStarted = group.Started; response.MarkDbEnded = group.Ended;
        response.ExecutionStarted = group.BodyStarted; response.MarkDbError = group.Error;
        if (!String.IsNullOrEmpty(group.Error)) { response.Success = false; response.Code = group.Started ? "MARKDB_GROUP_INCOMPLETE" : "MARKDB_FAILED"; response.ErrorMessage = (response.ErrorMessage ?? "") + " " + group.Error; }
        return Task.FromResult(response);
    }

    private Task<RunPmlCommandResponse> RunCore(RunPmlCommandRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PmlCommandRpcHost.AssertMainThread();

        var pmlCommand = request == null ? null : request.PmlCommand;
        RunPmlCommandResponse response;

        try
        {
            var target = PmlCommandRpcHost.GetTarget();

            if (request != null && !String.IsNullOrEmpty(request.DevelopmentMacroPath) && !String.IsNullOrEmpty(request.TestOutcomeGlobal))
            {
                string variable = request.TestOutcomeGlobal;
                if (!System.Text.RegularExpressions.Regex.IsMatch(variable, @"\A[A-Za-z][A-Za-z0-9_]{0,127}\z")) throw new ArgumentException("A plain test outcome variable is required.");
                try
                {
                    if (!target.RunPmlCommand("!!" + variable + " = 'NotRun'")) throw new InvalidOperationException("Cannot reset test outcome.");
                    bool testSuccess = target.RunPmlCommand(pmlCommand);
                    string outcome = testSuccess ? target.RunPmlCommandString("!!" + variable) : "Failed: macro dispatch failed";
                    RunPmlCommandResponse testResponse = CreateResponse(testSuccess && outcome == "Passed", outcome == "Passed" ? "OK" : "PML_TEST_NOT_PASSED", outcome == "Passed" ? null : outcome, pmlCommand);
                    testResponse.TestOutcome = outcome == "Passed" ? "Passed" : outcome.StartsWith("Failed", StringComparison.Ordinal) ? "Failed" : "NotReported";
                    testResponse.TestMessage = outcome.Length > 2048 ? outcome.Substring(0,2048) : outcome;
                    return Task.FromResult(testResponse);
                }
                finally { if (!target.RunPmlCommand("!!" + variable + ".delete()")) throw new InvalidOperationException("Test outcome cleanup failed."); }
            }

            if (request != null && request.FormQuery != null)
            {
                YuzuhaToolkit.Pml.PmlFormQuery.ValidateEnvelope(request.ReadArray, request.ReturnList, request.PmlCommand, request.GlobalVar, request.DeleteGlobalVar);
                response = CreateResponse(true, "OK", null, null);
                response.FormQueryResult = YuzuhaToolkit.Pml.PmlFormQuery.Read(request.FormQuery, new PmlFormQueryHost());
                return Task.FromResult(response);
            }

            if (request != null && request.ReadArray)
            {
                if (request.ReturnList || !string.IsNullOrEmpty(request.PmlCommand) || request.DeleteGlobalVar)
                    throw new ArgumentException("ReadArray only reads an existing array; expression, ReturnList and deletion are not allowed.");
                response = CreateResponse(true, "OK", null, null);
                response.ArrayWindow = GetRequireFromE3D.GetPmlVariableWindow(request.GlobalVar, request.StartIndex, request.MaxItems, request.MaxCharacters);
                return Task.FromResult(response);
            }

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
