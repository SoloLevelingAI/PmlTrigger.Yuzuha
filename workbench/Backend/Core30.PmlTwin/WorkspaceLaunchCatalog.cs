using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static CoreData ReadLaunchCatalog(FeatureContext context, CoreData request)
    {
        var operation = request.Children().Single();
        var installation = ReadSelectedInstallation(context, operation);
        var (catalog, cache) = LaunchCatalogCache.Read(context, installation.Product, installation.Release,
            installation.Directory, (bool?)operation.Field("refresh") == true);
        var library = ReadLaunchLibrary(context.FilePath(Value(request, "file", "workspace.json")));
        var logins = SavedLaunchLogins(library, installation).Concat(catalog.Logins).ToLookup(login => login.Project, StringComparer.OrdinalIgnoreCase);
        var projects = catalog.Projects.Select(project => ProjectChoice(project, logins[project.Code]));
        return new CoreData("result", new CoreField("cache", cache), new CoreField("observedUtc", catalog.ObservedUtc.ToString("O")),
            new CoreField("installation", installation.Directory),
            InstalledLaunchModules.Read(installation.Product, installation.Directory).Select(module => module.ToData()),
            projects, SavedProjectChoices(logins, catalog.Projects));
    }

    private static IEnumerable<LaunchLoginSource> SavedLaunchLogins(LaunchLibrary library, LaunchInstallation installation)
    {
        foreach (var profile in OrderedLaunchProfiles(library, installation.Product))
        {
            var settings = ReadStoredLaunch(profile);
            var preferred = ProfileInstallation(profile, settings);
            if (preferred.Length == 0 || SameInstallationPath(preferred, installation.Directory))
                yield return new(profile.Project.ToUpperInvariant(), settings.User ?? "", settings.Mdb ?? "", profile.Name, "workspace-profile", 0);
        }
    }

    private static IEnumerable<CoreData> SavedProjectChoices(ILookup<string, LaunchLoginSource> logins, LaunchProjectSource[] projects)
    {
        var known = projects.Select(project => project.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return logins.Where(group => group.Key.Length != 0 && !known.Contains(group.Key) && group.Any(login => login.Kind == "workspace-profile"))
            .Select(group => ProjectChoice(new LaunchProjectSource(group.Key, "", "", "工作区配置库", false, false), group));
    }

    private static CoreData ProjectChoice(LaunchProjectSource project, IEnumerable<LaunchLoginSource> logins) =>
        new("project", new CoreField("code", project.Code), new CoreField("directory", project.Directory),
            new CoreField("systemDatabase", project.SystemDatabase), new CoreField("found", project.Found),
            new CoreField("registered", project.Registered), new CoreField("source", project.Source), logins
                .Where(login => login.User.Length != 0 || login.Mdb.Length != 0)
                .DistinctBy(login => (login.User.ToUpperInvariant(), login.Mdb.ToUpperInvariant(), login.Kind, login.Source))
                .Select(login => new CoreData("login", new CoreField("user", login.User), new CoreField("mdb", login.Mdb),
                    new CoreField("source", login.Source), new CoreField("kind", login.Kind), new CoreField("changedUtcTicks", login.ChangedUtc))));
}
