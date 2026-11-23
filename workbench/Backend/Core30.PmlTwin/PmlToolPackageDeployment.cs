using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed partial class PmlDeployment
{
    private const long SnapshotLimit = 80L * 1024 * 1024;

    private CoreData PreviewPackage(FeatureContext context, CoreData command) => StorePlan(context, PreparePackagePlan(context, command));

    private CoreData ScanPackages(FeatureContext context, CoreData command)
    {
        var folder = Attribute(command, "folder");
        var candidates = Directory.EnumerateFiles(context.FilePath(folder))
            .Where(IsArchiveCandidate).Order(StringComparer.OrdinalIgnoreCase).Take(33).ToArray();
        return new CoreData("result", new CoreField("mode", "scan"), new CoreField("folder", folder),
            new CoreField("count", Math.Min(candidates.Length, 32)), new CoreField("truncated", candidates.Length > 32),
            candidates.Take(32).Select(path => InspectCandidate(context, path)));
    }

    private bool IsArchiveCandidate(string path) =>
        FindArchiveSource(path) is not null || Path.GetExtension(path).Equals(".rar", StringComparison.OrdinalIgnoreCase);

    private IToolPackageSource? FindArchiveSource(string path) =>
        archives.Find(Path.GetExtension(path));

    private IToolPackageSource RequireArchiveSource(string path) =>
        FindArchiveSource(path) ?? throw new InvalidDataException("请选择 ZIP 或 7Z 压缩包。");

    private CoreData InspectCandidate(FeatureContext context, string path)
    {
        var relative = Path.GetRelativePath(context.Root, path).Replace('\\', '/');
        var source = FindArchiveSource(path);
        if (source is null)
            return Candidate(relative, "Unsupported");
        try
        {
            var package = source.Read(context.FilePath(relative), includePayload: false);
            var entry = Candidate(relative, "Ready");
            entry.Add(new CoreField("sha256", package.Sha256), new CoreField("files", package.Entries.Length),
                new CoreField("skippedIndexes", package.SkippedIndexes),
                package.Entries.Select(file => new CoreData("file",
                    new CoreField("path", file.Path), new CoreField("bytes", file.Length))));
            return entry;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            var entry = Candidate(relative, "Invalid");
            entry.Add(new CoreData("reason", error.Message));
            return entry;
        }
    }

    private static CoreData Candidate(string path, string status) =>
        new("package", new CoreField("path", path), new CoreField("status", status));

    private DeploymentPlan PreparePackagePlan(FeatureContext context, CoreData command)
    {
        var target = Attribute(command, "target");
        var source = Attribute(command, "package");
        var directory = RequireInstallDirectory(Attribute(command, "directory"));
        var install = OpenInstallCopy(context, target);
        var path = context.FilePath(source);
        var archive = RequireArchiveSource(path).Read(path, includePayload: true);
        var package = CapturePackage(source, directory, archive, relative => install.FilePath($"{directory}/{relative}"));
        var plan = new DeploymentPlan(target, "", [], null, Package: package);
        if (command.Children().SingleOrDefault() is { } evars)
        {
            var library = PackagePmlLibrary(directory, Attribute(evars, "library"), package.Files);
            var file = RequireEvars(Attribute(evars, "file"));
            var encoding = (string?)evars.Field("encoding") ?? "auto";
            plan = IncludeEvars(plan with { Library = library }, install, file, encoding);
        }
        return ValidatePreview(plan, install.FilePath);
    }

    private static PackageDeployment CapturePackage(string source, string directory, ToolPackage archive, Func<string, string> destination) =>
        new(source, archive.Sha256, directory, ReadPackageChanges(destination, archive), archive.SkippedIndexes);

    private static FileChange[] ReadPackageChanges(Func<string, string> destination, ToolPackage archive)
    {
        var files = new List<FileChange>(archive.Entries.Length);
        long bytes = 0;
        foreach (var entry in archive.Entries)
        {
            var target = destination(entry.Path);
            var baseline = ReadIfPresent(target, ZipToolPackages.FileLimit);
            bytes += entry.Length + (baseline?.LongLength ?? 0);
            RequireSnapshotBudget(bytes);
            files.Add(new FileChange(entry.Path, baseline, entry.Content!));
        }
        return files.ToArray();
    }

    private static string PackagePmlLibrary(string directory, string library, FileChange[] files)
    {
        var prefix = RequireLibrary(library);
        if (!files.Any(file => file.Path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("工具包内没有选定 PMLLIB 目录下的文件。");
        return RequireLibrary($"{directory}/{prefix}");
    }

    private static string RequireInstallDirectory(string value)
    {
        var directory = value.Replace('\\', '/');
        FeatureContext.ValidateRelativePath(directory);
        if (directory == ".")
            throw new InvalidDataException("请指定安装副本下的工具子目录。");
        return directory;
    }

    private static void RequirePackagePlan(PackageDeployment package)
    {
        if (package.Files is not { Length: >= 1 and <= ZipToolPackages.EntryLimit }
            || package.Files.Any(file => InvalidChange(file, ZipToolPackages.FileLimit)))
            throw new InvalidDataException("工具包计划缺少有效的文件副本。");
        RequireSnapshotBudget(package.Files.Sum(file => file.After.LongLength + (file.Before?.LongLength ?? 0)));
    }

    private static void RequireSnapshotBudget(long bytes)
    {
        if (bytes > SnapshotLimit)
            throw new InvalidDataException("目标和工具包快照合计超过 80 MiB。");
    }

    private static IEnumerable<ResolvedFile> ResolvePackageFiles(Func<string, string> destination, PackageDeployment package, bool local)
    {
        var prefix = local ? "" : RequireInstallDirectory(package.Directory) + "/";
        var resolve = local ? LocalPayloadDestination(destination, package.Path) : destination;
        return package.Files.Select(file => ResolveFile(resolve, prefix + file.Path, file, ZipToolPackages.FileLimit));
    }

    private CoreData ReadPackageStatus(FeatureContext context, CoreData command)
    {
        var plan = PreparePackagePlan(context, command);
        var package = plan.Package!;
        var state = package.Files.All(file => DescribeAction(file) == "Unchanged") ? "Installed"
            : package.Files.All(file => file.Before is null) ? "Missing" : "Differences";
        return new CoreData("result", new CoreField("mode", "status"), new CoreField("status", state),
            new CoreField("target", plan.Target), DescribePackage(package));
    }

    private static CoreData DescribePackage(PackageDeployment package) =>
        new("package", new CoreField("path", package.Path), new CoreField("sha256", package.Sha256),
            new CoreField("directory", package.Directory), new CoreField("skippedIndexes", package.SkippedIndexes),
            package.Files.Select(change => Describe(change, "file")));

    private sealed record PackageDeployment(string Path, string Sha256, string Directory, FileChange[] Files, int SkippedIndexes);
}
