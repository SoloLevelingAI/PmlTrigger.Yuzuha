using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager.LaunchDesk;

namespace YuzuhaToolkit.Manager;

/// <summary>管理器内的只读安装与静态工程发现入口。</summary>
public static class InstallationOperations
{
    /// <summary>不注册、启动或执行厂商环境脚本。</summary>
    public static IEnumerable<OperationRegistration> Registrations()
    {
        yield return Entry("installation.discover", "\"roots\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":32,\"items\":{\"type\":\"string\",\"minLength\":1}},\"maximumDepth\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":8},\"maximumFiles\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":300000},\"timeoutSeconds\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":120}", "\"roots\"");
        yield return Entry("installation.projects", "\"installationRoot\":{\"type\":\"string\",\"minLength\":1}", "\"installationRoot\"");
        OperationRegistration Entry(string id, string properties, string required)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Read-only bounded installation and static project/module discovery ported from LaunchDesk.Next.", schema.RootElement.Clone()),
                new DelegateOperationHandler((operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        JsonElement result;
                        if (operation == "installation.discover")
                            result = JsonSerializer.SerializeToElement(ManagerInstallationDiscovery.Scan(input.GetProperty("roots").EnumerateArray().Select(v => v.GetString()!).ToArray(),
                                Number("maximumDepth", 5), Number("maximumFiles", 300000), Number("timeoutSeconds", 30), token), ManagerJsonContext.Default.ManagerInstallationReport);
                        else result = JsonSerializer.SerializeToElement(new FindAvevaProjectsAndModules(new DiscoveryFileSystem()).Execute(input.GetProperty("installationRoot").GetString()!, ct: token), ManagerJsonContext.Default.ProjectModuleReport);
                        return Task.FromResult(OperationResult.Ok(operation, result));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
                    { return Task.FromResult(OperationResult.Fail(operation, 2, "INSTALLATION_DISCOVERY_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                    int Number(string key, int fallback) => input.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;
                }));
        }
    }
}
