using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Manager;
using Xunit;
using System.Security.Cryptography;

namespace YuzuhaToolkit.Tests;

/// <summary>准备工作区的执行门禁、真实临时文件恢复与安全文档入口验收。</summary>
public sealed class PreparationWorkbenchTests
{
    /// <summary>与公共Manager回执相同的JSON命名约定。</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>从不执行的合成安装身份。</summary>
    private static readonly ManagerInstallationIdentity Identity = new("installation-1", "C:/synthetic-product", "C:/synthetic-product/mon.exe", new('a', 64), "E3D", "2.1", "synthetic", "Monitor", "C:/synthetic-product/evars.bat", new('b', 64));
    /// <summary>生成不访问真实厂商环境的发现候选。</summary>
    private static ManagerInstallationCandidate Candidate() => new(Identity, new([], [], [], [], []));
    /// <summary>生成明确标记的隔离计划引用。</summary>
    private static OperationResult Reference(string operation, string kind) => OperationResult.Ok(operation, JsonSerializer.SerializeToElement(new ManagerPlanReference(new('1', 32), kind, new('c', 64), JsonSerializer.SerializeToElement(new { environmentFile = "C:/synthetic-product/evars.bat", changedFiles = 4 })), Json));
    /// <summary>通过实际模型发现结果明确选中隔离候选。</summary>
    private static async Task Select(PreparationWorkbench work)
    { await work.DiscoverAsync(["C:/synthetic-product"]);work.Select(work.Candidates[0]); }
    /// <summary>生成多个候选以验证不自动选择第一项。</summary>
    private static OperationResult Found(string operation) => OperationResult.Ok(operation, JsonSerializer.SerializeToElement(new ManagerInstallationReport(true, 1, [Candidate(), Candidate() with { Identity = Identity with { Id = "other" } }], []), Json));

    /// <summary>验证阅读和发现既不自动选择目标，也不授予写入。</summary>
    [Fact]
    public async Task DiscoveryAndReadingDoNotChooseAnInstallationOrGrantWrite()
    {
        var calls = new List<string>();var work = new PreparationWorkbench((id, input, context, token) => { calls.Add(id);Assert.False(context.AllowWrite);return Task.FromResult(Found(id)); });
        work.SelectStage("preview");work.Reader = "scripts";Assert.Empty(calls);
        await work.DiscoverAsync(["C:/synthetic-product"]);Assert.Equal(2, work.Candidates.Length);Assert.Null(work.Installation);Assert.False(work.CanApply);
        Assert.Throws<ArgumentException>(() => work.Select(Candidate() with { Identity = Identity with { Id = "unseen" } }));
        work.Select(work.Candidates[1]);Assert.Equal("other", work.Installation!.Id);Assert.Equal("scripts", work.Reader);Assert.Single(calls);
    }

    /// <summary>验证冻结预览绑定实际安装，输入变化只撤销执行资格。</summary>
    [Fact]
    public async Task FrozenPreviewBindsActualInstallationAndInputChangesInvalidateOnlyExecutablePlan()
    {
        JsonElement input = default;var work = new PreparationWorkbench((id, data, context, token) => { if (id == "installation.discover") return Task.FromResult(Found(id));input = data;Assert.False(context.AllowWrite);return Task.FromResult(Reference(id, "runtime.deployment")); });
        await Select(work);work.Change(work.Draft with { PackageRoot = "C:/application", Mode = "reference", RuntimeRoot = "C:/ignored" });
        await work.PreviewAsync();Assert.Equal("installation-1", input.GetProperty("expectedInstallationId").GetString());Assert.Equal("C:/application", input.GetProperty("runtimeRoot").GetString());Assert.Equal("net48", input.GetProperty("framework").GetString());Assert.True(work.CanApply);
        await work.PreviewAsync(true);Assert.Equal("C:/application", input.GetProperty("packageRoot").GetString());
        var plan = work.Plan;work.SelectStage("apply");work.Reader = "contract";work.Change(work.Draft);Assert.Same(plan, work.Plan);
        work.Change(work.Draft with { Profile = "3.1.6" });Assert.Null(work.Plan);Assert.False(work.CanApply);Assert.Equal("contract", work.Reader);
    }

    /// <summary>验证准备模型使用Manager同一产品映射，在错误Host组合进入后端前拒绝冻结计划。</summary>
    [Fact]
    public async Task IncompatibleProductAndHostProfileNeverReachEvarsPreview()
    {
        foreach (var (product, profile) in new[] { ("AM", "PDMS"), ("PDMS", "E3D2.1"), ("E3D", "AM"), ("AM", "E3D3.1.6") })
        {
            var calls = new List<string>();var candidate = Candidate() with { Identity = Identity with { Product = product } };
            var work = new PreparationWorkbench((id, input, context, token) => { calls.Add(id);return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(new ManagerInstallationReport(true, 1, [candidate], []), Json))); });
            await work.DiscoverAsync(["C:/synthetic-product"]);work.Select(Assert.Single(work.Candidates));work.Change(work.Draft with { Profile = profile, PackageRoot = "C:/application" });
            await Assert.ThrowsAsync<ArgumentException>(() => work.PreviewAsync(true));Assert.Equal(new[] { "installation.discover" }, calls);Assert.Null(work.Plan);Assert.False(work.CanApply);
        }
    }

    /// <summary>用真实后端和临时完整包贯通发现、部署、EVARS与原字节条件恢复。</summary>
    [Fact]
    public async Task RealManagerDiscoveryDeploymentAndEvarsShareSupportedProfileAndConditionRestore()
    {
        var root = Path.Combine(Path.GetTempPath(), "preparation-native-" + Guid.NewGuid().ToString("N"));var package = Path.Combine(root, "PmlTrigger.Package");var installation = Path.Combine(root, "Everything3D2.10");var state = Path.Combine(root, "state");var target = Path.Combine(root, "YuzuhaRuntime");
        void Write(string folder, string relative, string body) { var path = Path.Combine(folder, relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path, body); }
        Write(installation, "mon.exe", "synthetic product entry, never executed");Write(installation, "evars.init", "@echo off\nset pmllib=vendor\nset pmlui=vendor\n");
        Write(package, "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc", "define function !!YuzuhaResolveRuntimePath()\n  !folderName = 'PMLTRI'\nendfunction\n");Write(package, "PMLUI/bootstrap.pmlfnc", "synthetic PML, never executed");Write(package, "runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll", "synthetic Host, never loaded");Write(package, "runtime/cli/PmlTwin.Phayton.Cli.exe", "synthetic CLI, never executed");
        var files = Directory.GetFiles(package, "*", SearchOption.AllDirectories).Select(path => new { path = Path.GetRelativePath(package, path).Replace('\\', '/'), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() }).ToArray();Write(package, ".setup-payload.json", JsonSerializer.Serialize(new { version = "synthetic", files }));
        var store = new ManagerPlanStore(state);var executor = new OperationExecutor(new OperationRegistry(InstallationOperations.Registrations().Concat(EvarsOperations.Registrations(store)).Concat(RuntimeDeploymentOperations.Registrations(store, new(state, Path.Combine(root, "user"))))));var work = new PreparationWorkbench(executor.InvokeAsync);
        try
        {
            var before = File.ReadAllBytes(Path.Combine(installation, "evars.init"));Assert.True((await work.DiscoverAsync([root])).Success);work.Select(Assert.Single(work.Candidates));Assert.Equal("", work.Draft.Profile);
            work.Change(work.Draft with { PackageRoot = package, RuntimeRoot = target, Mode = "copy", Profile = "E3D2.1" });Assert.True((await work.PreviewAsync()).Success);Assert.False(Directory.Exists(target));Assert.Equal(before, File.ReadAllBytes(Path.Combine(installation, "evars.init")));
            Assert.True((await work.ApplyAsync(true)).Success);Assert.Contains(target, File.ReadAllText(Path.Combine(installation, "evars.init")));Assert.True(File.Exists(Path.Combine(target, ".yuzuha-runtime.json")));Assert.True((await work.RestoreAsync(true)).Success);Assert.Equal(before, File.ReadAllBytes(Path.Combine(installation, "evars.init")));
            work.Change(work.Draft with { Mode = "reference", RuntimeRoot = package });Assert.True((await work.PreviewAsync(true)).Success);Assert.True((await work.ApplyAsync(true)).Success);Assert.Contains("set Yuzuha=E3D2.1", File.ReadAllText(Path.Combine(installation, "evars.init")));Assert.True((await work.RestoreAsync(true)).Success);Assert.Equal(before, File.ReadAllBytes(Path.Combine(installation, "evars.init")));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>验证真实临时EVARS写完后丢回执仍保留恢复引用，恢复前禁止新计划或重放。</summary>
    [Fact]
    public async Task ActualEvarsWriteWithLostReplyRetainsRecoveryReferenceAndRestoresBeforeAllowingNewPlan()
    {
        var root = Path.Combine(Path.GetTempPath(), "preparation-unknown-" + Guid.NewGuid().ToString("N"));var installation = Path.Combine(root, "Everything3D2.10");Directory.CreateDirectory(installation);
        var environment = Path.Combine(installation, "evars.init");File.WriteAllText(Path.Combine(installation, "mon.exe"), "synthetic, never executed");File.WriteAllText(environment, "@echo off\nset pmllib=vendor\nset pmlui=vendor\n");var before = File.ReadAllBytes(environment);
        var store = new ManagerPlanStore(Path.Combine(root, "state"));var applied = 0;
        var executor = new OperationExecutor(new OperationRegistry(InstallationOperations.Registrations().Concat(EvarsOperations.Registrations(store, plan => { applied++;var receipt = EvarsEditor.Apply(plan);throw new EvarsApplyException("synthetic lost receipt after actual temporary-file replacement", receipt, new IOException("reply lost")); }))));
        var work = new PreparationWorkbench(executor.InvokeAsync);
        try
        {
            Assert.True((await work.DiscoverAsync([root])).Success);work.Select(Assert.Single(work.Candidates));work.Change(work.Draft with { PackageRoot = Path.Combine(root, "app"), Profile = "E3D2.1" });Assert.True((await work.PreviewAsync(true)).Success);
            var unknown = await work.ApplyAsync(true);Assert.Equal("EVARS_APPLY_UNKNOWN", unknown.Error!.Code);Assert.True(unknown.Error.MayHaveExecuted);Assert.True(work.Unresolved);Assert.NotNull(work.Receipt);Assert.Equal("evars.restore", work.RestoreOperation);Assert.NotEqual(before, File.ReadAllBytes(environment));
            await Assert.ThrowsAsync<InvalidOperationException>(() => work.PreviewAsync(true));await Assert.ThrowsAsync<InvalidOperationException>(() => work.ApplyAsync(true));Assert.Equal(1, applied);
            Assert.True((await work.RestoreAsync(true)).Success);Assert.False(work.Unresolved);Assert.Null(work.Receipt);Assert.Equal(before, File.ReadAllBytes(environment));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>验证明确应用消费冻结引用，草稿变化保留原恢复身份。</summary>
    [Fact]
    public async Task ExplicitApplyConsumesFrozenPlanAndKeepsConditionRestoreReceiptAcrossDraftChanges()
    {
        var calls = new List<(string Id, OperationContext Context)>();var work = new PreparationWorkbench((id, data, context, token) => {
            calls.Add((id, context));return Task.FromResult(id == "installation.discover" ? Found(id) : id.EndsWith(".restore") ? OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { restored = true })) : Reference(id, id.EndsWith(".apply") ? "runtime.receipt" : "runtime.deployment")); });
        await Select(work);await work.PreviewAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(() => work.ApplyAsync(false));Assert.DoesNotContain(calls, c => c.Id == "deployment.apply");
        await work.ApplyAsync(true);Assert.Null(work.Plan);Assert.NotNull(work.Receipt);Assert.True(calls.Single(c => c.Id == "deployment.apply").Context.AllowWrite);
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.ApplyAsync(true));work.Change(work.Draft with { RuntimeRoot = "C:/other" });Assert.NotNull(work.Receipt);
        await work.RestoreAsync(true);Assert.Null(work.Receipt);Assert.True(calls.Single(c => c.Id == "deployment.restore").Context.AllowWrite);
    }

    /// <summary>验证未知写入只记录一次并阻止新可执行预览。</summary>
    [Fact]
    public async Task UnknownWriteNeverAutomaticallyReplaysAndBlocksNewExecutablePreview()
    {
        var calls = new List<string>();var work = new PreparationWorkbench((id, data, context, token) => { calls.Add(id);if (context.AllowWrite) throw new IOException("reply lost after write");return Task.FromResult(id == "installation.discover" ? Found(id) : Reference(id, "evars.edit")); });
        await Select(work);await work.PreviewAsync(true);var result = await work.ApplyAsync(true);Assert.Equal("PREPARATION_OUTCOME_UNKNOWN", result.Error!.Code);Assert.True(work.Unresolved);Assert.False(work.CanApply);
        await work.InspectAsync();Assert.True(work.Result!.Success);Assert.Same(result, Assert.Single(work.UnresolvedResults));
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.PreviewAsync());await Assert.ThrowsAsync<InvalidOperationException>(() => work.ApplyAsync(true));Assert.Equal(1, calls.Count(id => id == "evars.apply"));
    }

    /// <summary>验证历史回执A恢复成功不能消除后来同类未知写入B的门禁。</summary>
    [Fact]
    public async Task HistoricalReceiptCannotClearLaterUnknownWithoutRecoveryIdentity() => await CheckReceiptIsolation(false);

    /// <summary>验证未知B携带自身完整回执时只恢复B即可清除该未知。</summary>
    [Fact]
    public async Task NewUnknownReceiptCanBeRestoredWithoutConfusingHistoricalReceipt() => await CheckReceiptIsolation(true);

    /// <summary>验证历史回执部分恢复及随后成功仍不能消除后来的无引用未知写入。</summary>
    [Fact]
    public async Task HistoricalRecoveryRequiredAndSuccessfulRestoreCannotClearLaterUnreferencedUnknown() => await CheckReceiptIsolation(false, true);

    /// <summary>验证部分部署恢复即使没有传输未知标志，仍保留精确身份并可按该回执恢复解除。</summary>
    [Fact]
    public async Task RecoveryRequiredRestoreKeepsExactReceiptUntilSuccessfulRetry()
    {
        var restores = 0;var work = new PreparationWorkbench((id, data, context, token) => Task.FromResult(id switch {
            "installation.discover" => Found(id), "deployment.preview" => Reference(id, "runtime.deployment"), "deployment.apply" => Reference(id, "runtime.receipt"),
            _ => ++restores == 1 ? OperationResult.Fail(id, 5, "RUNTIME_RESTORE_RECOVERY_REQUIRED", "synthetic conditional conflict, no transport uncertainty") : OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { restored = true })) }));
        await Select(work);await work.PreviewAsync();await work.ApplyAsync(true);var receipt = work.Receipt;
        var partial = await work.RestoreAsync(true);Assert.False(partial.Error!.MayHaveExecuted);Assert.True(work.Unresolved);Assert.Same(receipt, work.Receipt);await Assert.ThrowsAsync<InvalidOperationException>(() => work.PreviewAsync());
        await work.RestoreAsync(true);Assert.False(work.Unresolved);Assert.Null(work.Receipt);Assert.Equal(2, restores);
    }

    /// <summary>验证恢复本身丢回包只允许同一回执的后续明确恢复清除未知。</summary>
    [Fact]
    public async Task UnknownRestoreKeepsExactReceiptUntilItsSuccessfulConditionRestore()
    {
        var restores = 0;var work = new PreparationWorkbench((id, data, context, token) => Task.FromResult(id switch {
            "installation.discover" => Found(id), "evars.preview" => Reference(id, "evars.edit"), "evars.apply" => Reference(id, "evars.receipt"),
            _ => ++restores == 1 ? OperationResult.Fail(id, 7, "EVARS_RESTORE_UNKNOWN", "synthetic lost restore reply", mayHaveExecuted: true) : OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { restored = true })) }));
        await Select(work);await work.PreviewAsync(true);await work.ApplyAsync(true);var receipt = work.Receipt;
        await work.RestoreAsync(true);Assert.True(work.Unresolved);Assert.Same(receipt, work.Receipt);
        await work.RestoreAsync(true);Assert.False(work.Unresolved);Assert.Null(work.Receipt);Assert.Equal(2, restores);
    }

    /// <summary>使用不同回执身份重现连续同类写入，拒绝按操作名猜测未知归属。</summary>
    private static async Task CheckReceiptIsolation(bool includesCurrentReference, bool recoveryRequiredBeforeSuccess = false)
    {
        var writes = 0;var restores = 0;
        JsonElement Receipt(char id, char hash) => JsonSerializer.SerializeToElement(new ManagerPlanReference(new(id, 32), "evars.receipt", new(hash, 64), JsonSerializer.SerializeToElement(new { file = "synthetic" })), Json);
        var work = new PreparationWorkbench((id, data, context, token) => Task.FromResult(id switch {
            "installation.discover" => Found(id), "evars.preview" => Reference(id, "evars.edit"),
            "evars.apply" => ++writes == 1 ? OperationResult.Ok(id, Receipt('a', 'c')) : OperationResult.Fail(id, 7, "EVARS_APPLY_UNKNOWN", "synthetic B outcome unknown", includesCurrentReference ? Receipt('b', 'd') : null, true),
            _ => ++restores == 1 && recoveryRequiredBeforeSuccess ? OperationResult.Fail(id, 5, "EVARS_RESTORE_RECOVERY_REQUIRED", "synthetic historical receipt conflict") : OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { restored = true })) }));
        await Select(work);await work.PreviewAsync(true);await work.ApplyAsync(true);Assert.Equal(new('a', 32), work.Receipt!.PlanId);
        work.Change(work.Draft with { PackageRoot = "C:/second-app" });await work.PreviewAsync(true);await work.ApplyAsync(true);Assert.True(work.Unresolved);
        Assert.Equal(new(includesCurrentReference ? 'b' : 'a', 32), work.Receipt!.PlanId);await work.RestoreAsync(true);
        if (recoveryRequiredBeforeSuccess) { Assert.True(work.Unresolved);Assert.NotNull(work.Receipt);await work.RestoreAsync(true); }
        Assert.Equal(!includesCurrentReference, work.Unresolved);Assert.Null(work.Receipt);
        if (!includesCurrentReference) await Assert.ThrowsAsync<InvalidOperationException>(() => work.PreviewAsync(true));
        else Assert.True((await work.PreviewAsync(true)).Success);
    }

    /// <summary>验证等待期间禁止重复调用和改输入，但允许阅读导航。</summary>
    [Fact]
    public async Task PendingCallKeepsDraftAndPreventsDuplicateInvocationWhileReadingCanChange()
    {
        var gate = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);var calls = 0;
        var work = new PreparationWorkbench((id, data, context, token) => { calls++;return gate.Task; });var pending = work.InspectAsync();
        Assert.True(work.Busy);Assert.Throws<InvalidOperationException>(() => work.Change(work.Draft with { PackageRoot = "changed" }));await Assert.ThrowsAsync<InvalidOperationException>(() => work.InspectAsync());
        work.SelectStage("recover");work.Reader = "evidence";gate.SetResult(OperationResult.Ok("setup.handoff.inspect", JsonSerializer.SerializeToElement(new { verified = true })));await pending;
        Assert.False(work.Busy);Assert.Equal(1, calls);Assert.Equal("recover", work.Stage);Assert.Equal("evidence", work.Reader);
    }

    /// <summary>验证真实统一执行器中的同步阻塞处理器不占用调用线程，等待期间保持单次调用与输入门禁。</summary>
    [Fact]
    public async Task SynchronousManagerHandlerRunsInBackgroundWhileCallerAndReadingRemainAvailable()
    {
        using var entered = new ManualResetEventSlim();using var release = new ManualResetEventSlim();using var returned = new ManualResetEventSlim();using var cancellation = new CancellationTokenSource();
        var callerThread = 0;var handlerThread = 0;var calls = 0;OperationContext? observed = null;CancellationToken observedToken = default;
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"packageRoot\":{\"type\":\"string\"}},\"required\":[\"packageRoot\"],\"additionalProperties\":false}");
        var executor = new OperationExecutor(new OperationRegistry([new(new("setup.handoff.inspect", 1, "synchronously blocked manager fixture", schema.RootElement.Clone()), new DelegateOperationHandler((id, input, context, token) =>
        { handlerThread = Environment.CurrentManagedThreadId;observed = context;observedToken = token;Interlocked.Increment(ref calls);entered.Set();release.Wait();return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { verified = true }))); }))]));
        var work = new PreparationWorkbench(executor.InvokeAsync);Task<OperationResult>? pending = null;
        var caller = new Thread(() => { callerThread = Environment.CurrentManagedThreadId;pending = work.InspectAsync(cancellation.Token);returned.Set(); }) { IsBackground = true };
        caller.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "同步处理器未进入；超时仅防止测试死锁。");
            Assert.True(returned.Wait(TimeSpan.FromSeconds(10)), "同步处理器尚未释放时，调用线程应已取得未完成任务。");
            Assert.NotEqual(callerThread, handlerThread);Assert.True(work.Busy);Assert.NotNull(pending);Assert.False(pending.IsCompleted);Assert.False(release.IsSet);
            Assert.Throws<InvalidOperationException>(() => work.Change(work.Draft with { PackageRoot = "changed" }));await Assert.ThrowsAsync<InvalidOperationException>(() => work.InspectAsync());
            work.SelectStage("recover");work.Reader = "scripts";Assert.Equal(1, Volatile.Read(ref calls));Assert.False(observed!.AllowWrite);Assert.Equal(cancellation.Token, observedToken);
        }
        finally { release.Set();Assert.True(caller.Join(TimeSpan.FromSeconds(10)), "调用线程未结束。"); }
        var result = await pending!;Assert.True(result.Success);Assert.False(work.Busy);Assert.Equal("recover", work.Stage);Assert.Equal("scripts", work.Reader);Assert.Equal(1, calls);
    }

    /// <summary>验证迁移只读预览和旧所有权转移保有不同作用与冻结身份。</summary>
    [Fact]
    public async Task MigrationPreviewAndOwnershipTransferKeepDistinctEffectsAndFrozenIdentities()
    {
        var calls = new List<(string Id, JsonElement Input, bool Write)>();var work = new PreparationWorkbench((id, data, context, token) => { calls.Add((id, data, context.AllowWrite));return Task.FromResult(Reference(id, "legacy.transfer")); });
        work.Change(new(PackageRoot: "C:/app", EnvironmentFile: "C:/old/evars.bat", MigrationSource: "C:/sources/rewrite.json"));await work.PreviewMigrationAsync();Assert.Null(work.Plan);Assert.False(calls[0].Write);Assert.Equal("migration.preview", calls[0].Id);
        await work.PreviewOwnershipAsync();Assert.Equal("setup.legacy.transfer", work.ApplyOperation);work.DiscardPreview();Assert.False(work.CanApply);
        await work.PreviewOwnershipAsync();await work.ApplyAsync(true);var apply = calls.Single(c => c.Id == "setup.legacy.transfer");Assert.True(apply.Write);Assert.Equal(2, apply.Input.EnumerateObject().Count());Assert.NotNull(apply.Input.GetProperty("sha256").GetString());Assert.Null(work.RestoreOperation);
    }

    /// <summary>验证已安装文档入口拒绝外部地址、编码绕过与路径穿越。</summary>
    [Fact]
    public void ExactInstalledDocumentationRejectsExternalEncodedAndEscapingTargets()
    {
        var root = Path.Combine(Path.GetTempPath(), "preparation-doc-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root, "api"));File.WriteAllText(Path.Combine(root, "api", "Type.html"), "<h1 id='member'>Actual member</h1>");
        try { var target = DesktopDocumentationTarget.Resolve(root, "api/Type.html#member");Assert.Equal("api/Type.html#member", target.RelativeUrl);Assert.EndsWith("Type.html", target.File);
            foreach (var unsafePath in new[] { "https://example.com/x.html", "../Type.html", "api/../../outside.html", "%2e%2e/Type.html", "api\\Type.html", "/api/Type.html", "api/Type.html?x=1", "api/Type.html#x#y", "api//Type.html" }) Assert.ThrowsAny<ArgumentException>(() => DesktopDocumentationTarget.Resolve(root, unsafePath));
            Assert.Throws<FileNotFoundException>(() => DesktopDocumentationTarget.Resolve(root, "missing.html"));
        } finally { Directory.Delete(root, true); }
    }
}
