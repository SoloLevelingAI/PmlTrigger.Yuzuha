using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Core30.Core;
using Core30.Generated;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private const int LaunchLibraryLimit = 2 * 1024 * 1024;
    private static readonly JsonNodeOptions LaunchNodeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions LaunchJsonOutput = new() { WriteIndented = true };

    /// <summary>Saves only non-secret launch settings and preserves the rest of the workspace document.</summary>
    private static CoreData SaveLaunchProfile(FeatureContext context, string file, CoreData operation)
    {
        var installation = ReadSelectedInstallation(context, operation);
        var (path, lockPath) = GetLibraryPaths(context, file);
        using var gate = File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var library = ReadLaunchLibrary(path, Value(operation, "revision"));
        var id = UpdateLaunchProfile(library, operation, installation);
        var revision = WriteLaunchLibrary(path, library.Document);
        return new CoreData("result", new CoreField("revision", revision), new CoreField("profile", id));
    }

    private static (string File, string Lock) GetLibraryPaths(FeatureContext context, string relative) =>
        (context.FilePath(relative), context.FilePath(relative + ".core30.lock"));

    private static CoreData ArchiveLaunchProfile(FeatureContext context, string file, CoreData operation)
    {
        var (path, lockPath) = GetLibraryPaths(context, file);
        using var gate = File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var library = ReadLaunchLibrary(path, Value(operation, "revision"));
        var id = Value(operation, "id").Trim();
        var profiles = library.Document["profiles"]!.AsArray();
        var entry = profiles.OfType<JsonObject>().SingleOrDefault(profile =>
            string.Equals(profile["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("所选启动配置已不存在。");
        var backupRelative = $".core30/launch-archive/{Guid.NewGuid():N}.json";
        var backup = context.FilePath(backupRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.WriteAllText(backup, entry.ToJsonString(LaunchJsonOutput));
        profiles.Remove(entry);
        var revision = WriteLaunchLibrary(path, library.Document);
        return new CoreData("result", new CoreField("profile", id), new CoreField("revision", revision),
            new CoreField("backup", backupRelative));
    }

    private static LaunchLibrary ReadLaunchLibrary(string path, string? revision = null)
    {
        var library = LoadLaunchLibrary(path);
        CheckLibraryRevision(library, revision);
        return library;
    }

    private static LaunchLibrary LoadLaunchLibrary(string path)
    {
        try
        {
            var bytes = ReadBounded(path, LaunchLibraryLimit);
            var workspace = ParseWorkspace(bytes);
            var document = JsonNode.Parse(StripUtf8Bom(bytes), LaunchNodeOptions)!.AsObject();
            RequireProfileArray(document);
            return new LaunchLibrary(workspace, document, Sha256(bytes));
        }
        catch (FileNotFoundException)
        {
            return new LaunchLibrary(new Workspace(), new JsonObject(LaunchNodeOptions)
            {
                ["schemaVersion"] = 1, ["profiles"] = new JsonArray()
            }, "missing");
        }
    }

    private static void RequireProfileArray(JsonObject document)
    {
        if (document["profiles"] is not JsonArray)
            throw new InvalidDataException("配置库需要 profiles 数组。");
    }

    private static void CheckLibraryRevision(LaunchLibrary library, string? expected)
    {
        if (expected is not null && library.Revision != expected)
            throw new InvalidDataException("配置库已被修改。请重新读取后再保存。");
    }

    private static string UpdateLaunchProfile(LaunchLibrary library, CoreData operation, LaunchInstallation installation)
    {
        var id = Value(operation, "id").Trim();
        var settings = LaunchProfileFields.ReadValidated(operation.Children().Single());
        ValidateSavedLaunch(settings, installation);
        var target = GetLaunchProfileNode(library.Document["profiles"]!.AsArray(), id, installation.Product);
        target["name"] = Value(operation, "name").Trim();
        target["executable"] = installation.EntryPoint;
        target["project"] = settings.Project;
        target["workingDirectory"] = settings.WorkingDirectory;
        UpdateStoredLaunch(target, settings, installation);
        return id;
    }

    private static void ValidateSavedLaunch(LaunchProfileFields settings, LaunchInstallation installation)
    {
        ValidateLaunchUsername(settings.User);
        if (!HasLaunchMdb(settings.Mdb))
            throw new InvalidDataException("请填写实际 MDB 名称。");
        if (settings.UicDirectory is { } uic)
        {
            if (installation.Product != "E3D")
                throw new InvalidDataException("工作区 UIC 目录仅用于 E3D。");
            FeatureContext.ValidateRelativePath(uic);
        }
    }

    private static void ValidateLaunchUsername(string? user)
    {
        if (ContainsLaunchCredentials(user))
            throw new InvalidDataException("用户字段仅填写用户名。");
    }

    private static bool ContainsLaunchCredentials(string? user) => user?.IndexOfAny(['/', '\\']) >= 0;

    private static JsonObject GetLaunchProfileNode(JsonArray profiles, string id, string product)
    {
        var target = profiles.OfType<JsonObject>().FirstOrDefault(profile =>
            string.Equals(profile["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            target = new JsonObject(LaunchNodeOptions) { ["id"] = id, ["product"] = product };
            profiles.Add(target);
        }
        RequireProfileProduct(target, product);
        return target;
    }

    private static void RequireProfileProduct(JsonObject profile, string product)
    {
        if (!string.Equals(profile["product"]?.GetValue<string>(), product, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("配置 ID 已用于另一个产品。");
    }

    private static void UpdateStoredLaunch(JsonObject target, LaunchProfileFields values, LaunchInstallation installation)
    {
        var launch = target["launch"] as JsonObject ?? new JsonObject(LaunchNodeOptions);
        values.WriteStoredFields(launch);
        if (values.LicenseHost is { } host) launch["licenseHost"] = host;
        if (values.LicensePort is { } port) launch["licensePort"] = port;
        launch["installationDirectory"] = installation.Directory;
        target["launch"] = launch;
    }

    private static string WriteLaunchLibrary(string path, JsonObject document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, LaunchJsonOutput);
        RequireLaunchLibrarySize(bytes.Length);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".core30-launch-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes);
            File.Move(temporary, path, overwrite: true);
            return Sha256(bytes);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void RequireLaunchLibrarySize(int length)
    {
        if (length > LaunchLibraryLimit)
            throw new InvalidDataException("启动配置库超过 2 MiB 上限。");
    }

    private static StoredLaunch ReadStoredLaunch(Profile? profile)
    {
        var settings = profile?.Launch is { ValueKind: JsonValueKind.Object } launch
            ? launch.Deserialize<StoredLaunch>(Json)! : new StoredLaunch();
        ValidateLaunchUsername(settings.User);
        return settings;
    }

    private sealed record LaunchLibrary(Workspace Workspace, JsonObject Document, string Revision);

    private sealed record StoredLaunch(string? User = null, string? Mdb = null, string? Module = null,
        string? LicenseHost = null, int? LicensePort = null, string? UicDirectory = null, string? InstallationDirectory = null);
}
