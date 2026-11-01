using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>可审阅的运行部署摘要，包含实际目标、引导适配和文件差异。</summary>
public sealed record RuntimeDeploymentSummary(string Mode, string RuntimeRoot, string EnvironmentFile,
    string Profile, string Framework, string BootstrapToken, int FileCount, int ChangedFiles, string[] TransformedFiles,
    ManagerInstallationIdentity Installation)
{
    /// <summary>冻结环境文件修改前的完整内容摘要。</summary>
    public string? BeforeHash { get; init; }
    /// <summary>冻结环境文件修改后的完整内容摘要。</summary>
    public string? AfterHash { get; init; }
    /// <summary>计划写入的固定受管变量赋值。</summary>
    public string[]? ManagedSettings { get; init; }
    /// <summary>当前固定受管变量赋值，不含其他环境正文。</summary>
    public string[]? BeforeManagedSettings { get; init; }
}

/// <summary>GUI/CLI 共用的运行部署操作。</summary>
public static class RuntimeDeploymentOperations
{
    /// <summary>建立两种部署模式的冻结计划、应用和恢复入口。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null, RuntimeDeploymentService? deployment = null)
    {
        var store = plans ?? new ManagerPlanStore(); var service = deployment;
        RuntimeDeploymentService Service() => service ??= new RuntimeDeploymentService();
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        var reference = "\"planId\":" + text + ",\"sha256\":" + text;
        yield return Entry("deployment.preview", "Preview runtime copy or in-place reference and its selected AVEVA environment.",
            string.Join(',', new[] { "packageRoot", "runtimeRoot", "installationRoot", "product", "profile", "framework", "environmentFile", "installationExecutable", "expectedInstallationId" }.Select(n => "\"" + n + "\":" + text)) +
            ",\"mode\":{\"type\":\"string\",\"enum\":[\"copy\",\"reference\"]},\"codePage\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":65001}",
            "\"packageRoot\",\"runtimeRoot\",\"installationRoot\",\"product\",\"profile\",\"framework\",\"mode\"", false);
        yield return Entry("deployment.apply", "Apply the exact reviewed runtime/environment plan; retain backups and recovery evidence.", reference, "\"planId\",\"sha256\"", true);
        yield return Entry("deployment.restore", "Conditionally restore the recorded runtime and environment transaction.", reference, "\"planId\",\"sha256\"", true);

        OperationRegistration Entry(string id, string description, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, description, schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (operation == "deployment.restore")
                        {
                            var receipt = store.Read("runtime.receipt", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.RuntimeDeploymentReceipt);
                            var restored = Service().Restore(receipt);
                            var data = JsonSerializer.SerializeToElement(restored, ManagerJsonContext.Default.RuntimeDeploymentDiagnostic);
                            return Task.FromResult(restored.RecoveryErrors.Length == 0 ? OperationResult.Ok(operation, data) :
                                OperationResult.Fail(operation, 7, "RECOVERY_REQUIRED", "存在外部修改或未恢复项，请检查记录。", data));
                        }
                        if (operation == "deployment.preview")
                        {
                            var request = JsonSerializer.Deserialize(input, ManagerJsonContext.Default.RuntimeDeploymentRequest) ?? throw new ArgumentException("缺少部署输入。");
                            var plan = Service().Preview(request);
                            var saved = store.Save("runtime.plan", plan, ManagerJsonContext.Default.RuntimeDeploymentPlan, Summary(plan));
                            return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(saved, ManagerJsonContext.Default.ManagerPlanReference)));
                        }
                        var frozen = store.Read("runtime.plan", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.RuntimeDeploymentPlan);
                        var applied = Service().Apply(frozen);
                        ManagerPlanReference reference;
                        try { reference = store.Save("runtime.receipt", applied, ManagerJsonContext.Default.RuntimeDeploymentReceipt, Summary(frozen)); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        {
                            var recovered = Service().Restore(applied);
                            throw new RuntimeDeploymentException("部署回执引用未保存，已尝试恢复。", recovered, error);
                        }
                        return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(reference, ManagerJsonContext.Default.ManagerPlanReference)));
                    }
                    catch (SetupEnvironmentOwnershipException error)
                    { return Task.FromResult(OperationResult.Fail(operation, 7, error.Code, error.Message)); }
                    catch (RuntimeDeploymentException error) when (error.InnerException is SetupEnvironmentOwnershipException && error.Diagnostic.RecoveryErrors.Length == 0)
                    { return Task.FromResult(OperationResult.Fail(operation, 7, ((SetupEnvironmentOwnershipException)error.InnerException).Code, error.Message,
                        JsonSerializer.SerializeToElement(error.Diagnostic, ManagerJsonContext.Default.RuntimeDeploymentDiagnostic))); }
                    catch (RuntimeDeploymentException error)
                    { return Task.FromResult(OperationResult.Fail(operation, 7, error.Diagnostic.RecoveryErrors.Length == 0 ? "DEPLOYMENT_FAILED_RESTORED" : "RECOVERY_REQUIRED", error.Message,
                        JsonSerializer.SerializeToElement(error.Diagnostic, ManagerJsonContext.Default.RuntimeDeploymentDiagnostic))); }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                        "DEPLOYMENT_REJECTED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                }));
        }
    }

    private static JsonElement Summary(RuntimeDeploymentPlan plan) => JsonSerializer.SerializeToElement(new RuntimeDeploymentSummary(
        plan.Request.Mode, plan.Request.RuntimeRoot, plan.Environment.File, plan.Request.Profile, plan.Request.Framework,
        plan.BootstrapToken, plan.Copy?.Files.Length ?? 0, plan.Copy?.Files.Count(f => f.TargetHash != f.DesiredHash) ?? 0,
        plan.Copy?.Files.Where(f => f.ReplacementBase64 is not null).Select(f => f.RelativePath).ToArray() ?? [], plan.Installation)
        { BeforeHash = plan.Environment.BeforeHash, AfterHash = plan.Environment.AfterHash, ManagedSettings = EvarsOperations.ManagedSettings(plan.Environment), BeforeManagedSettings = EvarsOperations.BeforeManagedSettings(plan.Environment) },
        ManagerJsonContext.Default.RuntimeDeploymentSummary);
    private static string S(JsonElement input, string key) => input.GetProperty(key).GetString()!;
}
