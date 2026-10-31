using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
namespace LaunchDesk.Next.Desktop.Features.Launch;

public sealed class LaunchInteraction(LaunchWorkspace workspace, IRecordLog records)
{
    private CancellationTokenSource? observation;
    private Guid? runId;
    public async Task InitializeAsync(LaunchViewModel vm)
    { workspace.Notice += message => vm.Status=message; await workspace.InitializeAsync(); await RefreshProfiles(vm); await ScanAsync(vm); }
    private async Task RefreshProfiles(LaunchViewModel vm)
    { vm.Configurations.Clear(); foreach (var profile in await workspace.ReadProfilesAsync()) vm.Configurations.Add(profile); }
    public async Task ScanAsync(LaunchViewModel vm)
    {
        vm.IsBusy = true; vm.Status = "正在查找 · 扫描不会启动应用";
        try
        {
            var report = await workspace.ScanAsync(); vm.Candidates.Clear(); foreach (var c in report.Candidates) vm.Candidates.Add(c);
            vm.Status = report.Candidates.Count == 0 ? "未找到应用 · 可手动填写或调整搜索范围" : $"发现 {report.Candidates.Count} 个候选 · 请选中并核对预填";
            foreach (var d in report.Diagnostics) vm.Evidence.Add(new($"扫描 {d.Code}", $"{d.Path}\n{d.Message}"));
            foreach (var candidate in report.Candidates) AddProjectModules(vm, candidate);
        }
        catch (Exception ex) { vm.Status = "查找失败 · " + ex.Message; }
        finally { vm.IsBusy = false; }
    }
    public async Task SelectCandidateAsync(LaunchViewModel vm, DiscoveryCandidate candidate)
    {
        await workspace.SelectCandidateAsync(candidate);
        AddProjectModules(vm, candidate);
    }
    private static void AddProjectModules(LaunchViewModel vm, DiscoveryCandidate candidate)
    {
        if (candidate.ProjectModules is not { } report) return;
        var projects = string.Join("\n", report.Projects.Select(value => $"{value.Code} → {value.Directory}\n  {value.Origin.Kind} · {value.Origin.File}:{value.Origin.Line} · 目录存在={value.DirectoryExists}"));
        var modules = string.Join("\n", report.Modules.Select(value => $"{value.Name} · {value.Origin.Kind} · {value.Origin.File}:{value.Origin.Line}"));
        var diagnostics = string.Join("\n", report.Diagnostics.Select(value => $"{value.Code} · {value.Path} · {value.Message}"));
        vm.Evidence.Add(new($"项目与模块候选 · {candidate.DisplayName}",
            $"静态候选需要核对，不代表已登录或具备许可。\n\nProject:\n{projects}\n\nModule:\n{modules}\n\n诊断:\n{diagnostics}"));
    }
    public void SelectConfiguration(LaunchViewModel vm, LaunchRequest request) => workspace.SelectProfile(request);
    public async Task SaveAsync(LaunchViewModel vm)
    {
        vm.IsBusy = true;
        try
        {
            var result = await workspace.SaveAsync(); vm.Status = result.Message;
            if (result.Success) await RefreshProfiles(vm);
        }
        catch (Exception ex) { vm.Status = "保存失败 · " + ex.Message; }
        finally { vm.IsBusy = false; }
    }
    public async Task LaunchAsync(LaunchViewModel vm, ISecretHandle? secret)
    {
        using (secret)
        {
                vm.IsBusy = true; vm.Status = "检查中 · 正在保存本次输入快照";
            try
            {
                var result = await workspace.ExecuteAsync(secret); Apply(vm, result); runId = result.RunId;
                if (runId is { } id) { observation?.Cancel(); observation = new(); _ = Observe(vm, id, observation.Token); }
            }
            catch (Exception ex) { vm.Status = "启动失败 · " + ex.Message; }
            finally { vm.IsBusy = false; RefreshEvidence(vm); }
        }
    }
    private async Task Observe(LaunchViewModel vm, Guid id, CancellationToken ct)
    { try { await foreach (var evt in workspace.ObserveAsync(id, ct)) { Apply(vm, evt.Result); RefreshEvidence(vm); } } catch (OperationCanceledException) { } catch (Exception) { vm.Status = "观察中断 · 目标进程继续运行"; } }
    private static void Apply(LaunchViewModel vm, LaunchResult result) => vm.Status = $"{Phase(result.Status)} · {result.Message} · 操作 {result.AttemptId:N}";
    private static string Phase(LaunchStatus status) => status switch { LaunchStatus.Checking => "检查中", LaunchStatus.Starting => "启动中", LaunchStatus.Created => "进程已创建 · 登录待确认", LaunchStatus.WindowObserved => "窗口已出现 · 请确认登录", LaunchStatus.Unconfirmed => "尚未确认窗口", LaunchStatus.Exited => "进程已退出", LaunchStatus.Rejected => "检查未通过", LaunchStatus.Failed => "启动失败", LaunchStatus.Cancelled => "已取消", LaunchStatus.Busy => "已有启动进行中", _ => "需要核验" };

    public void RefreshEvidence(LaunchViewModel vm)
    {
        vm.Evidence.Clear(); foreach (var r in records.Records)
        {
            var detail = $"操作：{r.AttemptId}\n运行：{r.RunId}\n时间：{r.Timestamp:O}\n阶段：{r.Stage} / {r.Outcome}\n";
            foreach (var pair in r.Snapshot) { var value = pair.Value; try { using var doc = System.Text.Json.JsonDocument.Parse(value); value = System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }); } catch (System.Text.Json.JsonException) { } detail += $"\n{pair.Key}\n{value}\n"; }
            vm.Evidence.Add(new($"{r.Sequence:000} · {r.Timestamp:HH:mm:ss} · {r.Stage} · {r.Outcome}", detail));
        }
        vm.SelectedEvidence = vm.Evidence.LastOrDefault();
    }
    public void Detach() { observation?.Cancel(); observation?.Dispose(); observation = null; }
    public void Reattach(LaunchViewModel vm) { if (runId is { } id) { if (workspace.GetLatest(id) is { } latest) Apply(vm, latest); observation = new(); _ = Observe(vm, id, observation.Token); } }
}
