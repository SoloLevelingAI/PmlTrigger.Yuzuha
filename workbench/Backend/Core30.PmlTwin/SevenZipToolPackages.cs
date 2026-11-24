using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Core30.PmlTwin;

/// <summary>Decodes 7Z into the same bounded snapshots as ZIP, without writing archive entries directly to disk.</summary>
internal sealed class SevenZipToolPackages : IToolPackageSource
{
    public ToolPackage Read(string path, bool includePayload)
    {
        try
        {
            return ReadArchive(path, includePayload);
        }
        catch (SharpCompressException error)
        {
            throw new InvalidDataException("无法读取 7Z 压缩包，请检查格式及加密状态。", error);
        }
    }

    private static ToolPackage ReadArchive(string path, bool includePayload)
    {
        using var input = File.OpenRead(path);
        ZipToolPackages.RequireArchiveSize(input.Length);
        var digest = Convert.ToHexStringLower(SHA256.HashData(input));
        input.Position = 0;
        using var archive = SevenZipArchive.OpenArchive(input, new ReaderOptions());
        var files = ReadFiles(archive);
        var payload = files.Where(file => !ZipToolPackages.IsPmlIndex(file.Path)).ToArray();
        RequireDeployableFiles(payload);
        var entries = payload.Select(file => new ToolPackageEntry(file.Path, file.Entry.Size,
            includePayload ? ReadContent(file.Entry) : null)).ToArray();
        return new ToolPackage(digest, entries, files.Length - payload.Length);
    }

    private static void RequireDeployableFiles(ArchiveFile[] files)
    {
        if (files.Length == 0)
            throw new InvalidDataException("7Z 中没有可部署文件。");
    }

    private static ArchiveFile[] ReadFiles(IArchive archive)
    {
        var entries = archive.Entries.Take(ZipToolPackages.EntryLimit * 2 + 1).ToArray();
        if (entries.Length > ZipToolPackages.EntryLimit * 2)
            throw new InvalidDataException("压缩包的目录项过多。");
        var files = entries.Select(ReadEntry).OfType<ArchiveFile>().ToArray();
        if (files.Length is < 1 or > ZipToolPackages.EntryLimit ||
            files.Any(file => file.Entry.Size is < 0 or > ZipToolPackages.FileLimit) ||
            files.Sum(file => file.Entry.Size) > ZipToolPackages.PayloadLimit)
            throw new InvalidDataException("7Z 须包含 1–128 个文件，每个最多 32 MiB，总计最多 64 MiB。");
        ZipToolPackages.ValidateFilePaths(files.Select(file => file.Path));
        return files;
    }

    private static ArchiveFile? ReadEntry(IArchiveEntry entry)
    {
        if (entry.IsEncrypted || !string.IsNullOrEmpty(entry.LinkTarget))
            throw new InvalidDataException("7Z 须为未加密的普通文件和目录。");
        var key = entry.Key?.Replace('\\', '/') ?? throw new InvalidDataException("7Z 条目没有路径。");
        var path = entry.IsDirectory ? key.TrimEnd('/') : key;
        if (entry.IsDirectory && path == ".")
            return null;
        if (path.StartsWith("./", StringComparison.Ordinal))
            path = path[2..];
        ZipToolPackages.RequireEntry(path, entry.Attrib ?? 0, entry.IsDirectory);
        return entry.IsDirectory ? null : new ArchiveFile(path, entry);
    }

    private static byte[] ReadContent(IArchiveEntry entry)
    {
        using var input = entry.OpenEntryStream();
        var bytes = new byte[(int)entry.Size];
        input.ReadExactly(bytes);
        if (input.ReadByte() >= 0)
            throw new InvalidDataException("7Z 实际文件大小与目录记录不符。");
        return bytes;
    }

    private sealed record ArchiveFile(string Path, IArchiveEntry Entry);
}
