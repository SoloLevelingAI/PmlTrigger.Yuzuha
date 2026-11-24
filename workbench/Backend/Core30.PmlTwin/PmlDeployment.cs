using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

/// <summary>Freezes, applies, and restores deployment changes through one journal.</summary>
internal sealed partial class PmlDeployment(IEvarsIntegration evars, ImplementationCatalog<string, IToolPackageSource> archives,
    IToolPackageSource folders)
{
    private const int FileLimit = 512 * 1024;
    private const int PlanLimit = 112 * 1024 * 1024;
    private const string DeploymentLock = ".core30-pml-deploy.lock";
    private const string PendingDeployment = ".core30-pml-deploy.pending";

    private CoreData PreviewPml(FeatureContext context, CoreData command) => StorePlan(context, PreparePmlPlan(context, command));

    private static CoreData ApplyPlan(FeatureContext context, CoreData command) =>
        RunReviewedPlan(context, command, PlanAction.Apply);

    private static CoreData RollbackPlan(FeatureContext context, CoreData command) =>
        RunReviewedPlan(context, command, PlanAction.Rollback);

    private static CoreData InspectPlan(FeatureContext context, CoreData command) =>
        RunReviewedPlan(context, command, PlanAction.Inspect);

    private static CoreData StorePlan(FeatureContext context, DeploymentPlan plan)
    {
        var id = Guid.NewGuid().ToString("N");
        var path = JournalPath(context, id, "plan.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(plan);
        var digest = Sha256(bytes);
        WriteAtomic(path, bytes);
        if (plan.LocalFiles)
            StoreLocalHistoryEntry(context, id, plan.Target, digest);
        return Report(plan, id, digest, Phase.Prepared);
    }

    private DeploymentPlan PreparePmlPlan(FeatureContext context, CoreData command)
    {
        var target = Attribute(command, "target");
        var install = OpenInstallCopy(context, target);
        var library = RequireLibrary(Attribute(command, "library"));
        var evarsFile = RequireEvars(Attribute(command, "evars"));
        var encoding = (string?)command.Field("encoding") ?? "auto";
        var pml = command.Children().Select(file => PreparePml(context, install, library, file)).ToArray();
        var plan = new DeploymentPlan(target, library, pml, null);
        return ValidatePreview(IncludeEvars(plan, install, evarsFile, encoding), install.FilePath);
    }

    private static DeploymentPlan ValidatePreview(DeploymentPlan plan, Func<string, string> destination)
    {
        ResolveFiles(destination, plan);
        return plan;
    }

    private DeploymentPlan IncludeEvars(DeploymentPlan plan, FeatureContext install, string file, string encoding)
    {
        RequireBatchPath(install.Root);
        var path = install.FilePath(file);
        var original = ReadBounded(path, FileLimit + 1024);
        var patch = evars.Integrate(original, path, plan.Library, encoding);
        return plan with
        {
            Evars = new FileChange(file, original, patch.Bytes),
            YuzuhaRegistered = patch.YuzuhaRegistered,
            LibraryAlreadyRegistered = patch.LibraryAlreadyRegistered
        };
    }

    private static FileChange PreparePml(FeatureContext context, FeatureContext install, string library, CoreData file)
    {
        var source = context.FilePath(RequirePmlPath(Attribute(file, "source")));
        var relative = RequirePmlPath(Attribute(file, "path")).Replace('\\', '/');
        var path = install.FilePath($"{library}/{relative}");
        return new FileChange(relative, ReadIfPresent(path), ReadBounded(source, FileLimit));
    }

    private static CoreData RunReviewedPlan(FeatureContext context, CoreData command, PlanAction action)
    {
        var id = Attribute(command, "id");
        var digest = Attribute(command, "planSha256");
        var operation = OpenOperation(context, command.Name.NamespaceName, id, digest);
        var phase = action == PlanAction.Inspect
            ? ReadPhase(JournalPath(context, id, "phase"))
            : CommitPlan(context, operation, id, rollback: action == PlanAction.Rollback);
        return Report(operation.Plan, id, digest, phase);
    }

    private static Phase CommitPlan(FeatureContext context, DeploymentOperation operation, string id, bool rollback)
    {
        using var gate = OpenDeploymentGate(operation, rollback);
        var marker = operation.FilePath(PendingDeployment);
        RequirePendingOwner(marker, id);
        var phaseFile = JournalPath(context, id, "phase");
        var phase = ReadPhase(phaseFile);
        RequireNonDestructiveEvars(operation.Plan, phase, rollback);
        var transition = SelectTransition(phase, rollback);
        foreach (var file in operation.Files)
            ReadExpected(file, file.FullPath, transition.Expected);

        if (transition.InProgress is { } inProgress)
        {
            WriteAtomic(marker, Encoding.ASCII.GetBytes(id));
            WritePhase(phaseFile, inProgress);
            WriteChanges(operation, transition.Expected, rollback);
            WritePhase(phaseFile, transition.Completed);
        }
        File.Delete(marker);
        return transition.Completed;
    }

    private static FileStream OpenDeploymentGate(DeploymentOperation operation, bool rollback)
    {
        var path = operation.FilePath(DeploymentLock);
        if (operation.Plan.LocalFiles && !rollback)
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
    }

    private static void RequireNonDestructiveEvars(DeploymentPlan plan, Phase phase, bool rollback)
    {
        if (!rollback && (phase is Phase.Prepared or Phase.Applying) && plan.Evars is { } file &&
            !file.After.AsSpan().StartsWith(file.Before))
            throw new InvalidDataException("旧布设计划改动了 EVARS 原行。请重新预览。若已部分写入，请先回滚。");
    }

    private static void WriteChanges(DeploymentOperation operation, Expected expected, bool rollback)
    {
        var files = rollback ? operation.Files.Reverse() : operation.Files;
        foreach (var file in files)
        {
            var path = operation.FilePath(file.RelativePath);
            var desired = rollback ? file.Change.Before : file.Change.After;
            if (!SameBytes(ReadExpected(file, path, expected), desired))
                WriteAtomic(path, desired);
        }
    }

    private static byte[]? ReadExpected(ResolvedFile file, string path, Expected expected)
    {
        var current = ReadIfPresent(path, file.Limit);
        var before = expected is not Expected.After && SameBytes(current, file.Change.Before);
        var after = expected is not Expected.Before && SameBytes(current, file.Change.After);
        if (!before && !after)
            throw new IOException($"布设目标在计划后发生变化，未覆盖 {file.RelativePath}。");
        return current;
    }

    private static Transition SelectTransition(Phase phase, bool rollback) => (phase, rollback) switch
    {
        (Phase.Prepared, false) => new(Expected.Before, Phase.Applying, Phase.Applied),
        (Phase.Applying, false) => new(Expected.Either, Phase.Applying, Phase.Applied),
        (Phase.Applied, false) => new(Expected.After, null, Phase.Applied),
        (Phase.Applied, true) => new(Expected.After, Phase.RollingBack, Phase.RolledBack),
        (Phase.Applying or Phase.RollingBack, true) => new(Expected.Either, Phase.RollingBack, Phase.RolledBack),
        (Phase.RolledBack, true) => new(Expected.Before, null, Phase.RolledBack),
        _ => throw new InvalidDataException($"计划处于 {phase}，不接受该操作。")
    };

    private static FeatureContext OpenInstallCopy(FeatureContext context, string target)
    {
        var path = context.FilePath(target);
        var relative = Path.GetRelativePath(context.Root, path);
        var first = relative.Split(Path.DirectorySeparatorChar)[0];
        if (first is "." || first.Equals("state", StringComparison.OrdinalIgnoreCase)
            || first.Equals(".core30", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("布设目标须为工作区中独立的安装副本目录。");
        return new FeatureContext(path);
    }

    private static ResolvedFile[] ResolveFiles(Func<string, string> destination, DeploymentPlan plan)
    {
        RequirePlanShape(plan);
        var payload = ResolvePayload(destination, plan);
        var files = AddEvarsFile(destination, plan.Evars, payload).ToArray();
        ZipToolPackages.ValidateFilePaths(files.Select(file => file.RelativePath));
        return files;
    }

    private static IEnumerable<ResolvedFile> ResolvePayload(Func<string, string> destination, DeploymentPlan plan)
    {
        if (plan.Package is { } package)
            return ResolvePackageFiles(destination, package, plan.LocalFiles);
        var library = RequireLibrary(plan.Library);
        return plan.Pml.Select(change => ResolveFile(destination, $"{library}/{RequirePmlPath(change.Path)}", change));
    }

    private static IEnumerable<ResolvedFile> AddEvarsFile(Func<string, string> destination, FileChange? evars, IEnumerable<ResolvedFile> files) =>
        evars is null ? files : files.Append(ResolveFile(destination, RequireEvars(evars.Path), evars));

    private static ResolvedFile ResolveFile(Func<string, string> destination, string relative, FileChange change, int limit = FileLimit + 1024)
    {
        var normalized = relative.Replace('\\', '/');
        return new ResolvedFile(normalized, destination(normalized), change, limit);
    }

    private static void RequirePlanShape(DeploymentPlan plan)
    {
        if (plan.LocalFiles && (plan.Package is not { Directory: "." } || plan.Evars is not null))
            throw new InvalidDataException("文件部署计划格式无效。");
        if (plan.Package is { } package)
            RequirePackagePlan(package);
        else if (plan.Pml is not { Length: >= 1 and <= 16 } || plan.Evars is null
            || plan.Pml.Any(change => InvalidChange(change, FileLimit + 1024)))
            throw new InvalidDataException("布设计划缺少有效的文件副本。");
        if (plan.Evars is { } evars && (evars.Before is null || InvalidChange(evars, FileLimit + 1024)))
            throw new InvalidDataException("布设计划缺少有效的 EVARS 副本。");
    }

    private static bool InvalidChange(FileChange? change, int limit) =>
        change?.After is null || change.After.Length > limit || change.Before?.Length > limit;

    private static string RequireLibrary(string library)
    {
        var relative = library.Replace('\\', '/');
        FeatureContext.ValidateRelativePath(relative);
        if (relative == "." || relative.Length > 512)
            throw new InvalidDataException("PMLLIB 须为安装副本内的相对目录。");
        RequireBatchPath(relative);
        return relative;
    }

    private static void RequireBatchPath(string path)
    {
        if (path.IndexOfAny(['&', '|', '<', '>', '^', '%', '!', ';', '"', '\r', '\n']) >= 0)
            throw new InvalidDataException("PMLLIB 目录含有无法用于无引号批处理 SET 的字符。");
    }

    private static string RequireEvars(string path) => path is "evars.init" or "evars.bat" ? path
        : throw new InvalidDataException("请选择安装副本根目录中的 evars.init 或 evars.bat。");

    private static string RequirePmlPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".pmlfnc" or ".pmlobj" or ".pmlfrm" or ".pmlcmd" or ".pmlmac" ? path
            : throw new InvalidDataException("布设只接收 PML 源文件，PML 索引由 AVEVA 管理。");

    private static DeploymentOperation OpenOperation(FeatureContext context, string contract, string id, string digest)
    {
        var plan = LoadPlan(context, id, digest);
        var version = plan.LocalFiles ? "v6" : plan.Package is null ? "v2" : "v3";
        if (contract != $"urn:core30:P03:{version}")
            throw new InvalidDataException("布设操作的契约版本与预览计划不符。");
        var destination = plan.LocalFiles ? OpenLocalDeploymentTarget(context, plan.Target) : OpenInstallCopy(context, plan.Target).FilePath;
        return new DeploymentOperation(plan, destination, ResolveFiles(destination, plan));
    }

    private static DeploymentPlan LoadPlan(FeatureContext context, string id, string digest)
    {
        var bytes = ReadBounded(JournalPath(context, id, "plan.json"), PlanLimit);
        if (Sha256(bytes) != digest)
            throw new InvalidDataException("计划摘要不匹配，请使用预览回执中的 planSha256。");
        return JsonSerializer.Deserialize<DeploymentPlan>(bytes)
            ?? throw new InvalidDataException("布设计划格式无效。");
    }

    private static void RequirePendingOwner(string marker, string id)
    {
        var owner = File.Exists(marker) ? Encoding.ASCII.GetString(ReadBounded(marker, 32)) : null;
        if (owner is not null && owner != id)
            throw new IOException($"该安装副本存在待完成计划 {owner}，请先续跑或回滚。");
    }

    private static Phase ReadPhase(string path)
    {
        if (!File.Exists(path))
            return Phase.Prepared;
        var text = Encoding.ASCII.GetString(ReadBounded(path, 16));
        if (!Enum.TryParse<Phase>(text, out var phase) || phase.ToString() != text)
            throw new InvalidDataException("无法识别布设计划的恢复状态。");
        return phase;
    }

    private static void WritePhase(string path, Phase phase) =>
        WriteAtomic(path, Encoding.ASCII.GetBytes(phase.ToString()));

    private static string JournalPath(FeatureContext context, string id, string name) =>
        context.FilePath($"state/pml-deploy/{id}/{name}");

    private static byte[]? ReadIfPresent(string path, int limit = FileLimit + 1024)
    {
        try
        {
            return ReadBounded(path, limit);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void WriteAtomic(string path, byte[]? bytes)
    {
        if (bytes is null)
        {
            File.Delete(path);
            return;
        }
        var directory = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(directory.FullName, $".core30-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static CoreData Report(DeploymentPlan plan, string id, string digest, Phase phase) =>
        new("result", new CoreField("id", id), new CoreField("planSha256", digest),
            new CoreField("status", phase.ToString()), new CoreField("target", plan.Target),
            new CoreField("plan", $"state/pml-deploy/{id}/plan.json"),
            DescribePayload(plan), DescribeEvars(plan));

    private static CoreData DescribePayload(DeploymentPlan plan) => plan.Package is { } package
        ? DescribePackage(package)
        : new CoreData("pmllib", new CoreField("directory", plan.Library), plan.Pml.Select(change => Describe(change, "pml")));

    private static CoreData? DescribeEvars(DeploymentPlan plan)
    {
        if (plan.Evars is not { } file)
            return null;
        var node = Describe(file, "evars");
        node.Add(new CoreField("legacyBlocksMasked", plan.LegacyBlocks), new CoreField("legacyPathsMasked", plan.LegacyPaths),
            new CoreField("yuzuhaRegistered", plan.YuzuhaRegistered),
            plan.LibraryAlreadyRegistered is bool present ? new CoreField("libraryAlreadyRegistered", present) : null);
        return node;
    }

    private static CoreData Describe(FileChange change, string element) =>
        new(element, new CoreField("path", change.Path),
            new CoreField("action", DescribeAction(change)),
            new CoreField("beforeSha256", change.Before is null ? "" : Sha256(change.Before)),
            new CoreField("afterSha256", Sha256(change.After)));

    private static string DescribeAction(FileChange change) =>
        change.Before is null ? "Add" : SameBytes(change.Before, change.After) ? "Unchanged" : "Replace";

    private static bool SameBytes(byte[]? left, byte[]? right) =>
        left is null || right is null ? left is null && right is null : left.AsSpan().SequenceEqual(right);

    private static string Attribute(CoreData element, string name) => (string)element.Field(name)!;

    private enum PlanAction { Apply, Rollback, Inspect }
    private enum Phase { Prepared, Applying, Applied, RollingBack, RolledBack }
    private enum Expected { Before, After, Either }
    private sealed record Transition(Expected Expected, Phase? InProgress, Phase Completed);
    private sealed record DeploymentPlan(string Target, string Library, FileChange[] Pml, FileChange? Evars,
        int LegacyBlocks = 0, int LegacyPaths = 0, PackageDeployment? Package = null,
        bool YuzuhaRegistered = false, bool? LibraryAlreadyRegistered = null, bool LocalFiles = false);
    private sealed record FileChange(string Path, byte[]? Before, byte[] After);
    private sealed record ResolvedFile(string RelativePath, string FullPath, FileChange Change, int Limit);
    private sealed record DeploymentOperation(DeploymentPlan Plan, Func<string, string> FilePath, ResolvedFile[] Files);
}
