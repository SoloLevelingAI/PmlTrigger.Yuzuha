using System.IO.Compression;
using System.Security.Cryptography;
using Core30.Core;

namespace Core30.PmlTwin;

internal interface IToolPackageSource
{
    ToolPackage Read(string path, bool includePayload);
}

internal sealed record ToolPackage(string Sha256, ToolPackageEntry[] Entries, int SkippedIndexes);
internal sealed record ToolPackageEntry(string Path, long Length, byte[]? Content);

/// <summary>Reads a ZIP inventory or bounded file bytes without extracting archive paths to disk.</summary>
internal sealed class ZipToolPackages : IToolPackageSource
{
    internal const int FileLimit = 32 * 1024 * 1024;
    internal const int EntryLimit = 128;
    internal const int PayloadLimit = 64 * 1024 * 1024;

    public ToolPackage Read(string path, bool includePayload)
    {
        using var input = File.OpenRead(path);
        RequireArchiveSize(input.Length);
        var digest = Convert.ToHexStringLower(SHA256.HashData(input));
        input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var entries = ReadEntries(archive);
        var files = entries.Where(entry => !IsPmlIndex(entry.Path)).ToArray();
        RequirePayload(files);
        var contents = files.Select(entry => Describe(entry, includePayload)).ToArray();
        return new ToolPackage(digest, contents, entries.Length - files.Length);
    }

    private static ArchiveFile[] ReadEntries(ZipArchive archive)
    {
        if (archive.Entries.Count > EntryLimit * 2)
            throw new InvalidDataException("压缩包的目录项过多。");
        return archive.Entries.Select(ReadEntry).OfType<ArchiveFile>().ToArray();
    }

    private static ArchiveFile? ReadEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/');
        var directory = name.EndsWith('/');
        var path = directory ? name[..^1] : name;
        RequireEntry(path, entry.ExternalAttributes, directory);
        return directory ? null : new ArchiveFile(path, entry);
    }

    internal static void RequireEntry(string path, int attributes, bool directory)
    {
        FeatureContext.ValidateRelativePath(path);
        var type = (attributes >> 16) & 0xf000;
        if (path == "." || (attributes & (int)FileAttributes.ReparsePoint) != 0
            || type != 0 && type != (directory ? 0x4000 : 0x8000))
            throw new InvalidDataException("工具包只能包含普通相对文件和目录。");
    }

    internal static void RequireArchiveSize(long bytes)
    {
        if (bytes > PayloadLimit)
            throw new InvalidDataException("压缩包超过 64 MiB。");
    }

    private static void RequirePayload(ArchiveFile[] files)
    {
        if (files.Length is < 1 or > EntryLimit || files.Any(file => file.Entry.Length > FileLimit)
            || files.Sum(file => file.Entry.Length) > PayloadLimit)
            throw new InvalidDataException("ZIP 须包含 1–128 个文件，每个最多 32 MiB，总计最多 64 MiB。");
        ValidateFilePaths(files.Select(file => file.Path));
    }

    internal static void ValidateFilePaths(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
            if (!names.Add(path))
                throw new InvalidDataException($"部署有重复目标 {path}。");
        foreach (var path in names)
            for (var separator = path.LastIndexOf('/'); separator >= 0; separator = path.LastIndexOf('/', separator - 1))
                if (names.Contains(path[..separator]))
                    throw new InvalidDataException($"部署文件与父目录冲突 {path}。");
    }

    private static ToolPackageEntry Describe(ArchiveFile file, bool includePayload) =>
        new(file.Path, file.Entry.Length, includePayload ? ReadContent(file.Entry) : null);

    private static byte[] ReadContent(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        var bytes = new byte[(int)entry.Length];
        input.ReadExactly(bytes);
        if (input.ReadByte() >= 0)
            throw new InvalidDataException("ZIP 实际文件大小与目录记录不符。");
        return bytes;
    }

    internal static bool IsPmlIndex(string path) =>
        Path.GetFileName(path).Equals("pml.index", StringComparison.OrdinalIgnoreCase);

    private sealed record ArchiveFile(string Path, ZipArchiveEntry Entry);
}
