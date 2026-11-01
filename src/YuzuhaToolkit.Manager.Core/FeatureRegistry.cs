using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>稳定的功能身份；启用、兼容与调用授权分别表达，不推断 CAF 已适配。</summary>
public sealed record FeatureDescriptor(string Id, string Version, int ContractVersion, string Owner,
    string[] Requires, string[] Operations, string EntryKind, bool Enabled, string ImplementationStatus,
    string[]? SupportedProductProfiles = null, string Runtime = "manager-net10",
    string CompatibilityStatus = "manager-adapter-only;CAF-unsupported;Host-unverified");

/// <summary>宿主启动时的显式装配选择；停用仅影响新请求，不撤销已受理操作。</summary>
public sealed record FeatureCompositionOptions(string[]? DisabledFeatures = null, string Runtime = "manager-net10",
    string? ProductProfile = null);

/// <summary>明确编译的装配入口；工厂只能返回操作声明，不应执行业务动作。</summary>
public sealed record FeatureRegistration(FeatureDescriptor Descriptor, Func<IEnumerable<OperationRegistration>> Factory);

/// <summary>唯一功能和操作所有权目录；先验证完整元数据，再调用已启用工厂。</summary>
public sealed class FeatureRegistry
{
    private readonly FeatureRegistration[] entries;
    /// <summary>拒绝重复身份、操作所有者和不支持的契约；不调用任何工厂。</summary>
    public FeatureRegistry(IEnumerable<FeatureRegistration> registrations, FeatureCompositionOptions? options = null)
    {
        options ??= new();
        if (options.Runtime != "manager-net10") throw new NotSupportedException("只支持管理进程 net10 装配；CAF/旧 Host 运行时尚无适配器。");
        if (options.ProductProfile is not null && options.ProductProfile is not ("AM" or "PDMS" or "E3D2.1" or "E3D3.1.6"))
            throw new NotSupportedException("未声明的产品 profile 不可装配：" + options.ProductProfile);
        var disabled = new HashSet<string>(options.DisabledFeatures ?? [], StringComparer.Ordinal);
        entries = registrations.Select(e => e with { Descriptor = e.Descriptor with
        { Requires = e.Descriptor.Requires.ToArray(), Operations = e.Descriptor.Operations.ToArray(),
            SupportedProductProfiles = e.Descriptor.SupportedProductProfiles?.ToArray(),
            Enabled = e.Descriptor.Enabled && !disabled.Contains(e.Descriptor.Id) } }).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var d = entry.Descriptor;
            if (string.IsNullOrWhiteSpace(d.Id) || !ids.Add(d.Id)) throw new ArgumentException("Feature 身份为空或重复：" + d.Id);
            if (d.ContractVersion != 1 || !System.Version.TryParse(d.Version, out var version) || version.Major != 1 ||
                d.EntryKind is not ("compiled" or "worker" or "addin") || d.Runtime != options.Runtime)
                throw new NotSupportedException("Feature 版本或装配方式不支持：" + d.Id);
            if (d.Enabled && options.ProductProfile is not null && d.SupportedProductProfiles is { Length: > 0 } profiles && !profiles.Contains(options.ProductProfile, StringComparer.Ordinal))
                throw new NotSupportedException("Feature 不支持所选产品 profile：" + d.Id);
            if (string.IsNullOrWhiteSpace(d.Owner) || d.Operations.Length == 0 || d.Operations.Any(o => string.IsNullOrWhiteSpace(o) || !operations.Add(o)))
                throw new ArgumentException("Feature 缺少所有者或操作存在重复所有者：" + d.Id);
        }
        if (disabled.Any(id => !ids.Contains(id))) throw new ArgumentException("停用清单含未注册 Feature，不能静默忽略拼写错误。");
        foreach (var entry in entries)
            if (entry.Descriptor.Requires.Any(r => !ids.Contains(r))) throw new ArgumentException("Feature 依赖未注册：" + entry.Descriptor.Id);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Feature 依赖形成循环：" + id);
            foreach (var dependency in entries.Single(e => e.Descriptor.Id == id).Descriptor.Requires) Visit(dependency);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var id in ids) Visit(id);
    }

    /// <summary>读取快照不会装配后端或加载 Addin/worker。</summary>
    public FeatureDescriptor[] Describe() => entries.Select(e => e.Descriptor with
    { Requires = e.Descriptor.Requires.ToArray(), Operations = e.Descriptor.Operations.ToArray(),
        SupportedProductProfiles = e.Descriptor.SupportedProductProfiles?.ToArray() }).ToArray();

    /// <summary>装配已启用功能；禁用依赖、不一致工厂及重复操作一律拒绝。</summary>
    public OperationRegistration[] Compose()
    {
        var enabled = entries.Where(e => e.Descriptor.Enabled).ToArray();
        if (enabled.Any(e => e.Descriptor.Requires.Any(r => !enabled.Any(x => x.Descriptor.Id == r))))
            throw new InvalidOperationException("已启用 Feature 的依赖被禁用。");
        var result = new List<OperationRegistration>();
        foreach (var entry in enabled)
        {
            var actual = entry.Factory().ToArray();
            if (!actual.Select(a => a.Descriptor.Id).Order(StringComparer.Ordinal).SequenceEqual(entry.Descriptor.Operations.Order(StringComparer.Ordinal)))
                throw new InvalidOperationException("Feature 工厂与声明操作不一致：" + entry.Descriptor.Id);
            result.AddRange(actual);
        }
        return result.ToArray();
    }

    /// <summary>将既有管理操作按稳定操作族登记，不创建第二套 Addin 或 worker 加载器。</summary>
    public static FeatureRegistry FromOperations(IEnumerable<OperationRegistration> registrations, FeatureCompositionOptions? options = null) => new(
        registrations.GroupBy(r => r.Descriptor.Id.StartsWith("pml.function", StringComparison.Ordinal) ? "functions" : r.Descriptor.Id.Split('.')[0], StringComparer.Ordinal)
        .Select(g => { var rows = g.ToArray(); return new FeatureRegistration(new(g.Key, "1.0", 1,
            g.Key switch { "functions" or "connection" => "CommunicationCliClient → independent communication CLI",
                "ribbon" => "RibbonHttpAdapter → Ribbon HTTP v1 compatibility / v2 frozen deployment",
                "plugin" => "ManagerPluginRegistry → existing worker boundary", _ => "ManagerOperationCatalog → existing application services" },
            [], rows.Select(r => r.Descriptor.Id).ToArray(), "compiled", true, "partial-operation-family-adapter",
            g.Key is "functions" or "connection" or "runtime" ? ["AM", "PDMS", "E3D2.1", "E3D3.1.6"] : []), () => rows); }), options);
}

/// <summary>功能目录的 AOT 友好序列化元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FeatureDescriptor[]))]
public partial class FeatureJsonContext : JsonSerializerContext;

/// <summary>只读功能目录入口。</summary>
public static class FeatureOperations
{
    /// <summary>注册功能列表；读取不启用扩展、不代表动作授权。</summary>
    public static OperationRegistration List(Func<FeatureRegistry> registry)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");
        return new(new("feature.list", 1, "列出唯一 Feature 身份、所有者、操作、启用与实现状态。", schema.RootElement.Clone()),
            new DelegateOperationHandler((id, _, _, token) => {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(registry().Describe(), FeatureJsonContext.Default.FeatureDescriptorArray)));
            }));
    }
}
