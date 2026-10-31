using System.IO.Compression;
using System.Security.Cryptography;

namespace LaunchDesk.Windows;

/// <summary>只读枚举和内容检查。</summary>
/// <param name="privateInspectionDirectory">临时解包与内容检查使用的私有目录。</param>
/// <param name="sevenZipExecutable">处理 7z 文件的受信任辅助程序路径。</param>
public sealed class PackageReader(string privateInspectionDirectory, string sevenZipExecutable) : IPackageReader
{
    private const long MaxBytes = 20L * 1024 * 1024 * 1024;

    /// <inheritdoc />
    public ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (!Directory.Exists(query.Directory))
                return ValueTask.FromResult(Result<SourceCatalog>.Fail("SourceDirectoryMissing", "来源目录不存在。"));
            var candidates = Directory.EnumerateFileSystemEntries(query.Directory, query.Pattern).Take(10000)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    var kind = Directory.Exists(path) ? PackageKind.Folder :
                        info.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? PackageKind.Zip :
                        info.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ? PackageKind.SevenZip :
                        PackageKind.Files;
                    return new SourceCandidate(Path.GetFullPath(path), kind, info.LastWriteTimeUtc,
                        kind == PackageKind.Folder ? 0 : info.Length);
                }).OrderByDescending(c => c.ModifiedAtUtc).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
            return ValueTask.FromResult(Result<SourceCatalog>.Ok(new SourceCatalog(candidates)));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<SourceCatalog>(ex, "SourceUnavailable"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)
    {
        try
        {
            ImmutableArray<SourceFile> files;
            string? archiveHash = null;
            if (source.Kind is PackageKind.Zip or PackageKind.SevenZip)
            {
                AtomicFiles.ProtectDirectory(privateInspectionDirectory);
                var inspection = Path.Combine(privateInspectionDirectory, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(inspection);
                var archive = Path.Combine(inspection, source.Kind == PackageKind.Zip ? "source.zip" : "source.7z");
                using (var input = SourceStream(source.Path))
                using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, ct);
                    output.Flush(true);
                }

                archiveHash = Hash(archive);
                var content = Path.Combine(inspection, "content");
                Directory.CreateDirectory(content);
                if (source.Kind == PackageKind.Zip) await ExtractZip(archive, content, ct);
                else await ExtractSevenZip(archive, content, ct);
                files = Enumerate(content, ct);
            }
            else if (source.Kind == PackageKind.Folder)
            {
                files = Enumerate(source.Path, ct);
            }
            else
            {
                var mappings = source.Files.IsDefaultOrEmpty
                    ? new[] { new ToolFile(source.Path, Path.GetFileName(source.Path)) }
                    : source.Files.ToArray();
                files = mappings.Select(file =>
                {
                    ct.ThrowIfCancellationRequested();
                    SafeRelative(file.RelativePath);
                    using var input = SourceStream(file.SourcePath);
                    return new SourceFile(Path.GetFullPath(file.SourcePath), file.RelativePath.Replace('/', '\\'),
                        Convert.ToHexStringLower(SHA256.HashData(input)), input.Length);
                }).ToImmutableArray();
            }

            if (files.IsEmpty || files.Length > 10000 || files.Sum(f => f.Bytes) > MaxBytes ||
                files.Select(f => f.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
                return Result<SourceSnapshot>.Fail("InvalidSource", "来源为空、过大或包含重复目标映射。");
            return Result<SourceSnapshot>.Ok(new SourceSnapshot(source, Digest(files, archiveHash), files,
                ArchiveSha256: archiveHash));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException)
        {
            return AtomicFiles.Failure<SourceSnapshot>(ex, "SourceInspectionFailed");
        }
    }

    internal static string Digest(IEnumerable<SourceFile> files, string? archiveHash)
    {
        return CanonicalDigest.Of(new
        {
            archiveHash,
            files = files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(f => new { f.RelativePath, f.Sha256, f.Bytes }).ToArray()
        });
    }

    internal static string Hash(string path)
    {
        using var stream = SourceStream(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal static FileStream SourceStream(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Source links are not supported.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    internal static void SafeRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') ||
            path.IndexOfAny(['\0', '*', '?']) >= 0 || path.Split(['/', '\\'])
                .Any(p => p is "." or ".." or "" || p.EndsWith(' ') || p.EndsWith('.')))
            throw new InvalidDataException("Invalid package mapping.");
    }

    private static ImmutableArray<SourceFile> Enumerate(string root, CancellationToken ct)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Source root is a link.");
        var files = ImmutableArray.CreateBuilder<SourceFile>();
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                ct.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Source contains a link.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                var relative = Path.GetRelativePath(root, entry);
                SafeRelative(relative);
                using var input = SourceStream(entry);
                files.Add(new SourceFile(entry, relative, Convert.ToHexStringLower(SHA256.HashData(input)),
                    input.Length));
                if (files.Count > 10000 || files.Sum(f => f.Bytes) > MaxBytes)
                    throw new InvalidDataException("Source exceeds limits.");
            }

        return files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    private static async Task ExtractZip(string archive, string target, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(archive);
        long bytes = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('/', '\\').TrimEnd('\\');
            SafeRelative(name);
            if (!seen.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 ||
                (entry.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("Archive contains duplicate entries or links.");
            bytes = checked(bytes + entry.Length);
            if (seen.Count > 20000 || bytes > MaxBytes) throw new InvalidDataException("Archive exceeds limits.");
        }

        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var destination = WindowsPaths.CombineRelative(target, entry.FullName.Replace('/', '\\').TrimEnd('\\'));
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, ct);
            output.Flush(true);
        }
    }

    private async Task ExtractSevenZip(string archive, string target, CancellationToken ct)
    {
        if (!File.Exists(sevenZipExecutable)) throw new FileNotFoundException("Bundled 7zr.exe is missing.");
        var listing = await RunSevenZip(["l", "-slt", "-sccUTF-8", "--", archive], ct);
        var items = listing.Split("----------", StringSplitOptions.None);
        if (items.Length < 2) throw new InvalidDataException("Invalid 7z listing.");
        long bytes = 0;
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in items[^1].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                var name = line[7..].Replace('/', '\\');
                SafeRelative(name);
                if (!seen.Add(name)) throw new InvalidDataException("Duplicate archive mapping.");
                count++;
            }

            if (line.StartsWith("Size = ", StringComparison.Ordinal) && long.TryParse(line[7..], out var size))
                bytes = checked(bytes + size);
            if (line.StartsWith("Symbolic Link = ", StringComparison.Ordinal) ||
                line.StartsWith("Hard Link = ", StringComparison.Ordinal) ||
                (line.StartsWith("Attributes = ", StringComparison.Ordinal) &&
                 line.Contains("l", StringComparison.Ordinal)))
                throw new InvalidDataException("Archive links are unsupported.");
        }

        if (count == 0 || count > 20000 || bytes > MaxBytes) throw new InvalidDataException("Archive exceeds limits.");
        await RunSevenZip(["x", "-y", "-sccUTF-8", "-o" + target, "--", archive], ct);
    }

    private async Task<string> RunSevenZip(string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo
        {
            FileName = sevenZipExecutable, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Cannot start the archive helper.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(ct);
            var error = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var result = await output;
            await error;
            if (process.ExitCode != 0) throw new InvalidDataException("Archive helper rejected the package.");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            throw;
        }
    }
}