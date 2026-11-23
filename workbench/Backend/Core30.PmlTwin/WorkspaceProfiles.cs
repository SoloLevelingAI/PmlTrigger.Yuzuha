using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Reads the workspace profile library and applies the requested search and scope.</summary>
    private static CoreData ListWorkspaceProfiles(FeatureContext context, CoreData request)
    {
        var bytes = ReadBounded(context.FilePath(Value(request, "file")), 2 * 1024 * 1024);
        var workspace = ParseWorkspace(bytes);
        var profiles = SelectProfiles(workspace.Profiles, request);
        return new CoreData("result", new CoreField("sha256", Sha256(bytes)),
            new CoreField("revision", XmlText(workspace.ConfigurationRevision ?? "")),
            profiles.Select(DescribeWorkspaceProfile));
    }

    private static CoreData DescribeWorkspaceProfile(Profile profile)
    {
        var launch = ReadPortableLaunch(profile);
        return new CoreData("profile",
            new CoreField("id", XmlText(profile.Id)), new CoreField("name", XmlText(profile.Name)),
            new CoreField("product", XmlText(profile.Product)), new CoreField("project", XmlText(profile.Project)),
            launch.User is null ? null : new CoreField("user", XmlText(launch.User)),
            launch.Module is null ? null : new CoreField("module", XmlText(launch.Module)),
            new CoreField("favorite", profile.IsFavorite), new CoreField("default", profile.IsDefault));
    }

    private static Workspace ParseWorkspace(byte[] bytes)
    {
        var workspace = JsonSerializer.Deserialize<Workspace>(StripUtf8Bom(bytes), Json);
        if (workspace is not { SchemaVersion: 1, Profiles: not null })
            throw new InvalidDataException("Expected a version 1 workspace configuration.");
        if (workspace.Profiles.Any(profile => profile is null || string.IsNullOrWhiteSpace(profile.Id)
            || string.IsNullOrWhiteSpace(profile.Name) || profile.Product is null || profile.Project is null)
            || workspace.Profiles.Select(profile => profile.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != workspace.Profiles.Length)
            throw new InvalidDataException("Workspace contains invalid or repeated profile identities.");
        return workspace;
    }

    // Match the original LaunchProfileCatalog ordering and filter dimensions.
    private static IEnumerable<Profile> SelectProfiles(Profile[] profiles, CoreData request)
    {
        var term = Value(request, "query").Trim();
        var product = Value(request, "product");
        var scope = Value(request, "scope", "all");
        var matching = profiles.Where(profile => MatchesProfile(profile, term, product, scope));
        return scope == "recent"
            ? matching.OrderByDescending(profile => profile.LastStartedUtc)
            : matching.OrderByDescending(profile => profile.IsDefault)
                .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private static bool MatchesProfile(Profile profile, string term, string product, string scope) =>
        (term.Length == 0 || new[] { profile.Name, profile.Project, profile.Executable, profile.WorkingDirectory }
            .Any(field => field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)) &&
        (product.Length == 0 || string.Equals(profile.Product, product, StringComparison.OrdinalIgnoreCase)) &&
        (scope != "favorites" || profile.IsFavorite) &&
        (scope != "recent" || profile.LastStartedUtc is not null);

    private sealed class Workspace
    {
        public int SchemaVersion { get; init; } = 1;
        public string ConfigurationRevision { get; init; } = "";
        public Profile[] Profiles { get; init; } = [];
    }

    private sealed class Profile
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Product { get; init; } = "";
        public string Project { get; init; } = "";
        public string? Executable { get; init; }
        public string? WorkingDirectory { get; init; }
        public bool IsFavorite { get; init; }
        public bool IsDefault { get; init; }
        public DateTimeOffset? LastStartedUtc { get; init; }
        public JsonElement? Launch { get; init; }
    }
}
