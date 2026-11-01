using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>来源文件的只读净化材料；摘要对应原始字节，不对应净化内容。</summary>
public sealed record MigrationPreviewMaterial(string File, string Sha256, string SourceVersion, JsonElement Content);
/// <summary>不含来源值的稳定诊断；Path为JSON Pointer。</summary>
public sealed record MigrationPreviewDiagnostic(string Code, string File, string Path);
/// <summary>未激活的迁移审查材料；Complete不表示可直接切换数据库。</summary>
public sealed record MigrationPreviewReport(int SchemaVersion, string Format, bool Activate, bool Complete,
    MigrationPreviewMaterial[] Materials, MigrationPreviewDiagnostic[] Diagnostics);

/// <summary>迁移预览专用无反射JSON元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MigrationPreviewReport))]
public partial class MigrationPreviewJsonContext : JsonSerializerContext;

/// <summary>读取明确选择的来源，不创建目录、不激活、不跟随来源中的路径。</summary>
public static class MigrationPreview
{
    private const int MaxFiles = 256, MaxFileBytes = 2 * 1024 * 1024, MaxTotalBytes = 16 * 1024 * 1024;

    /// <summary>按rewrite、next或ribbon格式读取受限来源；文件级拒绝通过诊断返回。</summary>
    public static MigrationPreviewReport Preview(string format, string source, CancellationToken cancellationToken = default)
    {
        var materials = new List<MigrationPreviewMaterial>();
        var diagnostics = new List<MigrationPreviewDiagnostic>();
        var complete = true; long total = 0; var fileCount = 0; var root = "";
        if (format is not ("rewrite" or "next" or "ribbon")) return Failed("FORMAT_UNSUPPORTED");
        if (!Path.IsPathFullyQualified(source) || source.StartsWith(@"\\", StringComparison.Ordinal)) return Failed("SOURCE_PATH_INVALID");
        try
        {
            source = Path.GetFullPath(source); PathRules.NoLinks(source);
            var isDirectory = Directory.Exists(source);
            root = isDirectory ? source : Path.GetDirectoryName(source)!;
            foreach (var file in Files())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++fileCount > MaxFiles) { Reject("FILE_COUNT_BUDGET", "", ""); break; }
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                try
                {
                    var safe = PathRules.Inside(root, relative);
                    using var stream = new FileStream(safe, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > MaxFileBytes || total + stream.Length > MaxTotalBytes)
                    { Reject("BYTE_BUDGET", relative, ""); continue; }
                    total += stream.Length;
                    var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
                    var content = Parse(bytes, relative);
                    if (content is null) continue;
                    materials.Add(new(relative, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                        SourceVersion(content.Value, relative),
                        Sanitize(content.Value, relative)));
                }
                catch (JsonException) { Reject("JSON_INVALID", relative, ""); }
                catch (DecoderFallbackException) { Reject("UTF8_INVALID", relative, ""); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                { Reject("SOURCE_READ_REJECTED", relative, ""); }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { Reject("SOURCE_READ_REJECTED", "", ""); }
        if (materials.Count == 0) Reject("NO_SUPPORTED_MATERIAL", "", "");
        return new(1, format, false, complete, materials.ToArray(), diagnostics.ToArray());

        MigrationPreviewReport Failed(string code) => new(1, format, false, false, [], [new(code, "", "")]);
        void Reject(string code, string file, string path) { complete = false; diagnostics.Add(new(code, file, path)); }
        string SourceVersion(JsonElement value, string file) => format switch
        {
            "rewrite" => "schema-" + Number(value, "SchemaVersion"),
            "ribbon" => Has(value, "project") ? "workspace-version-unversioned/project-1" : "workspace-document-unversioned",
            _ => Path.GetFileName(file) == "records.jsonl" ? "records-unversioned" :
                value.ValueKind == JsonValueKind.Array ? "launch-requests-unversioned" :
                Has(value, "Model") ? "snapshot-unversioned/model-" + Number(Field(value, "Model"), "SchemaVersion") :
                Has(value, "SchemaVersion") ? "model-" + Number(value, "SchemaVersion") : "launch-request-unversioned"
        };

        IEnumerable<string> Files()
        {
            if (!Directory.Exists(source)) { yield return source; yield break; }
            if (format == "rewrite") { yield return Path.Combine(source, "workspace.json"); yield break; }
            if (format == "next")
            {
                foreach (var name in new[] { "profiles.json", "draft.json", "logs/records.jsonl" })
                { var path = PathRules.Inside(source, name); if (File.Exists(path)) yield return path; }
                var snapshots = PathRules.Inside(source, "snapshots");
                if (Directory.Exists(snapshots)) foreach (var file in Directory.EnumerateFiles(snapshots, "*.json")) yield return file;
                yield break;
            }
            var documents = PathRules.Inside(source, "documents");
            var folders = Directory.EnumerateDirectories(documents).GetEnumerator();
            using (folders)
            {
                var count = 0;
                while (folders.MoveNext())
                {
                    if (++count > MaxFiles) { Reject("DIRECTORY_COUNT_BUDGET", "", ""); yield break; }
                    var folder = folders.Current; PathRules.NoLinks(folder);
                    if (!Guid.TryParseExact(Path.GetFileName(folder), "D", out _)) { Reject("DOCUMENT_ID_INVALID", "", ""); continue; }
                    yield return Path.Combine(folder, "document.json");
                    var versions = PathRules.Inside(folder, "versions");
                    if (Directory.Exists(versions)) foreach (var file in Directory.EnumerateFiles(versions, "*.json")) yield return file;
                }
            }
        }

        JsonElement? Parse(byte[] bytes, string file)
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            if (format == "next" && Path.GetFileName(file) == "records.jsonl")
            {
                var rows = new JsonArray();
                using var lines = new StringReader(text);
                while (lines.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using var row = JsonDocument.Parse(line, new() { MaxDepth = 64 });
                    if (!Unique(row.RootElement) || !Has(row.RootElement, "Sequence", "Timestamp", "AttemptId", "Snapshot"))
                    { Reject("HISTORY_STRUCTURE_INVALID", file, ""); return null; }
                    rows.Add(JsonNode.Parse(row.RootElement.GetRawText()));
                }
                return JsonSerializer.SerializeToElement(rows, MigrationPreviewNodeContext.Default.JsonArray);
            }
            using var doc = JsonDocument.Parse(text, new() { MaxDepth = 64 }); var value = doc.RootElement;
            if (!Unique(value)) { Reject("DUPLICATE_FIELD", file, ""); return null; }
            var valid = false;
            if (format == "rewrite")
            {
                var version = Number(value, "SchemaVersion");
                if (version == 3 && Has(value, "Generation", "Sha256"))
                {
                    var generation = Field(value, "Generation"); var digest = Field(value, "Sha256");
                    if (generation.ValueKind != JsonValueKind.String || digest.ValueKind != JsonValueKind.String ||
                        !Guid.TryParseExact(Path.GetFileNameWithoutExtension(generation.GetString()), "N", out _) ||
                        generation.GetString() != Path.GetFileName(generation.GetString()) ||
                        !generation.GetString()!.EndsWith(".json", StringComparison.Ordinal) ||
                        digest.GetString()!.Length != 64 || !digest.GetString()!.All(Uri.IsHexDigit))
                    { Reject("GENERATION_POINTER_INVALID", file, ""); return null; }
                    var relative = ".control/generations/" + generation.GetString();
                    var generationPath = PathRules.Inside(root, relative);
                    using var stream = new FileStream(generationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (++fileCount > MaxFiles || stream.Length > MaxFileBytes || total + stream.Length > MaxTotalBytes)
                    { Reject("BYTE_BUDGET", relative, ""); return null; }
                    total += stream.Length;
                    var generationBytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(generationBytes);
                    var hash = Convert.ToHexString(SHA256.HashData(generationBytes)).ToLowerInvariant();
                    if (!hash.Equals(digest.GetString(), StringComparison.OrdinalIgnoreCase))
                    { Reject("GENERATION_HASH_MISMATCH", relative, ""); return null; }
                    using var envelope = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(generationBytes).TrimStart('\uFEFF'), new() { MaxDepth = 64 });
                    var body = envelope.RootElement;
                    if (!Unique(body) || Number(body, "SchemaVersion") != 3 || !Has(body, "Snapshot", "History") ||
                        !HasArrays(Field(body, "Snapshot"), "Applications", "Profiles") || !Has(Field(body, "Snapshot"), "WorkspaceId") || Field(body, "History").ValueKind != JsonValueKind.Object)
                    { Reject("GENERATION_STRUCTURE_INVALID", relative, ""); return null; }
                    materials.Add(new(relative, hash, "schema-3", Sanitize(body, relative)));
                    return value.Clone();
                }
                valid = version == 2 && HasArrays(value, "Applications", "Profiles") ||
                    version == 3 && Has(value, "Snapshot", "History") &&
                    HasArrays(Field(value, "Snapshot"), "Applications", "Profiles") && Has(Field(value, "Snapshot"), "WorkspaceId") &&
                    Field(value, "History").ValueKind == JsonValueKind.Object;
            }
            if (format == "next") valid = Next(value);
            if (format == "ribbon")
            {
                if (!Guid.TryParseExact(Path.GetFileName(Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar))), "D", out _) && Path.GetFileName(file) == "document.json")
                    valid = false;
                else if (Path.GetFileName(file) == "document.json") valid = Has(value, "id", "lastVersion", "bindings") && NoFuture(value);
                else valid = Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "D", out _) &&
                    Has(value, "id", "sequence", "project") && Number(Field(value, "project"), "schemaVersion") == 1 &&
                    HasArrays(Field(value, "project"), "buttons", "tabs") && NoFuture(value);
            }
            if (!valid) { Reject("FORMAT_OR_VERSION_UNSUPPORTED", file, ""); return null; }
            return value.Clone();
        }

        JsonElement Sanitize(JsonElement input, string file)
        {
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) Write(input, writer, "");
            using var doc = JsonDocument.Parse(output.ToArray()); return doc.RootElement.Clone();
            void Write(JsonElement value, Utf8JsonWriter writer, string path)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (value.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject())
                    {
                        var next = path + "/" + property.Name.Replace("~", "~0").Replace("/", "~1");
                        if (Sensitive(property.Name)) { diagnostics.Add(new("FIELD_REDACTED", file, next)); continue; }
                        if (property.Name.Equals("Protocol", StringComparison.OrdinalIgnoreCase) &&
                            (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var protocol) && protocol == 2 ||
                             property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Equals("Direct", StringComparison.OrdinalIgnoreCase)))
                            diagnostics.Add(new("DIRECT_UNSUPPORTED", file, next));
                        writer.WritePropertyName(property.Name); Write(property.Value, writer, next);
                    }
                    writer.WriteEndObject();
                }
                else if (value.ValueKind == JsonValueKind.Array)
                { writer.WriteStartArray(); var i = 0; foreach (var item in value.EnumerateArray()) Write(item, writer, path + "/" + i++); writer.WriteEndArray(); }
                else value.WriteTo(writer);
            }
        }
    }

    private static bool Sensitive(string name)
    {
        var key = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return new[] { "password", "passwd", "pwd", "secret", "token", "credential", "authorization", "authentication", "privatekey", "apikey", "cookie", "bearer", "connectionstring", "sessionkey" }.Any(key.Contains) ||
            key is "environment" or "environmentvariables" or "arguments" or "argumentplan" or "commandline" or "inputsnapshot" or "reason" or "message" or "exception" or "error";
    }
    private static JsonElement Field(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
        ? value.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value : default;
    private static int Number(JsonElement value, string name) => Field(value, name).ValueKind == JsonValueKind.Number && Field(value, name).TryGetInt32(out var number) ? number : -1;
    private static bool Has(JsonElement value, params string[] names) => names.All(n => Field(value, n).ValueKind != JsonValueKind.Undefined);
    private static bool HasArrays(JsonElement value, params string[] names) => names.All(n => Field(value, n).ValueKind == JsonValueKind.Array);
    private static bool NoFuture(JsonElement value) => !Has(value, "SchemaVersion") || Number(value, "SchemaVersion") == 1;
    private static bool Next(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().All(Next);
        if (Has(value, "Model")) return Has(value, "AttemptId", "Revision", "CapturedUtc") && Next(Field(value, "Model")) && NoFuture(value);
        return Has(value, "ConfigurationId", "Protocol", "ExecutablePath") && NoFuture(value);
    }
    private static bool Unique(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == value.EnumerateObject().Count() && value.EnumerateObject().All(p => Unique(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().All(Unique),
        _ => true
    };
}

[JsonSerializable(typeof(JsonArray))]
internal partial class MigrationPreviewNodeContext : JsonSerializerContext;
