using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Chooses a launch-ready saved profile against a fresh P02 scan.</summary>
    private static CoreData PrepareDefaultLaunch(FeatureContext context, string file, CoreData operation)
    {
        var receipt = ReadScanReceipt(context, operation);
        var installations = OrderLaunchInstallations(receipt);
        var library = ReadLaunchLibrary(context.FilePath(file));
        var choice = SelectDefaultLaunch(library, installations, (string?)operation.Field("preferredProfile"));
        return DescribeDefaultLaunch(library, choice);
    }

    private static CoreData DescribeDefaultLaunch(LaunchLibrary library, LaunchChoice? choice)
    {
        if (choice is null)
            return new CoreData("result", new CoreField("configured", false));
        var result = DescribeLaunchDraft(library, choice.Installation, choice.Profile);
        var profiles = OrderedLaunchProfiles(library, choice.Installation.Product).Select(DescribeLaunchProfile);
        result.Add(new CoreData("profiles", profiles));
        return result;
    }

    private static LaunchInstallation[] OrderLaunchInstallations(CoreData receipt) =>
        InstallationRows(receipt).Select(LaunchInstallation.FromScanIfUsable).OfType<LaunchInstallation>()
            .OrderByDescending(installation => installation.Product == "E3D")
            .ThenByDescending(installation => ReadReleaseNumber(installation.Release))
            .ThenBy(installation => installation.Directory, StringComparer.OrdinalIgnoreCase).ToArray();

    private static Version ReadReleaseNumber(string release)
    {
        var number = new string(release.TakeWhile(character => char.IsAsciiDigit(character) || character == '.').ToArray());
        return Version.TryParse(number.TrimEnd('.'), out var version) ? version : new Version();
    }

    private static LaunchChoice? SelectDefaultLaunch(LaunchLibrary library, LaunchInstallation[] installations, string? preferredProfile)
    {
        var match = OrderedLaunchProfiles(library, "").OrderByDescending(profile =>
                string.Equals(profile.Id, preferredProfile, StringComparison.OrdinalIgnoreCase))
            .Select(profile => MatchSavedProfile(profile, installations))
            .FirstOrDefault(choice => choice is not null);
        return match ?? installations.Select(installation => new LaunchChoice(installation, null)).FirstOrDefault();
    }

    private static LaunchChoice? MatchSavedProfile(Profile profile, LaunchInstallation[] installations)
    {
        var settings = ReadStoredLaunch(profile);
        var preferred = ProfileInstallation(profile, settings);
        var installation = installations.FirstOrDefault(candidate => MatchesInstallation(candidate, profile, preferred));
        return installation is not null && HasLaunchSettings(profile, settings) ? new LaunchChoice(installation, profile) : null;
    }

    private static bool MatchesInstallation(LaunchInstallation candidate, Profile profile, string preferred) =>
        string.Equals(candidate.Product, profile.Product, StringComparison.OrdinalIgnoreCase)
        && (preferred.Length == 0 || SameInstallationPath(candidate.Directory, preferred));

    private static bool HasLaunchSettings(Profile? profile, StoredLaunch settings) =>
        HasLaunchMdb(settings.Mdb) && new[] { profile?.Project, settings.User, settings.Module }
            .All(value => !string.IsNullOrWhiteSpace(value));

    private static bool HasLaunchMdb(string? mdb) => !string.IsNullOrWhiteSpace(mdb?.Trim().TrimStart('/'));

    private sealed record LaunchChoice(LaunchInstallation Installation, Profile? Profile);
}
