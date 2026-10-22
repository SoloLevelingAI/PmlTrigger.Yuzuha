using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class DevelopmentExecutionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "yuzuha-development-" + Guid.NewGuid().ToString("N"));
    private readonly ExecutionAuthorizationStore policy;
    private readonly PmlFunctionRegistry registry;
    private readonly FakeService service = new();
    private readonly YuzuhaRpcBridge bridge;
    private readonly RuntimeJournal journal;
    public DevelopmentExecutionTests()
    {
        Directory.CreateDirectory(root); policy = new(Path.Combine(root, "authorization.json")); registry = new(Path.Combine(root, "functions.json"));
        bridge = new(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), new Connections(service), policy, registry, root);
        journal = new("development-tests", root);
    }
    private void Development(params string[] rules) => policy.Save(new() { Mode="Development", ProcessId=42, ProcessStartTimeUtcTicks=12345, Model="Design", SourceRoot=root, Reason="isolated regression fixture", BypassRules=rules, ExpiresUtc=DateTimeOffset.UtcNow.AddHours(1) });
    private PmlFunctionRegistration Register(bool allow = true)
    {
        var path=Path.Combine(root,"Sample.pmlfnc"); File.WriteAllText(path,"define function !!Sample()\nendfunction\n");
        var entry=registry.Register(path,"Sample"); if(allow) registry.SetPermission("Sample",true); return entry;
    }

    [Fact]
    public async Task ConservativeDeniesAllDispatchButAllowsRegistrationAndDiscovery()
    {
        Register(); policy.Save(new(){Mode="Conservative"}); Assert.Single(bridge.DiscoverSessions().Sessions);
        await bridge.SelectSessionAsync(42,"Design",default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunPmlCommandAsync("Q",default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.ReadPmlArrayAsync("A",1,1,100,default));
        Assert.Equal(0,service.Commands);
    }
    [Fact]
    public async Task StandardRequiresPermissionAndCarriesAutomaticGroupReceipt()
    {
        Register(false); await bridge.SelectSessionAsync(42,"Design",default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunPmlCommandAsync("Q",default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default));
        registry.SetPermission("Sample",true);
        var response=await bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default);
        Assert.True(response.Success); Assert.True(service.Last!.AutomaticMarkDb); Assert.Equal("Standard",response.AuthorizationMode);
        registry.SetPermission("Sample",false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default)); Assert.Equal(1,service.Commands);
    }
    [Fact]
    public async Task RegisteredReadOnlyApprovalIsBoundToExactSourceAndRevokedOnRegistration()
    {
        var entry=Register();
        Assert.Throws<InvalidDataException>(()=>registry.ConfirmReadOnly("Sample",new string('0',64),true,"reviewed"));
        registry.ConfirmReadOnly("Sample",entry.SourceSha256,true,"all callees inspected; loaded revision checked");
        await bridge.SelectSessionAsync(42,"Design",default);
        var response=await bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default);
        Assert.False(service.Last!.AutomaticMarkDb); Assert.Equal(entry.SourceSha256,service.Last.ReadOnlySourceSha256);
        Assert.Contains(entry.SourceSha256,response.ReadOnlyEvidence);
        File.AppendAllText(entry.SourceFile,"-- edited\n");
        await Assert.ThrowsAsync<InvalidDataException>(()=>bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default));
        var fresh=registry.Register(entry.SourceFile,"Sample"); Assert.False(fresh.ReadOnlyConfirmed); Assert.False(fresh.AllowInvoke);
    }
    [Fact]
    public async Task TypedReadOnlyExemptionDoesNotApplyToExpressionAssignment()
    {
        var entry=Register(); registry.ConfirmReadOnly("Sample",entry.SourceSha256,true,"reviewed loaded source"); await bridge.SelectSessionAsync(42,"Design",default);
        await bridge.ReadPmlArrayAsync("A",1,1,100,default); Assert.False(service.Last!.AutomaticMarkDb);
        await bridge.RunPmlCommandListAsync("!!Sample()","A",true,default); Assert.True(service.Last!.AutomaticMarkDb);
        await Assert.ThrowsAsync<ArgumentException>(()=>bridge.RunPmlCommandAsync("!!Sample(1);DELETE)",default));
    }
    [Fact]
    public async Task DevelopmentPermissionBypassIsScopedAndDoesNotGrantRawExecution()
    {
        Register(false); Development("function-permission"); await bridge.SelectSessionAsync(42,"Design",default);
        Assert.True((await bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunPmlCommandAsync("Q",default));
        var state=policy.Read(); state.ProcessStartTimeUtcTicks++; policy.Save(state);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunRegisteredFunctionAsync(registry,"Sample",[],default));
        Assert.Equal(1,service.Commands);
    }
    [Fact]
    public async Task ExpiredDevelopmentGrantCannotDispatch()
    {
        Development("raw-command"); var state=policy.Read(); state.ExpiresUtc=DateTimeOffset.UtcNow.AddSeconds(-1); policy.Save(state);
        await bridge.SelectSessionAsync(42,"Design",default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>bridge.RunPmlCommandAsync("Q",default)); Assert.Equal(0,service.Commands);
    }
    [Fact]
    public async Task OperationAuthorizationDenialReportsNoPossibleDispatch()
    {
        var executor=YuzuhaOperationCatalog.Create(bridge,new PmlFunctionTrustStore(Path.Combine(root,"trust.json")),bridge.CreateIndependent);
        using var input=JsonDocument.Parse("{\"pmlCommand\":\"Q\"}");
        var result=await executor.InvokeAsync(YuzuhaOperationIds.Execute,input.RootElement,new(42,"Design",true));
        Assert.Equal(4,result.ExitCode); Assert.False(result.Error!.MayHaveExecuted); Assert.Equal(0,service.Commands);
    }
    [Fact]
    public async Task OldHostIsRejectedBeforeMutationAndManualBoundariesCannotNest()
    {
        Development("raw-command"); service.MarkDbVersion=0; await bridge.SelectSessionAsync(42,"Design",default);
        await Assert.ThrowsAsync<NotSupportedException>(()=>bridge.RunPmlCommandAsync("Q",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.RunPmlCommandAsync("MARKDB 'nested'",default)); Assert.Equal(0,service.Commands);
    }
    [Fact]
    public async Task TransportUnknownIsNotRetriedAndBlocksFurtherDispatchUntilExplicitReconnect()
    {
        Development("raw-command"); await bridge.SelectSessionAsync(42,"Design",default); service.ThrowTransport=true;
        await Assert.ThrowsAsync<IOException>(()=>bridge.RunPmlCommandAsync("Q",default)); service.ThrowTransport=false;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.RunPmlCommandAsync("Q",default)); Assert.Equal(1,service.Commands);
        Assert.Contains(RuntimeReader.Query(stateDirectory:root),entry=>entry.Phase=="cancelled-unknown" && entry.TargetStartTimeUtcTicks==12345);
    }
    [Fact]
    public async Task IncompleteGroupIsFailureAndCannotBeReportedAsSuccess()
    {
        Development("raw-command"); service.Ended=false; await bridge.SelectSessionAsync(42,"Design",default);
        var response=await bridge.RunPmlCommandAsync("Q",default); Assert.False(response.Success); Assert.Equal("MARKDB_GROUP_INCOMPLETE",response.Code);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.RunPmlCommandAsync("Q",default)); Assert.Equal(1,service.Commands);
    }
    [Fact]
    public async Task DevelopmentWritesRequireHashForOverwriteAndExecuteImmutableSnapshotWithPendingReview()
    {
        Development(); var tools=new DevelopmentTools(policy,bridge,journal);
        using var created=JsonDocument.Parse(tools.WriteDevelopmentFile("tests/smoke.pmlmac","$P test harness","test"));
        var hash=created.RootElement.GetProperty("SourceSha256").GetString()!;
        Assert.Throws<ModelContextProtocol.McpException>(()=>tools.WriteDevelopmentFile("tests/smoke.pmlmac","changed","test"));
        Assert.Throws<ModelContextProtocol.McpException>(()=>tools.WriteDevelopmentFile("../escape.pmlmac","Q"));
        using var executed=JsonDocument.Parse(await tools.RunDevelopmentFile("tests/smoke.pmlmac",hash));
        Assert.Equal("Dispatched",executed.RootElement.GetProperty("ExecutionStatus").GetString());
        Assert.Equal("Pending",executed.RootElement.GetProperty("HumanReview").GetString());
        Assert.Contains(".yuzuha-runs",service.Last!.DevelopmentMacroPath); Assert.True(service.Last.AutomaticMarkDb);
        using var edited=JsonDocument.Parse(tools.WriteDevelopmentFile("tests/smoke.pmlmac","$P changed","test",hash));
        Assert.Equal("NotRun",edited.RootElement.GetProperty("ExecutionStatus").GetString());
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(()=>tools.RunDevelopmentFile("tests/smoke.pmlmac",hash));
    }
    [Fact]
    public void SourceScopeRejectsPrefixSiblingAndSourceIdentityRejectsChanges()
    {
        Assert.Throws<UnauthorizedAccessException>(()=>ExecutionAuthorizationStore.RequireSourcePath(root,root+"-sibling/test.pmlmac"));
        var entry=Register(); PmlSourceIdentity.Verify(entry.SourceFile,entry.SourceSha256); File.AppendAllText(entry.SourceFile,"--changed");
        Assert.Throws<InvalidDataException>(()=>PmlSourceIdentity.Verify(entry.SourceFile,entry.SourceSha256));
        Assert.Throws<ArgumentException>(()=>PmlSourceIdentity.MacroCommand("C:/bad$path/file.pmlmac"));
    }
    [Fact]
    public void UndoGroupNeverRunsBodyAfterBeginFailureAndAlwaysAttemptsEndAfterBodyException()
    {
        var commands=new List<string>();var bodies=0;
        var failed=PmlUndoGroup.Run(command=>{commands.Add(command);return false;},()=>++bodies);
        Assert.Equal(0,bodies); Assert.Single(commands); Assert.False(failed.BodyStarted);
        commands.Clear();var thrown=PmlUndoGroup.Run<int>(command=>{commands.Add(command);return true;},()=>throw new InvalidOperationException("body failed"));
        Assert.True(thrown.Started);Assert.True(thrown.Ended);Assert.Equal("ENDMARKDB",commands[1]);Assert.Contains("body failed",thrown.Error);
        var endFailure=PmlUndoGroup.Run(command=>command!="ENDMARKDB",()=>7);Assert.Equal(7,endFailure.Value);Assert.Contains("ENDMARKDB_FAILED",endFailure.Error);
    }
    [Fact]
    public void RuntimeFilteringUsesSessionStartIdentityAndExportCannotOverwrite()
    {
        var entries=new[] {new ActivityEntry(DateTimeOffset.UtcNow,"fixture",1,"a","tool","failed",1,42,"{}","mode=Standard"){TargetStartTimeUtcTicks=1},new ActivityEntry(DateTimeOffset.UtcNow,"fixture",1,"b","other","succeeded",1,42,"{}",""){TargetStartTimeUtcTicks=2}};
        Assert.Equal("a",Assert.Single(RuntimeReader.Filter(entries,42,1,"tool","failed","Standard")).CallId);
        var file=Path.Combine(root,"export.jsonl"); Assert.Equal(1,RuntimeReader.Export(file,RuntimeReader.Filter(entries,42,1)));
        using var exported=JsonDocument.Parse(Assert.Single(File.ReadAllLines(file))); Assert.Equal("a",exported.RootElement.GetProperty("callId").GetString());
        Assert.Throws<IOException>(()=>RuntimeReader.Export(file,entries));
    }
    public void Dispose(){bridge.Dispose();journal.Dispose();Directory.Delete(root,true);}
    private sealed class Discovery : IAvevaSessionDiscovery
    {
        private readonly AvevaSessionCandidate candidate=new(){ProcessId=42,ProcessStartTimeUtcTicks=12345,PipeName="yuzuha.pml.command.v1.pid-42",PipeDetected=true};
        public AvevaSessionListResponse Discover()=>new(){Sessions=[candidate],Count=1};
        public AvevaSessionCandidate RequireCandidate(int pid)=>pid==42?candidate:throw new InvalidOperationException("unknown PID");
    }
    private sealed class Connections(FakeService service) : IPmlRpcConnectionFactory
    {
        public IPmlRpcConnection Connect(string pipe)=>new Connection(service);
        private sealed class Connection(FakeService service) : IPmlRpcConnection { public IPmlCommandService Service=>service; public void Dispose(){} }
    }
    private sealed class FakeService : IPmlCommandService
    {
        public int Commands,MarkDbVersion=1;public bool ThrowTransport,Ended=true; public RunPmlCommandRequest? Last;
        public Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request,CancellationToken token)=>Task.FromResult(new HostIdentityResponse{ProcessId=42,ProcessStartTimeUtcTicks=12345,Model="Design",PipeName="yuzuha.pml.command.v1.pid-42",AutomaticMarkDbVersion=MarkDbVersion,BoundedArrayReadVersion=1,FormQueryVersion=1});
        public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request,CancellationToken token)
        {
            Last=request;Commands++;if(ThrowTransport)throw new IOException("lost after dispatch");
            return Task.FromResult(new RunPmlCommandResponse{Success=true,Code="OK",MarkDbStarted=request.AutomaticMarkDb,MarkDbEnded=request.AutomaticMarkDb&&Ended,ExecutionStarted=true,MarkDbId=request.AutomaticMarkDb?"fixture-group":null});
        }
    }
}
