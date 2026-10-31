using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Manager.LaunchDesk;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>发现范围及总扫描预算；省略根目录时读取注册表与标准安装位置。</summary>
public sealed record LaunchDiscoveryRequest(string[]? Roots = null, string? Product = null, string? Version = null,
    int MaximumDepth = 5, int MaximumFiles = 300000, int TimeoutSeconds = 30,
    string? CandidateId = null, string? PreferredExecutable = null, Dictionary<string, string>? Overrides = null);
/// <summary>只读安装来源，不能证明产品正在运行。</summary>
public sealed record LaunchDiscoveryRoot(string Path, string Source);
/// <summary>逐字段解析依据、人工覆盖、验证结果和待选择值。</summary>
public sealed record LaunchResolvedField(string Field, string? Value, string Origin, bool UserOverride,
    string Validation, string[] Alternatives, bool NeedsInput);
/// <summary>带来源的静态安装与 APS 可用性证据。</summary>
public sealed record LaunchDiscoveryCandidate(ManagerInstallationCandidate Installation, string[] Sources, bool ApsAvailable,
    ProductModuleSet ModuleOptions);
/// <summary>扫描范围、预算和实际文件计数；不完整结果保留可用候选。</summary>
public sealed record LaunchDiscoveryCoverage(LaunchDiscoveryRoot[] Roots, bool Complete, long FilesSeen, int MaximumDepth, int MaximumFiles, int TimeoutSeconds);
/// <summary>可直接消费的发现与预填结果；草稿不保存、不包含密码、不授予启动许可。</summary>
public sealed record LaunchDiscoveryResult(string State, DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt,
    string EvidenceKind, LaunchDiscoveryCoverage Coverage, LaunchDiscoveryCandidate[] Candidates,
    [property: JsonConverter(typeof(LaunchDiscoveryDraftConverter))] LaunchProfile? Draft,
    LaunchResolvedField[] Fields, string[] NeedsInput, DiscoveryDiagnostic[] Diagnostics);

/// <summary>将发现草稿投影为既有 preview/save 输入，不输出运行历史、扩展字段或空许可策略。</summary>
public sealed class LaunchDiscoveryDraftConverter : JsonConverter<LaunchProfile>
{
    private static readonly HashSet<string> InputFields = new(StringComparer.Ordinal)
    { "id", "name", "kind", "executable", "initialization", "product", "version", "workingDirectory", "project", "user", "mdb", "module", "license", "credentialKey", "rememberPassword", "isFavorite", "isDefault", "environment", "licensePrecheck" };

    /// <summary>使用既有配置元数据读取输入草稿。</summary>
    public override LaunchProfile? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize(ref reader, LaunchLifecycleJsonContext.Default.LaunchProfile);

    /// <summary>仅写出既有输入契约允许的非空字段，使调用方可直接预览和保存。</summary>
    public override void Write(Utf8JsonWriter writer, LaunchProfile value, JsonSerializerOptions options)
    {
        var profile = JsonSerializer.SerializeToElement(value, LaunchLifecycleJsonContext.Default.LaunchProfile);
        writer.WriteStartObject();
        foreach (var property in profile.EnumerateObject())
            if (InputFields.Contains(property.Name) && property.Value.ValueKind != JsonValueKind.Null) property.WriteTo(writer);
        writer.WriteEndObject();
    }
}

/// <summary>组合现有安装扫描器与工作区草稿模型，供 CLI 与 HTML 共用。</summary>
public sealed class LaunchDiscoveryService(Func<LaunchDiscoveryRoot[]>? defaultRoots = null)
{
    /// <summary>在本机有界扫描并解析唯一候选；显式目标、有效偏好依次优先。</summary>
    public LaunchDiscoveryResult Discover(LaunchDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        var observed = DateTimeOffset.UtcNow;
        if (request.MaximumDepth is < 0 or > 8 || request.MaximumFiles is < 1 or > 300000 || request.TimeoutSeconds is < 1 or > 120 || request.Roots is { Length: > 32 })
            throw new ArgumentException("安装扫描范围或预算无效。");
        var sourceDiagnostics = new List<DiscoveryDiagnostic>();
        var roots = request.Roots is { Length: > 0 } ? request.Roots.Select(p => new LaunchDiscoveryRoot(p, "explicit-root")).ToArray()
            : defaultRoots is not null ? defaultRoots() : CollectDefaultRoots(sourceDiagnostics);
        var uniqueRoots = roots.GroupBy(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path)), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
        var report = uniqueRoots.Length == 0 ? new ManagerInstallationReport(true, 0, [], [])
            : ManagerInstallationDiscovery.Scan(uniqueRoots.Select(r => r.Path).ToArray(), request.MaximumDepth, request.MaximumFiles, request.TimeoutSeconds, cancellationToken);
        var diagnostics = report.Diagnostics.Concat(sourceDiagnostics).ToList();
        var candidates = report.Candidates.Where(c => (request.Product is null || c.Identity.Product.Equals(request.Product, StringComparison.OrdinalIgnoreCase)) &&
            (request.Version is null || c.Identity.ProductVersion == request.Version || c.Identity.VersionHint.Contains(request.Version, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new LaunchDiscoveryCandidate(c, roots.Where(r => IsWithin(c.Identity.Root, r.Path)).Select(r => r.Source).Distinct().ToArray(),
                c.ProjectModules.Projects.Any(p => p.Code.Equals("APS", StringComparison.OrdinalIgnoreCase) && p.DirectoryExists), Modules(c.Identity))).ToArray();
        ProductModuleSet Modules(ManagerInstallationIdentity identity)
        {
            try { return new ProductModuleCatalog().Get(new(identity.Executable, identity.Product, identity.ProductVersion, identity.Kind)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new("ModuleMetadataUnreadable", identity.Executable, error.Message));
                return new ProductModuleCatalog().Get(new("", identity.Product, identity.ProductVersion, identity.Kind));
            }
        }
        LaunchDiscoveryCandidate? selected = null;
        var origin = "unique-candidate";
        if (request.CandidateId is not null) { selected = candidates.SingleOrDefault(c => c.Installation.Identity.Id == request.CandidateId); origin = "explicit-target"; }
        else if (request.PreferredExecutable is not null) { selected = candidates.SingleOrDefault(c => c.Installation.Identity.Executable.Equals(request.PreferredExecutable, StringComparison.OrdinalIgnoreCase)); origin = "validated-preference"; }
        if (selected is null && request.CandidateId is null && request.PreferredExecutable is null && candidates.Length == 1) selected = candidates[0];
        var fields = new List<LaunchResolvedField>();
        LaunchProfile? draft = null;
        if (selected is not null)
        {
            var identity = selected.Installation.Identity;
            draft = LaunchDrafts.FromApplication(identity.Executable);
            draft.Product = identity.Product; draft.Kind = identity.Kind;
            draft.User = request.Overrides?.GetValueOrDefault("user") ?? "";
            draft.Mdb = request.Overrides?.GetValueOrDefault("mdb") ?? "";
            fields.Add(new("user", draft.User, "user-input", request.Overrides?.ContainsKey("user") == true, draft.User.Length > 0 ? "provided" : "unresolved", [], draft.User.Length == 0));
            fields.Add(new("mdb", draft.Mdb, "user-input", request.Overrides?.ContainsKey("mdb") == true, draft.Mdb.Length > 0 ? "provided" : "unresolved", [], draft.Mdb.Length == 0));
            var projects = selected.Installation.ProjectModules.Projects.Where(p => p.DirectoryExists).Select(p => p.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var modules = selected.ModuleOptions.Options.Select(m => m.Value).ToArray();
            var staticModules = selected.Installation.ProjectModules.Modules.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matchedModules = modules.Where(staticModules.Contains).ToArray();
            draft.Project = projects.Length == 1 ? projects[0] : "";
            draft.Module = matchedModules.Length == 1 ? matchedModules[0] : "";
            Resolve("project", projects, draft.Project, v => draft.Project = v);
            Resolve("module", modules, draft.Module, v => draft.Module = v);
            Resolve("workingDirectory", [draft.WorkingDirectory], draft.WorkingDirectory, v => draft.WorkingDirectory = v, true);
            Resolve("initialization", [draft.Initialization], draft.Initialization, v => draft.Initialization = v, true);
            fields.Add(new("executable", draft.Executable, origin, request.CandidateId is not null, "valid", candidates.Select(c => c.Installation.Identity.Executable).ToArray(), false));
            draft.Name = LaunchDrafts.SuggestName(draft);
            void Resolve(string name, string[] alternatives, string value, Action<string> set, bool path = false)
            {
                var overridden = request.Overrides?.TryGetValue(name, out var manual) == true;
                if (overridden) value = request.Overrides![name];
                var valid = path ? (name == "initialization" && value.Length == 0 && draft.Kind != "Monitor") ||
                    (Path.IsPathFullyQualified(value) && (name == "workingDirectory" ? Directory.Exists(value) : File.Exists(value)))
                    : !string.IsNullOrWhiteSpace(value) && alternatives.Contains(value, StringComparer.OrdinalIgnoreCase);
                var fieldOrigin = overridden ? "user-override" : "static-discovery";
                var validation = valid ? "valid" : "unresolved-or-invalid";
                if (name == "module" && valid)
                {
                    value = alternatives.Single(v => v.Equals(value, StringComparison.OrdinalIgnoreCase));
                    fieldOrigin = overridden ? "user-override" : "static-discovery-and-product-module-catalog";
                    validation = selected.ModuleOptions.Options.Single(m => m.Value == value).Verified ? "installation-metadata-not-runtime-verified" : "product-mapped-unverified";
                }
                set(value); fields.Add(new(name, value, fieldOrigin, overridden, validation, alternatives, !valid));
            }
        }
        else fields.Add(new("candidateId", null, "static-discovery", request.CandidateId is not null, "unresolved", candidates.Select(c => c.Installation.Identity.Id).ToArray(), true));
        foreach (var candidate in candidates) diagnostics.AddRange(candidate.Installation.ProjectModules.Diagnostics);
        var needs = fields.Where(f => f.NeedsInput).Select(f => f.Field).ToArray();
        return new(needs.Length > 0 ? "needsInput" : "resolved", observed, observed.AddMinutes(5), "static-not-session-verified",
            new(roots, report.Complete && sourceDiagnostics.Count == 0 && candidates.All(c => c.Installation.ProjectModules.Diagnostics.Count == 0), report.FilesSeen, request.MaximumDepth, request.MaximumFiles, request.TimeoutSeconds), candidates, draft, fields.ToArray(), needs, diagnostics.ToArray());
    }

    private static bool IsWithin(string path, string root) => path.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>仅查询固定注册表位置和标准 AVEVA 目录，不扫描整个磁盘或执行厂商脚本。</summary>
    public static LaunchDiscoveryRoot[] DefaultRoots() => CollectDefaultRoots([]);

    private static LaunchDiscoveryRoot[] CollectDefaultRoots(List<DiscoveryDiagnostic> diagnostics)
    {
        var roots = new List<LaunchDiscoveryRoot>();
        if (Path.GetPathRoot(Environment.SystemDirectory) is { Length: > 0 } systemDrive)
            roots.Add(new(Path.Combine(systemDrive, "AVEVA"), "default-install-directory"));
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct())
            if (!string.IsNullOrWhiteSpace(folder)) roots.Add(new(Path.Combine(folder, "AVEVA"), "default-install-directory"));
        if (OperatingSystem.IsWindows())
        {
            foreach (var registryHive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    try
                    {
                        using var hive = RegistryKey.OpenBaseKey(registryHive, view);
                        using var aveva = hive.OpenSubKey(@"SOFTWARE\AVEVA");
                        var seen = 0;
                        if (aveva is not null) ReadVendor(aveva, 0);
                        void ReadVendor(RegistryKey key, int depth)
                        {
                            if (++seen > 256) { diagnostics.Add(new("RegistryLimit", key.Name, "注册表扫描达到键预算。")); return; }
                            foreach (var name in key.GetValueNames().Take(64))
                            {
                                if (!new[] { "InstallPath", "InstallLocation", "Path", "InstallDir", "Directory", "RootDir", "RootPath", "InstallationPath" }.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                                if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value && Path.IsPathFullyQualified(value) && !value.StartsWith(@"\\", StringComparison.Ordinal))
                                    roots.Add(new(value, "registry-aveva-" + registryHive + "-" + view));
                            }
                            if (depth >= 4) return;
                            foreach (var name in key.GetSubKeyNames().Take(64))
                            { if (seen >= 256) break; using var child = key.OpenSubKey(name); if (child is not null) ReadVendor(child, depth + 1); }
                        }
                    }
                    catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                    { diagnostics.Add(new("RegistryReadFailed", registryHive + "/AVEVA/" + view, error.Message)); }
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                try
                {
                    using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var uninstall = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    foreach (var name in (uninstall?.GetSubKeyNames() ?? []).Take(4096))
                    {
                        using var item = uninstall!.OpenSubKey(name);
                        if ((item?.GetValue("DisplayName") as string)?.Contains("AVEVA", StringComparison.OrdinalIgnoreCase) != true) continue;
                        if (item!.GetValue("InstallLocation") is string location && Path.IsPathFullyQualified(location) && !location.StartsWith(@"\\", StringComparison.Ordinal))
                            roots.Add(new(location, "registry-uninstall-" + view));
                    }
                }
                catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                { diagnostics.Add(new("RegistryReadFailed", "HKLM/Uninstall/" + view, error.Message)); }
        }
        var distinct = roots.GroupBy(r => Path.TrimEndingDirectorySeparator(r.Path), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
        if (distinct.Length > 32) diagnostics.Add(new("RootLimit", "registry/default-roots", "安装来源超过 32 个根目录预算；请明确指定所需范围。"));
        return distinct.Take(32).ToArray();
    }
}
