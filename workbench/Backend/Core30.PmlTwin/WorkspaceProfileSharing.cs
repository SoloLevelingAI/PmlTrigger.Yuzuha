using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static readonly JsonSerializerOptions ShareJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.CjkUnifiedIdeographs),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>Projects selected configuration entries into a portable JSON catalog.</summary>
    private static CoreData ShareWorkspaceProfiles(FeatureContext context, CoreData request)
    {
        var command = request.Children().Single();
        var path = Value(command, "file");
        var bytes = ReadBounded(context.FilePath(path), 2 * 1024 * 1024);
        var workspace = ParseWorkspace(bytes);
        var selection = SelectSharedProfiles(workspace.Profiles, command);
        var title = Value(command, "name").Trim();
        var json = JsonSerializer.Serialize(new SharedCatalog(1, title, selection), ShareJson);
        return new CoreData("result", new CoreField("count", selection.Length), new CoreField("name", title),
            new CoreField("source", path), new CoreField("sourceSha256", Sha256(bytes)), new CoreData("json", json));
    }

    private static SharedProfile[] SelectSharedProfiles(Profile[] profiles, CoreData command)
    {
        var all = profiles.ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        var ids = command.Children().Select(element => Value(element, "id")).ToArray();
        RequireShareSelection(ids, all);
        var preferences = (bool?)command.Field("includePreferences") ?? false;
        var includeUser = (bool?)command.Field("includeUser") ?? false;
        return ids.Select(id => ExportProfile(all[id], preferences, includeUser)).ToArray();
    }

    private static void RequireShareSelection(string[] ids, IReadOnlyDictionary<string, Profile> profiles)
    {
        if (ids.Any(id => !profiles.ContainsKey(id)) || ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
            throw new InvalidDataException("分享条目须在当前配置库中存在且互不重复。");
    }

    private static SharedProfile ExportProfile(Profile profile, bool preferences, bool includeUser)
    {
        var launch = ReadPortableLaunch(profile);
        if (!includeUser)
            launch = launch with { User = null };
        return new SharedProfile(profile.Id, profile.Name, profile.Product, profile.Project,
            launch is { User: null, Mdb: null, Module: null } ? null : launch,
            preferences ? profile.IsFavorite : null, preferences ? profile.IsDefault : null);
    }

    private static PortableLaunch ReadPortableLaunch(Profile profile)
    {
        var user = ReadPortableLaunchField(profile.Launch, "user");
        return new PortableLaunch(ContainsLaunchCredentials(user) ? null : user,
            ReadPortableLaunchField(profile.Launch, "mdb"), ReadPortableLaunchField(profile.Launch, "module"));
    }

    private static string? ReadPortableLaunchField(JsonElement? launch, string name)
    {
        if (launch is not { ValueKind: JsonValueKind.Object } value)
            return null;
        var field = value.EnumerateObject().FirstOrDefault(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
        var text = field.ValueKind == JsonValueKind.String ? field.GetString()?.Trim() : null;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private sealed record SharedCatalog(int SchemaVersion, string ConfigurationRevision, SharedProfile[] Profiles);
    private sealed record SharedProfile(string Id, string Name, string Product, string Project, PortableLaunch? Launch, bool? IsFavorite, bool? IsDefault);
    private sealed record PortableLaunch(string? User, string? Mdb, string? Module);
}
