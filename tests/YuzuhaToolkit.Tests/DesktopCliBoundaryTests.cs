using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

[Collection("UserData environment")]
public sealed class DesktopCliBoundaryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaDesktopCli-" + Guid.NewGuid().ToString("N"));
    private readonly List<(string Operation, JsonElement Input, OperationContext Context)> requests = [];
    private string model = "Design";
    private long ticks = 200;
    private bool loseReply;
    private readonly AvevaSessionCandidate candidate = new() { ProcessId = 42, ProcessStartTimeUtcTicks = 200, PipeDetected = true, PipeName = "synthetic-only" };
    private readonly string? oldState;
    public DesktopCliBoundaryTests() { oldState = Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR"); Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", root); }

    [Theory]
    [InlineData("cli")]
    [InlineData("desktop")]
    [InlineData("net10")]
    [InlineData("manager")]
    public void SeparateRuntimeEntrypointsUseTheSamePackageRootForAdvisoryTrust(string component)
    {
        Assert.Equal(root, YuzuhaToolkitPaths.ResolveRoot(Path.Combine(root, "runtime", component), null));
        var explicitRoot = Path.Combine(root, "explicit");
        Assert.Equal(explicitRoot, YuzuhaToolkitPaths.ResolveRoot(Path.Combine(root, "runtime", component), explicitRoot));
        Assert.Equal(Path.Combine(root, "standalone"), YuzuhaToolkitPaths.ResolveRoot(Path.Combine(root, "standalone"), null));
    }
    private Task<CommunicationResponse> Call(string operation, JsonElement input, OperationContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); requests.Add((operation, input.Clone(), context));
        JsonElement data;
        if (operation == "aveva.sessions.list") data = JsonSerializer.SerializeToElement(new AvevaSessionListResponse { Count = 1, Sessions = [candidate] });
        else if (operation.StartsWith("aveva.")) data = JsonSerializer.SerializeToElement(new YuzuhaConnectionStatus { Connected = true, TargetVerified = true, SessionSelected = true,
            ExpectedProcessId = 42, ExpectedProcessStartTimeUtcTicks = 200, ExpectedModel = "Design", Host = new() { ProcessId = 42, ProcessStartTimeUtcTicks = ticks, Model = model, PipeName = "synthetic-only", FormQueryVersion = 1, BoundedArrayReadVersion = 1 } });
        else data = JsonSerializer.SerializeToElement(new { Success = true, RequestId = "synthetic", Items = new[] { new { Path = "CE.name", Value = "fixture", Depth = 1, Type = "STRING" } }, Unparsed = Array.Empty<string>(), UnparsedCount = 0 });
        var result = loseReply && operation.StartsWith("pml.") ? OperationResult.Fail(operation, 7, "CLI_TRANSPORT_FAILED", "synthetic reply loss", mayHaveExecuted: true) : OperationResult.Ok(operation, data);
        return Task.FromResult(new CommunicationResponse(1, Guid.NewGuid().ToString("N"), result, "synthetic"));
    }

    [Fact]
    public async Task DiscoveryWaitIsAsynchronousAndHoldsTheSameBusyGuardAsTargetOperations()
    {
        var pending = new TaskCompletionSource<CommunicationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new PmlFunctionRegistry(Path.Combine(root, "functions.json"));
        using var host = new McpWorkbenchHost(registry, new DesktopCliSession((_, _, _, _) => pending.Task));
        var workbench = new SessionWorkbench(host, registry); var task = workbench.DiscoverAsync();
        Assert.True(workbench.IsBusy); Assert.False(task.IsCompleted); Assert.Throws<InvalidOperationException>(() => workbench.Discover());
        pending.SetResult(await Call("aveva.sessions.list", JsonSerializer.SerializeToElement(new { }), new(), default));
        Assert.Single((await task).Sessions); Assert.False(workbench.IsBusy);
    }

    [Fact]
    public async Task DesktopSelectionPinsDiscoveredStartAndHostModuleForEveryLaterInvocation()
    {
        var channel = new DesktopCliSession(Call);
        await Assert.ThrowsAsync<InvalidOperationException>(() => channel.SelectAsync(42, null, default)); Assert.Empty(requests);
        using var host = new McpWorkbenchHost(new PmlFunctionRegistry(Path.Combine(root, "functions.json")), channel);
        Assert.Single(host.Discover().Sessions); await host.SelectAsync(42, null, default); await host.StatusAsync(default);
        await host.QueryAsync(new() { Kind = "shown-forms", StartIndex = 2, MaxItems = 3, MaxCharacters = 128 }, default);
        await host.ReadArrayAsync("Fixture", 3, 2, 128, default);
        Assert.Equal(new[] { "aveva.sessions.list", "aveva.session.select", "aveva.session.status", "pml.forms.list", "pml.array.window" }, requests.Select(r => r.Operation));
        Assert.All(requests.Skip(1), r => { Assert.Equal(42, r.Context.ProcessId); Assert.Equal(200, r.Context.ExpectedProcessStartTimeUtcTicks); });
        Assert.All(requests.Skip(2), r => Assert.Equal("Design", r.Context.ExpectedModel));
        Assert.False(requests[1].Context.AllowExecution); Assert.True(requests[3].Context.AllowExecution); Assert.Equal(2, requests[3].Input.GetProperty("startIndex").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusChangeDropsTargetAndCannotDispatchAgain(bool changedStart)
    {
        var channel = new DesktopCliSession(Call); await channel.DiscoverAsync(); await channel.SelectAsync(42, "Design", default);
        if (changedStart) ticks++; else model = "Paragon";
        await Assert.ThrowsAsync<InvalidOperationException>(() => channel.StatusAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await channel.InvokeAsync("pml.command.execute", new { pmlCommand = "never" }, default));
        Assert.DoesNotContain(requests, r => r.Operation.StartsWith("pml."));
    }

    [Fact]
    public async Task ObjectAndRegisteredFunctionUseExistingCliOperationsAndLostReplyDoesNotResend()
    {
        var channel = new DesktopCliSession(Call); var registry = new PmlFunctionRegistry(Path.Combine(root, "functions.json"));
        Directory.CreateDirectory(root); var source = Path.Combine(root, "Fixture.pmlfnc"); File.WriteAllText(source, "define function !!Fixture() is STRING\nreturn 'fixture'\nendfunction\n");
        registry.Register(source, "Fixture"); registry.SetPermission("Fixture", true);
        using var host = new McpWorkbenchHost(registry, channel); host.Discover(); await host.SelectAsync(42, "Design", default);
        Assert.Single((await host.ReadObjectAsync(new("current", "!!YuzuhaReadCurrentElement(1,1,'')"), default)).Items);
        Assert.True((await host.InvokeAsync("Fixture", [], default)).Success);
        loseReply = true; await Assert.ThrowsAsync<IOException>(() => host.InvokeAsync("Fixture", [], default));
        Assert.Equal(2, requests.Count(r => r.Operation == "pml.function.invoke"));
        Assert.True(requests.Single(r => r.Operation == "pml.array.read").Input.GetProperty("deleteGlobalVar").GetBoolean());
    }

    [Fact]
    public async Task DevelopmentAdapterKeepsExactTargetAndCollectsUnknownEvidence()
    {
        using var host = new DesktopDevelopmentHost(new(42, 200, "Design"), new DesktopCliSession(Call));
        Assert.Equal(new DevelopmentTarget(42, 200, "Design"), await host.VerifyAsync(default));
        Assert.Single((await host.ReadAsync("Fixture", default)).Items);
        loseReply = true; var reply = await host.ExecuteAsync("synthetic", default); Assert.False(reply.Success); Assert.True(reply.MayHaveExecuted);
        Assert.All(requests, r => { Assert.Equal(42, r.Context.ProcessId); Assert.Equal(200, r.Context.ExpectedProcessStartTimeUtcTicks); Assert.Equal("Design", r.Context.ExpectedModel); });
        Assert.Single(requests, r => r.Operation == "pml.command.execute");
    }
    public void Dispose() { Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", oldState); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
