using System.Text.Json;
using System.IO;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Desktop;

/// <summary>准备任务的非秘密草稿；改变任一实际输入会撤销当前可执行预览。</summary>
/// <param name="PackageRoot">已安装应用或待验证载荷根。</param>
/// <param name="RuntimeRoot">独立运行根，reference模式使用载荷根。</param>
/// <param name="Mode">copy或reference。</param>
/// <param name="EnvironmentFile">明确选中的INIT/BAT。</param>
/// <param name="Profile">Host产品版本profile。</param>
/// <param name="MigrationFormat">只读迁移来源格式。</param>
/// <param name="MigrationSource">只读迁移文件。</param>
internal sealed record PreparationDraft(string PackageRoot = "", string RuntimeRoot = "", string Mode = "reference",
    string EnvironmentFile = "", string Profile = "", string MigrationFormat = "rewrite", string MigrationSource = "");

/// <summary>准备工作区的可测试状态；统一调用Manager执行器，导航和输入本身没有写入效果。</summary>
internal sealed class PreparationWorkbench
{
    /// <summary>统一操作执行边界，不在界面复制写入逻辑。</summary>
    private readonly Func<string, JsonElement, OperationContext, CancellationToken, Task<OperationResult>> invoke;
    /// <summary>Manager公共JSON字段约定。</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>当前未知结果唯一对应的恢复身份；无引用或多个未知时为空。</summary>
    private string? unresolvedRecovery;
    /// <summary>未决期间产生的错误回执，后续只读调用不会覆盖人工核查依据。</summary>
    private readonly List<OperationResult> unresolvedResults = [];
    /// <summary>当前草稿，不包含密码。</summary>
    internal PreparationDraft Draft { get; private set; } = new();
    /// <summary>人工明确选择的实际安装身份。</summary>
    internal ManagerInstallationIdentity? Installation { get; private set; }
    /// <summary>最近一次有界发现返回的全部候选；不自动选择第一项。</summary>
    internal ManagerInstallationCandidate[] Candidates { get; private set; } = [];
    /// <summary>最近成功静态核验的工具包根；输入变化后需重新核验。</summary>
    internal string? VerifiedPackageRoot { get; private set; }
    /// <summary>当前阅读阶段，不代表该阶段已完成。</summary>
    internal string Stage { get; private set; } = "install";
    /// <summary>当前阅读栏目，切换阶段后保留。</summary>
    internal string Reader { get; set; } = "summary";
    /// <summary>最近一次真正操作的原始回执。</summary>
    internal OperationResult? Result { get; private set; }
    /// <summary>需要人工核查的期间回执；只有精确恢复解除全部未知时才清除。</summary>
    internal IReadOnlyList<OperationResult> UnresolvedResults => unresolvedResults;
    /// <summary>已冻结、供本次明确应用的计划身份。</summary>
    internal ManagerPlanReference? Plan { get; private set; }
    /// <summary>最近一次应用返回的恢复回执，含结果未知的部分写入；改变草稿不会丢弃恢复身份。</summary>
    internal ManagerPlanReference? Receipt { get; private set; }
    /// <summary>当前预览对应的实际apply操作。</summary>
    internal string? ApplyOperation { get; private set; }
    /// <summary>最近回执对应的条件恢复操作。</summary>
    internal string? RestoreOperation { get; private set; }
    /// <summary>正在等待同一次调用时禁止修改输入、重发或再次写入。</summary>
    internal bool Busy { get; private set; }
    /// <summary>存在不确定或需恢复结果时必须先人工核查。</summary>
    internal bool Unresolved { get; private set; }
    /// <summary>仅在有当前冻结计划、无等待/未知结果时允许明确申请写入。</summary>
    internal bool CanApply => !Busy && !Unresolved && Plan is not null && ApplyOperation is not null;

    /// <summary>注入既有统一执行边界，便于在隔离夹具验证而不访问厂商环境。</summary>
    internal PreparationWorkbench(Func<string, JsonElement, OperationContext, CancellationToken, Task<OperationResult>>? call = null)
    {
        if (call is not null) invoke = call;
        else { var executor = ManagerOperationCatalog.Create(); invoke = executor.InvokeAsync; }
    }
    /// <summary>切换阅读位置；不会触发调用或改变计划。</summary>
    internal void SelectStage(string id) { if (!new[] { "install", "environment", "ownership", "preview", "apply", "recover", "launch", "verify" }.Contains(id)) throw new ArgumentException("未知准备阶段。"); Stage = id; }
    /// <summary>更新输入并失效旧预览，保留历史回执和恢复定位。</summary>
    internal void Change(PreparationDraft draft) { if (Busy) throw new InvalidOperationException("正在等待同一次操作，请勿修改输入。"); if (draft == Draft) return; Draft = draft; ClearPlan(); }
    /// <summary>切换预览范围时撤销旧计划，不改变草稿或恢复回执。</summary>
    internal void DiscardPreview() { if (Busy) throw new InvalidOperationException("操作尚未结束。");ClearPlan(); }
    /// <summary>人工选择发现结果，重新绑定产品/入口/环境，不修改厂商文件。</summary>
    internal void Select(ManagerInstallationCandidate candidate)
    {
        if (Busy) throw new InvalidOperationException("操作尚未结束。");
        if (!Candidates.Any(c => c.Identity == candidate.Identity)) throw new ArgumentException("候选不属于本次发现。");
        Installation = candidate.Identity;
        var profile = candidate.Identity.Product switch { "AM" => "AM", "PDMS" => "PDMS", "E3D" when candidate.Identity.ProductVersion.StartsWith("2.1", StringComparison.Ordinal) => "E3D2.1", "E3D" when candidate.Identity.ProductVersion.StartsWith("3.1", StringComparison.Ordinal) => "E3D3.1.6", _ => "" };
        Draft = Draft with { EnvironmentFile = candidate.Identity.EnvironmentFile ?? "", Profile = profile };
        ClearPlan();
    }
    /// <summary>只读核对应用清单和安装所有权，不将启动参数当执行授权。</summary>
    internal async Task<OperationResult> InspectAsync(CancellationToken token = default)
    { var result = await Call("setup.handoff.inspect", new { packageRoot = Draft.PackageRoot }, false, token);VerifiedPackageRoot = result.Success ? Draft.PackageRoot : null;return result; }
    /// <summary>有界读取人工给定的扫描根；新结果不自动选择或替换现有目标。</summary>
    internal async Task<OperationResult> DiscoverAsync(string[] roots, CancellationToken token = default)
    {
        var result = await Call("installation.discover", new { roots, maximumDepth = 5, maximumFiles = 80000, timeoutSeconds = 20 }, false, token);
        if (result.Success && result.Data is { } data) Candidates = data.Deserialize<ManagerInstallationReport>(Json)?.Candidates ?? [];
        return result;
    }
    /// <summary>通过既有后端冻结运行部署或单独EVARS编辑；预览不写业务目标。</summary>
    internal async Task<OperationResult> PreviewAsync(bool evarsOnly = false, CancellationToken token = default)
    {
        if (Installation is not { } installation) throw new InvalidOperationException("请先发现并明确选择一个产品环境。");
        if (Unresolved) throw new InvalidOperationException("上次写入结果待核查，不创建新的可执行计划。");
        var framework = ManagerHostProfiles.Framework(installation.Product, Draft.Profile);
        ClearPlan();
        var runtime = Draft.Mode == "reference" ? Draft.PackageRoot : Draft.RuntimeRoot;
        var input = evarsOnly ? JsonSerializer.SerializeToElement(new { packageRoot = Draft.PackageRoot, installationRoot = installation.Root, product = installation.Product, environmentFile = Draft.EnvironmentFile, runtimeRoot = runtime, profile = Draft.Profile, framework }) :
            JsonSerializer.SerializeToElement(new { packageRoot = Draft.PackageRoot, runtimeRoot = runtime, mode = Draft.Mode, installationRoot = installation.Root, product = installation.Product, environmentFile = Draft.EnvironmentFile, profile = Draft.Profile, framework, installationExecutable = installation.Executable, expectedInstallationId = installation.Id });
        var result = await Call(evarsOnly ? "evars.preview" : "deployment.preview", input, false, token);
        if (result.Success && result.Data is { } data) { Plan = data.Deserialize<ManagerPlanReference>(Json); ApplyOperation = evarsOnly ? "evars.apply" : "deployment.apply"; }
        return result;
    }
    /// <summary>只读核对旧安装器所有权，移交计划不写EVARS。</summary>
    internal async Task<OperationResult> PreviewOwnershipAsync(CancellationToken token = default)
    {
        if (Unresolved) throw new InvalidOperationException("上次结果待核查。");
        ClearPlan();var result = await Call("setup.legacy.preview", new { packageRoot = Draft.PackageRoot, environmentFile = Draft.EnvironmentFile }, false, token);
        if (result.Success && result.Data is { } data) { Plan = data.Deserialize<ManagerPlanReference>(Json); ApplyOperation = "setup.legacy.transfer"; }
        return result;
    }
    /// <summary>只读展示已有rewrite/Next/Ribbon文件，不激活配置或切换存储。</summary>
    internal Task<OperationResult> PreviewMigrationAsync(CancellationToken token = default) => Call("migration.preview", new { format = Draft.MigrationFormat, source = Draft.MigrationSource }, false, token);
    /// <summary>只提交人工确认的当前冻结引用；先清本窗口执行资格以防再次点击或失败后重放。</summary>
    internal async Task<OperationResult> ApplyAsync(bool approved, CancellationToken token = default)
    {
        if (!approved) throw new UnauthorizedAccessException("需要明确确认本次写入范围。");
        if (!CanApply) throw new InvalidOperationException("当前没有有效冻结计划，或上次结果仍待核查。");
        var plan = Plan!;var operation = ApplyOperation!;ClearPlan();
        var result = await Call(operation, new { planId = plan.PlanId, sha256 = plan.Sha256 }, true, token);
        if ((operation is "deployment.apply" or "evars.apply") && RecoveryReference(result.Data, operation == "evars.apply" ? "evars.receipt" : "runtime.receipt") is { } receipt)
        { Receipt = receipt; RestoreOperation = operation.Replace(".apply", ".restore", StringComparison.Ordinal);if (Unresolved) unresolvedRecovery = RecoveryKey(receipt); }
        return result;
    }
    /// <summary>明确确认后按原回执条件恢复；外部修改时由后端拒绝，不猜测可覆盖。</summary>
    internal async Task<OperationResult> RestoreAsync(bool approved, CancellationToken token = default)
    {
        if (!approved) throw new UnauthorizedAccessException("需要明确确认恢复目标。");
        if (Busy || Receipt is null || RestoreOperation is null) throw new InvalidOperationException("没有可核对的恢复回执。");
        var receipt = Receipt;var key = RecoveryKey(receipt);var wasUnresolved = Unresolved;var previousRecovery = unresolvedRecovery;
        var result = await Call(RestoreOperation, new { planId = receipt.PlanId, sha256 = receipt.Sha256 }, true, token);
        if (result.Success)
        { if (!Unresolved || unresolvedRecovery == key) { Unresolved = false;unresolvedRecovery = null;unresolvedResults.Clear(); } Receipt = null; RestoreOperation = null; }
        else if (Unresolved && NeedsRecovery(result))
        { unresolvedRecovery = !wasUnresolved || previousRecovery == key ? key : null; }
        return result;
    }
    /// <summary>仅接受同类操作返回的完整恢复引用，缺失或非引用摘要保留为人工核查。</summary>
    private static ManagerPlanReference? RecoveryReference(JsonElement? data, string kind)
    {
        if (data is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty("planId", out var id) || id.ValueKind != JsonValueKind.String || !Guid.TryParseExact(id.GetString(), "N", out _) || !value.TryGetProperty("sha256", out var hash) || hash.ValueKind != JsonValueKind.String || hash.GetString() is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit) || !value.TryGetProperty("kind", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != kind) return null;
        return value.Deserialize<ManagerPlanReference>(Json);
    }
    /// <summary>把未知结果精确绑定到本次恢复回执，而不是同名操作或历史回执。</summary>
    private static string RecoveryKey(ManagerPlanReference receipt) => receipt.Kind + "/" + receipt.PlanId + "/" + receipt.Sha256;
    /// <summary>部分恢复同样需要人工核查，不依赖传输层未知标记。</summary>
    private static bool NeedsRecovery(OperationResult result) => result.Error?.MayHaveExecuted == true || result.Error?.Code.Contains("RECOVERY_REQUIRED", StringComparison.Ordinal) == true;
    /// <summary>撤销当前执行资格，同时保留恢复回执。</summary>
    private void ClearPlan() { Plan = null; ApplyOperation = null; }
    /// <summary>在后台单次调用统一执行器，回到调用方上下文更新状态；写入中断保留为未知而非重发。</summary>
    private async Task<OperationResult> Call(string operation, object input, bool write, CancellationToken token)
    {
        if (Busy) throw new InvalidOperationException("同一次操作尚在等待，不会再次发送。");
        Busy = true;
        try
        {
            var json = input is JsonElement element ? element : JsonSerializer.SerializeToElement(input);
            Result = await Task.Run(() => invoke(operation, json, new(AllowWrite: write), token), CancellationToken.None);
            if (write && NeedsRecovery(Result)) { if (!Unresolved) unresolvedRecovery = null;Unresolved = true;unresolvedResults.Add(Result); }
            return Result;
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or InvalidOperationException)
        {
            if (write) { if (!Unresolved) unresolvedRecovery = null;Unresolved = true; }
            Result = OperationResult.Fail(operation, 7, write ? "PREPARATION_OUTCOME_UNKNOWN" : "PREPARATION_READ_FAILED", error.Message, mayHaveExecuted: write);
            if (write) unresolvedResults.Add(Result);
            return Result;
        }
        finally { Busy = false; }
    }
}
