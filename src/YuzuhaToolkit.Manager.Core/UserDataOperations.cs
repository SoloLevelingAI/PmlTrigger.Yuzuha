using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>共享用户数据迁移服务的管理入口；只复制并校验，保留原资料。</summary>
public static class UserDataOperations
{
    /// <summary>迁移预览保存冻结内容，执行仍由原服务做占用、冲突及回退检查。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPlanStore? plans = null)
    {
        var store = plans ?? new ManagerPlanStore();
        yield return Entry("data.migration.preview", "\"root\":{\"type\":\"string\",\"minLength\":1}", "\"root\"", false);
        yield return Entry("data.migration.apply", "\"planId\":{\"type\":\"string\",\"minLength\":1},\"sha256\":{\"type\":\"string\",\"minLength\":1}", "\"planId\",\"sha256\"", true);
        OperationRegistration Entry(string id, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Preview or apply the shared user data migration without deleting source data.", schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        JsonElement data;
                        if (operation.EndsWith("preview", StringComparison.Ordinal))
                        {
                            var plan = UserDataMigrationService.Preview(input.GetProperty("root").GetString()!);
                            data = JsonSerializer.SerializeToElement(store.Save("data.migration", plan,
                                ManagerJsonContext.Default.UserDataMigration,
                                JsonSerializer.SerializeToElement(plan, ManagerJsonContext.Default.UserDataMigration)), ManagerJsonContext.Default.ManagerPlanReference);
                        }
                        else
                        {
                            var plan = store.Read("data.migration", input.GetProperty("planId").GetString()!, input.GetProperty("sha256").GetString()!, ManagerJsonContext.Default.UserDataMigration);
                            UserDataMigrationService.Apply(plan);
                            data = JsonSerializer.SerializeToElement(new ManagerEnvironment(UserDataPaths.Root, UserDataPaths.KnowledgeDirectory(), WorkspaceSettings.StateDirectory, UserDataPaths.InstallRoot), ManagerJsonContext.Default.ManagerEnvironment);
                        }
                        return Task.FromResult(OperationResult.Ok(operation, data));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or JsonException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7, "DATA_MIGRATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                }));
        }
    }
}
