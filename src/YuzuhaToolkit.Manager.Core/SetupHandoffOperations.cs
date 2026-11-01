using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>安装到管理器的只读交接、旧所有权预览与明确移交操作。</summary>
public static class SetupHandoffOperations
{
    /// <summary>注册真实操作；移交仅修改安装所有权，仍需显式写入授权。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null, SetupHandoffService? service = null)
    {
        var store = plans ?? new ManagerPlanStore();var handoff = service ?? new SetupHandoffService();
        yield return Entry("setup.handoff.inspect", "Read-only installed package/ownership verification; does not connect AVEVA.", "\"packageRoot\":{\"type\":\"string\",\"minLength\":1}", "\"packageRoot\"", false);
        yield return Entry("setup.legacy.preview", "Freeze one legacy v2 environment ownership transfer without changing EVARS or the installer marker.", "\"packageRoot\":{\"type\":\"string\",\"minLength\":1},\"environmentFile\":{\"type\":\"string\",\"minLength\":1}", "\"packageRoot\",\"environmentFile\"", false);
        yield return Entry("setup.legacy.transfer", "Explicitly transfer frozen legacy ownership; retain old record/receipt and never edit EVARS.", "\"planId\":{\"type\":\"string\",\"minLength\":1},\"sha256\":{\"type\":\"string\",\"pattern\":\"^[a-fA-F0-9]{64}$\"}", "\"planId\",\"sha256\"", true);
        OperationRegistration Entry(string id, string description, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, description, schema.RootElement.Clone(), RequiresWrite: write), new DelegateOperationHandler((operation, input, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (operation == "setup.handoff.inspect") return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(handoff.Inspect(S(input, "packageRoot")), SetupHandoffJsonContext.Default.SetupHandoffInspection)));
                    if (operation == "setup.legacy.preview")
                    {
                        var plan = handoff.PreviewLegacy(S(input, "packageRoot"), S(input, "environmentFile"));
                        using var originalEntry = JsonDocument.Parse(plan.OriginalEntryJson);
                        var summary = new SetupLegacyTransferSummary(plan.PackageRoot, plan.EnvironmentFile, plan.EnvironmentHash, plan.MarkerHash,
                            originalEntry.RootElement.GetProperty("original").ValueKind != JsonValueKind.Null,
                            "只移交安装标记中这一环境的所有权；EVARS原字节不变，旧卸载随后不再恢复该目标。");
                        var reference = store.Save("setup.legacy.transfer", plan, SetupHandoffJsonContext.Default.SetupLegacyTransferPlan, JsonSerializer.SerializeToElement(summary, SetupHandoffJsonContext.Default.SetupLegacyTransferSummary));
                        return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(reference, ManagerJsonContext.Default.ManagerPlanReference)));
                    }
                    var frozen = store.Read("setup.legacy.transfer", S(input, "planId"), S(input, "sha256"), SetupHandoffJsonContext.Default.SetupLegacyTransferPlan);
                    return Task.FromResult(OperationResult.Ok(operation, JsonSerializer.SerializeToElement(handoff.Transfer(frozen), SetupHandoffJsonContext.Default.SetupLegacyTransferReceipt)));
                }
                catch (SetupTransferUnknownException error) { return Task.FromResult(OperationResult.Fail(operation, 7, "SETUP_TRANSFER_UNKNOWN", error.Message, JsonSerializer.SerializeToElement(error.Receipt, SetupHandoffJsonContext.Default.SetupLegacyTransferReceipt), mayHaveExecuted: true)); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or FormatException)
                { return Task.FromResult(OperationResult.Fail(operation, 7, "SETUP_HANDOFF_REJECTED", error.Message)); }
            }));
        }
    }
    private static string S(JsonElement input, string key) => input.GetProperty(key).GetString()!;
}
