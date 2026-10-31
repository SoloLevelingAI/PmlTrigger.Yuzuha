using System.Text.Json;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public sealed record DraftDocument(Guid Generation, LaunchModel Model);
public sealed record ModelSnapshot(Guid AttemptId, long Revision, DateTimeOffset CapturedUtc, LaunchModel Model);

// Owns editing/persistence and execution boundaries. Editors only bind Model and render results.
public sealed class LaunchWorkspace
{
    public LaunchModel Model { get; }
    private readonly SavedProfiles profiles;
    private readonly ILaunchService launcher;
    private readonly IApplicationDiscovery discovery;
    private readonly DiscoveryOptions options;
    private readonly IRecordLog records;
    private readonly IJsonDocumentStore draft;
    private readonly IJsonDocumentDirectory snapshots;
    private readonly SemaphoreSlim writes = new(1,1);
    private Guid generation;
    private bool initialized;
    public string? DraftError { get; private set; }
    public event Action<string>? Notice;

    public LaunchWorkspace(LaunchModel model, SavedProfiles profiles, ILaunchService launcher,
        IApplicationDiscovery discovery, DiscoveryOptions options, IRecordLog records,
        IJsonDocumentStore draft, IJsonDocumentDirectory snapshots)
    {
        Model=model;this.profiles=profiles;this.launcher=launcher;this.discovery=discovery;
        this.options=options;this.records=records;this.draft=draft;this.snapshots=snapshots;
        Model.Changed += field => { if(initialized) _ = SaveDraftInBackgroundAsync(); };
    }
    public async Task InitializeAsync()
    {
        var saved=await draft.ReadAsync<DraftDocument>();
        if(saved is not null) { saved.Model.EnsureNoSecretFields();Model.Apply(saved.Model);generation=saved.Generation; }
        initialized=true;
    }
    private async Task SaveDraftInBackgroundAsync()
    {
        try { await FlushDraftAsync(); }
        catch(Exception e) { DraftError="模型草稿未保存："+Explain(e);Notice?.Invoke(DraftError); }
    }
    public async Task FlushDraftAsync(CancellationToken ct=default)
    {
        await writes.WaitAsync(ct);
        try
        {
            using var lease=draft is IDocumentLock locked ? await locked.AcquireAsync(ct) : null;
            var frozen=Model.Freeze(); frozen.EnsureNoSecretFields();
            var current=await draft.ReadAsync<DraftDocument>(ct);
            if((current?.Generation ?? Guid.Empty)!=generation)
                throw new InvalidOperationException("模型文件已由其他编辑器更改；请重新载入，不能覆盖。");
            var next=Guid.NewGuid();await draft.ReplaceAsync(new DraftDocument(next,frozen),ct);
            generation=next;DraftError=null;
        }
        finally { writes.Release(); }
    }
    public Task<IReadOnlyList<LaunchRequest>> ReadProfilesAsync() => profiles.ReadAllAsync();
    public async Task<DiscoveryReport> ScanAsync()
    {
        var report=await discovery.DiscoverAsync(options);
        await records.AppendAsync(report.ScanId,null,"Discovery","Completed",new Dictionary<string,string>
        {
            ["Roots"]=string.Join(";",options.SearchRoots),
            ["Candidates"]=JsonSerializer.Serialize(report.Candidates.Select(c=>c with { ProjectModules=null })),
            ["Diagnostics"]=JsonSerializer.Serialize(report.Diagnostics)
        });
        foreach(var candidate in report.Candidates.Where(c=>c.ProjectModules is not null))
            await records.AppendAsync(report.ScanId,null,"ProjectModules","StaticCandidates",new Dictionary<string,string>
            { ["Application"]=candidate.DisplayName,["Report"]=JsonSerializer.Serialize(candidate.ProjectModules) });
        return report;
    }
    public async Task SelectCandidateAsync(DiscoveryCandidate candidate)
    {
        Model.Apply(LaunchModel.FromRequest(candidate.Prefill),true);
        await FlushDraftAsync();
    }
    public void SelectProfile(LaunchRequest request) => Model.Apply(LaunchModel.FromRequest(request));
    public async Task<SaveResult> SaveAsync()
    {
        var frozen=Model.Freeze();
        try
        {
            frozen.EnsureNoSecretFields(); await FlushDraftAsync();
            var request=frozen.ToInput().CreateSnapshot(Guid.NewGuid());
            var before=await profiles.ReadAsync(frozen.ConfigurationId);
            if(!await records.AppendAsync(request.AttemptId,null,"InputEdited","SaveRequested",
                new Dictionary<string,string> { ["Before"]=JsonSerializer.Serialize(before),["Model"]=JsonSerializer.Serialize(frozen) }))
                return new(false,null,"保存中止 · 无法写入输入证据");
            var result=await profiles.SaveAsync(request);
            // Do not replace edits made while saving with an older result.
            if(result.Success && result.Configuration is {} saved && Model.Revision==frozen.Revision)
                Model.Apply(LaunchModel.FromRequest(saved));
            return result;
        }
        catch(Exception e) { return new(false,null,"配置未保存："+Explain(e)); }
    }
    public async Task<LaunchResult> ExecuteAsync(ISecretHandle? secret=null,CancellationToken ct=default)
    {
        var frozen=Model.Freeze();var id=Guid.NewGuid();LaunchRequest request;var stage="检查可持久化字段";
        try
        {
            frozen.EnsureNoSecretFields();
            stage="保存模型草稿";
            await FlushDraftAsync(ct);
            stage="保存执行快照";
            await snapshots.Open(id.ToString("N")).ReplaceAsync(new ModelSnapshot(id,frozen.Revision,DateTimeOffset.UtcNow,frozen),ct);
            stage="保存快照记录";
            if(!await records.AppendAsync(id,null,"ModelSnapshot","Captured",new Dictionary<string,string>
                { ["Revision"]=frozen.Revision.ToString(),["Model"]=JsonSerializer.Serialize(frozen) }))
                return new(id,null,LaunchStatus.Rejected,"输入快照记录失败；未执行。");
            try { request=frozen.ToInput().CreateSnapshot(id); }
            catch(ArgumentException e)
            {
                await records.AppendAsync(id,null,"Result","Rejected",new Dictionary<string,string>{{"Reason",e.Message}});
                return new(id,null,LaunchStatus.Rejected,e.Message);
            }
        }
        catch(OperationCanceledException) { return new(id,null,LaunchStatus.Cancelled,"快照阶段已取消；未执行。"); }
        catch(Exception e) { return new(id,null,LaunchStatus.Rejected,stage+"失败；未执行。"+Explain(e)); }
        // Once handed to the coordinator, never translate uncertainty into “not executed”.
        return await launcher.ExecuteSnapshotAsync(request,secret,ct);
    }
    public IAsyncEnumerable<LaunchEvent> ObserveAsync(Guid runId,CancellationToken ct) => launcher.ObserveAsync(runId,ct);
    public LaunchResult? GetLatest(Guid runId) => launcher.GetLatest(runId);
    private static string Explain(Exception error) =>
        (string.IsNullOrWhiteSpace(error.Message)?"未提供错误消息":error.Message)+$" [{error.GetType().Name}; 0x{error.HResult:X8}]";
}
