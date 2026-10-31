using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>冻结的启动目标文件；内容摘要用于执行前复验。</summary>
public sealed record LaunchTargetFile(string Path, string Sha256);
/// <summary>不含密码和命令行的启动计划；摘要绑定修订、目标和有效期。</summary>
public sealed record LaunchWorkflowPlan(string OperationId, string ProfileId, string ExpectedRevision,
    LaunchTargetFile[] Files, DateTimeOffset ExpiresAt, string PlanDigest);
/// <summary>持久启动事实；入口进程和登录核验分别表示，未知状态禁止重放。</summary>
public sealed record LaunchReceipt(string OperationId, string PlanDigest, string AttemptId, string? RequestId,
    string State, string Phase, string Code, string Message, DateTimeOffset UpdatedAt,
    int? ProcessId = null, long? ProcessStartUtcTicks = null, string ProcessObservation = "not-observed",
    string HostVerification = "unverified", string[]? RecoveryActions = null,
    string? EntryExecutable = null, LaunchChildEvidence[]? ChildCandidates = null);
/// <summary>启动协调契约的 Native AOT JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LaunchWorkflowPlan))]
[JsonSerializable(typeof(LaunchReceipt))]
public partial class LaunchWorkflowJsonContext : JsonSerializerContext;

/// <summary>复用配置与启动后端的薄协调层，提供跨进程租约、先受理后执行及持久观察。</summary>
public sealed class LaunchWorkflowService
{
    private readonly LaunchProfileService profiles;
    private readonly string root;
    private readonly Func<LaunchProfile, string, int> start;
    private readonly Func<LaunchProfile, CancellationToken, Task<LaunchPrecheckResult>> preflight;
    private readonly TimeProvider clock;
    private readonly Action<LaunchReceipt>? beforeReceiptWrite;
    private readonly Func<int, LaunchChildEvidence[]> children;

    /// <summary>注入现有配置服务和唯一进程边界；测试不需要启动真实产品。</summary>
    public LaunchWorkflowService(LaunchProfileService? profiles = null, string? receiptRoot = null,
        Func<LaunchProfile, string, int>? startProcess = null,
        Func<LaunchProfile, CancellationToken, Task<LaunchPrecheckResult>>? licensePreflight = null,
        TimeProvider? clock = null, IProductModuleCatalog? modules = null, Action<LaunchReceipt>? beforeReceiptWrite = null,
        Func<int, LaunchChildEvidence[]>? childSnapshot = null)
    {
        this.profiles = profiles ?? new(modules: modules);
        root = Path.GetFullPath(receiptRoot ?? Path.Combine(WorkspaceSettings.StateDirectory, "launch-operations"));
        start = startProcess ?? ((profile, password) => LaunchService.Start(profile, password, modules));
        preflight = licensePreflight ?? ((profile, token) => LaunchLicensePreflight.CheckAsync(profile, token: token));
        this.clock = clock ?? TimeProvider.System;
        this.beforeReceiptWrite = beforeReceiptWrite;
        children = childSnapshot ?? LaunchChildObservation.Snapshot;
    }

    /// <summary>只读冻结入口、显式初始化文件及按既有 INIT/BAT 规则选择的 EVARS，并缓存非秘密计划；不改变配置或厂商文件，不读取密码或受理执行。</summary>
    public LaunchWorkflowPlan Plan(string profileId, string expectedRevision, string operationId)
    {
        ValidateId(operationId);
        Bound(profileId, 128); Bound(expectedRevision, 256);
        var snapshot = profiles.ReadProfile(profileId);
        if (snapshot.Revision != expectedRevision) throw new ConfigurationConflictException(snapshot.Revision);
        Validate(snapshot.Profile);
        var files = Targets(snapshot.Profile);
        var plan = new LaunchWorkflowPlan(operationId, profileId, expectedRevision, files, clock.GetUtcNow().AddMinutes(10), "");
        plan = plan with { PlanDigest = Digest(plan) };
        var directory = Path.Combine(root, "plans");
        Directory.CreateDirectory(directory);
        using var cacheLease = new FileStream(Path.Combine(root, "plans.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var cachedFiles = Directory.EnumerateFiles(directory, "*.json").Take(257).ToArray();
        foreach (var old in cachedFiles.Where(file => File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddMinutes(-15))) File.Delete(old);
        if (Directory.EnumerateFiles(directory, "*.json").Take(256).Count() >= 256)
            throw new InvalidDataException("PLAN_CACHE_FULL：计划缓存已达 256 项，请等待旧计划过期后重新预览。");
        var path = Path.Combine(directory, plan.PlanDigest + ".json");
        if (!File.Exists(path))
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(JsonSerializer.SerializeToUtf8Bytes(plan, LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan)); stream.Flush(true); }
        return plan;
    }

    /// <summary>持久受理后调用唯一启动边界；密码只在本次内存调用中使用，重复编号不再次执行。</summary>
    public async Task<LaunchReceipt> ExecuteAsync(LaunchWorkflowPlan plan, string password, string? requestId,
        bool allowLaunch, CancellationToken token = default)
    {
        ValidateId(plan.OperationId);
        Bound(plan.ProfileId, 128); Bound(plan.ExpectedRevision, 256);
        if (requestId is not null) Bound(requestId, 128);
        if (password.Length > 4096 || password.Any(char.IsControl)) throw new ArgumentException("密码长度或字符无效。");
        if (plan.PlanDigest is null || plan.PlanDigest.Length != 64 || plan.Files is null || plan.Files.Length is < 2 or > 3 ||
            plan.Files.Any(file => file is null || file.Path is null || file.Path.Length > 4096 || file.Sha256 is null || file.Sha256.Length != 64))
            throw new ArgumentException("计划字段长度或目标数量无效。");
        if (!allowLaunch) throw new UnauthorizedAccessException("启动需要显式 allow-launch 授权。");
        if (plan.PlanDigest != Digest(plan)) throw new InvalidDataException("PLAN_DIGEST_INVALID：计划摘要无效，请重新生成计划。");
        Directory.CreateDirectory(root);
        if (!File.Exists(Path.Combine(root, plan.OperationId + ".json")) && !File.Exists(Path.Combine(root, "plans", plan.PlanDigest + ".json")))
            throw new InvalidDataException("PLAN_NOT_FOUND：本机冻结计划不存在，请重新生成。");
        using var lease = Acquire(plan.OperationId);
        var existing = Read(plan.OperationId);
        if (existing is not null)
        {
            if (existing.PlanDigest != plan.PlanDigest) throw new InvalidDataException("OPERATION_CONFLICT：相同操作编号绑定了不同计划。");
            return Recover(existing);
        }
        var cachedPath = Path.Combine(root, "plans", plan.PlanDigest + ".json");
        if (!File.Exists(cachedPath)) throw new InvalidDataException("PLAN_NOT_FOUND：本机冻结计划不存在，请重新生成。");
        var cached = JsonSerializer.Deserialize(File.ReadAllBytes(cachedPath), LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan);
        if (cached is null || cached.PlanDigest != plan.PlanDigest || Digest(cached) != plan.PlanDigest)
            throw new InvalidDataException("PLAN_CACHE_INVALID：本机冻结计划不可核对，请重新生成。");
        if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidDataException("PLAN_EXPIRED：计划已过期，请重新生成。");
        var profile = Revalidate(plan);
        var check = await preflight(profile, token).ConfigureAwait(false);
        if (!check.Allowed) throw new InvalidDataException(check.Code + "：" + check.Message);
        token.ThrowIfCancellationRequested();
        profile = Revalidate(plan);
        var receipt = new LaunchReceipt(plan.OperationId, plan.PlanDigest, Guid.NewGuid().ToString("N"), requestId,
            "accepted", "admitted", "LAUNCH_ADMITTED", "已持久受理；尚未取得入口进程回执。", clock.GetUtcNow(), EntryExecutable: profile.Executable);
        Save(receipt); // 此写入失败时，绝不能触达外部启动边界。
        try
        {
            var pid = start(profile, password);
            long? ticks = null;
            try { using var process = Process.GetProcessById(pid); ticks = process.StartTime.ToUniversalTime().Ticks; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            receipt = receipt with { State = "success", Phase = "entry-process-created", Code = "ENTRY_PROCESS_CREATED",
                Message = "入口进程已创建；工程登录及 Host 身份尚未核验。", UpdatedAt = clock.GetUtcNow(), ProcessId = pid, ProcessStartUtcTicks = ticks };
            TrySaveUnknown(ref receipt);
            return receipt;
        }
        catch (Exception)
        {
            // 外部边界可能已生效；不记录可能包含密码的异常文字，不推定失败或补偿。
            receipt = receipt with { State = "unknown", Phase = "launch-boundary", Code = "LAUNCH_OUTCOME_UNKNOWN",
                Message = "启动边界未返回可靠回执，请按原编号观察；禁止自动重放。", UpdatedAt = clock.GetUtcNow(), RecoveryActions = ["launch.observe"] };
            TrySaveUnknown(ref receipt);
            return receipt;
        }
    }

    /// <summary>核对持久事实及 PID、创建时刻；进程存在不作为登录或 Host 成功证据。</summary>
    public LaunchReceipt Observe(string operationId)
    {
        ValidateId(operationId);
        if (!File.Exists(Path.Combine(root, operationId + ".json"))) throw new KeyNotFoundException("操作编号不存在。");
        using var lease = Acquire(operationId);
        var receipt = Recover(Read(operationId) ?? throw new KeyNotFoundException("操作编号不存在。"));
        if (receipt.ProcessId is not { } pid) return receipt;
        var observation = "identity-unverified";
        try
        {
            using var process = Process.GetProcessById(pid);
            if (receipt.ProcessStartUtcTicks is { } ticks)
                observation = process.StartTime.ToUniversalTime().Ticks == ticks ? "same-entry-process-present" : "pid-reused";
        }
        catch (ArgumentException) { observation = "entry-process-absent"; }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        LaunchChildEvidence[] candidates = [];
        var entryExecutable = receipt.EntryExecutable ?? CachedEntry(receipt);
        if (observation != "pid-reused" && receipt.ProcessStartUtcTicks is { } startTicks && entryExecutable is { } executable)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executable));
            candidates = children(pid).Where(child => child.ParentProcessId == pid && child.ProcessId > 0 && child.ProcessId != pid &&
                child.ProcessStartUtcTicks >= startTicks && child.ProcessStartUtcTicks - startTicks <= TimeSpan.FromMinutes(2).Ticks &&
                Path.IsPathFullyQualified(child.Executable) &&
                string.Equals(Path.GetDirectoryName(Path.GetFullPath(child.Executable)), directory, StringComparison.OrdinalIgnoreCase) &&
                new[] { "des.exe", "dra.exe", "draft.exe", "paragon.exe", "specon.exe", "lexicon.exe" }.Contains(Path.GetFileName(child.Executable), StringComparer.OrdinalIgnoreCase))
                .Take(16).ToArray();
        }
        var message = observation == "entry-process-absent"
            ? candidates.Length > 0 ? "入口已退出；观察到同安装模块候选，请核验工程会话。" : "入口已退出，模块交接待核验；不能据此判断工程已退出。"
            : receipt.Message;
        return receipt with { ProcessObservation = observation, UpdatedAt = clock.GetUtcNow(), ChildCandidates = candidates, Message = message, EntryExecutable = entryExecutable };
    }

    private string? CachedEntry(LaunchReceipt receipt)
    {
        if (receipt.PlanDigest.Length != 64 || receipt.PlanDigest.Any(c => !char.IsAsciiHexDigit(c))) return null;
        var path = Path.Combine(root, "plans", receipt.PlanDigest + ".json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16384) return null;
            var plan = JsonSerializer.Deserialize(File.ReadAllBytes(path), LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan);
            return plan is not null && plan.OperationId == receipt.OperationId && plan.PlanDigest == receipt.PlanDigest && Digest(plan) == receipt.PlanDigest
                && plan.Files is { Length: > 0 } ? plan.Files[0].Path : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private LaunchProfile Revalidate(LaunchWorkflowPlan plan)
    {
        var snapshot = profiles.ReadProfile(plan.ProfileId);
        if (snapshot.Revision != plan.ExpectedRevision) throw new ConfigurationConflictException(snapshot.Revision);
        Validate(snapshot.Profile);
        if (!Targets(snapshot.Profile).SequenceEqual(plan.Files)) throw new InvalidDataException("TARGET_CHANGED：目标文件已改变，请重新生成计划。");
        return snapshot.Profile;
    }
    private void Validate(LaunchProfile profile)
    {
        var validation = profiles.Validate(profile);
        if (!validation.IsValid) throw new LaunchProfileValidationException(validation);
    }
    private static LaunchTargetFile[] Targets(LaunchProfile profile)
    {
        var installation = Path.GetDirectoryName(Path.GetFullPath(profile.Executable))!;
        var selected = !string.IsNullOrWhiteSpace(profile.Initialization) &&
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(profile.Initialization)), installation, StringComparison.OrdinalIgnoreCase) &&
            new[] { "evars.init", "evar.init", "evars.bat", "evar.bat" }.Contains(Path.GetFileName(profile.Initialization), StringComparer.OrdinalIgnoreCase)
                ? profile.Initialization : null;
        string environment;
        try { environment = EvarsEditor.SelectFile(installation, profile.Product, selected); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new InvalidDataException("EVARS_SELECTION_FAILED：安装环境文件缺失、歧义或不可读，请重新发现并核对 INIT/BAT 选择。", error); }
        return new[] { profile.Executable, profile.Initialization, environment }
            .Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => { using var file = File.OpenRead(path); return new LaunchTargetFile(path, Convert.ToHexString(SHA256.HashData(file))); }).ToArray();
    }
    private static string Digest(LaunchWorkflowPlan plan) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(plan with { PlanDigest = "" }, LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan)));
    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("操作编号仅允许 1 到 128 位字母、数字、横线及下划线。");
    }
    private FileStream Acquire(string id) => new(Path.Combine(root, id + ".lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private LaunchReceipt? Read(string id) => File.Exists(Path.Combine(root, id + ".json"))
        ? JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(root, id + ".json")), LaunchWorkflowJsonContext.Default.LaunchReceipt) ?? throw new InvalidDataException("回执为空。") : null;
    private LaunchReceipt Recover(LaunchReceipt receipt)
    {
        if (receipt.State != "accepted") return receipt;
        receipt = receipt with { State = "unknown", Code = "ADMISSION_INTERRUPTED", Message = "受理后执行回执中断，请核对原操作，禁止重放。", RecoveryActions = ["launch.observe"], UpdatedAt = clock.GetUtcNow() };
        TrySaveUnknown(ref receipt);
        return receipt;
    }
    private static void Bound(string value, int limit)
    {
        if (string.IsNullOrEmpty(value) || value.Length > limit || value.Any(char.IsControl)) throw new ArgumentException("输入字段长度或字符无效。");
    }
    private void TrySaveUnknown(ref LaunchReceipt receipt)
    {
        try { Save(receipt); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            receipt = receipt with { State = "unknown", Code = "RECEIPT_PERSISTENCE_FAILED",
                Message = "动作可能已执行，但最终回执无法持久化；请保留原操作编号观察，禁止重放。", RecoveryActions = ["launch.observe"] };
        }
    }
    private void Save(LaunchReceipt receipt)
    {
        beforeReceiptWrite?.Invoke(receipt);
        var target = Path.Combine(root, receipt.OperationId + ".json");
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(JsonSerializer.SerializeToUtf8Bytes(receipt, LaunchWorkflowJsonContext.Default.LaunchReceipt)); stream.Flush(true); }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
