using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>复制计划的审阅摘要，逐文件差异留在冻结计划中。</summary>
public sealed record ManagerCopySummary(string Source, string Target, int Files, int Changed, int Skipped);
/// <summary>普通工具与 7z 格式共用的冻结复制计划。</summary>
public sealed record ManagerCopyPlan(DeploymentPlan Files, SevenZipCopyPlan? SevenZip);
/// <summary>保存实际文件事务及其条件恢复所需计划。</summary>
public sealed record ManagerCopyReceipt(DeploymentPlan Plan, DeploymentReceipt Receipt);
/// <summary>有非空信息、阶段、日志与恢复结果的复制错误。</summary>
public sealed record ManagerCopyDiagnostic(string Code, string Phase, string Message, string ExceptionType,
    string? LogPath, string RollbackStatus, string[] Unresolved);

/// <summary>文件、目录、ZIP 复制复用既有事务，不依赖外部 LaunchDesk 窗口。</summary>
public static class CopyOperations
{
    /// <summary>明确根目录和插件子目录；应用与恢复均需要写授权。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null, string? stateDirectory = null,
        Func<DeploymentPlan, string, DeploymentReceipt>? apply = null)
    {
        var store = plans ?? new ManagerPlanStore(stateDirectory);
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        yield return Entry("copy.formats", "", "", false);
        yield return Entry("copy.preview", "\"source\":" + text + ",\"registeredRoot\":" + text + ",\"name\":" + text + ",\"sevenZipTool\":" + text, "\"source\",\"registeredRoot\",\"name\"", false);
        yield return Entry("copy.apply", "\"planId\":" + text + ",\"sha256\":" + text, "\"planId\",\"sha256\"", true);
        yield return Entry("copy.restore", "\"planId\":" + text + ",\"sha256\":" + text, "\"planId\",\"sha256\"", true);
        OperationRegistration Entry(string id, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Preview, apply or conditionally restore the shared file/folder/ZIP copy transaction.", schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) => {
                    token.ThrowIfCancellationRequested(); var phase = operation.Split('.')[1];
                    var unresolved = Array.Empty<string>(); var rollback = "not-started";
                    try
                    {
                        JsonElement data;
                        if (operation == "copy.formats") data = JsonSerializer.SerializeToElement(SevenZipCopy.Formats(), ManagerJsonContext.Default.CopyFormatCapabilityArray);
                        else if (operation == "copy.preview")
                        {
                            var source = S(input, "source");
                            var archive = Path.GetExtension(source).Equals(".7z", StringComparison.OrdinalIgnoreCase) ? SevenZipCopy.Preview(source, S(input, "registeredRoot"), S(input, "name"),
                                input.TryGetProperty("sevenZipTool", out var tool) ? tool.GetString() : null, stateDirectory, token) : null;
                            var files = archive?.Files ?? PluginDeployment.PreviewTool(source, S(input, "registeredRoot"), S(input, "name"));
                            var plan = new ManagerCopyPlan(files, archive);
                            data = JsonSerializer.SerializeToElement(store.Save("copy.tool-plan", plan, ManagerJsonContext.Default.ManagerCopyPlan,
                                Summary(files)), ManagerJsonContext.Default.ManagerPlanReference);
                        }
                        else if (operation == "copy.apply")
                        {
                            var plan = store.Read("copy.tool-plan", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.ManagerCopyPlan);
                            var state = stateDirectory ?? WorkspaceSettings.StateDirectory;
                            var combined = plan.SevenZip is not null ? SevenZipCopy.Apply(plan.SevenZip, state, token) :
                                new ManagerCopyReceipt(plan.Files, apply is not null ? apply(plan.Files, state) : PluginDeployment.Apply(plan.Files, state));
                            var receipt = combined.Receipt;
                            try { data = JsonSerializer.SerializeToElement(store.Save("copy.receipt", combined, ManagerJsonContext.Default.ManagerCopyReceipt, Summary(plan.Files)), ManagerJsonContext.Default.ManagerPlanReference); }
                            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                            {
                                unresolved = PluginDeployment.Restore(combined.Plan, receipt).Unresolved;
                                rollback = unresolved.Length == 0 ? "restored" : "partial";
                                throw new IOException("复制后保存回执失败；已执行条件恢复。Backup=" + receipt.BackupDirectory, error);
                            }
                        }
                        else
                        {
                            var combined = store.Read("copy.receipt", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.ManagerCopyReceipt);
                            var restored = PluginDeployment.Restore(combined.Plan, combined.Receipt);
                            unresolved = restored.Unresolved; rollback = unresolved.Length == 0 ? "restored" : "partial";
                            data = JsonSerializer.SerializeToElement(restored, ManagerJsonContext.Default.DeploymentRestoreReceipt);
                            if (unresolved.Length != 0) return Task.FromResult(Failed(new IOException("复制恢复有待处理项，保留了外部改动。"), "COPY_RECOVERY_REQUIRED"));
                        }
                        return Task.FromResult(OperationResult.Ok(operation, data));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or JsonException)
                    { return Task.FromResult(Failed(error, "COPY_OPERATION_FAILED")); }

                    OperationResult Failed(Exception error, string code)
                    {
                        if (error is DeploymentApplyException transactionFailure)
                        { unresolved = transactionFailure.RecoveryErrors; rollback = unresolved.Length == 0 ? "restored" : "partial"; }
                        var message = string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
                        var diagnostic = new ManagerCopyDiagnostic(code, phase, message, error.GetType().FullName ?? error.GetType().Name, null, rollback, unresolved);
                        var folder = Path.Combine(stateDirectory ?? WorkspaceSettings.StateDirectory, "CopyDiagnostics");
                        try
                        {
                            PathRules.NoLinks(folder); Directory.CreateDirectory(folder);
                            var log = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
                            diagnostic = diagnostic with { LogPath = log };
                            using var output = new FileStream(log, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                            JsonSerializer.Serialize(output, diagnostic, ManagerJsonContext.Default.ManagerCopyDiagnostic); output.Flush(true);
                        }
                        catch (Exception loggingError) when (loggingError is IOException or UnauthorizedAccessException)
                        { diagnostic = diagnostic with { LogPath = null, Message = message + " 日志保存失败：" + (string.IsNullOrWhiteSpace(loggingError.Message) ? loggingError.GetType().Name : loggingError.Message) }; }
                        return OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                            code, diagnostic.Message, JsonSerializer.SerializeToElement(diagnostic, ManagerJsonContext.Default.ManagerCopyDiagnostic));
                    }
                }));
        }
    }
    private static string S(JsonElement input, string key) => input.GetProperty(key).GetString()!;
    private static JsonElement Summary(DeploymentPlan plan) => JsonSerializer.SerializeToElement(new ManagerCopySummary(plan.Source, plan.Target,
        plan.Files.Length, plan.Files.Count(f => f.TargetHash != f.DesiredHash), plan.Skipped), ManagerJsonContext.Default.ManagerCopySummary);
}
