using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core30.PmlTwin;

/// <summary>Classifies installations using product labels and local launch-file evidence.</summary>
internal static partial class AvevaInstallationEvidence
{
    private static readonly HashSet<string> KnownFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "mon.exe", "des.exe", "design.exe", "pdms.exe", "e3d.exe", "marine.exe", "marodes.exe", "marhdes.exe",
        "pdms.bat", "marine.bat", "launch.init", "evars.bat"
    };

    public static bool IsEvidenceFile(string name) => KnownFiles.Contains(name);

    public static AvevaInstallation? Identify(string directory, IReadOnlyDictionary<string, string> files, ScanRoot origin)
    {
        var monitor = ReadVendorBinary(FirstFile(files, "mon.exe"));
        var design = FindVendorDesign(files);
        var product = RecognizeInstallation(directory, files, monitor, design, origin);
        return product is { } family ? Describe(directory, files, origin, family, monitor, design) : null;
    }

    private static AvevaProduct? RecognizeInstallation(string directory, IReadOnlyDictionary<string, string> files,
        VendorBinary? monitor, VendorBinary? design, ScanRoot origin)
    {
        if (monitor is null && design is null)
            return null;
        var binary = design?.Product ?? monitor?.Product;
        if (binary is not null)
            return binary;
        if (files.ContainsKey("marine.bat") || files.ContainsKey("marodes.exe") || files.ContainsKey("marhdes.exe"))
            return AvevaProduct.AM;
        if (files.ContainsKey("pdms.bat"))
            return AvevaProduct.PDMS;
        return ProductFromDirectory(directory) ?? ProductAtRoot(directory, origin);
    }

    private static AvevaProduct? ProductAtRoot(string directory, ScanRoot origin) =>
        StringComparer.OrdinalIgnoreCase.Equals(directory, origin.Path) ? origin.Product : null;

    private static AvevaInstallation Describe(string directory, IReadOnlyDictionary<string, string> files, ScanRoot origin,
        AvevaProduct product, VendorBinary? monitor, VendorBinary? design)
    {
        var binary = design ?? monitor!;
        var (entry, found) = FindLaunchFiles(product, files, monitor?.Path, design?.Path);
        var release = ReleaseName(directory, origin, binary.Version);
        var evidence = files.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new AvevaInstallation(product, release, binary.Version, directory, entry!, found, binary.Vendor,
            evidence, origin.Source);
    }

    private static (string? Entry, bool Found) FindLaunchFiles(AvevaProduct product, IReadOnlyDictionary<string, string> files,
        string? monitor, string? design) =>
        product switch
        {
            AvevaProduct.E3D => (monitor ?? design, monitor is not null && design is not null && files.ContainsKey("launch.init")),
            AvevaProduct.AM => (FirstFile(files, "marine.bat") ?? monitor ?? design,
                monitor is not null && design is not null && (files.ContainsKey("marine.bat") || HasMarineStartup(files))),
            _ => (FirstFile(files, "pdms.bat") ?? monitor ?? design, HasPdmsStartup(files, monitor, design))
        };

    private static bool HasMarineStartup(IReadOnlyDictionary<string, string> files) =>
        files.ContainsKey("evars.bat") && (files.ContainsKey("marodes.exe") || files.ContainsKey("marhdes.exe"));

    private static bool HasPdmsStartup(IReadOnlyDictionary<string, string> files, string? monitor, string? design) =>
        design is not null && (Path.GetFileName(design).Equals("pdms.exe", StringComparison.OrdinalIgnoreCase)
            || monitor is not null && (files.ContainsKey("pdms.bat") || files.ContainsKey("evars.bat")));

    private static string? FirstFile(IReadOnlyDictionary<string, string> files, params string[] names) =>
        names.Select(name => files.GetValueOrDefault(name)).FirstOrDefault(path => path is not null);

    private static VendorBinary? FindVendorDesign(IReadOnlyDictionary<string, string> files) =>
        new[] { "des.exe", "design.exe", "marodes.exe", "marhdes.exe", "pdms.exe", "marine.exe", "e3d.exe" }
            .Select(name => ReadVendorBinary(files.GetValueOrDefault(name))).OfType<VendorBinary>().FirstOrDefault();

    public static AvevaProduct? ProductFromDirectory(string directory)
    {
        var folder = new DirectoryInfo(directory);
        return ProductFromLabel(folder.Name) ?? ProductFromLabel(folder.Parent?.Name);
    }

    public static AvevaProduct? ProductFromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;
        if (E3DName().IsMatch(label))
            return AvevaProduct.E3D;
        if (MarineName().IsMatch(label))
            return AvevaProduct.AM;
        return PdmsName().IsMatch(label) ? AvevaProduct.PDMS : null;
    }

    private static VendorBinary? ReadVendorBinary(string? executable)
    {
        if (executable is null)
            return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(executable);
            return IsAvevaVendor(info)
                ? new VendorBinary(executable, ProductFromLabel(info.ProductName), info.ProductVersion?.Trim() ?? "",
                    info.CompanyName?.Trim() ?? info.ProductName?.Trim() ?? "")
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    // Shared runtime names such as mon.exe are also used in test copies and unrelated software.
    public static bool IsAvevaVendor(FileVersionInfo? info) =>
        info?.CompanyName?.Contains("AVEVA", StringComparison.OrdinalIgnoreCase) == true
        || info?.CompanyName?.Contains("CADCentre", StringComparison.OrdinalIgnoreCase) == true
        || info?.ProductName?.StartsWith("AVEVA ", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record VendorBinary(string Path, AvevaProduct? Product, string Version, string Vendor);

    private static string ReleaseName(string directory, ScanRoot origin, string binaryVersion)
    {
        var match = ReleasePattern().Match(Path.GetFileName(directory));
        if (match.Success)
            return match.Groups["version"].Value.TrimEnd('.');
        return binaryVersion.Length > 0 ? binaryVersion : origin.Release ?? "";
    }

    [GeneratedRegex(@"(?:^|[^a-z])(?:everything\s*3d|e3d)(?:$|[^a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex E3DName();

    [GeneratedRegex(@"(?:^|[^a-z])(?:marine|hull\s*(?:and|&)\s*outfitting)(?:$|[^a-z])|^(?:aveva\s+)?am(?=$|[\s\d_.-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarineName();

    [GeneratedRegex(@"(?:^|[^a-z])pdms(?:$|[^a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdmsName();

    [GeneratedRegex(@"(?:everything\s*3d|e3d(?:\s+design)?|marine|pdms|oh)\s*[-_]?\s*(?<version>\d[\w.]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleasePattern();
}
