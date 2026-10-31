using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>只含路径、profile 和内容指纹的环境预览。</summary>
public sealed record EvarsPreviewSummary(string File, string RuntimeRoot, string Profile, string Framework,
    int CodePage, string BeforeHash, string AfterHash, string[] ManagedSettings);

/// <summary>写入后的恢复定位，不包含环境文件正文。</summary>
public sealed record EvarsReceiptSummary(string File, string? BackupFile, string BeforeHash, string AfterHash);

/// <summary>GUI/管理 CLI 共用的环境预览、应用与条件恢复操作。</summary>
public static class EvarsOperations
{
    /// <summary>建立显式选定环境的操作，不执行任何厂商脚本。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null)
    {
        var store = plans ?? new ManagerPlanStore();
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        var reference = "\"planId\":" + text + ",\"sha256\":" + text;
        yield return Entry("evars.preview", "Preview the selected installation's effective INIT/BAT and retain a local frozen plan.",
            "\"installationRoot\":" + text + ",\"product\":{\"type\":\"string\",\"enum\":[\"E3D\",\"AM\",\"PDMS\"]},\"environmentFile\":" + text +
            ",\"runtimeRoot\":" + text + ",\"profile\":" + text + ",\"framework\":{\"type\":\"string\",\"enum\":[\"net35\",\"net48\"]},\"codePage\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":65001}",
            "\"installationRoot\",\"product\",\"runtimeRoot\",\"profile\",\"framework\"", false);
        yield return Entry("evars.apply", "Apply a frozen environment plan after checking its fingerprint; retain the original backup.", reference, "\"planId\",\"sha256\"", true);
        yield return Entry("evars.restore", "Restore a recorded environment change only if the target still equals the applied content.", reference, "\"planId\",\"sha256\"", true);

        OperationRegistration Entry(string id, string description, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, description, schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        ManagerPlanReference result;
                        if (operation == "evars.preview")
                        {
                            var product = S(input, "product"); var framework = S(input, "framework");
                            if ((product == "E3D") != (framework == "net48")) throw new ArgumentException("所选产品与 Host 框架不匹配。");
                            var file = EvarsEditor.SelectFile(S(input, "installationRoot"), product,
                                input.TryGetProperty("environmentFile", out var selected) ? selected.GetString() : null);
                            var plan = EvarsEditor.Preview(file, S(input, "runtimeRoot"), S(input, "profile"), framework,
                                input.TryGetProperty("codePage", out var cp) ? cp.GetInt32() : null);
                            var settings = new List<string> {
                                "pmllib=" + Path.Combine(plan.RuntimeRoot, "PMLLIB") + ";%pmllib%",
                                (plan.Framework == "net48" ? "pmlui=" : "pdmsui=") + Path.Combine(plan.RuntimeRoot, "PMLUI") +
                                    (plan.Framework == "net48" ? ";%pmlui%" : ";%pdmsui%"),
                                "Yuzuha=" + plan.Profile, "YuzuhaFramework=" + plan.Framework };
                            if (plan.StartupActions is not null) settings.Add("YUZUHA_STARTUP_ACTIONS=" + plan.StartupActions);
                            result = store.Save("evars.edit", plan, ManagerJsonContext.Default.EvarsEditPlan,
                                JsonSerializer.SerializeToElement(new EvarsPreviewSummary(plan.File, plan.RuntimeRoot, plan.Profile,
                                    plan.Framework, plan.CodePage, plan.BeforeHash, plan.AfterHash, settings.ToArray()), ManagerJsonContext.Default.EvarsPreviewSummary));
                        }
                        else if (operation == "evars.apply")
                        {
                            var plan = store.Read("evars.edit", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.EvarsEditPlan);
                            var receipt = EvarsEditor.Apply(plan);
                            try
                            {
                                result = store.Save("evars.receipt", receipt, ManagerJsonContext.Default.EvarsEditReceipt,
                                    JsonSerializer.SerializeToElement(new EvarsReceiptSummary(receipt.File, receipt.BackupFile,
                                        receipt.BeforeHash, receipt.AfterHash), ManagerJsonContext.Default.EvarsReceiptSummary));
                            }
                            catch (Exception recordingError) when (recordingError is IOException or UnauthorizedAccessException)
                            {
                                try { EvarsEditor.Restore(receipt); }
                                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                                { throw new IOException("环境写入后回执失败且恢复待处理。Backup=" + receipt.BackupFile + " " + restoreError.Message, recordingError); }
                                throw new IOException("回执未保存，已恢复本次环境修改。" + recordingError.Message, recordingError);
                            }
                        }
                        else
                        {
                            var receipt = store.Read("evars.receipt", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.EvarsEditReceipt);
                            EvarsEditor.Restore(receipt);
                            return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(
                                new EvarsReceiptSummary(receipt.File, receipt.BackupFile, receipt.AfterHash, receipt.BeforeHash),
                                ManagerJsonContext.Default.EvarsReceiptSummary)));
                        }
                        return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(result, ManagerJsonContext.Default.ManagerPlanReference)));
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                        "EVARS_OPERATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                }));
        }
    }

    private static string S(JsonElement input, string key) => input.GetProperty(key).GetString()!;
}
