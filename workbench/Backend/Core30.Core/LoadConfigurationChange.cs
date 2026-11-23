using System.Xml;
using System.Xml.Linq;

namespace Core30.Core;

public enum LoadConfigurationKind { ToolboxModule, AvevaAddin }
public enum LoadActivationBoundary { NextCoreRequest, NextAvevaModuleStart }

public sealed record LoadConfigurationRevision(string? BeforeSha256, string WrittenSha256);

/// <summary>A reviewed change to a load list. Previewing is a read-only operation.</summary>
public sealed class LoadConfigurationChange
{
    private const int Limit = 4 * 1024 * 1024;

    private readonly FeatureContext _scope;
    private readonly string _target;
    private readonly byte[]? _original;
    private readonly byte[]? _proposed;
    private readonly VerifiedSource? _source;

    internal LoadConfigurationChange(LoadConfigurationKind kind, string entry, bool enabledBefore, bool enable,
        FeatureContext scope, string target, byte[]? original, byte[]? proposed, VerifiedSource? source = null)
    {
        Kind = kind;
        Entry = entry;
        EnabledBefore = enabledBefore;
        Enable = enable;
        _scope = scope;
        _target = target;
        _original = original;
        _proposed = proposed;
        _source = source;
        TargetPath = scope.FilePath(target);
        BeforeSha256 = original is null ? null : FileContent.Sha256(original);
        ProposedSha256 = proposed is null ? null : FileContent.Sha256(proposed);
    }

    public LoadConfigurationKind Kind { get; }
    public string Entry { get; }
    public bool EnabledBefore { get; }
    public bool Enable { get; }
    public bool HasChanges => EnabledBefore != Enable;
    public LoadActivationBoundary Activation => Kind == LoadConfigurationKind.ToolboxModule
        ? LoadActivationBoundary.NextCoreRequest : LoadActivationBoundary.NextAvevaModuleStart;
    public string? SourcePath => _source?.Path;
    public string? SourceSha256 => _source?.Sha256;
    public string TargetPath { get; }
    public string? BeforeSha256 { get; }
    public string? ProposedSha256 { get; }

    public LoadConfigurationReceipt Apply()
    {
        if (!HasChanges)
        {
            RequireCurrent(_scope.FilePath(_target), BeforeSha256);
            return new(this, null);
        }
        using var gate = OpenWriteLock(_scope, _target);
        _source?.Verify();
        var target = _scope.FilePath(_target);
        RequireCurrent(target, BeforeSha256);
        var backup = _original is null ? null : BackupPath(_scope, _target, BeforeSha256!);
        if (backup is not null)
            AtomicFileWrite.EnsureSnapshot(backup, _original!, Read);
        if (_original is null)
            AtomicFileWrite.WriteNew(target, _proposed!);
        else
            AtomicFileWrite.Replace(target, _proposed!, BeforeSha256!, Read, preserveMetadata: true);
        RequireCurrent(target, ProposedSha256);
        return new(this, backup);
    }

    internal void Restore(LoadConfigurationRevision revision) => Restore(_scope, _target, revision);

    internal static void Restore(FeatureContext scope, string relative, LoadConfigurationRevision revision)
    {
        var written = NormalizeSha256(revision.WrittenSha256);
        var before = revision.BeforeSha256 is { } saved ? NormalizeSha256(saved) : null;
        using var gate = OpenWriteLock(scope, relative);
        var target = scope.FilePath(relative);
        RequireCurrent(target, written);
        if (before is null)
        {
            File.Delete(target);
        }
        else
        {
            var backup = Read(BackupPath(scope, relative, before));
            FileContent.RequireMatchingSha256(FileContent.Sha256(backup), before, "备份内容已变化。");
            AtomicFileWrite.Replace(target, backup, written, Read, preserveMetadata: true);
        }
        RequireCurrent(target, before);
    }

    internal static byte[] Read(string path) => FileContent.ReadBounded(path, Limit);

    internal static byte[]? ReadOptional(string path)
    {
        try { return Read(path); }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
    }

    internal static XDocument Parse(byte[] bytes, bool preserveWhitespace)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, XmlInput.CreateReaderSettings(Limit));
        return XDocument.Load(reader, LoadOptions.SetLineInfo |
            (preserveWhitespace ? LoadOptions.PreserveWhitespace : LoadOptions.None));
    }

    private static FileStream OpenWriteLock(FeatureContext scope, string target) =>
        AtomicFileWrite.Lock(scope.FilePath(target + ".core30.lock"));

    private static string BackupPath(FeatureContext scope, string target, string before) =>
        scope.FilePath($"{target}.core30-{before}.bak");

    private static string NormalizeSha256(string hash) =>
        hash.Length == 64 && hash.All(char.IsAsciiHexDigit) ? hash.ToLowerInvariant()
            : throw new InvalidDataException("回执须包含 64 位十六进制 SHA-256。");

    private static void RequireCurrent(string target, string? expected)
    {
        var bytes = ReadOptional(target);
        var actual = bytes is null ? null : FileContent.Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException("目标文件已变化。请重新查看当前配置。");
    }

    internal sealed record VerifiedSource(string Path, string Sha256, string? AssemblyPath = null)
    {
        public void Verify()
        {
            var context = LocalSource.OpenParent(Path);
            FileContent.RequireMatchingSha256(FileContent.Sha256(Read(context.FilePath(System.IO.Path.GetFileName(Path)))),
                Sha256, "模块清单已变化。请重新预览。");
            if (AssemblyPath is not null && !File.Exists(context.FilePath(System.IO.Path.GetFileName(AssemblyPath))))
                throw new InvalidDataException("找不到模块程序集。");
        }
    }
}

public sealed class LoadConfigurationReceipt
{
    internal LoadConfigurationReceipt(LoadConfigurationChange change, string? backupPath)
    {
        Change = change;
        BackupPath = backupPath;
    }

    public LoadConfigurationChange Change { get; }
    public string? BackupPath { get; }
    public LoadConfigurationRevision? Revision => Change.HasChanges
        ? new(Change.BeforeSha256, Change.ProposedSha256!) : null;

    public void Rollback()
    {
        if (Revision is { } revision)
            Change.Restore(revision);
    }
}
