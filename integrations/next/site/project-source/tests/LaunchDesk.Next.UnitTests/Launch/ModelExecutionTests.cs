using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Platform.Storage;
using NUnit.Framework;
namespace LaunchDesk.Next.UnitTests.Launch;

public sealed class ModelExecutionTests
{
    private static LaunchModel Valid() => LaunchModel.FromRequest(new LaunchInput
    {DisplayName="fixture",ExecutablePath="fixture.exe",WorkingDirectory="fixture",Protocol=EntryProtocol.Direct,CheckLicence=false}.CreateSnapshot(Guid.Empty));
    [Test] public void ModelRoundTripDoesNotInventEditsOrAlterRevision()
    {
        var model=Valid();model.Project="manual";model.LicencePort="unfinished";
        var frozen=model.Freeze();model.Project="later";
        Assert.Multiple(()=>{Assert.That(frozen.Project,Is.EqualTo("manual"));Assert.That(frozen.Revision,Is.EqualTo(2));Assert.That(frozen.EditedFields,Is.EquivalentTo(new[]{"Project","LicencePort"}));Assert.That(frozen.LicencePort,Is.EqualTo("unfinished"));});
    }
    [Test] public async Task InvalidRawInputIsSnapshottedAndNeverReachesLauncher()
    {
        var model=Valid();model.LicencePort="bad";var launcher=new Launcher();var documents=new Documents();
        var result=await Workspace(model,launcher,documents).ExecuteAsync();
        Assert.Multiple(()=>{Assert.That(result.Status,Is.EqualTo(LaunchStatus.Rejected));Assert.That(launcher.Calls,Is.Zero);Assert.That(documents.Values.Single().Value,Does.Contain("bad"));});
    }
    [Test] public async Task SnapshotFailureOrRequiredRecordFailureNeverReachesLauncher()
    {
        foreach(bool failSnapshot in new[]{true,false})
        {
            var launcher=new Launcher();var docs=new Documents{Fail=failSnapshot};var log=new Log{Fail=!failSnapshot};
            var result=await Workspace(Valid(),launcher,docs,log).ExecuteAsync();
            Assert.That(result.Status,Is.EqualTo(LaunchStatus.Rejected));Assert.That(launcher.Calls,Is.Zero);
        }
    }
    [Test] public async Task EditsDuringSnapshotWriteDoNotChangeExecutionParameters()
    {
        var model=Valid();model.Project="captured";model.LicenceServer="server-a";model.LicencePort="4546";
        var entered=new TaskCompletionSource();var resume=new TaskCompletionSource();
        var docs=new Documents{BeforeWrite=async()=>{entered.SetResult();await resume.Task;}};var launcher=new Launcher();
        var task=Workspace(model,launcher,docs).ExecuteAsync();await entered.Task;
        model.Project="next";model.LicenceServer="server-b";resume.SetResult();await task;
        Assert.Multiple(()=>{Assert.That(launcher.Request!.Project,Is.EqualTo("captured"));Assert.That(launcher.Request.LicenceServer,Is.EqualTo("server-a"));Assert.That(launcher.Request.LicencePort,Is.EqualTo(4546));Assert.That(model.Project,Is.EqualTo("next"));});
    }
    [Test] public async Task SecretEnvironmentCannotEnterDraftOrSnapshot()
    {
        var model=Valid();model.Environment["PASSWORD"]="not-for-storage";var docs=new Documents();var launcher=new Launcher();
        var result=await Workspace(model,launcher,docs).ExecuteAsync();
        Assert.That(result.Status,Is.EqualTo(LaunchStatus.Rejected));Assert.That(docs.Values,Is.Empty);Assert.That(launcher.Calls,Is.Zero);
    }
    [Test] public async Task DraftReloadAndTwoEditorsDetectConflictingDiskGeneration()
    {
        var root=Path.Combine(TestContext.CurrentContext.WorkDirectory,"model-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var aModel=Valid();var bModel=Valid();var a=Workspace(aModel,new(),new(),draft:new JsonDocumentStore(Path.Combine(root,"draft.json")));
        await a.FlushDraftAsync();
        var b=Workspace(bModel,new(),new(),draft:new JsonDocumentStore(Path.Combine(root,"draft.json")));
        // Loading applies stored values to the same model object, not a second editor copy.
        await b.InitializeAsync();Assert.That(bModel.DisplayName,Is.EqualTo(aModel.DisplayName));
        aModel.Project="a";await a.FlushDraftAsync();
        var result=await b.ExecuteAsync();Assert.That(result.Status,Is.EqualTo(LaunchStatus.Rejected));
        Assert.That((await new JsonDocumentStore(Path.Combine(root,"draft.json")).ReadAsync<DraftDocument>())!.Model.Project,Is.EqualTo("a"));
    }
    private static LaunchWorkspace Workspace(LaunchModel model,Launcher launcher,Documents docs,Log? log=null,IJsonDocumentStore? draft=null) =>
        new(model,new SavedProfiles(new Memory()),launcher,new Discovery(),new DiscoveryOptions([]),log??new(),draft??new Memory(),docs);
    private sealed class Discovery:IApplicationDiscovery {public Task<DiscoveryReport> DiscoverAsync(DiscoveryOptions options,CancellationToken cancellationToken=default)=>throw new NotSupportedException();}
    private sealed class Launcher:ILaunchService
    {
        public int Calls;public LaunchRequest? Request;
        public Task<LaunchResult> ExecuteAsync(LaunchRequest request,ISecretHandle? secret=null,CancellationToken ct=default){Calls++;Request=request;return Task.FromResult(new LaunchResult(request.AttemptId,null,LaunchStatus.Created,"fixture"));}
        public LaunchResult? GetLatest(Guid id)=>null;
        public async IAsyncEnumerable<LaunchEvent> ObserveAsync(Guid id,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct=default){await Task.CompletedTask;yield break;}
    }
    private sealed class Memory:IJsonDocumentStore
    {
        public string? Json;public Func<Task>? BeforeWrite;public bool Fail;
        public Task<T?> ReadAsync<T>(CancellationToken ct=default)=>Task.FromResult(Json is null?default:JsonSerializer.Deserialize<T>(Json));
        public async Task ReplaceAsync<T>(T value,CancellationToken ct=default){if(Fail)throw new IOException("fixture");if(BeforeWrite!=null)await BeforeWrite();Json=JsonSerializer.Serialize(value);}
    }
    private sealed class Documents:IJsonDocumentDirectory
    {
        public bool Fail;public Func<Task>? BeforeWrite;private readonly Dictionary<string,Memory> stores=new();
        public Dictionary<string,string> Values=>stores.Where(p=>p.Value.Json!=null).ToDictionary(p=>p.Key,p=>p.Value.Json!);
        public IJsonDocumentStore Open(string name){var store=new Memory{Fail=Fail,BeforeWrite=BeforeWrite};stores.Add(name,store);return store;}
    }
    private sealed class Log:IRecordLog
    {
        public bool Fail;public IReadOnlyList<Record> Records=>[];
        public Task<bool> AppendAsync(Guid id,Guid? run,string stage,string outcome,IReadOnlyDictionary<string,string>? snapshot=null)=>Task.FromResult(!Fail);
    }
}
