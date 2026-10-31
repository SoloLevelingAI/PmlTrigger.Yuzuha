using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>插件目录与乐观修订；列表读取不执行插件。</summary>
public sealed record ManagerPluginCatalog(string Revision, ManagerPluginRow[] Plugins);
/// <summary>保留插件原始操作和进程结果的外层管理回执。</summary>
public sealed record ManagerPluginInvocation(string PluginId, string Operation, OperationResult Result);

/// <summary>GUI/管理 CLI 使用同一显式插件登记与进程边界。</summary>
public static class PluginOperations
{
    /// <summary>基础目录不依赖插件存在；构造不检查包或运行工作进程。</summary>
    public static IEnumerable<OperationRegistration> Registrations(ManagerPluginRegistry? registry = null, ManagerPlanStore? plans = null)
    {
        var plugins = registry ?? new ManagerPluginRegistry(); var store = plans ?? new ManagerPlanStore();
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        yield return Entry("plugin.list", "", "", false);
        yield return Entry("plugin.describe", "\"id\":" + text, "\"id\"", false);
        yield return Entry("plugin.install.preview", "\"source\":" + text + ",\"enabled\":{\"type\":\"boolean\"}", "\"source\"", false);
        yield return Entry("plugin.install.apply", "\"planId\":" + text + ",\"sha256\":" + text, "\"planId\",\"sha256\"", true);
        yield return Entry("plugin.enable", "\"id\":" + text + ",\"expectedRevision\":" + text + ",\"enabled\":{\"type\":\"boolean\"}", "\"id\",\"expectedRevision\",\"enabled\"", true);
        yield return Entry("plugin.unregister", "\"id\":" + text + ",\"expectedRevision\":" + text, "\"id\",\"expectedRevision\"", true);
        yield return Entry("plugin.invoke", "\"pluginId\":" + text + ",\"operation\":" + text + ",\"workerTimeoutSeconds\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":3600},\"input\":{\"type\":\"object\",\"properties\":{},\"additionalProperties\":true}", "\"pluginId\",\"operation\",\"input\"", false);
        OperationRegistration Entry(string id, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Manage explicit optional plugin packages or invoke one declared worker operation; unavailable plugins never block base operations.", schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler(async (operation, input, context, token) => {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        JsonElement data;
                        switch (operation)
                        {
                            case "plugin.list": data = JsonSerializer.SerializeToElement(new ManagerPluginCatalog(plugins.Read().Revision, plugins.List()), ManagerJsonContext.Default.ManagerPluginCatalog); break;
                            case "plugin.describe":
                                var row = plugins.List().SingleOrDefault(r => r.Registration.Id == S("id")) ?? throw new ArgumentException("插件未安装。");
                                data = JsonSerializer.SerializeToElement(row, ManagerJsonContext.Default.ManagerPluginRow); break;
                            case "plugin.install.preview":
                                var plan = plugins.PreviewInstall(S("source"), input.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean());
                                data = JsonSerializer.SerializeToElement(store.Save("plugin.install", plan, ManagerJsonContext.Default.ManagerPluginInstallPlan,
                                    JsonSerializer.SerializeToElement(plan, ManagerJsonContext.Default.ManagerPluginInstallPlan)), ManagerJsonContext.Default.ManagerPlanReference); break;
                            case "plugin.install.apply":
                                var install = store.Read("plugin.install", S("planId"), S("sha256"), ManagerJsonContext.Default.ManagerPluginInstallPlan);
                                data = JsonSerializer.SerializeToElement(plugins.Install(install), ManagerJsonContext.Default.ManagerPluginRegistrySnapshot); break;
                            case "plugin.enable":
                            case "plugin.unregister": data = JsonSerializer.SerializeToElement(plugins.Change(S("id"), S("expectedRevision"), operation == "plugin.unregister" ? null : input.GetProperty("enabled").GetBoolean()), ManagerJsonContext.Default.ManagerPluginRegistrySnapshot); break;
                            default:
                                var result = await plugins.InvokeAsync(S("pluginId"), S("operation"), input.GetProperty("input"), context, token,
                                    input.TryGetProperty("workerTimeoutSeconds", out var timeout) ? TimeSpan.FromSeconds(timeout.GetInt32()) : null).ConfigureAwait(false);
                                return result with { OperationId = operation, Data = JsonSerializer.SerializeToElement(new ManagerPluginInvocation(S("pluginId"), S("operation"), result), ManagerJsonContext.Default.ManagerPluginInvocation) };
                        }
                        return OperationResult.Ok(operation, data);
                    }
                    catch (ConfigurationConflictException error)
                    { return OperationResult.Fail(operation, 10, "CONFIGURATION_CONFLICT", error.Message,
                        JsonSerializer.SerializeToElement(new LaunchOperationDiagnostic(error.ActualRevision), LaunchOperationJsonContext.Default.LaunchOperationDiagnostic)); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or JsonException)
                    { return OperationResult.Fail(operation, error is ArgumentException or InvalidDataException or JsonException ? 2 : 7,
                        "PLUGIN_OPERATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message); }
                    string S(string key) => input.GetProperty(key).GetString()!;
                }));
        }
    }
}
