using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>明确登记的插件及启用状态，清单指纹不能自行更新。</summary>
public sealed record ManagerPluginRegistration(string Id, string Version, string Directory, string ManifestSha256, bool Enabled);
/// <summary>插件登记文档；未知顶层字段保留。</summary>
public sealed record ManagerPluginRegistryDocument(int SchemaVersion, ManagerPluginRegistration[] Registrations)
{
    /// <summary>未知字段不参与构造器绑定，更新登记时原样保留。</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
/// <summary>无自动写入的登记快照和修订。</summary>
public sealed record ManagerPluginRegistrySnapshot(string Revision, ManagerPluginRegistryDocument Document);
/// <summary>兼容、缺失、停用等状态逐个报告，不触发插件进程。</summary>
public sealed record ManagerPluginRow(ManagerPluginRegistration Registration, string State, string Message, ManagerPluginManifest? Manifest);
/// <summary>冻结明确来源、目标与登记修订的安装预览；默认保持停用。</summary>
public sealed record ManagerPluginInstallPlan(string Source, string Target, string ManifestSha256, string Id,
    string Version, string ExpectedRevision, int Files, bool Enabled);

/// <summary>插件独立登记和包状态，构造与基础管理操作不加载插件。</summary>
public sealed class ManagerPluginRegistry(string? stateDirectory = null)
{
    private readonly string root = Path.Combine(Path.GetFullPath(stateDirectory ?? WorkspaceSettings.StateDirectory), "Plugins");
    private string FilePath => Path.Combine(root, "registry.json");

    /// <summary>读取登记，不创建目录、不扫描其他程序或运行工作进程。</summary>
    public ManagerPluginRegistrySnapshot Read()
    {
        PathRules.NoLinks(FilePath);
        if (!File.Exists(FilePath)) return new("missing", new(1, []));
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024) throw new IOException("插件登记超过 1 MiB 预算。");
        var hash = Hash(stream); stream.Position = 0;
        var document = JsonSerializer.Deserialize(stream, ManagerJsonContext.Default.ManagerPluginRegistryDocument) ?? throw new InvalidDataException("插件登记为空。");
        if (document.SchemaVersion != 1 || document.Registrations is null || document.Registrations.Length > 100 ||
            document.Registrations.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Version) ||
                !Path.IsPathFullyQualified(r.Directory) || r.ManifestSha256 is null || r.ManifestSha256.Length != 64 || !r.ManifestSha256.All(Uri.IsHexDigit)) ||
            document.Registrations.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != document.Registrations.Length)
            throw new InvalidDataException("插件登记版本或身份无效。");
        return new(hash, document);
    }

    /// <summary>分别报告每个登记失败，不让一个损坏插件隐藏其他项。</summary>
    public ManagerPluginRow[] List() => Read().Document.Registrations.Select(registration =>
    {
        try
        {
            using var package = new PluginPackage(registration.Directory);
            if (package.ManifestSha256 != registration.ManifestSha256 || package.Manifest.Id != registration.Id || package.Manifest.Version != registration.Version)
                return new ManagerPluginRow(registration, "changed", "插件身份与登记不一致，需要重新安装预览。", package.Manifest);
            if (package.Manifest.ContractVersion != 1) return new(registration, "incompatible", "插件契约不兼容。", package.Manifest);
            return new(registration, registration.Enabled ? "ready" : "disabled", registration.Enabled ? "可按声明操作显式调用。" : "已安装但停用，不启动工作进程。", package.Manifest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
        { return new ManagerPluginRow(registration, "unavailable", Message(error), null); }
    }).ToArray();

    /// <summary>检查明确选定包并冻结目标；此时不复制，也不启用工作进程。</summary>
    public ManagerPluginInstallPlan PreviewInstall(string source, bool enabled = false)
    {
        if (!Path.IsPathFullyQualified(source)) throw new ArgumentException("插件包需要绝对目录。");
        using var package = new PluginPackage(source);
        if (package.Manifest.ContractVersion != 1) throw new ArgumentException("插件契约不兼容，不能安装为可调用插件。");
        var target = PathRules.Inside(root, "Packages/" + package.Manifest.Id + "/" + package.ManifestSha256);
        return new(package.Directory, target, package.ManifestSha256, package.Manifest.Id, package.Manifest.Version,
            Read().Revision, package.Manifest.Files.Count, enabled);
    }

    /// <summary>在私有目录复制并校验包，最后更新登记；原包保留。</summary>
    public ManagerPluginRegistrySnapshot Install(ManagerPluginInstallPlan plan)
    {
        var fresh = PreviewInstall(plan.Source, plan.Enabled);
        if (fresh != plan) throw new IOException("插件包或登记在预览后变化，请重新预览。");
        PathRules.NoLinks(root); Directory.CreateDirectory(root);
        using var guard = new FileStream(Path.Combine(root, "registry.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var snapshot = Read(); DemandRevision(snapshot, plan.ExpectedRevision);
        using var source = new PluginPackage(plan.Source);
        if (!Directory.Exists(plan.Target))
        {
            var parent = Path.GetDirectoryName(plan.Target)!; Directory.CreateDirectory(parent);
            var stage = Path.Combine(parent, ".stage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try
            {
                source.CopyTo(stage);
                using (var verified = new PluginPackage(stage)) if (verified.ManifestSha256 != plan.ManifestSha256) throw new IOException("插件暂存身份不一致。");
                Directory.Move(stage, plan.Target);
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        using (var installed = new PluginPackage(plan.Target)) if (installed.ManifestSha256 != plan.ManifestSha256) throw new IOException("目标插件包不一致，保留当前登记。");
        var row = new ManagerPluginRegistration(plan.Id, plan.Version, plan.Target, plan.ManifestSha256, plan.Enabled);
        return Write(snapshot.Document with { Registrations = [.. snapshot.Document.Registrations.Where(r => r.Id != plan.Id), row] });
    }

    /// <summary>明确启停或取消登记；不执行插件，不删除原包或用户证据。</summary>
    public ManagerPluginRegistrySnapshot Change(string id, string expectedRevision, bool? enabled)
    {
        PathRules.NoLinks(root); Directory.CreateDirectory(root);
        using var guard = new FileStream(Path.Combine(root, "registry.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var snapshot = Read(); DemandRevision(snapshot, expectedRevision);
        var existing = snapshot.Document.Registrations.SingleOrDefault(r => r.Id == id) ?? throw new ArgumentException("插件未安装。");
        if (enabled == true)
        {
            var state = List().Single(r => r.Registration.Id == id);
            if (state.State is not ("ready" or "disabled")) throw new IOException("插件无法启用：" + state.Message);
        }
        var rows = snapshot.Document.Registrations.Where(r => r.Id != id).ToList();
        if (enabled is not null) rows.Add(existing with { Enabled = enabled.Value });
        return Write(snapshot.Document with { Registrations = rows.ToArray() });
    }

    /// <summary>调用前固定包、核对工作进程能力与操作授权，超时不重发。</summary>
    public async Task<OperationResult> InvokeAsync(string id, string operation, JsonElement input, OperationContext context, CancellationToken token = default, TimeSpan? workerTimeout = null)
    {
        var row = List().SingleOrDefault(r => r.Registration.Id == id);
        if (row is null) return OperationResult.Fail(operation, 3, "PLUGIN_NOT_INSTALLED", "插件未安装。");
        if (row.State != "ready") return OperationResult.Fail(operation, row.State == "disabled" ? 4 : 6, "PLUGIN_" + row.State.ToUpperInvariant(), row.Message);
        using var package = new PluginPackage(row.Registration.Directory);
        if (package.ManifestSha256 != row.Registration.ManifestSha256) return OperationResult.Fail(operation, 5, "PLUGIN_CHANGED", "插件包已变化。");
        var descriptor = package.Manifest.Operations.SingleOrDefault(d => d.Id == operation);
        if (descriptor is null) return OperationResult.Fail(operation, 3, "UNDECLARED_PLUGIN_OPERATION", "插件没有声明此操作。");
        var executor = new OperationExecutor(new OperationRegistry([new(descriptor, new DelegateOperationHandler(async (target, body, trustedContext, cancellation) => {
            var client = new CommunicationCliClient(new(package.EntryPoint, Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"), workerTimeout),
                ["plugin.capabilities", .. package.Manifest.Operations.Select(d => d.Id)]);
            using var empty = JsonDocument.Parse("{}");
            var handshake = await client.InvokeAsync("plugin.capabilities", empty.RootElement, new(), cancellation).ConfigureAwait(false);
            if (!handshake.Result.Success) return handshake.Result with { OperationId = target };
            var capabilities = handshake.Result.Data?.Deserialize(PluginJsonContext.Default.PluginCapabilities);
            if (capabilities is null || capabilities.PluginId != package.Manifest.Id || capabilities.Version != package.Manifest.Version || capabilities.ContractVersion != 1 ||
                !SameOperations(package.Manifest.Operations, capabilities.Operations)) return OperationResult.Fail(target, 6, "PLUGIN_HANDSHAKE_FAILED", "插件实际能力与固定清单不匹配。");
            return (await client.InvokeAsync(target, body, trustedContext, cancellation).ConfigureAwait(false)).Result;
        }))]));
        return await executor.InvokeAsync(operation, input, context, token).ConfigureAwait(false);
    }

    private ManagerPluginRegistrySnapshot Write(ManagerPluginRegistryDocument document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ManagerJsonContext.Default.ManagerPluginRegistryDocument);
        if (bytes.Length > 1024 * 1024) throw new IOException("插件登记超过预算，保留原登记。");
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); } File.Move(temporary, FilePath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return Read();
    }
    private static bool SameOperations(OperationDescriptor[] declared, OperationDescriptor[] actual)
    {
        if (actual is null || actual.Length != declared.Length || actual.Any(d => d is null)) return false;
        foreach (var expected in declared)
        {
            var matches = actual.Where(d => d.Id == expected.Id).ToArray(); if (matches.Length != 1) return false; var found = matches[0];
            if (found.ContractVersion != expected.ContractVersion || found.RequiresPid != expected.RequiresPid || found.RequiresExecution != expected.RequiresExecution ||
                found.RequiresWrite != expected.RequiresWrite || found.RequiresLaunch != expected.RequiresLaunch || !JsonElement.DeepEquals(found.InputSchema, expected.InputSchema)) return false;
        }
        return true;
    }
    private static void DemandRevision(ManagerPluginRegistrySnapshot snapshot, string expected) { if (snapshot.Revision != expected) throw new ConfigurationConflictException(snapshot.Revision); }
    private static string Hash(Stream input) => Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    private static string Message(Exception error) => string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
}
