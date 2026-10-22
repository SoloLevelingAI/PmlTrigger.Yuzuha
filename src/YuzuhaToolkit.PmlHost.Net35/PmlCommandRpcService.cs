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
                FormQueryVersion = 1,
                AutomaticMarkDbVersion = 1,
                HostVersion = typeof(PmlCommandMethod).Assembly
                    .GetName().Version.ToString(),
                ServerTimeUtc = DateTime.UtcNow
            };
        }

        /// <summary>在当前 AVEVA 主线程执行一次非空命令并返回 AVEVA 执行标志。</summary>
        /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
        public RunPmlCommandResponse RunPmlCommand(RunPmlCommandRequest request)
        {
            PmlCommandRpcHost.AssertMainThread();
            if (request == null) return CreateResponse(false, "PML_REQUEST_REQUIRED", "Request is null.", null);
        if (request != null && (request.FormQuery != null || request.ReadArray)) return RunCore(request);
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
                return RunCore(request);
            }
        }
        catch (Exception error) { return CreateResponse(false, "PML_SOURCE_IDENTITY_FAILED", error.Message, request == null ? null : request.PmlCommand); }
            YuzuhaToolkit.Pml.PmlUndoGroupResult<RunPmlCommandResponse> group = YuzuhaToolkit.Pml.PmlUndoGroup.Run(
                delegate(string command) { return PmlCommandRpcHost.GetTarget().RunPmlCommand(command); },
                delegate { return RunCore(request); });
            RunPmlCommandResponse response = group.Value ?? CreateResponse(false, "MARKDB_FAILED", group.Error, request.PmlCommand);
            response.MarkDbId = group.Id; response.MarkDbStarted = group.Started; response.MarkDbEnded = group.Ended;
            response.ExecutionStarted = group.BodyStarted; response.MarkDbError = group.Error;
            if (!String.IsNullOrEmpty(group.Error)) { response.Success = false; response.Code = group.Started ? "MARKDB_GROUP_INCOMPLETE" : "MARKDB_FAILED"; response.ErrorMessage = (response.ErrorMessage ?? "") + " " + group.Error; }
            return response;
        }

        private RunPmlCommandResponse RunCore(RunPmlCommandRequest request)
        {
            PmlCommandRpcHost.AssertMainThread();
            string pmlCommand = request == null ? null : request.PmlCommand;

            try
            {
                PmlCommandMethod target = PmlCommandRpcHost.GetTarget();
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
                    return testResponse;
                }
                finally { if (!target.RunPmlCommand("!!" + variable + ".delete()")) throw new InvalidOperationException("Test outcome cleanup failed."); }
            }

                if (request != null && request.FormQuery != null)
                {
                    YuzuhaToolkit.Pml.PmlFormQuery.ValidateEnvelope(request.ReadArray, request.ReturnList, request.PmlCommand, request.GlobalVar, request.DeleteGlobalVar);
                    RunPmlCommandResponse queryResponse = CreateResponse(true, "OK", null, null);
                    queryResponse.FormQueryResult = YuzuhaToolkit.Pml.PmlFormQuery.Read(request.FormQuery, new PmlFormQueryHost());
                    return queryResponse;
                }
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
