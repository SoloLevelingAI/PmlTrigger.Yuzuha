using System.Text.Json;
using System.Text.Json.Nodes;

namespace YuzuhaToolkit.Workspace;

/// <summary>启动配置的完整应用用例，界面与 CLI 共用校验、提交、冲突和恢复逻辑。</summary>
public sealed class LaunchProfileService
{
    private readonly IConfigurationStore<WorkspaceSettings> store;
    private readonly IProductModuleCatalog modules;
    private const string Effect = "已保存配置仅在下次启动时使用；当前 AVEVA 会话和进程环境保持不变。";

    /// <summary>注入配置存储和产品模块目录；默认使用当前用户 JSON 状态目录。</summary>
    public LaunchProfileService(IConfigurationStore<WorkspaceSettings>? store = null, IProductModuleCatalog? modules = null)
    { this.store = store ?? new WorkspaceConfigurationStore(); this.modules = modules ?? new ProductModuleCatalog(); }

    /// <summary>读取完整独立快照，用于界面编辑和版本检查。</summary>
    public ConfigSnapshot<WorkspaceSettings> Read() => store.Read();

    /// <summary>读取一个配置及其文档修订。</summary>
    public LaunchProfileRead ReadProfile(string id)
    {
        var snapshot = Read();
        return new(Find(snapshot.Value, id), snapshot.Revision, snapshot.FilePath, Effect);
    }

    /// <summary>组合文本、产品和收藏/最近条件查询配置。</summary>
    public LaunchProfileRead[] List(string query = "", string? product = null, string scope = "all")
    {
        var snapshot = Read();
        return LaunchProfileCatalog.Select(snapshot.Value.Profiles, query, product, scope)
            .Select(p => new LaunchProfileRead(p, snapshot.Revision, snapshot.FilePath, Effect)).ToArray();
    }

    /// <summary>校验产品模块及实际路径，结果可定位到具体字段。</summary>
    public LaunchValidation Validate(LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var catalog = modules.Get(new(profile.Executable ?? "", profile.Product ?? "", profile.Version ?? "", profile.Kind ?? ""));
        var issues = new List<LaunchValidationIssue>();
        void Issue(string field, string code, string message) => issues.Add(new(field, code, message));
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 128) Issue("id", "INVALID_ID", "启动配置 ID 不能为空或超过 128 字符。");
        if (string.IsNullOrWhiteSpace(profile.Name)) Issue("name", "NAME_REQUIRED", "请填写配置名称。");
        if (catalog.Options.Length == 0) Issue("product", "UNKNOWN_PRODUCT", "请选择 AM、E3D 或 PDMS 产品。");
        else if (!catalog.Options.Any(m => string.Equals(m.Value, profile.Module, StringComparison.OrdinalIgnoreCase)))
            Issue("module", "MODULE_PRODUCT_MISMATCH", $"模块 {profile.Module} 不属于 {catalog.Product} 的受支持模块，请重新选择。");
        if (catalog.Product == "AM" && profile.Kind != "MarineBatch") Issue("kind", "PRODUCT_ENTRY_MISMATCH", "AM 应使用 MarineBatch 入口。");
        if (catalog.Product == "E3D" && profile.Kind != "Monitor") Issue("kind", "PRODUCT_ENTRY_MISMATCH", "E3D 应使用 Monitor 入口。");
        foreach (var (field, value) in new[] { ("project", profile.Project), ("user", profile.User), ("mdb", profile.Mdb), ("module", profile.Module) })
            if (string.IsNullOrWhiteSpace(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"'))
                Issue(field, "INVALID_TOKEN", "项目、用户名、MDB 和模块不可为空或包含空白、控制字符、双引号。");
        if (string.IsNullOrWhiteSpace(profile.Executable) || !Path.IsPathFullyQualified(profile.Executable) || !File.Exists(profile.Executable))
            Issue("executable", "ENTRY_NOT_FOUND", "请选择实际存在的程序入口绝对路径。");
        if (string.IsNullOrWhiteSpace(profile.WorkingDirectory) || !Path.IsPathFullyQualified(profile.WorkingDirectory) || !Directory.Exists(profile.WorkingDirectory))
            Issue("workingDirectory", "DIRECTORY_NOT_FOUND", "请选择实际存在的工作目录。");
        if (profile.Kind == "Monitor" && (string.IsNullOrWhiteSpace(profile.Initialization) || !File.Exists(profile.Initialization)))
            Issue("initialization", "INIT_NOT_FOUND", "Monitor 初始化文件不存在。");
        if (profile.Environment is null || profile.Environment.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key.Any(c => c is '=' or '\0') || p.Value is null || p.Value.Contains('\0')))
            Issue("environment", "INVALID_ENVIRONMENT", "环境变量名称或值无效。");
        if (profile.LicensePrecheck is { } precheck)
        {
            if (precheck.Mode is not ("off" or "required")) Issue("licensePrecheck.mode", "INVALID_LICENSE_POLICY", "许可预检模式应为 off 或 required。");
            if (precheck.Mode == "required" && (string.IsNullOrWhiteSpace(precheck.Host) || precheck.Host.Length > 255 || precheck.Host.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ||
                precheck.RmsPort is < 1 or > 65535 || precheck.RulesPort is < 1 or > 65535 || precheck.TimeoutMilliseconds is < 1 or > 10000 ||
                precheck.ServiceName is { } serviceName && (serviceName.Length is < 1 or > 128 || serviceName.Any(c => !char.IsAsciiLetterOrDigit(c) && !" _.()-".Contains(c)))))
                Issue("licensePrecheck", "INVALID_LICENSE_POLICY", "强制预检需要有效的主机、端口、超时及可选本机服务名。");
        }
        if (issues.Count == 0)
        {
            try { _ = LaunchService.Preview(profile, modules); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException) { Issue("executable", "INVALID_LAUNCH_ARGUMENTS", e.Message); }
        }
        return new(issues.Count == 0, issues.ToArray(), catalog);
    }

    /// <summary>校验并预览保存差异，不修改文件。</summary>
    public LaunchProfilePreview PreviewSave(LaunchProfile profile, string expectedRevision)
    {
        var current = Current(expectedRevision);
        var candidate = Prepared(current.Value, profile);
        var saved = Find(candidate, profile.Id);
        return new(saved, expectedRevision, WorkspaceConfigurationStore.Difference(current.Value, candidate), Validate(saved), Effect);
    }

    /// <summary>统一保存完整配置，保护已有未知字段，授权只来自可信调用方。</summary>
    public ConfigCommit<WorkspaceSettings> Save(LaunchProfile profile, string expectedRevision, bool allowWrite, string reason = "编辑启动配置")
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision);
        var candidate = Prepared(current.Value, profile);
        RequireValid(Find(candidate, profile.Id));
        return store.Commit(candidate, expectedRevision, new(reason, profile.Id)) with { Effect = Effect };
    }

    /// <summary>按已知字段局部修改，适合 JSON 输入；未知字段不能由拼写错误意外创建。</summary>
    public ConfigCommit<WorkspaceSettings> Update(string id, JsonElement changes, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision);
        var profile = Find(current.Value, id);
        if (changes.ValueKind != JsonValueKind.Object) throw new ArgumentException("changes 必须是对象。");
        var node = JsonSerializer.SerializeToNode(profile, LaunchLifecycleJsonContext.Default.LaunchProfile)!.AsObject();
        var allowed = new HashSet<string>(["name", "kind", "executable", "initialization", "product", "version", "workingDirectory", "project", "user", "mdb", "module", "license", "licensePrecheck", "environment", "rememberPassword", "isFavorite", "isDefault"], StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in changes.EnumerateObject())
        {
            if (!allowed.Contains(field.Name) || !seen.Add(field.Name)) throw new ArgumentException("不允许或重复的修改字段：" + field.Name);
            node[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        }
        var changed = node.Deserialize(LaunchLifecycleJsonContext.Default.LaunchProfile) ?? throw new ArgumentException("配置不能为空。");
        return Save(changed, expectedRevision, allowWrite);
    }

    /// <summary>删除配置本身，不删除独立凭据，也不影响正在运行的进程。</summary>
    public ConfigCommit<WorkspaceSettings> Delete(string id, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite); var current = Current(expectedRevision); var profile = Find(current.Value, id);
        current.Value.Profiles.Remove(profile);
        return store.Commit(current.Value, expectedRevision, new("删除启动配置", id)) with { Effect = Effect };
    }

    /// <summary>切换收藏，保持其他字段及修订规则。</summary>
    public ConfigCommit<WorkspaceSettings> SetFavorite(string id, bool favorite, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite); var current = Current(expectedRevision); Find(current.Value, id).IsFavorite = favorite;
        return store.Commit(current.Value, expectedRevision, new(favorite ? "收藏启动配置" : "取消收藏", id)) with { Effect = Effect };
    }

    /// <summary>设置唯一默认配置；不会自动启动。</summary>
    public ConfigCommit<WorkspaceSettings> SetDefault(string id, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite); var current = Current(expectedRevision); _ = Find(current.Value, id);
        foreach (var profile in current.Value.Profiles) profile.IsDefault = profile.Id == id;
        return store.Commit(current.Value, expectedRevision, new("设置默认启动配置", id)) with { Effect = Effect };
    }

    /// <summary>创建独立副本，不复制凭据引用、默认标记或运行时间。</summary>
    public LaunchProfileRead Duplicate(string id, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite); var current = Current(expectedRevision);
        var copy = LaunchProfileCatalog.Duplicate(Find(current.Value, id));
        var result = Save(copy, expectedRevision, allowWrite, "复制启动配置");
        return new(Find(result.Snapshot.Value, copy.Id), result.Snapshot.Revision, result.Snapshot.FilePath, Effect);
    }

    /// <summary>获取关联此配置的修订，包含旧版和外部编辑快照。</summary>
    public ConfigHistoryEntry[] History(string? profileId = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        return store.History(500).Where(e => profileId is null || e.ProfileId is null || e.ProfileId == profileId).Take(limit).ToArray();
    }

    /// <summary>比较指定配置的历史内容和当前内容，其他配置不会恢复。</summary>
    public LaunchProfilePreview PreviewRestore(string profileId, string revision, string expectedRevision)
    {
        var current = Current(expectedRevision);
        var historical = CloneProfile(Find(store.ReadRevision(revision).Value, profileId));
        var candidate = Prepared(current.Value, historical);
        return new(historical, expectedRevision, WorkspaceConfigurationStore.Difference(current.Value, candidate), Validate(historical), Effect);
    }

    /// <summary>恢复指定配置并产生新修订；当前校验仍有效，不自动启动或恢复密码。</summary>
    public ConfigCommit<WorkspaceSettings> Restore(string profileId, string revision, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision);
        var historical = CloneProfile(Find(store.ReadRevision(revision).Value, profileId));
        RequireValid(historical);
        var candidate = Prepared(current.Value, historical);
        return store.Commit(candidate, expectedRevision, new("恢复启动配置历史", profileId, revision)) with
        { Effect = Effect + " 凭据引用已保留；若原凭据已删除，需要重新关联。" };
    }

    private ConfigSnapshot<WorkspaceSettings> Current(string expectedRevision)
    {
        var current = Read();
        if (current.Revision != expectedRevision) throw new ConfigurationConflictException(current.Revision);
        return current;
    }
    private void RequireValid(LaunchProfile profile)
    { var validation = Validate(profile); if (!validation.IsValid) throw new LaunchProfileValidationException(validation); }
    private static void DemandWrite(bool allowWrite)
    { if (!allowWrite) throw new UnauthorizedAccessException("修改配置需要显式的配置写入许可。"); }
    private static LaunchProfile Find(WorkspaceSettings settings, string id) => settings.Profiles.SingleOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException("未找到启动配置：" + id);
    private static LaunchProfile CloneProfile(LaunchProfile profile) => JsonSerializer.Deserialize(
        JsonSerializer.SerializeToUtf8Bytes(profile, LaunchLifecycleJsonContext.Default.LaunchProfile), LaunchLifecycleJsonContext.Default.LaunchProfile)!;
    private static WorkspaceSettings Prepared(WorkspaceSettings original, LaunchProfile profile)
    {
        var candidate = WorkspaceConfigurationStore.Clone(original);
        var copy = CloneProfile(profile);
        var existing = candidate.Profiles.SingleOrDefault(p => p.Id == copy.Id);
        // Older editors do not expose this optional policy. Clearing uses explicit mode=off.
        if (copy.LicensePrecheck is null && existing?.LicensePrecheck is not null)
            copy.LicensePrecheck = CloneProfile(existing).LicensePrecheck;
        if (existing?.ExtensionData is not null)
        {
            copy.ExtensionData ??= new(StringComparer.Ordinal);
            foreach (var field in existing.ExtensionData) copy.ExtensionData.TryAdd(field.Key, field.Value.Clone());
        }
        if (existing?.LicensePrecheck?.ExtensionData is { } policyFields && copy.LicensePrecheck is { } policy)
        {
            policy.ExtensionData ??= new(StringComparer.Ordinal);
            foreach (var field in policyFields) policy.ExtensionData.TryAdd(field.Key, field.Value.Clone());
        }
        if (existing is not null) candidate.Profiles[candidate.Profiles.IndexOf(existing)] = copy;
        else candidate.Profiles.Add(copy);
        if (copy.IsDefault) foreach (var other in candidate.Profiles.Where(p => p.Id != copy.Id)) other.IsDefault = false;
        return candidate;
    }
}
