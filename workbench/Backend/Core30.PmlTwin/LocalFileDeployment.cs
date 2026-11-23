using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

internal sealed partial class PmlDeployment
{
    private CoreData PreviewLocalFiles(FeatureContext context, CoreData command) => StorePlan(context, PrepareLocalFilePlan(context, command));

    private static CoreData ReadLocalHistory(FeatureContext context, CoreData _)
    {
        var folder = context.FilePath("state/pml-deploy");
        if (!Directory.Exists(folder))
            return new CoreData("result");
        var plans = Directory.EnumerateDirectories(folder, "*", new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true
        }).Select(path => new DirectoryInfo(path)).OrderByDescending(item => item.LastWriteTimeUtc)
            .Take(40).Select(item => ReadLocalHistoryEntry(context, item.Name)).OfType<CoreData>();
        return new CoreData("result", plans);
    }

    private static CoreData? ReadLocalHistoryEntry(FeatureContext context, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _) || !File.Exists(JournalPath(context, id, "phase")))
            return null;
        try
        {
            var phasePath = JournalPath(context, id, "phase");
            var phase = ReadPhase(phasePath);
            if (phase is Phase.Prepared)
                return null;
            var summary = LoadLocalHistoryEntry(context, id);
            return summary is null ? null : new CoreData("deployment",
                new CoreField("id", id), new CoreField("planSha256", summary.Digest), new CoreField("status", phase),
                new CoreField("target", summary.Target), new CoreField("dateUtc", File.GetLastWriteTimeUtc(phasePath)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new CoreData("unavailable", new CoreField("id", id), new CoreField("reason", error.GetType().Name));
        }
    }

    private static void StoreLocalHistoryEntry(FeatureContext context, string id, string target, string digest) =>
        WriteAtomic(JournalPath(context, id, "local-history.json"), JsonSerializer.SerializeToUtf8Bytes(new LocalHistoryEntry(target, digest)));

    private static LocalHistoryEntry? LoadLocalHistoryEntry(FeatureContext context, string id)
    {
        var index = JournalPath(context, id, "local-history.json");
        if (File.Exists(index))
            return JsonSerializer.Deserialize<LocalHistoryEntry>(ReadBounded(index, 16 * 1024));
        var bytes = ReadBounded(JournalPath(context, id, "plan.json"), PlanLimit);
        using var legacy = JsonDocument.Parse(bytes);
        var root = legacy.RootElement;
        return root.TryGetProperty("LocalFiles", out var local) && local.ValueKind == JsonValueKind.True
            && root.TryGetProperty("Target", out var target) && target.GetString() is { } path
            ? new LocalHistoryEntry(path, Sha256(bytes)) : null;
    }

    private sealed record LocalHistoryEntry(string Target, string Digest);

    private DeploymentPlan PrepareLocalFilePlan(FeatureContext context, CoreData command)
    {
        var source = LocalSource.Resolve(context, Path.TrimEndingDirectorySeparator(Attribute(command, "source")));
        var target = ReadLocalTargetPath(context, Attribute(command, "target"));
        var destination = OpenLocalDeploymentTarget(context, target);
        var archive = ReadLocalSource(source, target);
        var package = CapturePackage(source, ".", archive, LocalPayloadDestination(destination, source));
        return ValidatePreview(new DeploymentPlan(target, "", [], null, Package: package, LocalFiles: true), destination);
    }

    private ToolPackage ReadLocalSource(string source, string target)
    {
        if (!Directory.Exists(source))
            return RequireArchiveSource(source).Read(source, includePayload: true);
        if (IsWithin(source, target))
            throw new InvalidDataException("请选择来源文件夹以外的目标目录。");
        return folders.Read(source, includePayload: true);
    }

    private static string ReadLocalTargetPath(FeatureContext context, string target)
    {
        var path = Path.IsPathFullyQualified(target) ? LocalSource.RequireAbsolutePath(target) : context.FilePath(target);
        return Path.TrimEndingDirectorySeparator(path);
    }

    private static Func<string, string> OpenLocalDeploymentTarget(FeatureContext context, string target)
    {
        var path = LocalSource.RequireAbsolutePath(target);
        RequireLocalTarget(context, path);
        var anchor = new FeatureContext(FindExistingDirectory(path));
        var prefix = Path.GetRelativePath(anchor.Root, path);
        return prefix == "." ? anchor.FilePath : file => anchor.FilePath(Path.Combine(prefix, file));
    }

    private static string FindExistingDirectory(string path)
    {
        while (!Directory.Exists(path))
        {
            if (File.Exists(path))
                throw new InvalidDataException($"目标目录位置是文件 {path}。");
            path = Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException("目标位置所在的磁盘不存在。");
        }
        return path;
    }

    private static void RequireLocalTarget(FeatureContext context, string target)
    {
        if (Path.GetDirectoryName(target) is null || target.Equals(context.Root, StringComparison.OrdinalIgnoreCase) ||
            IsWithin(Path.Combine(context.Root, "state"), target) || IsWithin(Path.Combine(context.Root, ".core30"), target))
            throw new InvalidDataException("请选择独立的本机部署目录。");
    }

    private static bool IsWithin(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return !Path.IsPathFullyQualified(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static Func<string, string> LocalPayloadDestination(Func<string, string> destination, string source) =>
        relative => ResolveLocalPayloadFile(destination, source, relative);

    private static string ResolveLocalPayloadFile(Func<string, string> destination, string source, string relative)
    {
        FeatureContext.ValidateRelativePath(relative);
        var top = relative.Split('/', '\\')[0];
        if (top.Equals(DeploymentLock, StringComparison.OrdinalIgnoreCase) ||
            top.Equals(PendingDeployment, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("来源包含部署日志使用的保留文件名。");
        var path = destination(relative);
        if (IsWithin(source, path))
            throw new InvalidDataException("部署文件会写回来源位置，请选择其他目标目录。");
        RequireFileDestination(path);
        return path;
    }

    private static void RequireFileDestination(string path)
    {
        if (Directory.Exists(path))
            throw new InvalidDataException($"目标文件位置已有目录 {path}。");
        _ = FindExistingDirectory(Path.GetDirectoryName(path)!);
    }
}
