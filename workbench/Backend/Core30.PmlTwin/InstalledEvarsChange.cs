using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

/// <summary>Previews or atomically applies a bounded EVARS edit, with a backup of the installed file.</summary>
internal sealed record InstalledEvarsChange(string Path, byte[] Original, EvarsPatch Patch, string[] DirectoriesToCreate, string? Product)
{
    private const int Limit = 1024 * 1024;
    public string Before => Sha256(Original);
    public string After => Sha256(Patch.Bytes);

    public static string Open(FeatureContext workspace, string name)
    {
        var path = LocalSource.Resolve(workspace, name);
        if (!PathIsEvars(path))
            throw new InvalidDataException("请选择本机 evars.init 或 evars.bat。");
        return path;
    }

    private static bool PathIsEvars(string path) => System.IO.Path.GetFileName(path).ToLowerInvariant() is "evars.init" or "evars.bat";

    public static PmlEnvironment Inspect(string path, string? product) =>
        PmlEnvironment.Parse(path, LocalText.Decode(ReadBounded(path, Limit)).Content, product);

    public static CoreData Preview(FeatureContext workspace, string path, string? library, string? uiDirectory, string? product,
        IEvarsIntegration integration)
    {
        var change = Prepare(workspace, path, library, uiDirectory, product, integration);
        return change.Receipt();
    }

    public static CoreData Apply(FeatureContext workspace, string path, string? library, string? uiDirectory, string? product,
        string expected, string proposed, IEvarsIntegration integration)
    {
        var parent = LocalSource.OpenParent(path);
        var name = System.IO.Path.GetFileName(path);
        using var gate = AcquireGate(parent, name);
        var change = Prepare(workspace, path, library, uiDirectory, product, integration);
        change.RequirePlan(expected, proposed);
        return change.Commit(parent, name);
    }

    private static FileStream AcquireGate(FeatureContext parent, string name) =>
        File.Open(parent.FilePath(name + ".core30.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

    private static InstalledEvarsChange Prepare(FeatureContext workspace, string path, string? library, string? uiDirectory,
        string? product, IEvarsIntegration integration)
    {
        var original = ReadBounded(path, Limit);
        var lib = ResolveDirectory(workspace, library);
        var ui = ResolveDirectory(workspace, uiDirectory);
        var patch = integration.PatchInstalled(path, original, lib, ui, product);
        var directories = new[] { CreateForAppend(library, lib, patch.LibraryAlreadyRegistered),
                CreateForAppend(uiDirectory, ui, patch.UiAlreadyRegistered) }
            .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new InstalledEvarsChange(path, original, patch, directories, product);
    }

    private static string? ResolveDirectory(FeatureContext workspace, string? name)
    {
        if (name is null)
            return null;
        var path = System.IO.Path.IsPathFullyQualified(name)
            ? LocalSource.RequireAbsolutePath(name)
            : workspace.FilePath(name);
        return System.IO.Path.TrimEndingDirectorySeparator(path);
    }

    private static string? CreateForAppend(string? input, string? path, bool? registered)
    {
        if (registered != false)
            return null;
        var directory = path!;
        ValidateBatchPath(directory);
        if (IsMissingWorkspaceDirectory(input!, directory))
            return directory;
        _ = new FeatureContext(directory);
        return null;
    }

    private static bool IsMissingWorkspaceDirectory(string name, string path) =>
        !System.IO.Path.IsPathFullyQualified(name) && !Directory.Exists(path) && !File.Exists(path);

    private static void ValidateBatchPath(string path)
    {
        if (path.IndexOfAny(['\r', '\n', '"', '%', '!', '&', '^', '|', '<', '>', ';']) >= 0)
            throw new InvalidDataException("PMLLIB 或界面目录路径含批处理控制字符。");
    }

    private void RequirePlan(string expected, string proposed)
    {
        if (!Before.Equals(expected, StringComparison.OrdinalIgnoreCase) || !After.Equals(proposed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("EVARS 文件或拟写结果已变化，请重新预览。");
    }

    private CoreData Commit(FeatureContext parent, string name)
    {
        if (Before == After)
            return Receipt();
        var backup = parent.FilePath($"{name}.core30-{Guid.NewGuid():N}.bak");
        ReplaceInstalledFile(parent, backup);
        var receipt = Receipt();
        receipt.Add(new CoreField("backup", backup), new CoreField("writtenSha256", VerifyWrittenFile()));
        return receipt;
    }

    private void ReplaceInstalledFile(FeatureContext parent, string backup)
    {
        var pending = parent.FilePath($".core30-evars-{Guid.NewGuid():N}.tmp");
        try
        {
            WritePending(pending, Patch.Bytes);
            RequireOriginal();
            foreach (var directory in DirectoriesToCreate)
                Directory.CreateDirectory(directory);
            File.Replace(pending, Path, backup);
        }
        finally
        {
            File.Delete(pending);
        }
    }

    private void RequireOriginal()
    {
        if (Sha256(ReadBounded(Path, Limit)) != Before)
            throw new InvalidDataException("EVARS 已被其他程序修改，请重新预览。");
    }

    private string VerifyWrittenFile()
    {
        var digest = Sha256(ReadBounded(Path, Limit));
        return digest == After ? digest : throw new InvalidDataException("EVARS 写入后摘要未通过核对。");
    }

    private static void WritePending(string path, byte[] bytes)
    {
        using var pending = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        pending.Write(bytes);
        pending.Flush(flushToDisk: true);
    }

    private CoreData Receipt()
    {
        var before = PmlEnvironment.Parse(Path, LocalText.Decode(Original).Content, Product);
        var after = PmlEnvironment.Parse(Path, LocalText.Decode(Patch.Bytes).Content, Product);
        return new CoreData("result", new CoreField("evars", Path), new CoreField("uiVariable", before.UiVariable),
            new CoreField("changed", Before != After), new CoreField("beforeSha256", Before),
            new CoreField("proposedSha256", After), new CoreField("legacyBlocksMasked", 0),
            new CoreField("legacyPathsMasked", 0), new CoreField("yuzuhaRegistered", Patch.YuzuhaRegistered),
            Patch.LibraryAlreadyRegistered is bool present ? new CoreField("libraryAlreadyRegistered", present) : null,
            Patch.UiAlreadyRegistered is bool uiPresent ? new CoreField("uiAlreadyRegistered", uiPresent) : null,
            Patch.AppendedSettings?.Select(setting => new CoreData("append", setting)),
            new CoreData("before", before.Describe()), new CoreData("after", after.Describe()));
    }
}
