using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private static readonly JsonSerializerOptions MigrationJson = new() { WriteIndented = true };

    private static CoreData RunF04(FeatureContext context, CoreData request)
    {
        var sourcePath = context.FilePath(Read(request, "source"));
        var baselinePath = context.FilePath(Read(request, "baseline"));
        var source = ReadMigrationWorkspace(sourcePath);
        var baseline = ReadMigrationWorkspace(baselinePath);
        var shared = SelectReferencedRecords(source.Data);
        var bundle = source.Codec.Write(shared);
        var result = MergeWorkspace(context, baseline.Data, shared);
        var copy = baseline.Codec.Write(result.Data);
        var paths = WriteMigrationFiles(CreateRun(context, "F04"), bundle, copy);
        var sourceUnchanged = source.Hash == FileHash(sourcePath);
        var baselineUnchanged = baseline.Hash == FileHash(baselinePath);
        return MigrationReport(result, paths, sourceUnchanged, baselineUnchanged);
    }

    private static MigrationInput ReadMigrationWorkspace(string path)
    {
        var input = ReadMigrationDocument(path);
        var codec = SelectMigrationCodec(input.Data);
        var workspace = codec.Read(input.Data);
        ValidateProfileLinks(workspace);
        return new MigrationInput(workspace, input.Hash, codec);
    }

    private static (JsonObject Data, string Hash) ReadMigrationDocument(string path)
    {
        var contents = ReadMigrationBytes(path);
        var document = ParseMigrationDocument(contents);
        return (document, Convert.ToHexString(SHA256.HashData(contents)));
    }

    private static byte[] ReadMigrationBytes(string path)
    {
        using var input = File.OpenRead(path);
        ValidateMigrationSize(input.Length);
        var contents = new byte[(int)input.Length];
        input.ReadExactly(contents);
        return contents;
    }

    private static JsonObject ParseMigrationDocument(byte[] contents)
    {
        var root = JsonNode.Parse(JsonContents(contents), new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        return RequireMigrationObject(root);
    }

    private static ReadOnlySpan<byte> JsonContents(byte[] bytes) =>
        bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? bytes.AsSpan(Encoding.UTF8.Preamble.Length) : bytes;

    private static JsonArray ReadSearchRoots(JsonObject workspace)
    {
        var roots = workspace["SearchRoots"] is null
            ? new JsonArray()
            : RequireMigrationArray(workspace["SearchRoots"], "SearchRoots");
        return new JsonArray(roots.Select(value => (JsonNode?)JsonValue.Create((string?)value)).ToArray());
    }

    private static void ValidateMigrationSize(long bytes)
    {
        if (bytes is 0 or > 5 * 1024 * 1024)
            throw new InvalidDataException("迁移工作区 JSON 须大于 0 字节且不超过 5 MB。");
    }

    private static IMigrationWorkspaceCodec SelectMigrationCodec(JsonObject document)
    {
        var version = (int?)document["SchemaVersion"];
        var codec = version is { } schemaVersion ? MigrationCodecs.Find(schemaVersion) : null;
        return codec ?? throw new InvalidDataException("配置迁移仅支持 SchemaVersion 1。");
    }

    private static Dictionary<string, JsonObject> IndexRecords(JsonObject input, string name, Func<JsonObject, JsonObject> copy)
    {
        var records = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var items = ReadMigrationRecords(input, name);
        foreach (var item in items.Select(copy))
            AddMigrationRecord(records, item);
        return records;
    }

    private static IEnumerable<JsonObject> ReadMigrationRecords(JsonObject input, string name) =>
        RequireMigrationArray(input[name], name).Select(RequireMigrationObject);

    private static void AddMigrationRecord(Dictionary<string, JsonObject> records, JsonObject item)
    {
        var id = RequireMigrationId(item, "Id");
        if (!records.TryAdd(id, item))
            throw new InvalidDataException("同一类迁移记录的 ID 不能重复。");
    }

    private static JsonArray RequireMigrationArray(JsonNode? value, string field) =>
        value as JsonArray ?? throw new InvalidDataException($"工作区字段 {field} 须为数组。");

    private static JsonObject RequireMigrationObject(JsonNode? value) =>
        value as JsonObject ?? throw new InvalidDataException("工作区记录须为 JSON 对象。");

    private static string RequireMigrationId(JsonObject record, string field)
    {
        var value = (string?)record[field];
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
            throw new InvalidDataException($"工作区字段 {field} 缺失或过长。");
        return XmlConvert.VerifyXmlChars(value);
    }

    private static string MigrationId(JsonObject record, string field) => (string)record[field]!;

    private static JsonObject CopyApplication(JsonObject record) => SelectMigrationFields(record,
        "Id", "Name", "Version", "Kind", "FileVersion", "ExecutablePath", "InitializationPath", "WorkingDirectory", "ProductCode");

    private static JsonObject CopyLicense(JsonObject record) => SelectMigrationFields(record,
        "Id", "Name", "Host", "RmsPort", "RequireRules", "RulesPort", "RmsCheck", "LocalServiceName", "VmPath", "VMwarePath",
        "PollSeconds", "Enabled");

    private static JsonObject CopyProfile(JsonObject record)
    {
        var result = SelectMigrationFields(record, "Id", "Name", "AppId", "LicenseId", "VersionGroup", "Project", "Username",
            "Mdb", "Module");
        result["ToolIds"] = new JsonArray(MigrationToolIds(record).Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        result["CredentialKey"] = "";
        result["AskForPassword"] = true;
        return result;
    }

    private static JsonObject CopyTool(JsonObject record)
    {
        var result = SelectMigrationFields(record, "Id", "Name", "Group", "SourceKind", "SourcePath", "ArchiveSearchDirectory",
            "TargetDirectory", "FlattenRoot", "KeepBackup");
        result["Files"] = ProjectMigrationObjects(record["Files"], file => SelectMigrationFields(file, "SourcePath", "RelativePath"));
        result["Variables"] = ProjectMigrationObjects(record["Variables"], CopyToolVariable);
        result["RecordsDirectory"] = "";
        return result;
    }

    private static JsonObject CopyToolVariable(JsonObject record)
    {
        var result = SelectMigrationFields(record, "Id", "Name", "Value", "Scope");
        result["WasApplied"] = false;
        result["AppliedName"] = "";
        result["AppliedValue"] = "";
        result["AppliedScope"] = "Process";
        return result;
    }

    private static JsonArray ProjectMigrationObjects(JsonNode? source, Func<JsonObject, JsonObject> project)
    {
        var records = source is null ? new JsonArray() : RequireMigrationArray(source, "工具数据");
        return new JsonArray(records.Select(item => (JsonNode?)project(RequireMigrationObject(item))).ToArray());
    }

    private static JsonObject SelectMigrationFields(JsonObject source, params string[] names)
    {
        var target = new JsonObject();
        foreach (var field in names)
            CopyMigrationScalar(source, target, field);
        return target;
    }

    private static void CopyMigrationScalar(JsonObject source, JsonObject target, string field)
    {
        if (source[field] is JsonValue value)
            target[field] = value.DeepClone();
    }

    private static IEnumerable<string> MigrationToolIds(JsonObject profile)
    {
        var tools = profile["ToolIds"] is null ? new JsonArray() : RequireMigrationArray(profile["ToolIds"], "ToolIds");
        return tools.Select(item => (string?)item ?? throw new InvalidDataException("ToolIds 须包含有效 ID。"));
    }

    private static void ValidateProfileLinks(MigrationWorkspace workspace)
    {
        foreach (var profile in workspace.Profiles.Values)
        {
            RequireMigrationLink(workspace.Applications, RequireMigrationId(profile, "AppId"));
            RequireMigrationLink(workspace.Licenses, RequireMigrationId(profile, "LicenseId"));
            foreach (var tool in MigrationToolIds(profile))
                RequireMigrationLink(workspace.Tools, tool);
        }
    }

    private static void RequireMigrationLink(Dictionary<string, JsonObject> records, string id)
    {
        if (!records.ContainsKey(id))
            throw new InvalidDataException("工作区存在无法解析的应用、许可或工具关联。");
    }

    private static MigrationWorkspace SelectReferencedRecords(MigrationWorkspace workspace)
    {
        var profiles = workspace.Profiles.Values;
        var applications = profiles.Select(profile => MigrationId(profile, "AppId"));
        var licenses = profiles.Select(profile => MigrationId(profile, "LicenseId"));
        var tools = profiles.SelectMany(MigrationToolIds);
        return new MigrationWorkspace(Referenced(workspace.Applications, applications), Referenced(workspace.Licenses, licenses),
            Referenced(workspace.Tools, tools), workspace.Profiles, new JsonArray(), true);
    }

    private static Dictionary<string, JsonObject> Referenced(Dictionary<string, JsonObject> records, IEnumerable<string> keys) =>
        keys.Distinct(StringComparer.Ordinal).ToDictionary(key => key, key => records[key], StringComparer.Ordinal);

    private static MigrationResult MergeWorkspace(FeatureContext context, MigrationWorkspace baseline, MigrationWorkspace incoming)
    {
        var notes = new List<string>();
        var apps = MergeApplications(context, baseline.Applications, incoming.Applications, notes);
        var licenses = TransferRecords(baseline.Licenses, incoming.Licenses);
        var tools = TransferRecords(baseline.Tools, incoming.Tools);
        var profiles = incoming.Profiles.Values.Select(profile => RemapProfile(profile, apps, licenses, tools))
            .Select(profile => baseline.Profiles[AddNewMigrationRecord(baseline.Profiles, profile)]).ToArray();
        return new MigrationResult(baseline, profiles, notes);
    }

    private static Dictionary<string, string> MergeApplications(FeatureContext context,
        Dictionary<string, JsonObject> baseline, Dictionary<string, JsonObject> incoming, List<string> notes)
    {
        var candidateApps = baseline.Values.ToArray();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, app) in incoming)
        {
            var match = FindLocalApplication(context, candidateApps, app);
            var destination = MapApplication(baseline, app, match);
            ids.Add(id, destination);
            notes.Add(match is null ? $"{id} → {destination}：保留原应用路径。" : $"{id} → {destination}：匹配本地应用记录。");
        }
        return ids;
    }

    private static JsonObject? FindLocalApplication(FeatureContext context, JsonObject[] candidates, JsonObject incoming)
    {
        var compatible = candidates.Where(candidate => SameApplicationKind(candidate, incoming)).ToArray();
        var exact = compatible.FirstOrDefault(candidate => SameApplicationPath(candidate, incoming));
        if (exact is not null || IsWorkspaceApplication(context, incoming))
            return exact;
        return FindInstalledApplication(context, compatible, incoming);
    }

    private static JsonObject? FindInstalledApplication(FeatureContext context, JsonObject[] candidates, JsonObject incoming)
    {
        var matching = candidates.Where(candidate => SameInstalledVersion(context, candidate, incoming)).Take(2).ToArray();
        return matching.Length == 1 ? matching[0] : null;
    }

    private static bool SameApplicationKind(JsonObject left, JsonObject right) =>
        string.Equals(left["Kind"]?.ToJsonString(), right["Kind"]?.ToJsonString(), StringComparison.OrdinalIgnoreCase);

    private static bool SameApplicationPath(JsonObject left, JsonObject right)
    {
        var path = (string?)left["ExecutablePath"];
        return !string.IsNullOrWhiteSpace(path) && EqualMigrationPath(path, (string?)right["ExecutablePath"])
            && EqualMigrationPath((string?)left["InitializationPath"], (string?)right["InitializationPath"]);
    }

    private static bool EqualMigrationPath(string? first, string? second) =>
        string.Equals(first?.Replace('\\', '/'), second?.Replace('\\', '/'),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool SameInstalledVersion(FeatureContext context, JsonObject candidate, JsonObject incoming)
    {
        var version = (string?)candidate["Version"];
        return !string.IsNullOrWhiteSpace(version) && version == (string?)incoming["Version"]
            && (string?)candidate["ProductCode"] == (string?)incoming["ProductCode"]
            && IsWorkspaceApplication(context, candidate);
    }

    private static bool IsWorkspaceApplication(FeatureContext context, JsonObject application)
    {
        var executable = (string?)application["ExecutablePath"];
        return !string.IsNullOrWhiteSpace(executable) && !Path.IsPathRooted(executable)
            && File.Exists(context.FilePath(executable));
    }

    private static string MapApplication(Dictionary<string, JsonObject> baseline, JsonObject incoming, JsonObject? existing) =>
        existing is null ? AddNewMigrationRecord(baseline, incoming) : MigrationId(existing, "Id");

    private static Dictionary<string, string> TransferRecords(Dictionary<string, JsonObject> baseline,
        Dictionary<string, JsonObject> incoming) =>
        incoming.ToDictionary(record => record.Key, record => AddNewMigrationRecord(baseline, record.Value), StringComparer.Ordinal);

    private static string AddNewMigrationRecord(Dictionary<string, JsonObject> baseline, JsonObject record)
    {
        var copy = record.DeepClone().AsObject();
        var id = Guid.NewGuid().ToString("N");
        copy["Id"] = id;
        baseline.Add(id, copy);
        return id;
    }

    private static JsonObject RemapProfile(JsonObject original, Dictionary<string, string> applications,
        Dictionary<string, string> licenses, Dictionary<string, string> tools)
    {
        var profile = original.DeepClone().AsObject();
        profile["AppId"] = applications[MigrationId(original, "AppId")];
        profile["LicenseId"] = licenses[MigrationId(original, "LicenseId")];
        profile["ToolIds"] = new JsonArray(MigrationToolIds(original).Select(tool => (JsonNode?)JsonValue.Create(tools[tool])).ToArray());
        return profile;
    }

    private static (string Bundle, string Copy) WriteMigrationFiles(string root, JsonObject bundle, JsonObject merged)
    {
        var bundlePath = Path.Combine(root, "share.json");
        var copyPath = Path.Combine(root, "workspace.json");
        File.WriteAllText(bundlePath, bundle.ToJsonString(MigrationJson));
        File.WriteAllText(copyPath, merged.ToJsonString(MigrationJson));
        return (bundlePath, copyPath);
    }

    private static CoreData MigrationReport(MigrationResult result, (string Bundle, string Copy) paths,
        bool sourceUnchanged, bool baselineUnchanged) =>
        new("result", new CoreField("mode", "IsolatedWorkspace"), new CoreField("sourceUnchanged", sourceUnchanged),
            new CoreField("baselineUnchanged", baselineUnchanged), new CoreField("bundle", paths.Bundle),
            new CoreField("copy", paths.Copy), new CoreData("preview", result.Notes.Select(note => new CoreData("note", note))),
            new CoreData("imported", result.Profiles.Select(DescribeMigrationProfile)));

    private static CoreData DescribeMigrationProfile(JsonObject profile) =>
        new("profile", new CoreField("id", MigrationId(profile, "Id")), new CoreField("appId", MigrationId(profile, "AppId")),
            new CoreField("licenseId", MigrationId(profile, "LicenseId")), new CoreField("credentialRemoved", true),
            MigrationToolIds(profile).Select(tool => new CoreData("tool", new CoreField("id", tool))));

    private static string FileHash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private sealed record MigrationWorkspace(Dictionary<string, JsonObject> Applications, Dictionary<string, JsonObject> Licenses,
        Dictionary<string, JsonObject> Tools, Dictionary<string, JsonObject> Profiles, JsonArray SearchRoots, bool NotifyOnDisconnect);

    private sealed record MigrationInput(MigrationWorkspace Data, string Hash, IMigrationWorkspaceCodec Codec);
    private sealed record MigrationResult(MigrationWorkspace Data, JsonObject[] Profiles, List<string> Notes);
}
