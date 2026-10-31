using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>原型资料的净化预览、冻结导入及读取入口。</summary>
public static class LaunchDeskImportOperations
{
    /// <summary>导入保留为未激活草稿，不创建外部 LaunchDesk 窗口。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null, string? stateDirectory = null)
    {
        var store = plans ?? new ManagerPlanStore(stateDirectory);
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        yield return Entry("launchdesk.import.preview", "\"source\":" + text, "\"source\"", false);
        yield return Entry("launchdesk.import.apply", "\"planId\":" + text + ",\"sha256\":" + text, "\"planId\",\"sha256\"", true);
        yield return Entry("launchdesk.import.read", "\"id\":" + text, "\"id\"", false);
        OperationRegistration Entry(string id, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Read explicitly selected prototype data and retain sanitized drafts/history without importing credentials.", schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        JsonElement result;
                        if (operation.EndsWith("preview", StringComparison.Ordinal))
                        {
                            var plan = LaunchDeskImport.Preview(S("source"), token);
                            result = JsonSerializer.SerializeToElement(store.Save("launchdesk.import", plan, ManagerJsonContext.Default.LaunchDeskImportPlan,
                                JsonSerializer.SerializeToElement(plan, ManagerJsonContext.Default.LaunchDeskImportPlan)), ManagerJsonContext.Default.ManagerPlanReference);
                        }
                        else if (operation.EndsWith("apply", StringComparison.Ordinal))
                        {
                            var plan = store.Read("launchdesk.import", S("planId"), S("sha256"), ManagerJsonContext.Default.LaunchDeskImportPlan);
                            result = JsonSerializer.SerializeToElement(LaunchDeskImport.Apply(plan, stateDirectory ?? WorkspaceSettings.StateDirectory, token), ManagerJsonContext.Default.LaunchDeskImportReceipt);
                        }
                        else
                        {
                            var id = S("id"); if (!Guid.TryParseExact(id, "N", out var parsedId)) throw new ArgumentException("导入编号无效。");
                            var file = PathRules.Inside(Path.Combine(stateDirectory ?? WorkspaceSettings.StateDirectory, "LaunchDeskImports"), id + "/import.json");
                            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                            if (stream.Length > 8 * 1024 * 1024) throw new IOException("导入快照超过读取预算。");
                            if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).StartsWith(id, StringComparison.OrdinalIgnoreCase)) throw new IOException("导入快照内容指纹已变化。");
                            stream.Position = 0;
                            var plan = JsonSerializer.Deserialize(stream, ManagerJsonContext.Default.LaunchDeskImportPlan) ?? throw new InvalidDataException("导入快照为空。");
                            result = JsonSerializer.SerializeToElement(plan, ManagerJsonContext.Default.LaunchDeskImportPlan);
                        }
                        return Task.FromResult(OperationResult.Ok(operation, result));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or JsonException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                        "LAUNCHDESK_IMPORT_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                    string S(string key) => input.GetProperty(key).GetString()!;
                }));
        }
    }
}
