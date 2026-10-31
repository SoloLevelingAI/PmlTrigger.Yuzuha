namespace LaunchDesk.Windows;

/// <summary>持久化/迁移模块的副本存储。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
/// <param name="target">待检查的目标目录或依赖属性对象。</param>
/// <param name="guard">控制资料保护端口。</param>
public sealed class JsonMigrationStore(
    WorkspaceLocation location,
    JsonWorkspaceStore target,
    IControlDataGuard? guard = null) : IMigrationStore
{

    private string FileFor(OperationId id)
    {
        return Path.Combine(location.ControlRoot, "migrations", id.ToString(), "prepared.json");
    }

    private sealed record Prepared(OperationRequestSnapshot Request, JsonWorkspaceStore.Envelope Source);

    /// <inheritdoc />
    public ValueTask<Result<WorkspaceSnapshotInternal>> ReadSourceAsync(string sourcePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ValueTask.FromResult(Result<WorkspaceSnapshotInternal>.Fail("MigrationSourceMissing", "尚未选择迁移来源。"));
        try
        {
            return ValueTask.FromResult(
                Result<WorkspaceSnapshotInternal>.Ok(LegacyWorkspaceReader.Read(sourcePath, location.MachineId)
                    .Snapshot));
        }
        catch (NotSupportedException)
        {
            return ValueTask.FromResult(Result<WorkspaceSnapshotInternal>.Fail("MigrationNotRequired",
                "这是当前版本工作空间，请直接打开原文件或移动完整目录。"));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException or KeyNotFoundException)
        {
            return ValueTask.FromResult(AtomicFiles.Failure<WorkspaceSnapshotInternal>(ex, "MigrationInvalidSource"));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> PrepareAsync(OperationRequestSnapshot request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var plan = request.Request.Migration;
        if (request.Kind != OperationKind.MigrateConfiguration || plan is null)
            return ValueTask.FromResult(Result<Unit>.Fail("MigrationSourceMissing", "缺少冻结迁移计划。"));
        try
        {
            if (PackageReader.Hash(plan.SourcePath) != plan.SourceSha256)
                return ValueTask.FromResult(Result<Unit>.Fail("MigrationSourceChanged", "原配置在预览后已改变，请重新预览。"));
            var selectedSource = Path.GetFullPath(plan.SourcePath);
            var source = LegacyWorkspaceReader.Read(selectedSource, location.MachineId);
            if (guard is not null)
                foreach (var root in source.Snapshot.Tools.Select(t => t.LegacyRecordsDirectory)
                             .Where(path => !string.IsNullOrWhiteSpace(path)).Prepend(selectedSource))
                {
                    var protectedRoot = Path.GetFullPath(root!);
                    var registered = guard.RegisterWorkspace(new WorkspaceLocation(protectedRoot, protectedRoot,
                        protectedRoot, location.MachineId));
                    if (!registered.IsSuccess) return ValueTask.FromResult(Result<Unit>.Fail(registered.Error!));
                }

            WorkspaceSchema.EnsureValid(source.Snapshot);
            var preparedFile = FileFor(request.Id);
            AtomicFiles.ProtectDirectory(Path.GetDirectoryName(preparedFile)!);
            File.Copy(selectedSource, Path.Combine(Path.GetDirectoryName(preparedFile)!, "original-source.json"),
                false);
            AtomicFiles.Save(preparedFile, new Prepared(request, source));
            return ValueTask.FromResult(Result<Unit>.Ok(new Unit()));
        }
        catch (NotSupportedException)
        {
            return ValueTask.FromResult(Result<Unit>.Fail("MigrationNotRequired",
                "这是当前版本工作空间。请使用 --config 打开原文件；移动时复制完整目录以保留日志与备份。"));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException or KeyNotFoundException)
        {
            return ValueTask.FromResult(AtomicFiles.Failure<Unit>(ex, "MigrationPrepareFailed"));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> ValidateAsync(OperationId operationId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var prepared = AtomicFiles.Read<Prepared>(FileFor(operationId));
            WorkspaceSchema.EnsureValid(prepared.Source.Snapshot);
            return ValueTask.FromResult(
                PackageReader.Hash(prepared.Request.Request.Migration!.SourcePath) ==
                prepared.Request.Request.Migration.SourceSha256
                    ? Result<Unit>.Ok(new Unit())
                    : Result<Unit>.Fail("MigrationSourceChanged", "来源已改变；未启用迁移副本。"));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<Unit>(ex, "MigrationValidationFailed"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Revision>> ActivateAsync(OperationId operationId, Revision expected,
        CancellationToken ct)
    {
        var valid = await ValidateAsync(operationId, ct);
        if (!valid.IsSuccess) return Result<Revision>.Fail(valid.Error!);
        var prepared = AtomicFiles.Read<Prepared>(FileFor(operationId));
        return await target.ActivateMigrationAsync(prepared.Source, expected, ct);
    }

    /// <inheritdoc />
    public async ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
        var prepared = AtomicFiles.Read<Prepared>(FileFor(operationId));
        var current = await target.ReadAsync(ct);
        if (!current.IsSuccess) return Result<OperationFact>.Fail(current.Error!);
        var equal = current.Value.WorkspaceId == prepared.Source.Snapshot.WorkspaceId;
        return Result<OperationFact>.Ok(new OperationFact(equal ? RunStage.Completed : RunStage.NeedsVerification,
            equal ? "MigrationIdentityObserved" : "MigrationNeedsVerification",
            equal ? "当前工作空间使用迁移后的稳定身份；原文件仍保留。" : "尚不能确认迁移启用，请保留原文件和副本。"));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<OperationFact>(ex, "MigrationInspectionFailed");
        }
    }
}

/// <summary>读取明确旧版来源并冻结摘要和预期修订，返回不改现有数据的迁移预览。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
/// <param name="migration">迁移预览或副本存储端口。</param>
public sealed class MigrationPlanningService(WorkspaceLocation location, IMigrationStore? migration = null) : IMigrationPlanningService
{
    /// <inheritdoc />
    public async ValueTask<Result<MigrationPreview>> PreviewAsync(string sourcePath, Revision expected, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var before = PackageReader.Hash(sourcePath);
            var read = migration is null
                ? Result<WorkspaceSnapshotInternal>.Ok(LegacyWorkspaceReader.Read(sourcePath, location.MachineId).Snapshot)
                : await migration.ReadSourceAsync(sourcePath, ct);
            if (!read.IsSuccess) return Result<MigrationPreview>.Fail(read.Error!);
            if (before != PackageReader.Hash(sourcePath)) return Result<MigrationPreview>.Fail("MigrationSourceChanged", "读取过程中来源已改变，请重新预览。");
            return Result<MigrationPreview>.Ok(new MigrationPreview(
                new MigrationPlan(Path.GetFullPath(sourcePath), before, expected), read.Value.PublicProjection(),
                ["将在当前独立工作空间启用迁移副本；原配置不会被覆盖。", "保留稳定 ID、关联和原 Windows 凭据引用；旧更新记录路径保留，缺失记录会明确显示。"]));
        }
        catch (NotSupportedException)
        {
            return Result<MigrationPreview>.Fail("MigrationNotRequired",
                "这是当前版本工作空间。请使用 --config 打开原文件；移动时复制完整目录以保留日志与备份。");
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException or KeyNotFoundException)
        {
            return AtomicFiles.Failure<MigrationPreview>(ex, "MigrationInvalidSource");
        }
    }
}

internal static class LegacyWorkspaceReader
{
    internal static JsonWorkspaceStore.Envelope Read(string source, Guid machineId)
    {
        using var file = PackageReader.SourceStream(source);
        if (file.Length > 32 * 1024 * 1024) throw new InvalidDataException("Configuration exceeds limit.");
        using var document = JsonDocument.Parse(file);
        var root = document.RootElement;
        if (Get(root, "SchemaVersion").GetInt32() == 3)
            throw new NotSupportedException(
                "Current workspaces must be opened in place or copied as complete directories.");
        var applications = Array(root, "Applications").Select(a => new ApplicationDefinition(Id(a), Text(a, "Name"),
            Text(a, "Version"), Number(a, "Kind") == 0 ? EntryKind.Monitor : EntryKind.MarineBatch,
            Text(a, "ExecutablePath"), Text(a, "InitializationPath"), Text(a, "WorkingDirectory"),
            Text(a, "ProductCode", "E3D"), true, [], [])).ToImmutableArray();
        var resources = Array(root, "Licenses").Select(l =>
        {
            var id = Id(l);
            return new LicenseDefinition(id, Text(l, "Name"), new ResourceDescriptor(id, new Revision(1), machineId,
                Text(l, "Host", "localhost"), Number(l, "RmsPort", 4545), Flag(l, "RequireRules", true),
                Number(l, "RulesPort", 4546), Number(l, "RmsCheck") == 1 ? Text(l, "LocalServiceName") : null,
                Text(l, "VmPath") is { Length: > 0 } vm ? vm : null, Flag(l, "Enabled", true),
                new ProbePolicy(PollSeconds: Math.Clamp(Number(l, "PollSeconds", 5), 1, 3600))));
        }).ToImmutableArray();
        var tools = Array(root, "Tools").Select(t => new ToolDefinition(Id(t), Text(t, "Name"), Text(t, "Group"),
            Text(t, "TargetDirectory"),
            new PackageSource(
                Number(t, "SourceKind") switch
                {
                    1 => PackageKind.Folder, 2 => PackageKind.Files,
                    _ => Path.GetExtension(Text(t, "SourcePath")).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                        ? PackageKind.Zip
                        : PackageKind.SevenZip
                }, Text(t, "SourcePath"),
                Array(t, "Files").Select(f => new ToolFile(Text(f, "SourcePath"), Text(f, "RelativePath")))
                    .ToImmutableArray()), Text(t, "ArchiveSearchDirectory"),
            Array(t, "Variables").Select(v =>
                    new ToolVariable(Id(v), Text(v, "Name"), Text(v, "Value"), (VariableScope)Number(v, "Scope")))
                .ToImmutableArray(), Flag(t, "FlattenRoot", true), Flag(t, "KeepBackup", true),
            Text(t, "RecordsDirectory"))).ToImmutableArray();
        var profiles = Array(root, "Profiles").Select(p =>
        {
            var id = Id(p);
            var key = Text(p, "CredentialKey");
            return new ProfileConfiguration(id, new ProfileDraft(id, Text(p, "Name"), Guid.Parse(Text(p, "AppId")),
                    Text(p, "VersionGroup"), Text(p, "Project"), Text(p, "Username"), Text(p, "Mdb"), Text(p, "Module"),
                    Guid.TryParse(Text(p, "LicenseId"), out var license) ? license : null,
                    Array(p, "ToolIds").Select(v => Guid.Parse(v.GetString()!)).ToImmutableArray()),
                key.Length == 0 ? null : new CredentialRef("LaunchDesk/Profile/" + key));
        }).ToImmutableArray();
        var workspaceId =
            new Guid(Convert.FromHexString(CanonicalDigest.Text(Path.GetFullPath(source).ToUpperInvariant())[..32]));
        var snapshot =
            new WorkspaceSnapshotInternal(workspaceId, new Revision(0), applications, profiles, resources, tools, []);
        WorkspaceSchema.EnsureValid(snapshot);
        return new JsonWorkspaceStore.Envelope(3, snapshot, ImmutableDictionary<string, CommitReceipt>.Empty);
    }

    private static JsonElement Get(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        return default;
    }

    private static IEnumerable<JsonElement> Array(JsonElement element, string name)
    {
        return Get(element, name).ValueKind == JsonValueKind.Array ? Get(element, name).EnumerateArray() : [];
    }

    private static Guid Id(JsonElement element)
    {
        return Guid.Parse(Text(element, "Id"));
    }

    private static string Text(JsonElement element, string name, string fallback = "")
    {
        return Get(element, name).ValueKind == JsonValueKind.String
            ? Get(element, name).GetString() ?? fallback
            : fallback;
    }

    private static int Number(JsonElement element, string name, int fallback = 0)
    {
        return Get(element, name).ValueKind == JsonValueKind.Number && Get(element, name).TryGetInt32(out var number)
            ? number
            : fallback;
    }

    private static bool Flag(JsonElement element, string name, bool fallback)
    {
        return Get(element, name).ValueKind is JsonValueKind.True or JsonValueKind.False
            ? Get(element, name).GetBoolean()
            : fallback;
    }
}
