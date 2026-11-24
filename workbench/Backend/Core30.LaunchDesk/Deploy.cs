using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private static CoreData RunF02(FeatureContext context, CoreData request)
    {
        var (source, baseline) = ReadDeploymentPaths(context, request);
        var plan = PlanDeployment(context, source, baseline);
        var folders = CreateDeploymentFolders(context);
        var evidence = ExecuteDeployment(plan, folders);
        return DeploymentReport(plan, folders, evidence);
    }

    private static (string Source, string Baseline) ReadDeploymentPaths(FeatureContext context, CoreData request)
    {
        var source = context.FilePath(Read(request, "source"));
        var baseline = context.FilePath(Read(request, "baseline"));
        var runParent = context.FilePath(Path.Combine("launchdesk-runs", "F02"));
        ValidateDeploymentFolder(source, runParent);
        ValidateDeploymentFolder(baseline, runParent);
        return (source, baseline);
    }

    private static void ValidateDeploymentFolder(string folder, string runParent)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"找不到部署输入目录 {folder}。");
        if (IsWithinFolder(folder, runParent) || IsWithinFolder(runParent, folder))
            throw new InvalidDataException("部署输入目录不能与本次运行记录目录重叠。");
    }

    private static bool IsWithinFolder(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static DeploymentPlan PlanDeployment(FeatureContext context, string source, string baseline)
    {
        var prior = DeploymentSnapshot(context, baseline);
        var incoming = DeploymentSnapshot(context, source);
        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var baselineByPath = prior.ToDictionary(file => file.RelativePath, comparison);
        var changes = incoming.Select(file => DescribeDeploymentChange(file, baselineByPath)).ToArray();
        return new DeploymentPlan(prior, changes);
    }

    private static DeploymentChange DescribeDeploymentChange(DeploymentFile file, Dictionary<string, DeploymentFile> baseline) =>
        baseline.TryGetValue(file.RelativePath, out var previous)
            ? new DeploymentChange(file, previous.Hash == file.Hash ? "Unchanged" : "Replace")
            : new DeploymentChange(file, "Add");

    private static DeploymentFolders CreateDeploymentFolders(FeatureContext context)
    {
        var root = CreateRun(context, "F02");
        var records = Directory.CreateDirectory(Path.Combine(root, "records"));
        return new DeploymentFolders(root, Path.Combine(root, "target"), Path.Combine(root, "baseline"),
            Path.Combine(root, "applied"), Path.Combine(records.FullName, "receipt.json"));
    }

    private static DeploymentEvidence ExecuteDeployment(DeploymentPlan plan, DeploymentFolders folders)
    {
        CopyDeploymentFiles(plan.Baseline, folders.Backup);
        CopyDeploymentFiles(plan.Baseline, folders.Target);
        WriteDeploymentReceipt(folders, "Prepared");
        var deployed = ApplyThenRestore(plan, folders);
        var restored = DeploymentSnapshot(new FeatureContext(folders.Root), folders.Target);
        ValidateRestored(plan.Baseline, restored);
        WriteDeploymentReceipt(folders, "RolledBack");
        return new DeploymentEvidence(deployed, restored);
    }

    private static DeploymentFile[] ApplyThenRestore(DeploymentPlan plan, DeploymentFolders folders)
    {
        try
        {
            CopyDeploymentFiles(plan.Changes.Where(change => change.Action != "Unchanged").Select(change => change.File),
                folders.Target, overwrite: true);
            var deployed = DeploymentSnapshot(new FeatureContext(folders.Root), folders.Target);
            WriteDeploymentReceipt(folders, "Success");
            return deployed;
        }
        finally
        {
            RestoreDeployment(folders);
            WriteDeploymentReceipt(folders, "Restored");
        }
    }

    private static void RestoreDeployment(DeploymentFolders folders)
    {
        Directory.Move(folders.Target, folders.Applied);
        try
        {
            Directory.Move(folders.Backup, folders.Target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Directory.Move(folders.Applied, folders.Target);
            throw;
        }
    }

    private static DeploymentFile[] DeploymentSnapshot(FeatureContext context, string root)
    {
        var files = new List<DeploymentFile>();
        foreach (var path in EnumerateDeploymentFiles(context, root))
        {
            var relative = Path.GetRelativePath(root, path);
            files.Add(new DeploymentFile(relative, path, FileHash(path)));
        }
        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    // Visit directories explicitly so reparse points are checked before descent.
    private static IEnumerable<string> EnumerateDeploymentFiles(FeatureContext context, string root)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var path = context.FilePath(Path.GetRelativePath(context.Root, entry));
                if (Directory.Exists(path))
                {
                    directories.Push(path);
                    continue;
                }
                yield return path;
            }
        }
    }

    private static void CopyDeploymentFiles(IEnumerable<DeploymentFile> files, string target, bool overwrite = false)
    {
        Directory.CreateDirectory(target);
        foreach (var file in files)
        {
            var destination = Path.Combine(target, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file.SourcePath, destination, overwrite);
            ValidateDeploymentHash(file, destination);
        }
    }

    private static void ValidateDeploymentHash(DeploymentFile expected, string copied)
    {
        if (expected.Hash != FileHash(copied))
            throw new IOException($"文件在预览后发生变化 {expected.RelativePath}。");
    }

    private static void ValidateRestored(DeploymentFile[] baseline, DeploymentFile[] restored)
    {
        if (!baseline.Select(FileState).SequenceEqual(restored.Select(FileState)))
            throw new IOException("回滚后的文件清单和内容与基线不符。");
    }

    private static (string Relative, string Hash) FileState(DeploymentFile file) => (file.RelativePath, file.Hash);

    private static void WriteDeploymentReceipt(DeploymentFolders folders, string status)
    {
        var receipt = new { Id = Path.GetFileName(folders.Root), Status = status, folders.Target, folders.Applied };
        var staged = folders.Receipt + ".pending";
        File.WriteAllText(staged, JsonSerializer.Serialize(receipt));
        File.Move(staged, folders.Receipt, overwrite: true);
    }

    private static CoreData DeploymentReport(DeploymentPlan plan, DeploymentFolders folders, DeploymentEvidence evidence) =>
        new("result", new CoreField("mode", "IsolatedFolder"),
            new CoreField("run", folders.Root), new CoreField("operation", Path.GetFileName(folders.Root)),
            new CoreData("preview", plan.Changes.Select(change => new CoreData("file",
                new CoreField("path", change.File.RelativePath), new CoreField("action", change.Action)))),
            new CoreData("deployed", new CoreField("status", "Success"), DeploymentFiles(evidence.Applied)),
            new CoreData("rolledBack", new CoreField("status", "RolledBack"), DeploymentFiles(evidence.Restored)));

    private static IEnumerable<CoreData> DeploymentFiles(IEnumerable<DeploymentFile> files) =>
        files.Select(file => new CoreData("file", new CoreField("path", file.RelativePath), new CoreField("sha256", file.Hash)));

    private sealed record DeploymentFile(string RelativePath, string SourcePath, string Hash);
    private sealed record DeploymentChange(DeploymentFile File, string Action);
    private sealed record DeploymentPlan(DeploymentFile[] Baseline, DeploymentChange[] Changes);
    private sealed record DeploymentFolders(string Root, string Target, string Backup, string Applied, string Receipt);
    private sealed record DeploymentEvidence(DeploymentFile[] Applied, DeploymentFile[] Restored);
}
