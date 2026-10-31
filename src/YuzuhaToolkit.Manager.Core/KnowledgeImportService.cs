using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>两种导入入口；只接受用户指定的来源，产品与版本为必填。</summary>
public sealed record KnowledgeImportRequest(string Product, string Version, string Role, string Name,
    string? Database = null, string? Manifest = null, string? PmlLib = null, string? PmlUi = null,
    string? WebHelp = null, KnowledgeSourceMapping[]? Mappings = null);
/// <summary>冻结来源内容、目标身份与预期操作；预览不建立索引。</summary>
public sealed record KnowledgeImportPlan(KnowledgeImportRequest Request, string Target, string Mode,
    string SourceFingerprint, string? ManifestFingerprint, long SourceFiles, long SourceBytes,
    string[] Warnings);
/// <summary>完成发布的版本库与完整分层报告。</summary>
public sealed record KnowledgeImportReceipt(RegisteredKnowledgeLibrary Library, KnowledgeInspectionReport Report);

/// <summary>一致快照或显式建库，在暂存检查全部通过后以目录重命名发布。</summary>
public sealed class KnowledgeImportService(string? knowledgeDirectory = null, Action<string>? onPhase = null,
    CancellationToken cancellationToken = default)
{
    private readonly string root = Path.GetFullPath(knowledgeDirectory ?? UserDataPaths.KnowledgeDirectory());

    /// <summary>只检查选定来源并冻结内容摘要；已有目标总是拒绝覆盖。</summary>
    public KnowledgeImportPlan Preview(KnowledgeImportRequest request)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var segment in new[] { request.Product, request.Version, request.Name })
            if (!Regex.IsMatch(segment, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$") || segment.EndsWith('.')) throw new ArgumentException("产品、版本和名称只能含字母、数字、点、横线或下划线。");
        if (request.Role is not ("official" or "custom")) throw new ArgumentException("资料角色必须为 official 或 custom。");
        if (request.Name.Equals("personal", StringComparison.OrdinalIgnoreCase) || request.Name.Equals("project", StringComparison.OrdinalIgnoreCase) || request.Name.Equals("experience", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("不能覆盖个人、项目或经验保留库。");
        var target = PathRules.Inside(root, $"Libraries/{request.Product}/{request.Version}/{request.Name}");
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("同名版本资料库已存在，请改名；导入不会覆盖或重建。");
        var mappings = (request.Mappings ?? []).Select(m => new KnowledgeSourceMapping(Local(m.RecordedRoot), Local(m.LocalRoot))).ToArray();
        if (mappings.GroupBy(m => m.RecordedRoot, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new ArgumentException("原始来源根映射重复。");
        request = request with { Database = Optional(request.Database), Manifest = Optional(request.Manifest),
            PmlLib = Optional(request.PmlLib), PmlUi = Optional(request.PmlUi), WebHelp = Optional(request.WebHelp), Mappings = mappings };
        if (request.Database is not null)
        {
            if (request.PmlLib is not null || request.PmlUi is not null || request.WebHelp is not null) throw new ArgumentException("已有库导入与资料源建库必须分别计划。");
            var report = KnowledgeInspector.Inspect(request.Database, mappings, request.Manifest, cancellationToken);
            if (!report.CanEnable) throw new InvalidDataException("已有库检查未通过：" + string.Join("; ", report.Items.Where(i => i.Status == "error").Select(i => i.Code + ": " + i.Message)));
            var manifest = ManifestHash(request.Manifest);
            return new(request, target, "database", report.DatabaseSha256, manifest, report.Sources,
                new FileInfo(request.Database).Length, report.Items.Where(i => i.Status == "warning").Select(i => i.Message).ToArray());
        }
        if (request.Manifest is not null) throw new ArgumentException("资料源建库不接受已有 manifest。");
        var evidence = SourceFingerprint(request, cancellationToken);
        return new(request, target, "sources", evidence.Hash, null, evidence.Count, evidence.Bytes, []);
    }

    /// <summary>应用预览；失败清除私有暂存目录，原有资料及启用状态保持。</summary>
    public KnowledgeImportReceipt Apply(KnowledgeImportPlan plan)
    {
        var fresh = Preview(plan.Request);
        if (JsonSerializer.Serialize(fresh, ManagerJsonContext.Default.KnowledgeImportPlan) != JsonSerializer.Serialize(plan, ManagerJsonContext.Default.KnowledgeImportPlan)) throw new IOException("来源或导入计划已变化，请重新预览。");
        var parent = Path.GetDirectoryName(plan.Target)!; PathRules.NoLinks(parent); Directory.CreateDirectory(parent);
        var stage = Path.Combine(parent, ".import-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try
        {
            onPhase?.Invoke("snapshot");
            cancellationToken.ThrowIfCancellationRequested();
            string database; string originalHash;
            if (plan.Mode == "database")
            {
                database = Path.Combine(stage, "library.sqlite3");
                originalHash = KnowledgeInspector.Snapshot(plan.Request.Database!, database);
                if (originalHash != plan.SourceFingerprint || ManifestHash(plan.Request.Manifest) != plan.ManifestFingerprint) throw new IOException("快照期间来源发生变化。");
                if (plan.Request.Manifest is not null)
                {
                    using var source = new FileStream(plan.Request.Manifest, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = new FileStream(Path.Combine(stage, "imported.manifest.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    source.CopyTo(output); output.Flush(true);
                }
                if (plan.Request.Manifest is not null && Hash(Path.Combine(stage, "imported.manifest.json")) != plan.ManifestFingerprint)
                    throw new IOException("复制期间来源 manifest 变化。");
            }
            else
            {
                var built = new KnowledgeRepository(stage).Build(plan.Request.PmlLib, plan.Request.PmlUi, plan.Request.WebHelp, "library", stage, false, 0, cancellationToken: cancellationToken);
                if (built.Warnings.Count > 0) throw new InvalidDataException("资料未完整索引，未发布：" + string.Join("; ", built.Warnings.Take(10)));
                database = built.Database; originalHash = Hash(database);
                if (SourceFingerprint(plan.Request, cancellationToken).Hash != plan.SourceFingerprint) throw new IOException("资料在索引期间变化，未发布新库。");
            }
            onPhase?.Invoke("validate");
            var report = KnowledgeInspector.WithManifest(KnowledgeInspector.InspectSnapshot(database, plan.Request.Database ?? database,
                originalHash, plan.Request.Mappings ?? [], cancellationToken), plan.Mode == "sources" ? Path.Combine(stage, "library.manifest.json") :
                    plan.Request.Manifest is null ? null : Path.Combine(stage, "imported.manifest.json"));
            if (!report.CanEnable) throw new InvalidDataException("暂存知识库检查未通过：" + string.Join("; ", report.Items.Where(i => i.Status == "error").Select(i => i.Message)));
            var publishedDatabase = Path.Combine(plan.Target, "library.sqlite3");
            // FTS self-check may change SQLite housekeeping bytes. Persist the final published identity.
            var hash = Hash(database);
            var registration = new KnowledgeLibraryRegistration(1, Guid.NewGuid().ToString("N"),
                plan.Request.Product, plan.Request.Version, plan.Request.Role, plan.Request.Name, hash,
                plan.Mode == "database" ? plan.SourceFingerprint : null, plan.ManifestFingerprint,
                plan.Request.Mappings ?? [], DateTime.UtcNow.ToString("o"));
            var finalReport = report with { Database = publishedDatabase, DatabaseSha256 = hash };
            File.WriteAllText(Path.Combine(stage, "inspection.json"), JsonSerializer.Serialize(finalReport, KnowledgeLibraryJson.Default.KnowledgeInspectionReport));
            onPhase?.Invoke("registration");
            File.WriteAllText(Path.Combine(stage, "registration.json"), JsonSerializer.Serialize(registration, KnowledgeLibraryJson.Default.KnowledgeLibraryRegistration));
            onPhase?.Invoke("publish");
            cancellationToken.ThrowIfCancellationRequested();
            PathRules.NoLinks(plan.Target);
            Directory.Move(stage, plan.Target);
            return new(new(publishedDatabase, registration), finalReport);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    private static (string Hash, long Count, long Bytes) SourceFingerprint(KnowledgeImportRequest request, CancellationToken cancellationToken)
    {
        var roots = new[] { ("pmllib", request.PmlLib), ("pmlui", request.PmlUi), ("webhelp", request.WebHelp) }.Where(r => r.Item2 is not null).ToArray();
        if (roots.Length == 0) throw new ArgumentException("至少选择一个 PMLLIB、PMLUI 或 WebHelp 来源。");
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long count = 0, bytes = 0;
        foreach (var (kind, directory) in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("来源目录不存在：" + directory);
            PathRules.NoLinks(directory!);
            foreach (var file in KnowledgeRepository.EnumerateIndexFiles(directory!, kind).Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (++count > 200000 || input.Length > 256L * 1024 * 1024 || (bytes += input.Length) > 8L * 1024 * 1024 * 1024) throw new IOException("资料源超过文件数或体积预算。");
                var name = kind + ":" + Path.GetRelativePath(directory!, file).Replace('\\', '/');
                digest.AppendData(Encoding.UTF8.GetBytes(name + "\0")); digest.AppendData(SHA256.HashData(input));
            }
        }
        if (count == 0) throw new InvalidDataException("所选来源没有可索引文件。");
        return (Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant(), count, bytes);
    }
    private static string? ManifestHash(string? file)
    {
        if (file is null) return null;
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 16 * 1024 * 1024) throw new IOException("来源清单超过 16 MiB 预算。");
        using var document = JsonDocument.Parse(input);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("manifest 必须是 JSON 对象。");
        input.Position = 0; return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }
    private static string Hash(string file) { using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Local(value);
    private static string Local(string value)
    {
        if (!Path.IsPathFullyQualified(value) || value.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("请选择明确的本机绝对路径。");
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)); PathRules.NoLinks(path); return path;
    }
}
