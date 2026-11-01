using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>只含路径、profile 和内容指纹的环境预览。</summary>
public sealed record EvarsPreviewSummary(string File, string RuntimeRoot, string Profile, string Framework,
    int CodePage, string BeforeHash, string AfterHash, string[] ManagedSettings)
{
    /// <summary>原托管块或既有固定变量的可审查赋值，不含其他环境正文。</summary>
    public string[]? BeforeManagedSettings { get; init; }
}

/// <summary>写入后的恢复定位，不包含环境文件正文。</summary>
public sealed record EvarsReceiptSummary(string File, string? BackupFile, string BeforeHash, string AfterHash);

/// <summary>冻结环境编辑及其明确包来源；旧的无包装计划需重新预览，恢复回执仍保持兼容。</summary>
public sealed record EvarsOperationPlan(EvarsEditPlan? Edit, string? PackageRoot);

/// <summary>GUI/管理 CLI 共用的环境预览、应用与条件恢复操作。</summary>
public static class EvarsOperations
{
    /// <summary>建立显式选定环境的操作，不执行任何厂商脚本。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null) => Registrations(plans, null, null);

    /// <summary>可注入文件边界的同一操作目录，用于验证写后失败与恢复状态，默认实现仍调用正式环境编辑器。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans,
        Func<EvarsEditPlan, EvarsEditReceipt>? apply, Action<EvarsEditReceipt>? restore = null)
    {
        var store = plans ?? new ManagerPlanStore();
        var applyEnvironment = apply ?? EvarsEditor.Apply;
        var restoreEnvironment = restore ?? EvarsEditor.Restore;
        var ownership = new SetupEnvironmentOwnershipGuard();
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        var reference = "\"planId\":" + text + ",\"sha256\":" + text;
        yield return Entry("evars.preview", "Preview the selected installation's effective INIT/BAT and retain a local frozen plan.",
            "\"installationRoot\":" + text + ",\"packageRoot\":" + text + ",\"product\":{\"type\":\"string\",\"enum\":[\"E3D\",\"AM\",\"PDMS\"]},\"environmentFile\":" + text +
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
                            if (framework != ManagerHostProfiles.Framework(product, S(input, "profile"))) throw new ArgumentException("所选产品、Host profile 与框架不匹配。");
                            var file = EvarsEditor.SelectFile(S(input, "installationRoot"), product,
                                input.TryGetProperty("environmentFile", out var selected) ? selected.GetString() : null);
                            var packageRoot = input.TryGetProperty("packageRoot", out var package) ? package.GetString() : null;
                            ownership.EnsureAvailable(file, packageRoot, S(input, "runtimeRoot"));
                            var plan = EvarsEditor.Preview(file, S(input, "runtimeRoot"), S(input, "profile"), framework,
                                input.TryGetProperty("codePage", out var cp) ? cp.GetInt32() : null);
                            result = store.Save("evars.edit", new EvarsOperationPlan(plan, packageRoot), EvarsOperationJsonContext.Default.EvarsOperationPlan,
                                JsonSerializer.SerializeToElement(new EvarsPreviewSummary(plan.File, plan.RuntimeRoot, plan.Profile,
                                    plan.Framework, plan.CodePage, plan.BeforeHash, plan.AfterHash, ManagedSettings(plan)) { BeforeManagedSettings = BeforeManagedSettings(plan) }, ManagerJsonContext.Default.EvarsPreviewSummary));
                        }
                        else if (operation == "evars.apply")
                        {
                            var frozen = store.Read("evars.edit", S(input, "planId"), S(input, "sha256"), EvarsOperationJsonContext.Default.EvarsOperationPlan);
                            var plan = frozen.Edit ?? throw new IOException("旧环境计划缺少所有权上下文，请重新预览；恢复回执不受影响。");
                            ownership.EnsureAvailable(plan.File, frozen.PackageRoot, plan.RuntimeRoot);
                            var receipt = applyEnvironment(plan);
                            try
                            {
                                result = store.Save("evars.receipt", receipt, ManagerJsonContext.Default.EvarsEditReceipt,
                                    JsonSerializer.SerializeToElement(new EvarsReceiptSummary(receipt.File, receipt.BackupFile,
                                        receipt.BeforeHash, receipt.AfterHash), ManagerJsonContext.Default.EvarsReceiptSummary));
                            }
                            catch (Exception recordingError) when (recordingError is IOException or UnauthorizedAccessException)
                            {
                                try { restoreEnvironment(receipt); }
                                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                                { return Task.FromResult(Unknown(operation, receipt, "环境写入后回执保存及恢复失败；请核对备份，禁止新计划重放。" + restoreError.Message)); }
                                return Task.FromResult(OperationResult.Fail(operation, 7, "EVARS_APPLY_RESTORED", "回执未保存，已恢复本次环境修改。" + recordingError.Message));
                            }
                        }
                        else
                        {
                            var receipt = store.Read("evars.receipt", S(input, "planId"), S(input, "sha256"), ManagerJsonContext.Default.EvarsEditReceipt);
                            try { restoreEnvironment(receipt); }
                            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                            {
                                var reference = new ManagerPlanReference(S(input, "planId"), "evars.receipt", S(input, "sha256"), ReceiptSummary(receipt));
                                return Task.FromResult(OperationResult.Fail(operation, 7, "EVARS_RESTORE_UNKNOWN", "恢复未取得可靠完成回执；保留原编号和备份，需人工核对。" + error.Message,
                                    JsonSerializer.SerializeToElement(reference, ManagerJsonContext.Default.ManagerPlanReference), mayHaveExecuted: true));
                            }
                            return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(
                                new EvarsReceiptSummary(receipt.File, receipt.BackupFile, receipt.AfterHash, receipt.BeforeHash),
                                ManagerJsonContext.Default.EvarsReceiptSummary)));
                        }
                        return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(result, ManagerJsonContext.Default.ManagerPlanReference)));
                    }
                    catch (SetupEnvironmentOwnershipException error)
                    { return Task.FromResult(OperationResult.Fail(operation, 7, error.Code, error.Message)); }
                    catch (EvarsApplyException error) when (error.Receipt is not null)
                    { return Task.FromResult(Unknown(operation, error.Receipt, error.Message + " 动作可能已写入，按恢复回执核对，禁止重放。")); }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                        "EVARS_OPERATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                }));
        }
        OperationResult Unknown(string operation, EvarsEditReceipt receipt, string message)
        {
            JsonElement data;
            try { data = JsonSerializer.SerializeToElement(store.Save("evars.receipt", receipt, ManagerJsonContext.Default.EvarsEditReceipt, ReceiptSummary(receipt)), ManagerJsonContext.Default.ManagerPlanReference); }
            catch (Exception recording) when (recording is IOException or UnauthorizedAccessException)
            { data = ReceiptSummary(receipt);message += " 恢复引用也未能保存，仅可按file/backupFile人工核对。"; }
            return OperationResult.Fail(operation, 7, "EVARS_APPLY_UNKNOWN", message, data, mayHaveExecuted: true);
        }
    }

    /// <summary>仅返回本次受管设置行，不公开环境文件原文。</summary>
    internal static string[] ManagedSettings(EvarsEditPlan plan)
    {
        var settings = new List<string> { "pmllib=" + Path.Combine(plan.RuntimeRoot, "PMLLIB") + ";%pmllib%",
            (plan.Framework == "net48" ? "pmlui=" : "pdmsui=") + Path.Combine(plan.RuntimeRoot, "PMLUI") + (plan.Framework == "net48" ? ";%pmlui%" : ";%pdmsui%"),
            "Yuzuha=" + plan.Profile, "YuzuhaFramework=" + plan.Framework };
        if (plan.StartupActions is not null) settings.Add("YUZUHA_STARTUP_ACTIONS=" + plan.StartupActions);return settings.ToArray();
    }
    /// <summary>只投影原托管块或既有固定变量赋值；不公开完整环境正文和其他变量。</summary>
    internal static string[] BeforeManagedSettings(EvarsEditPlan plan)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var text = System.Text.Encoding.GetEncoding(plan.CodePage).GetString(Convert.FromBase64String(plan.OriginalBase64)).TrimStart('\ufeff');
        var block = System.Text.RegularExpressions.Regex.Match(text, @"(?ims)^[ \t]*rem >>> Yuzuha managed settings[ \t]*\r?\n(.*?)^[ \t]*rem <<< Yuzuha managed settings");
        if (block.Success) text = block.Groups[1].Value;
        var names = new HashSet<string>(["pmllib", "pmlui", "pdmsui", "Yuzuha", "YuzuhaFramework", "YUZUHA_STARTUP_ACTIONS"], StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var value = line.Trim().TrimStart('@');if (!value.StartsWith("set ", StringComparison.OrdinalIgnoreCase)) continue;
            value = value[4..].Trim();if (value.StartsWith('"') && value.EndsWith('"')) value = value[1..^1];var split = value.IndexOf('=');
            if (split > 0 && names.Contains(value[..split].Trim()) && value.Length <= 2048) result.Add(value);
        }
        return result.ToArray();
    }
    private static JsonElement ReceiptSummary(EvarsEditReceipt receipt) => JsonSerializer.SerializeToElement(new EvarsReceiptSummary(receipt.File, receipt.BackupFile, receipt.BeforeHash, receipt.AfterHash), ManagerJsonContext.Default.EvarsReceiptSummary);

    private static string S(JsonElement input, string key) => input.GetProperty(key).GetString()!;
}

/// <summary>环境操作冻结包装的独立 JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EvarsOperationPlan))]
public partial class EvarsOperationJsonContext : JsonSerializerContext;
