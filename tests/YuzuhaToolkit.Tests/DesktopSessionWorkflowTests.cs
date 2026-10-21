using Xunit;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Tests;

public sealed class DesktopSessionWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "yuzuha-desktop-flows-" + Guid.NewGuid().ToString("N"));
    private readonly FakeHost host = new();
    private SessionWorkbench Create() => new(host, new PmlFunctionRegistry(Path.Combine(root, "functions.json")));
    private string Source()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "UiProbe.pmlfnc");
        File.WriteAllText(path, "define function !!UiProbe(!name is STRING) is STRING\nreturn !name\nendfunction");
        return path;
    }
    private static PmlFormQueryRequest Forms(int budget = 20) => new() { Kind = "shown-forms", StartIndex = 1, MaxItems = budget, MaxCharacters = 32768 };

    [Fact]
    public void DiscoveryDoesNotSelectOrExecute()
    {
        var model = Create();
        Assert.Single(model.Discover().Sessions);
        Assert.False(model.Status.TargetVerified);
        Assert.Equal(0, host.Selections);
        Assert.Equal(0, host.Executions);
    }

    [Fact]
    public async Task SelectionRequiresDiscoveredIdentityAndDetectedHost()
    {
        var model = Create(); model.Discover();
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.SelectAsync(new() { ProcessId = 99 }));
        host.Candidate.PipeDetected = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.SelectAsync(host.Candidate));
        Assert.Equal(0, host.Selections);
    }

    [Fact]
    public async Task SamePidWithDifferentStartTimeCannotBecomeVerified()
    {
        var model = Create(); model.Discover();
        host.Connection.Host!.ProcessStartTimeUtcTicks++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.SelectAsync(host.Candidate));
        Assert.False(model.Status.TargetVerified);
    }

    [Fact]
    public async Task FailedTargetSwitchCannotKeepPreviousSessionCallable()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.SelectAsync(new() { ProcessId = 99 }));
        Assert.False(model.Status.TargetVerified);
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.QueryAsync(Forms()));
        Assert.Equal(0, host.Executions);
    }

    [Fact]
    public async Task OldHostRejectsQueriesBeforeExecution()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Connection.Host!.FormQueryVersion = 0;
        await Assert.ThrowsAsync<NotSupportedException>(() => model.QueryAsync(Forms()));
        Assert.Equal(0, host.Executions);
    }

    [Fact]
    public async Task EmptyWindowContinuesFromNativeNextIndex()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Pages.Enqueue(new() { Kind = "shown-forms", StartIndex = 1, Scanned = 20, IndexUpperBound = 80, NextIndex = 21, Truncated = true, Items = [] });
        host.Pages.Enqueue(new() { Kind = "shown-forms", StartIndex = 21, Scanned = 20, IndexUpperBound = 80, NextIndex = 41, Truncated = true, Items = [new() { Index = 23, Name = "Probe", Type = "FORM" }] });
        await model.QueryAsync(Forms());
        Assert.Empty(model.Forms.Items);
        Assert.Equal(21, model.Forms.NextIndex);
        await model.QueryAsync(Forms(), append: true);
        Assert.Equal(new[] { 1, 21 }, host.Starts);
        Assert.Equal(23, Assert.Single(model.Forms.Items).Index);
        Assert.Equal(41, model.Forms.NextIndex);
    }

    [Fact]
    public async Task ChangedBudgetCannotAppendToPreviousQuery()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Pages.Enqueue(new() { Kind = "shown-forms", StartIndex = 1, NextIndex = 21, Items = [] });
        await model.QueryAsync(Forms());
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.QueryAsync(Forms(30), append: true));
        Assert.Equal(1, host.Executions);
    }

    [Fact]
    public async Task ExpiredSessionClearsResultsAndBlocksNewQuery()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Pages.Enqueue(new() { Kind = "shown-forms", StartIndex = 1, Items = [new() { Name = "OldForm" }] });
        await model.QueryAsync(Forms());
        host.Connection.TargetVerified = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.QueryAsync(Forms()));
        Assert.Empty(model.Forms.Items);
        Assert.False(model.Status.TargetVerified);
        Assert.Equal(1, host.Executions);
    }

    [Fact]
    public async Task RegistrationAndPermissionWorkOfflineAndReregistrationRevokes()
    {
        var model = Create(); var source = Source();
        Assert.False(model.Register(source, "UiProbe").AllowInvoke);
        Assert.True(model.SetPermission("UiProbe", true).AllowInvoke);
        Assert.False(model.Register(source, "UiProbe").AllowInvoke);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => model.InvokeAsync("UiProbe", [new() { Type = "string", Value = "sample" }]));
        Assert.Equal(0, host.Executions);
    }

    [Fact]
    public async Task ChangedSourceIsRejectedBeforeDispatch()
    {
        var model = Create(); var source = Source(); model.Register(source, "UiProbe"); model.SetPermission("UiProbe", true);
        File.AppendAllText(source, "\n-- edited");
        await Assert.ThrowsAsync<InvalidDataException>(() => model.InvokeAsync("UiProbe", [new() { Type = "string", Value = "sample" }]));
        Assert.Equal("rejected", model.Outcome);
        Assert.Equal(0, host.Executions);
    }

    [Fact]
    public void ExistingTextTransportFailureCannotBecomeAValidReceipt()
    {
        Assert.Throws<IOException>(() => WorkbenchReply.ParseRegisteredCall("PML RPC failed: pipe disconnected"));
        Assert.Throws<IOException>(() => WorkbenchReply.ParseRegisteredCall("{broken json"));
        Assert.Throws<IOException>(() => WorkbenchReply.ParseRegisteredCall("{}"));
        Assert.False(WorkbenchReply.ParseRegisteredCall("{\"Success\":false,\"Code\":\"PML_ERROR\"}").Success);
    }

    [Fact]
    public void DesktopTrustPathSharesManagedRootAndRespectsExistingOverrides()
    {
        var desktop = Path.Combine(root, "runtime", "desktop");
        Assert.Equal(Path.Combine(root, "trust", "pml-function-trust.json"), WorkbenchPaths.TrustFile(desktop, null, null));
        Assert.Equal(Path.Combine(root, "other", "trust", "pml-function-trust.json"), WorkbenchPaths.TrustFile(desktop, null, Path.Combine(root, "other")));
        Assert.Equal(Path.Combine(root, "explicit.json"), WorkbenchPaths.TrustFile(desktop, Path.Combine(root, "explicit.json"), Path.Combine(root, "other")));
    }

    [Fact]
    public async Task LostInvocationReplyStaysUnknownAndIsNeverRetried()
    {
        var model = Create(); var source = Source(); model.Register(source, "UiProbe"); model.SetPermission("UiProbe", true);
        model.Discover(); await model.SelectAsync(host.Candidate); host.InvocationError = new IOException("reply lost");
        await Assert.ThrowsAsync<IOException>(() => model.InvokeAsync("UiProbe", [new() { Type = "string", Value = "sample" }]));
        Assert.Equal("unknown", model.Outcome);
        Assert.Equal(1, host.Executions);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task GadgetUnavailableAndEmptyTextRemainDistinct()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Pages.Enqueue(new() { Kind = "gadget", StartIndex = 1, Items = [new() { Name = "accept", Properties = [new() { Name = "tooltip", Status = "available", Value = "" }, new() { Name = "visible", Status = "unavailable", Error = "unsupported" }] }] });
        await model.QueryAsync(new() { Kind = "gadget", FormName = "Probe", GadgetName = "accept", StartIndex = 1, MaxItems = 1, MaxCharacters = 32768 });
        var properties = Assert.Single(model.GadgetDetails!.FormQueryResult!.Items).Properties;
        Assert.Equal("", properties[0].Value);
        Assert.Equal("unavailable", properties[1].Status);
        Assert.Null(properties[1].Value);
    }

    [Fact]
    public async Task ChangingGadgetSelectionClearsOldDetailsWithoutExecution()
    {
        var model = Create(); model.Discover(); await model.SelectAsync(host.Candidate);
        host.Pages.Enqueue(new() { Kind = "gadget", StartIndex = 1, Items = [new() { Name = "accept", Properties = [] }] });
        await model.QueryAsync(new() { Kind = "gadget", FormName = "Probe", GadgetName = "accept", StartIndex = 1, MaxItems = 1, MaxCharacters = 32768 });
        Assert.NotNull(model.GadgetDetails);
        model.ResetGadgetDetails();
        Assert.Null(model.GadgetDetails); Assert.Null(model.LastResponse); Assert.Equal(1, host.Executions);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class FakeHost : IWorkbenchHost
    {
        internal AvevaSessionCandidate Candidate = new() { ProcessId = 123, ProcessStartTimeUtcTicks = 456, PipeDetected = true, Product = "E3D", PipeName = "pid-123" };
        internal YuzuhaConnectionStatus Connection = new() { TargetVerified = true, Connected = true, ExpectedProcessId = 123, ExpectedProcessStartTimeUtcTicks = 456, ExpectedModel = "Design", Host = new() { ProcessId = 123, ProcessStartTimeUtcTicks = 456, Model = "Design", FormQueryVersion = 1, BoundedArrayReadVersion = 1 } };
        internal int Selections, Executions;
        internal Exception? InvocationError;
        internal Queue<PmlFormQueryResult> Pages = new();
        internal List<int> Starts = [];
        public AvevaSessionListResponse Discover() => new() { Sessions = [Candidate], Count = 1 };
        public Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token) { Selections++; return Task.FromResult(Connection); }
        public Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token) => Task.FromResult(Connection);
        public Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token) { Executions++; Starts.Add(request.StartIndex); return Task.FromResult(new RunPmlCommandResponse { Success = true, FormQueryResult = Pages.Dequeue() }); }
        public Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token) => throw new NotImplementedException();
        public Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token) { Executions++; if (InvocationError is not null) throw InvocationError; return Task.FromResult(new RunPmlCommandResponse { Success = true }); }
    }
}
