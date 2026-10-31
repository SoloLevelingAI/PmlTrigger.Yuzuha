using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>打开并固定插件全部声明文件，持有只读租约直到操作结束。</summary>
internal sealed class PluginPackage : IDisposable
{
    /// <summary>插件包根目录必须包含的清单文件名。</summary>
    public const string ManifestName = "manager-plugin.json";
    private readonly List<(string Name, FileStream Stream)> files = [];
    /// <summary>经过绝对路径解析且禁止链接的插件包目录。</summary>
    public string Directory { get; }
    /// <summary>从固定文件租约解析并通过身份、文件和操作 schema 校验的清单。</summary>
    public ManagerPluginManifest Manifest { get; }
    /// <summary>打开包时读取的清单内容 SHA256，用于固定清单身份。</summary>
    public string ManifestSha256 { get; }
    /// <summary>将清单入口解析到包目录内，拒绝越界路径。</summary>
    public string EntryPoint => PathRules.Inside(Directory, Manifest.EntryPoint);
    /// <summary>验证清单、文件哈希、大小预算和操作命名空间，并保持所有已声明文件的只读租约。</summary>
    public PluginPackage(string directory)
    {
        Directory = Path.GetFullPath(directory); PathRules.NoLinks(Directory);
        try
        {
            var manifestFile = Open(ManifestName, 2 * 1024 * 1024);
            ManifestSha256 = Hash(manifestFile); manifestFile.Position = 0;
            Manifest = JsonSerializer.Deserialize(manifestFile, PluginJsonContext.Default.ManagerPluginManifest) ?? throw new InvalidDataException("插件清单为空。");
            if (Manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(Manifest.Id) || !Regex.IsMatch(Manifest.Id, @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)*$") ||
                Manifest.Id.Length > 80 || string.IsNullOrWhiteSpace(Manifest.Version) || Manifest.Version.Length > 80 || Manifest.Files is null || Manifest.Operations is null ||
                Manifest.Files.Count is < 1 or > 2000 || Manifest.Operations.Length is < 1 or > 100 || Manifest.Operations.Any(d => d is null))
                throw new InvalidDataException("插件清单标识、版本或预算无效。");
            if (Manifest.Files.ContainsKey(ManifestName) || !Manifest.Files.ContainsKey(Manifest.EntryPoint) ||
                !new[] { ".exe", ".dll" }.Contains(Path.GetExtension(Manifest.EntryPoint), StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("清单必须声明相对 EXE/DLL 入口，且不得循环声明清单自身。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long bytes = 0;
            foreach (var pair in Manifest.Files)
            {
                if (!names.Add(pair.Key.Replace('\\', '/')) || pair.Value is null || pair.Value.Length != 64 || !pair.Value.All(Uri.IsHexDigit)) throw new InvalidDataException("插件文件声明重复或 SHA256 无效。");
                var stream = Open(pair.Key, 256L * 1024 * 1024); bytes = checked(bytes + stream.Length);
                if (bytes > 1024L * 1024 * 1024 || !Hash(stream).Equals(pair.Value, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("插件文件内容与清单不匹配：" + pair.Key);
            }
            var declared = names.Append(ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>(); pending.Push(Directory); var count = 0;
            while (pending.TryPop(out var current))
            {
                PathRules.NoLinks(current);
                foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(current))
                {
                    PathRules.NoLinks(entry); if (++count > 4000) throw new IOException("插件包条目超过预算。");
                    if (System.IO.Directory.Exists(entry)) pending.Push(entry);
                    else if (!declared.Contains(Path.GetRelativePath(Directory, entry).Replace('\\', '/'))) throw new InvalidDataException("插件包含未声明文件：" + entry);
                }
            }
            _ = new PmlTwin.Phayton.OperationRegistry(Manifest.Operations.Select(d => new PmlTwin.Phayton.OperationRegistration(d,
                new PmlTwin.Phayton.DelegateOperationHandler((id, _, _, _) => throw new NotSupportedException(id)))));
            if (Manifest.Operations.Any(d => !d.Id.StartsWith(Manifest.Id + ".", StringComparison.Ordinal) || d.InputSchema.ValueKind != JsonValueKind.Object))
                throw new InvalidDataException("插件操作必须处于自己的命名空间。");
            foreach (var descriptor in Manifest.Operations) ValidateSchema(descriptor.InputSchema, 0);
        }
        catch { Dispose(); throw; }
    }
    private FileStream Open(string relative, long budget)
    {
        var file = PathRules.Inside(Directory, relative);
        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        files.Add((relative, stream)); if (stream.Length > budget) throw new IOException("插件文件超过预算：" + relative); return stream;
    }
    /// <summary>从已固定的文件流复制到目标目录内的新文件并刷新到磁盘；不覆盖已有文件。</summary>
    public void CopyTo(string directory)
    {
        foreach (var file in files)
        {
            var destination = PathRules.Inside(directory, file.Name); System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            file.Stream.Position = 0;
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Stream.CopyTo(output); output.Flush(true);
        }
    }
    private static string Hash(Stream input) { input.Position = 0; return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
    private static void ValidateSchema(JsonElement schema, int depth)
    {
        if (depth > 32 || schema.ValueKind != JsonValueKind.Object) throw new InvalidDataException("插件输入 schema 超出受支持的结构预算。");
        if (schema.TryGetProperty("anyOf", out var choices))
        {
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() is < 1 or > 20) throw new InvalidDataException("插件 anyOf 无效。");
            foreach (var child in choices.EnumerateArray()) ValidateSchema(child, depth + 1); return;
        }
        if (!schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
            type.GetString() is not ("object" or "array" or "string" or "number" or "integer" or "boolean" or "null")) throw new InvalidDataException("插件 schema 类型不受支持。");
        if (type.GetString() == "object")
        {
            if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object) throw new InvalidDataException("插件 object schema 必须显式声明 properties。");
            foreach (var property in properties.EnumerateObject()) ValidateSchema(property.Value, depth + 1);
            if (schema.TryGetProperty("additionalProperties", out var additional))
            { if (additional.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ValidateSchema(additional, depth + 1); }
            if (schema.TryGetProperty("required", out var required) && (required.ValueKind != JsonValueKind.Array || required.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))) throw new InvalidDataException("插件 required 无效。");
        }
        if (type.GetString() == "array")
        { if (!schema.TryGetProperty("items", out var items)) throw new InvalidDataException("插件 array schema 缺少 items。"); ValidateSchema(items, depth + 1); }
        if (schema.TryGetProperty("enum", out var values) && values.ValueKind != JsonValueKind.Array) throw new InvalidDataException("插件 enum 无效。");
        foreach (var key in new[] { "minimum", "maximum", "minLength", "maxLength" })
            if (schema.TryGetProperty(key, out var value) && (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))) throw new InvalidDataException("插件 schema 数值约束无效：" + key);
        if (schema.TryGetProperty("pattern", out var pattern))
        { if (pattern.ValueKind != JsonValueKind.String) throw new InvalidDataException("插件 pattern 无效。"); _ = new Regex(pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
    }
    /// <summary>释放包内清单及声明文件的只读租约并清空持有列表。</summary>
    public void Dispose() { foreach (var file in files) file.Stream.Dispose(); files.Clear(); }
}
