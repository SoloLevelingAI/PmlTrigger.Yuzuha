using System.Text.Json;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Knowledge;

/// <summary>已原子发布的版本隔离资料库，保留导入身份及本机映射。</summary>
public sealed record KnowledgeLibraryRegistration(int SchemaVersion, string Id, string Product, string Version,
    string Role, string Name, string DatabaseSha256, string? ImportedDatabaseSha256,
    string? ImportedManifestSha256, KnowledgeSourceMapping[] Mappings, string PublishedAtUtc);
/// <summary>一个可检索库的登记与当前绝对位置。</summary>
public sealed record RegisteredKnowledgeLibrary(string Database, KnowledgeLibraryRegistration Registration);

/// <summary>跨 GUI、管理 CLI 和旧检索入口共用的编译期 JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(KnowledgeLibraryRegistration))]
[JsonSerializable(typeof(RegisteredKnowledgeLibrary[]))]
[JsonSerializable(typeof(KnowledgeInspectionReport))]
public partial class KnowledgeLibraryJson : JsonSerializerContext;

/// <summary>仅读取完成目录发布的登记，不创建目录或自动导入。</summary>
public static class KnowledgeLibraries
{
    /// <summary>列出版本隔离登记；暂存目录没有 registration.json，不会被启用。</summary>
    public static RegisteredKnowledgeLibrary[] List(string knowledgeDirectory, string? product = null, string? version = null)
    {
        var root = Path.Combine(Path.GetFullPath(knowledgeDirectory), "Libraries");
        PathRules.NoLinks(root);
        if (!Directory.Exists(root)) return [];
        var result = new List<RegisteredKnowledgeLibrary>();
        foreach (var productDirectory in SafeDirectories(root).Where(p => product is null || Path.GetFileName(p).Equals(product, StringComparison.OrdinalIgnoreCase)))
        foreach (var versionDirectory in SafeDirectories(productDirectory).Where(p => version is null || Path.GetFileName(p).Equals(version, StringComparison.OrdinalIgnoreCase)))
        foreach (var libraryDirectory in SafeDirectories(versionDirectory))
        {
            if (Path.GetFileName(libraryDirectory).StartsWith('.')) continue;
            var file = PathRules.Inside(libraryDirectory, "registration.json");
            if (!File.Exists(file)) continue;
            if (new FileInfo(file).Length > 1024 * 1024) throw new IOException("资料库登记超过预算：" + file);
            KnowledgeLibraryRegistration row;
            try
            {
                row = JsonSerializer.Deserialize(File.ReadAllText(file), KnowledgeLibraryJson.Default.KnowledgeLibraryRegistration)
                    ?? throw new InvalidDataException("资料库登记为空：" + file);
            }
            catch (JsonException error) { throw new InvalidDataException("资料库登记 JSON 损坏：" + file, error); }
            if (row.SchemaVersion != 1 || row.Product != Path.GetFileName(productDirectory) || row.Version != Path.GetFileName(versionDirectory) || row.Name != Path.GetFileName(libraryDirectory))
                throw new InvalidDataException("资料库登记身份与目录不一致：" + file);
            if (product is not null && !row.Product.Equals(product, StringComparison.OrdinalIgnoreCase) || version is not null && !row.Version.Equals(version, StringComparison.OrdinalIgnoreCase)) continue;
            var database = PathRules.Inside(libraryDirectory, "library.sqlite3");
            if (!File.Exists(database)) throw new InvalidDataException("已登记资料库缺少数据库：" + database);
            result.Add(new(database, row));
            if (result.Count > 10000) throw new IOException("资料库登记超过 10000 条预算。");
        }
        return result.OrderBy(r => r.Registration.Product).ThenBy(r => r.Registration.Version).ThenBy(r => r.Registration.Name).ToArray();
    }
    private static IEnumerable<string> SafeDirectories(string root)
    { PathRules.NoLinks(root); return Directory.EnumerateDirectories(root).Where(p => !Path.GetFileName(p).StartsWith('.')); }
}
