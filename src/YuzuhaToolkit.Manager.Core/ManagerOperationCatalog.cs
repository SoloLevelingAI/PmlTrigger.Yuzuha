using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Manager.LaunchDesk;

namespace YuzuhaToolkit.Manager;

/// <summary>管理器所使用的明确目录；读取不会创建或迁移任何用户数据。</summary>
public sealed record ManagerEnvironment(string UserDirectory, string KnowledgeDirectory,
    string StateDirectory, string InstallDirectory);

/// <summary>管理器 JSON 输出的元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ManagerEnvironment))]
[JsonSerializable(typeof(ManagerStoredPlan))]
[JsonSerializable(typeof(ManagerPlanReference))]
[JsonSerializable(typeof(EvarsEditPlan))]
[JsonSerializable(typeof(EvarsEditReceipt))]
[JsonSerializable(typeof(EvarsPreviewSummary))]
[JsonSerializable(typeof(EvarsReceiptSummary))]
[JsonSerializable(typeof(RuntimeOwnership))]
[JsonSerializable(typeof(RuntimeDeploymentRequest))]
[JsonSerializable(typeof(RuntimeDeploymentPlan))]
[JsonSerializable(typeof(RuntimeDeploymentReceipt))]
[JsonSerializable(typeof(RuntimeDeploymentDiagnostic))]
[JsonSerializable(typeof(RuntimeDeploymentSummary))]
[JsonSerializable(typeof(KnowledgeImportRequest))]
[JsonSerializable(typeof(KnowledgeImportPlan))]
[JsonSerializable(typeof(KnowledgeImportReceipt))]
[JsonSerializable(typeof(KnowledgeSourceMapping[]))]
[JsonSerializable(typeof(ManagerKnowledgeSearch))]
[JsonSerializable(typeof(ManagerKnowledgeChunk))]
[JsonSerializable(typeof(UserDataMigration))]
[JsonSerializable(typeof(ManagerInstallationReport))]
[JsonSerializable(typeof(ProjectModuleReport))]
[JsonSerializable(typeof(ManagerConnectionTestResult))]
[JsonSerializable(typeof(ManagerCopySummary))]
[JsonSerializable(typeof(DeploymentPlan))]
[JsonSerializable(typeof(ManagerCopyReceipt))]
[JsonSerializable(typeof(ManagerCopyDiagnostic))]
[JsonSerializable(typeof(DeploymentRestoreReceipt))]
[JsonSerializable(typeof(LaunchDeskImportPlan))]
[JsonSerializable(typeof(LaunchDeskImportReceipt))]
[JsonSerializable(typeof(ManagerCopyPlan))]
[JsonSerializable(typeof(SevenZipCopyPlan))]
[JsonSerializable(typeof(CopyFormatCapability[]))]
[JsonSerializable(typeof(ManagerPluginRegistryDocument))]
[JsonSerializable(typeof(ManagerPluginRegistrySnapshot))]
[JsonSerializable(typeof(ManagerPluginRow))]
[JsonSerializable(typeof(ManagerPluginInstallPlan))]
[JsonSerializable(typeof(ManagerPluginCatalog))]
[JsonSerializable(typeof(ManagerPluginInvocation))]
public partial class ManagerJsonContext : JsonSerializerContext;

/// <summary>GUI 与管理 CLI 共用的操作入口，不持有 AVEVA RPC Bridge。</summary>
public static class ManagerOperationCatalog
{
    /// <summary>建立管理服务；可注入配置存储和启动边界供隔离验证使用。</summary>
    public static OperationExecutor Create(LaunchProfileService? profiles = null,
        IProductModuleCatalog? modules = null, Func<LaunchProfile, string, int>? startProcess = null,
        Func<CommunicationCliClient>? communication = null, FeatureCompositionOptions? featuresOptions = null)
    {
        profiles ??= new LaunchProfileService();
        var registrations = LaunchOperationCatalog.Registrations(profiles, modules, startProcess).ToList();
        registrations.AddRange(LaunchWorkflowOperations.Registrations(new LaunchWorkflowService(profiles,
            startProcess: startProcess, modules: modules)));
        registrations.AddRange(EvarsOperations.Registrations());
        registrations.AddRange(RuntimeDeploymentOperations.Registrations());
        registrations.AddRange(KnowledgeOperations.Registrations());
        registrations.AddRange(UserDataOperations.Registrations());
        registrations.AddRange(InstallationOperations.Registrations());
        registrations.AddRange(LaunchDiscoveryOperations.Registrations());
        registrations.AddRange(CopyOperations.Registrations());
        registrations.AddRange(LaunchDeskImportOperations.Registrations());
        registrations.AddRange(PluginOperations.Registrations());
        registrations.AddRange(FunctionManagementOperations.Registrations(communication));
        registrations.AddRange(RibbonHttpAdapter.Registrations());
        using var empty = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");
        using var communicationInputDocument = JsonDocument.Parse("{}");
        var communicationInput = communicationInputDocument.RootElement.Clone();
        registrations.Add(new(new("manager.status", 1,
            "Read the shared manager directories without creating data or starting a GUI.", empty.RootElement.Clone()),
            new DelegateOperationHandler((id, _, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(
                    new ManagerEnvironment(UserDataPaths.Root, UserDataPaths.KnowledgeDirectory(),
                        WorkspaceSettings.StateDirectory, UserDataPaths.InstallRoot),
                    ManagerJsonContext.Default.ManagerEnvironment)));
            })));
        foreach (var (id, target, pid) in new[] {
            ("connection.sessions", "aveva.sessions.list", false),
            ("connection.select", "aveva.session.select", true),
            ("connection.status", "aveva.session.status", true),
            ("connection.test", "aveva.session.status", true),
            ("connection.capabilities", "communication.capabilities", false) })
        {
            using var testSchema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"profile\":{\"type\":\"string\",\"enum\":[\"AM\",\"PDMS\",\"E3D2.1\",\"E3D3.1.6\"]},\"runtimeRoot\":{\"type\":\"string\",\"minLength\":1},\"environmentFile\":{\"type\":\"string\",\"minLength\":1}}}");
            registrations.Add(new(new(id, 1, "Call the independent communication CLI; no PML is executed.",
                pid ? testSchema.RootElement.Clone() : empty.RootElement.Clone(), RequiresPid: pid),
                new DelegateOperationHandler(async (operation, input, context, token) =>
                {
                    if (pid && context.ExpectedProcessStartTimeUtcTicks is not > 0)
                        return OperationResult.Fail(operation, 5, "PROCESS_IDENTITY_REQUIRED", "请先发现目标并提供启动时间，不能只指定 PID。");
                    if (pid && string.IsNullOrWhiteSpace(context.ExpectedModel))
                        return OperationResult.Fail(operation, 5, "MODULE_IDENTITY_REQUIRED", "请提供本次发现并选定的模块，不能省略目标模块核验。");
                    try
                    {
                        var profile = input.TryGetProperty("profile", out var selectedProfile) ? selectedProfile.GetString() : null;
                        var fingerprint = pid ? ConnectionEvidence.Fingerprint(input.TryGetProperty("runtimeRoot", out var runtime) ? runtime.GetString() : null,
                            profile, input.TryGetProperty("environmentFile", out var environment) ? environment.GetString() : null) : "unbound";
                        var client = (communication ?? CommunicationCliClient.FromEnvironment)();
                        var response = await client.InvokeAsync(target, communicationInput, context, token).ConfigureAwait(false);
                        if (pid)
                        {
                            var evidence = ConnectionEvidence.FromResponse(response, context, profile, fingerprint);
                            var after = ConnectionEvidence.Fingerprint(input.TryGetProperty("runtimeRoot", out runtime) ? runtime.GetString() : null,
                                profile, input.TryGetProperty("environmentFile", out environment) ? environment.GetString() : null);
                            evidence = ConnectionEvidence.Revalidate(evidence, context.ProcessId, context.ExpectedProcessStartTimeUtcTicks, context.ExpectedModel, after);
                            return response.Result with { OperationId = operation, Data = JsonSerializer.SerializeToElement(new ManagerConnectionTestResult(evidence, response.Result), ManagerJsonContext.Default.ManagerConnectionTestResult) };
                        }
                        return response.Result with { OperationId = operation };
                    }
                    catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
                    { return OperationResult.Fail(operation, 7, "COMMUNICATION_CLI_UNAVAILABLE", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message); }
                })));
        }
        FeatureRegistry? features = null;
        registrations.Add(FeatureOperations.List(() => features!));
        features = FeatureRegistry.FromOperations(registrations, featuresOptions);
        return new(new OperationRegistry(features.Compose()));
    }
}
