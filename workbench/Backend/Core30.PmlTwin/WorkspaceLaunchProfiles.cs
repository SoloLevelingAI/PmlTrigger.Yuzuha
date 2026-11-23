using System.Xml;
using System.Xml.Linq;
using Core30.Core;
using Core30.Generated;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static readonly XNamespace VendorLaunchContract = "urn:core30:F01:v2";
    private static readonly IReadOnlyDictionary<string, Func<FeatureContext, string, CoreData, CoreData>> LaunchProfileOperations =
        new Dictionary<string, Func<FeatureContext, string, CoreData, CoreData>>(StringComparer.Ordinal)
        {
            ["list"] = ListLaunchProfiles, ["prepare"] = PrepareLaunchProfile,
            ["prepare-default"] = PrepareDefaultLaunch, ["save"] = SaveLaunchProfile, ["archive"] = ArchiveLaunchProfile
        };

    /// <summary>Connects a P02 installation and the workspace profile library to the F01 preview contract.</summary>
    private static CoreData ManageLaunchProfiles(FeatureContext context, CoreData request)
    {
        var file = Value(request, "file", "workspace.json");
        var operation = request.Children().Single();
        return LaunchProfileOperations[operation.Name.LocalName](context, file, operation);
    }

    private static CoreData PrepareLaunchProfile(FeatureContext context, string file, CoreData operation)
    {
        var library = ReadLaunchLibrary(context.FilePath(file), (string?)operation.Field("revision"));
        var installation = ReadSelectedInstallation(context, operation);
        var profile = FindLaunchProfile(library, (string?)operation.Field("profile"), installation.Product);
        return DescribeLaunchDraft(library, installation, profile);
    }

    private static CoreData DescribeLaunchDraft(LaunchLibrary library, LaunchInstallation installation, Profile? profile)
    {
        var settings = ReadStoredLaunch(profile);
        var draft = CreateVendorPreview(installation, profile, settings);
        return new CoreData("result", new CoreField("revision", library.Revision),
            new CoreField("product", installation.Product), new CoreField("release", installation.Release),
            new CoreField("installation", installation.Directory), new CoreField("profile", profile?.Id ?? ""),
            new CoreField("configured", HasLaunchSettings(profile, settings)),
            draft);
    }

    private static CoreData CreateVendorPreview(LaunchInstallation installation, Profile? profile, StoredLaunch settings) =>
        new(VendorLaunchContract + "request", new CoreField("mode", "preview"),
            new CoreField("executable", installation.EntryPoint),
            new CoreField("workingDirectory", LaunchWorkingDirectory(installation, profile, settings)),
            new VendorLaunchFields(Project: profile?.Project ?? "", User: settings.User ?? "", Mdb: settings.Mdb ?? "",
                Module: settings.Module ?? "", LicenseHost: settings.LicenseHost ?? "", LicensePort: settings.LicensePort ?? 4545).ToFields(),
            installation.CreateProtocol(settings.UicDirectory));

    private static string LaunchWorkingDirectory(LaunchInstallation installation, Profile? profile, StoredLaunch settings)
    {
        var original = profile?.WorkingDirectory;
        var preferred = ProfileInstallation(profile, settings);
        return string.IsNullOrWhiteSpace(original) || SameInstallationPath(original, preferred)
            ? installation.Directory : original;
    }

    private static CoreData ListLaunchProfiles(FeatureContext context, string file, CoreData operation)
    {
        var library = ReadLaunchLibrary(context.FilePath(file));
        var product = Value(operation, "product");
        return new CoreData("result", new CoreField("revision", library.Revision),
            OrderedLaunchProfiles(library, product).Select(DescribeLaunchProfile));
    }

    private static IEnumerable<Profile> OrderedLaunchProfiles(LaunchLibrary library, string product) =>
        library.Workspace.Profiles.Where(profile => IsLaunchProduct(profile.Product)
                && (product.Length == 0 || string.Equals(profile.Product, product, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(profile => profile.IsDefault).ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase);

    private static CoreData DescribeLaunchProfile(Profile profile)
    {
        var settings = ReadStoredLaunch(profile);
        return new CoreData("profile", new CoreField("id", profile.Id), new CoreField("name", profile.Name),
            new CoreField("product", profile.Product.ToUpperInvariant()), new CoreField("project", profile.Project),
            new CoreField("user", settings.User ?? ""), new CoreField("module", settings.Module ?? ""),
            new CoreField("mdb", settings.Mdb ?? ""),
            new CoreField("installation", ProfileInstallation(profile, settings)),
            new CoreField("configured", HasLaunchSettings(profile, settings)));
    }

    private static bool IsLaunchProduct(string product) =>
        string.Equals(product, "E3D", StringComparison.OrdinalIgnoreCase) || string.Equals(product, "AM", StringComparison.OrdinalIgnoreCase);

    private static Profile? FindLaunchProfile(LaunchLibrary library, string? id, string product)
    {
        if (id is null)
            return null;
        return library.Workspace.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.Product, product, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("所选配置不存在，或配置与安装产品不匹配。");
    }

    private static string ProfileInstallation(Profile? profile, StoredLaunch settings) =>
        settings.InstallationDirectory ?? Path.GetDirectoryName(profile?.Executable) ?? "";

    private static bool SameInstallationPath(string left, string right) => LocalSource.SamePath(left, right);

    private static LaunchInstallation ReadSelectedInstallation(FeatureContext context, CoreData operation)
    {
        var receipt = ReadScanReceipt(context, operation);
        var selected = FindInstallationInReceipt(receipt, operation);
        return LaunchInstallation.FromScan(selected);
    }

    private static CoreData FindInstallationInReceipt(CoreData receipt, CoreData operation)
    {
        var directory = Value(operation, "installation");
        var product = Value(operation, "product");
        return InstallationRows(receipt)
            .FirstOrDefault(item => Value(item, "product") == product && SameInstallationPath(Value(item, "directory"), directory))
            ?? throw new InvalidDataException("本次扫描回执未包含所选安装。");
    }

    private static IEnumerable<CoreData> InstallationRows(CoreData receipt) =>
        receipt.Child("result")!.Child("installations")!.Children("installation");

    private static CoreData ReadScanReceipt(FeatureContext context, CoreData operation)
    {
        var receipt = context.Input?.InstallationScan;
        if (receipt is null)
            return ReadEnvironmentReceipt(context.FilePath(Value(operation, "scan")));
        RequireEnvironmentReceipt(receipt);
        return receipt;
    }

    private static CoreData ReadEnvironmentReceipt(string path)
    {
        using var reader = XmlReader.Create(path, XmlInput.CreateReaderSettings(8 * 1024 * 1024));
        var receipt = CoreXml.Read(XElement.Load(reader));
        RequireEnvironmentReceipt(receipt);
        return receipt;
    }

    private static void RequireEnvironmentReceipt(CoreData receipt)
    {
        if (receipt.Name != "run" || Value(receipt, "feature") != "P02" || Value(receipt, "version") != "v2"
            || Value(receipt, "status") != "ok" || receipt.Child("result")?.Child("installations") is null)
            throw new InvalidDataException("需要成功的 P02 v2 本机环境扫描回执。");
    }

    private sealed record LaunchInstallation(string Product, string Release, string Directory, string EntryPoint)
    {
        public static LaunchInstallation FromScan(CoreData row) => FromScanIfUsable(row)
            ?? throw new InvalidDataException("所选安装缺少当前 LaunchDesk 协议所需的启动文件。");

        public static LaunchInstallation? FromScanIfUsable(CoreData row)
        {
            var installation = new LaunchInstallation(Value(row, "product"), Value(row, "release"),
                Value(row, "directory"), Value(row, "entryPoint"));
            return installation.HasLaunchFiles(row) ? installation : null;
        }

        private bool HasLaunchFiles(CoreData row)
        {
            var monitor = Product == "E3D";
            var expected = monitor ? "mon.exe" : "marine.bat";
            return Product is "E3D" or "AM" && (bool?)row.Field("launchFilesFound") == true
                && Path.IsPathFullyQualified(Directory) && SameInstallationPath(EntryPoint, Path.Combine(Directory, expected))
                && (!monitor || row.Children("file").Any(file => file.Value.Equals("launch.init", StringComparison.OrdinalIgnoreCase)));
        }

        public CoreData CreateProtocol(string? uic) => Product == "AM"
            ? new CoreData(VendorLaunchContract + "marine")
            : new CoreData(VendorLaunchContract + "monitor", new CoreField("product", "E3D"),
                new CoreField("init", Path.Combine(Directory, "launch.init")),
                string.IsNullOrEmpty(uic) ? null : new CoreField("uicDirectory", uic));
    }
}
