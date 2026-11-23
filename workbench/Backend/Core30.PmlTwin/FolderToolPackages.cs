using System.Security.Cryptography;
using System.Text;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

/// <summary>Snapshots ordinary folder files with the same limits and layout as a ZIP tool package.</summary>
internal sealed class FolderToolPackages : IToolPackageSource
{
    public ToolPackage Read(string path, bool includePayload)
    {
        var folder = new FeatureContext(path);
        var inventory = ReadInventory(folder.Root);
        var files = inventory.Where(file => !ZipToolPackages.IsPmlIndex(file)).ToArray();
        RequireInventory(files, inventory.Length);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var entries = SnapshotFiles(folder, files, digest, includePayload);
        return new ToolPackage(Convert.ToHexStringLower(digest.GetHashAndReset()), entries, inventory.Length - files.Length);
    }

    private static string[] ReadInventory(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false
        }).Take(ZipToolPackages.EntryLimit * 2 + 1)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void RequireInventory(string[] files, int inventoryCount)
    {
        if (inventoryCount > ZipToolPackages.EntryLimit * 2 || files.Length is < 1 or > ZipToolPackages.EntryLimit)
            throw new InvalidDataException("来源文件夹须包含 1–128 个可部署文件。");
        ZipToolPackages.ValidateFilePaths(files);
    }

    private static ToolPackageEntry[] SnapshotFiles(FeatureContext folder, string[] files, IncrementalHash digest, bool includePayload)
    {
        var remaining = ZipToolPackages.PayloadLimit;
        var entries = new List<ToolPackageEntry>(files.Length);
        foreach (var file in files)
        {
            var content = ReadBounded(folder.FilePath(file), Math.Min(ZipToolPackages.FileLimit, remaining));
            remaining -= content.Length;
            digest.AppendData(Encoding.UTF8.GetBytes(file + "\0"));
            digest.AppendData(SHA256.HashData(content));
            entries.Add(new ToolPackageEntry(file, content.Length, includePayload ? content : null));
        }
        return entries.ToArray();
    }
}
