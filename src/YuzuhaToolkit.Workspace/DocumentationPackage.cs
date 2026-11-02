using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace YuzuhaToolkit.Workspace;

/// <summary>文档清单中的一个相对文件及其精确字节身份。</summary>
/// <param name="Path">使用正斜线的包内相对路径。</param>
/// <param name="Sha256">原始文件字节的 SHA256。</param>
/// <param name="Bytes">原始文件长度。</param>
public sealed record DocumentationFile(string Path, string Sha256, long Bytes);

/// <summary>用户说明指定的独立开发参考发行物身份；不下载或解压该文件。</summary>
/// <param name="FileName">发布的 ZIP 文件名。</param>
/// <param name="Sha256">完整 ZIP 的 SHA256，供下载后独立核对。</param>
/// <param name="ManifestSha256">解压根 developer-reference.json 的 SHA256。</param>
/// <param name="SourceCommit">开发参考对应的完整源码提交。</param>
public sealed record DeveloperReferenceIdentity(string FileName, string Sha256, string ManifestSha256, string SourceCommit);

/// <summary>已校验的默认用户说明包；未登记的旧安装文件不属于可提供内容。</summary>
/// <param name="Root">用户站绝对目录。</param>
/// <param name="SourceCommit">用户说明对应的源码提交。</param>
/// <param name="Version">发行版本。</param>
/// <param name="Files">以站内相对路径为键的只读文件白名单。</param>
/// <param name="DeveloperReference">唯一匹配的独立开发参考身份。</param>
/// <param name="ProfileFile">自指清单之外、经原始字节摘要绑定的用户说明身份文件。</param>
public sealed record UserDocumentationPackage(string Root, string SourceCommit, string Version,
    IReadOnlyDictionary<string, DocumentationFile> Files, DeveloperReferenceIdentity DeveloperReference, DocumentationFile ProfileFile);

/// <summary>已与用户说明绑定并完整核验的独立开发参考。</summary>
/// <param name="PackageRoot">明确选择的解压包根目录。</param>
/// <param name="SiteRoot">包中 site 的绝对目录。</param>
/// <param name="SourceCommit">开发参考对应的源码提交。</param>
/// <param name="ManifestSha256">开发包清单原始字节的 SHA256。</param>
/// <param name="Files">以 site 内相对路径为键的只读文件白名单。</param>
public sealed record ValidatedDocumentationReference(string PackageRoot, string SiteRoot, string SourceCommit,
    string ManifestSha256, IReadOnlyDictionary<string, DocumentationFile> Files);

/// <summary>默认说明和独立开发参考共用的有界只读身份验证，不下载、解压或保存配置。</summary>
public static class DocumentationPackage
{
    private const long ManifestLimit = 8 * 1024 * 1024;
    private const long FileLimit = 128L * 1024 * 1024;
    private const long ReferenceLimit = 512L * 1024 * 1024;
    private const long UserGuideLimit = 15L * 1024 * 1024;
    private const int MemberLimit = 12000;

    /// <summary>核对用户说明清单及其列出的全部文件；未列旧文件保留但不会进入白名单。</summary>
    /// <param name="root">包含 documentation-profile.json 的明确绝对目录。</param>
    /// <returns>已验证的说明包及独立参考身份。</returns>
    public static UserDocumentationPackage ValidateUserGuide(string root)
    {
        root = Root(root);
        var manifest = ReadManifest(root, "documentation-profile.json");
        using var document = manifest.Document;
        var value = document.RootElement;
        Fields(value, "schema", "sourceCommit", "version", "developerReference", "files");
        Require(Text(value, "schema") == "phayton.user-guide/1", "用户说明格式不支持。");
        var commit = Commit(value); var version = Version(value);
        var reference = value.GetProperty("developerReference");
        Fields(reference, "fileName", "sha256", "manifestSha256", "sourceCommit");
        var fileName = Text(reference, "fileName");
        Require(fileName == "Phayton-" + version + "-Developer-Reference.zip" && Relative(root, fileName) == Path.Combine(root, fileName) && !fileName.Contains('/'), "开发参考 ZIP 文件名必须与发行版本一致。");
        var identity = new DeveloperReferenceIdentity(fileName, Hash(reference, "sha256"), Hash(reference, "manifestSha256"), Commit(reference));
        Require(identity.SourceCommit == commit, "用户说明与开发参考的源码修订不一致。");
        var files = Files(root, value, UserGuideLimit, "documentation-profile.json");
        var forbidden = new HashSet<string>(["api", "reference", "source", "sources", "source-docs", "test", "tests", "trace", "ui-trace", "metadata"], StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>([".html", ".css", ".js", ".mjs", ".json", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".txt", ".md", ".yml"], StringComparer.OrdinalIgnoreCase);
        Require(files.Keys.All(path => !path.Split('/').Any(forbidden.Contains) && extensions.Contains(Path.GetExtension(path))), "用户说明不能登记开发资料树、源码映射或未支持资源。");
        Require(files.ContainsKey("index.html") && files.ContainsKey("developer-reference.html"), "用户说明缺少首页或开发参考说明页。");
        Require(files.Keys.Count(path => path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)) <= 40, "用户说明超过 40 个 HTML 页面预算。");
        VerifyAll(root, files);
        Require(Sha(ReadManifestBytes(root, "documentation-profile.json")) == manifest.Sha256, "用户说明清单在验证期间变化。");
        return new(root, commit, version, ReadOnly(files), identity, new("documentation-profile.json", manifest.Sha256, manifest.Bytes));
    }

    /// <summary>核对独立包修订、清单 SHA、完整成员、大小及流式 SHA，并绑定用户说明中的身份。</summary>
    /// <param name="userSiteRoot">当前用户说明站点根目录。</param>
    /// <param name="referencePackageRoot">用户明确选择的开发参考解压根目录。</param>
    /// <returns>仅 site 文件可被挂载的已验证开发参考。</returns>
    public static ValidatedDocumentationReference ValidateReference(string userSiteRoot, string referencePackageRoot)
    {
        var user = ValidateUserGuide(userSiteRoot);
        var root = Root(referencePackageRoot);
        var manifest = ReadManifest(root, "developer-reference.json");
        using var document = manifest.Document;
        Require(manifest.Sha256 == user.DeveloperReference.ManifestSha256, "开发参考清单摘要与用户说明不匹配。");
        var value = document.RootElement;
        Fields(value, "schema", "sourceCommit", "version", "siteRoot", "files");
        Require(Text(value, "schema") == "phayton.developer-reference/1" && Text(value, "siteRoot") == "site", "开发参考格式或站点目录不支持。");
        var commit = Commit(value);
        Require(commit == user.SourceCommit && Version(value) == user.Version, "开发参考版本或源码修订与用户说明不匹配。");
        var files = Files(root, value, ReferenceLimit, "developer-reference.json");
        Require(files.Keys.All(path => path.StartsWith("site/", StringComparison.Ordinal) || path.StartsWith("source-docs/", StringComparison.Ordinal)), "开发参考清单只能登记 site/ 与 source-docs/。");
        Require(files.ContainsKey("site/index.html"), "开发参考缺少 site/index.html。");
        Require(files.Keys.Any(path => path.StartsWith("source-docs/", StringComparison.Ordinal)), "开发参考必须包含已登记的 source-docs 原始文档。");
        VerifyAll(root, files);
        VerifyInventory(root, files);
        Require(Sha(ReadManifestBytes(root, "developer-reference.json")) == manifest.Sha256, "开发参考清单在验证期间变化。");
        var siteFiles = files.Where(pair => pair.Key.StartsWith("site/", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key[5..], pair => pair.Value with { Path = pair.Key[5..] }, StringComparer.OrdinalIgnoreCase);
        return new(root, Path.Combine(root, "site"), commit, manifest.Sha256, ReadOnly(siteFiles));
    }

    /// <summary>重新核对路径、链接、大小与原始字节后打开一个白名单文件；流保持只读且拒绝并发写入。</summary>
    /// <param name="root">已验证的站点根目录。</param>
    /// <param name="file">已验证清单中的期望成员。</param>
    /// <returns>位于开头、由调用方负责释放的只读流。</returns>
    public static Stream OpenRead(string root, DocumentationFile file)
    {
        root = Root(root);
        var path = Relative(root, file.Path);
        NoLinks(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        try
        {
            Require(stream.Length == file.Bytes && stream.Length <= FileLimit, "文档文件大小已变化。");
            Require(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() == file.Sha256, "文档文件内容已变化。");
            stream.Position = 0;
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private static Dictionary<string, DocumentationFile> Files(string root, JsonElement value, long totalLimit, string manifestName)
    {
        var entries = value.GetProperty("files");
        Require(entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() is > 0 and <= MemberLimit, "文档文件清单为空或超过预算。");
        var files = new Dictionary<string, DocumentationFile>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            Fields(entry, "path", "sha256", "bytes");
            var path = Text(entry, "path"); _ = Relative(root, path);
            Require(!path.Equals(manifestName, StringComparison.OrdinalIgnoreCase), "清单不能登记自身。");
            var size = entry.GetProperty("bytes"); long bytes = 0;
            Require(size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out bytes) && bytes is >= 0 and <= FileLimit, "文档成员大小无效或超过预算。");
            total = checked(total + bytes);
            Require(total <= totalLimit, "文档总大小超过预算。");
            Require(files.TryAdd(path, new(path, Hash(entry, "sha256"), bytes)), "文档清单包含重复路径。");
        }
        return files;
    }

    private static void VerifyAll(string root, Dictionary<string, DocumentationFile> files)
    {
        foreach (var file in files.Values) using (OpenRead(root, file)) { }
    }

    private static void VerifyInventory(string root, Dictionary<string, DocumentationFile> files)
    {
        var pending = new Queue<string>(); pending.Enqueue(root); var count = 0; var directories = 0;
        while (pending.TryDequeue(out var directory))
        {
            Require(++directories <= MemberLimit, "开发参考目录数超过预算。");
            NoLinks(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                NoLinks(entry);
                if (Directory.Exists(entry)) { pending.Enqueue(entry); continue; }
                Require(++count <= MemberLimit + 1, "开发参考成员数超过预算。");
                var relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
                Require(relative == "developer-reference.json" || files.ContainsKey(relative), "开发参考包含未登记文件。");
            }
        }
        Require(count == files.Count + 1, "开发参考成员与清单不一致。");
    }

    private static (JsonDocument Document, string Sha256, long Bytes) ReadManifest(string root, string name)
    {
        var bytes = ReadManifestBytes(root, name);
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 24 });
        try { UniqueProperties(document.RootElement); return (document, Sha(bytes), bytes.LongLength); }
        catch { document.Dispose(); throw; }
    }

    private static byte[] ReadManifestBytes(string root, string name)
    {
        var path = Relative(root, name); NoLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length is > 0 and <= ManifestLimit, "文档清单为空或超过 8MiB 预算。");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }

    private static void UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { Require(seen.Add(property.Name), "文档清单包含重复字段。"); UniqueProperties(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) UniqueProperties(item);
    }

    private static void Fields(JsonElement value, params string[] names)
    {
        Require(value.ValueKind == JsonValueKind.Object, "文档清单字段必须是对象。");
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        Require(actual.Length == names.Length && actual.All(name => names.Contains(name, StringComparer.Ordinal)), "文档清单缺少字段或含未知字段。");
    }

    private static string Text(JsonElement value, string name)
    {
        var element = value.GetProperty(name);
        Require(element.ValueKind == JsonValueKind.String, "文档身份字段必须是文本。");
        var text = element.GetString()!;
        Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 2048 && !text.Any(char.IsControl), "文档身份文本无效。");
        return text;
    }
    private static string Hash(JsonElement value, string name) { var text = Text(value, name); Require(Hex(text, 64), "文档摘要无效。"); return text; }
    private static string Commit(JsonElement value) { var text = Text(value, "sourceCommit"); Require(Hex(text, 40), "文档源码修订必须是完整提交。"); return text; }
    private static string Version(JsonElement value) { var text = Text(value, "version"); Require(text.Length <= 128, "文档版本过长。"); return text; }
    private static bool Hex(string text, int length) => text.Length == length && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static IReadOnlyDictionary<string, DocumentationFile> ReadOnly(Dictionary<string, DocumentationFile> files) => new ReadOnlyDictionary<string, DocumentationFile>(files);
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
    private static string Root(string root)
    {
        Require(Path.IsPathFullyQualified(root), "文档目录必须是明确的绝对路径。");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        NoLinks(root); Require(Directory.Exists(root), "文档目录不存在。"); return root;
    }
    private static string Relative(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && relative.Length <= 1024 && !Path.IsPathRooted(relative) && !relative.Any(char.IsControl) && relative.IndexOfAny(['\\', ':', '%']) < 0, "文档路径无效。");
        foreach (var segment in relative.Split('/'))
        {
            var stem = segment.Split('.')[0].ToUpperInvariant();
            Require(segment is not ("" or "." or "..") && !segment.EndsWith(' ') && !segment.EndsWith('.') && segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                stem is not ("CON" or "PRN" or "AUX" or "NUL") && !((stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem.Length == 4 && "123456789¹²³".Contains(stem[3])), "文档路径含别名或设备名。");
        }
        var full = Path.GetFullPath(Path.Combine(root, relative));
        Require(full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "文档路径越界。"); return full;
    }
    private static void NoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0) continue;
            Require(OperatingSystem.IsWindows() && IsCloud(current), "文档路径含符号链接、目录联接或未知重解析点。");
        }
    }
    private static bool IsCloud(string path)
    {
        using var handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) return false;
        var buffer = new byte[16 * 1024];
        return DeviceIoControl(handle, 0x000900a8, IntPtr.Zero, 0, buffer, buffer.Length, out var returned, IntPtr.Zero) && returned >= 4 &&
            (BitConverter.ToUInt32(buffer, 0) & 0xffff0fff) == 0x9000001a;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize,
        [Out] byte[] output, int outputSize, out int returned, IntPtr overlapped);
}
