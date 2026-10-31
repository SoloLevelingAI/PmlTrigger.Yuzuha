using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Tests;

public sealed class CommunicationProtocolTests
{
    [Theory]
    [InlineData(2, "aveva.sessions.list", false)]
    [InlineData(1, "launch.profile.start", false)]
    [InlineData(1, "aveva.session.status", true)]
    public async Task InvalidProtocolOrBarePidIsRejectedBeforeAnyHostCall(int version, string operation, bool pid)
    {
        var request = new CommunicationRequest(version, Guid.NewGuid().ToString("N"), operation,
            JsonSerializer.Deserialize<JsonElement>("{}"), pid ? new(42) : new());
        using var input = new StringReader(JsonSerializer.Serialize(request, CommunicationJsonContext.Default.CommunicationRequest));
        using var output = new StringWriter();
        Assert.Equal(2, await CommunicationCliEndpoint.RunAsync(input, output));
        var response = JsonSerializer.Deserialize(output.ToString(), CommunicationJsonContext.Default.CommunicationResponse)!;
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal("INVALID_PROTOCOL_REQUEST", response.Result.Error?.Code);
        Assert.False(response.Result.Error?.MayHaveExecuted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ProtocolExecutionRequiresAnExplicitModuleEvenWithValidStartTicks(string? model)
    {
        var request = new CommunicationRequest(1, Guid.NewGuid().ToString("N"), "pml.command.execute",
            JsonSerializer.Deserialize<JsonElement>("{\"pmlCommand\":\"synthetic-never-executed\"}"),
            new(42, model, AllowExecution: true, ExpectedProcessStartTimeUtcTicks: 200));
        using var input = new StringReader(JsonSerializer.Serialize(request, CommunicationJsonContext.Default.CommunicationRequest));
        using var output = new StringWriter();
        Assert.Equal(5, await CommunicationCliEndpoint.RunAsync(input, output));
        var response = JsonSerializer.Deserialize(output.ToString(), CommunicationJsonContext.Default.CommunicationResponse)!;
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal("MODULE_IDENTITY_REQUIRED", response.Result.Error?.Code);
        Assert.False(response.Result.Error?.MayHaveExecuted);
    }

    [Theory]
    [InlineData("pml.command.execute", "{\"pmlCommand\":\"synthetic-never-executed\"}")]
    [InlineData("pml.array.read", "{\"pmlCommand\":\"synthetic-never-executed\",\"globalVar\":\"Fixture\",\"deleteGlobalVar\":false}")]
    [InlineData("pml.array.window", "{\"globalVar\":\"Fixture\"}")]
    [InlineData("pml.forms.list", "{}")]
    [InlineData("pml.gadgets.list", "{\"formName\":\"Fixture\"}")]
    [InlineData("pml.gadget.read", "{\"formName\":\"Fixture\",\"gadgetName\":\"Apply\"}")]
    [InlineData("pml.function.invoke", "{\"functionName\":\"UnregisteredFixture\",\"parameters\":[]}")]
    [InlineData("pml.undo.begin", "{}")]
    [InlineData("pml.undo.end", "{}")]
    public async Task EveryPinnedExecutionPathRejectsAMissingModuleBeforeConnecting(string operation, string json)
    {
        var connections = new RejectConnections();
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), connections);
        var executor = CommunicationOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), bridge.CreateIndependent);
        var result = await executor.InvokeAsync(operation, JsonSerializer.Deserialize<JsonElement>(json),
            new(42, AllowExecution: true, ExpectedProcessStartTimeUtcTicks: 200));
        Assert.Equal(5, result.ExitCode);
        Assert.Equal("MODULE_IDENTITY_REQUIRED", result.Error?.Code);
        Assert.False(result.Error?.MayHaveExecuted);
        Assert.Equal(0, connections.Calls);
    }

    [Fact]
    public async Task ReadOnlySelectionCanLearnTheModuleWhilePinnedExecutionAndLegacyCallsKeepTheirBoundaries()
    {
        var service = new ChangingService { AllowCommands = true };
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), new ChangingConnections(service));
        var executor = CommunicationOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), bridge.CreateIndependent);
        var empty = JsonSerializer.Deserialize<JsonElement>("{}");
        var command = JsonSerializer.Deserialize<JsonElement>("{\"pmlCommand\":\"synthetic-recorded-only\"}");
        var initial = new OperationContext(42, ExpectedProcessStartTimeUtcTicks: 200);
        Assert.True((await executor.InvokeAsync("aveva.session.select", empty, initial)).Success);
        Assert.True((await executor.InvokeAsync("aveva.session.status", empty, initial)).Success);
        service.Model = "Paragon";
        var unpinned = await executor.InvokeAsync("pml.command.execute", command, initial with { AllowExecution = true });
        Assert.Equal("MODULE_IDENTITY_REQUIRED", unpinned.Error?.Code);
        var stale = await executor.InvokeAsync("pml.command.execute", command, initial with { AllowExecution = true, ExpectedModel = "Design" });
        Assert.Equal(5, stale.ExitCode);
        Assert.Equal(0, service.Commands);
        Assert.True((await executor.InvokeAsync("pml.command.execute", command, initial with { AllowExecution = true, ExpectedModel = "Paragon" })).Success);
        Assert.True((await executor.InvokeAsync("pml.command.execute", command, new(42, AllowExecution: true))).Success);
        Assert.Equal(2, service.Commands);
    }

    [Fact]
    public async Task FrozenStartIdentityIsCheckedBeforeConnectingAndInheritedByIndependentBridge()
    {
        var connections = new RejectConnections();
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), connections)
        { RequiredProcessStartTimeUtcTicks = 100 };
        using var independent = bridge.CreateIndependent();
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => independent.SelectSessionAsync(42, "Design", default));
        Assert.Contains("AVEVA_PROCESS_IDENTITY_CHANGED", rejected.Message);
        Assert.Equal(0, connections.Calls);
        using var legacy = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), connections);
        await Assert.ThrowsAsync<InvalidOperationException>(() => legacy.SelectSessionAsync(42, "Design", default, 100));
        Assert.Equal(0, connections.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HostStartOrModuleChangeClearsVerifiedStateAndCannotDispatch(bool changedStart)
    {
        var service = new ChangingService();
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new Discovery(), new ChangingConnections(service))
        { RequiredProcessStartTimeUtcTicks = 200 };
        Assert.True((await bridge.SelectSessionAsync(42, "Design", default, 200)).TargetVerified);
        if (changedStart) service.StartTicks = 201; else service.Model = "Paragon";
        Assert.False((await bridge.GetConnectionStatusAsync(default)).TargetVerified);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.RunPmlCommandAsync("synthetic-never-executed", default));
        Assert.Equal(0, service.Commands);
        using var nextInvocation = bridge.CreateIndependent();
        await Assert.ThrowsAsync<InvalidOperationException>(() => nextInvocation.SelectSessionAsync(42, "Design", default, 200));
        Assert.Equal(0, service.Commands);
    }

    private sealed class ChangingConnections(ChangingService service) : IPmlRpcConnectionFactory
    {
        public IPmlRpcConnection Connect(string pipeName) => new Connection(service);
        private sealed class Connection(ChangingService service) : IPmlRpcConnection
        { public IPmlCommandService Service => service; public void Dispose() { } }
    }
    private sealed class ChangingService : IPmlCommandService
    {
        public long StartTicks = 200;
        public string Model = "Design";
        public int Commands;
        public bool AllowCommands;
        public Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token) => Task.FromResult(new HostIdentityResponse
        { ProcessId = 42, ProcessStartTimeUtcTicks = StartTicks, Model = Model, PipeName = "synthetic" });
        public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token)
        {
            Commands++;
            if (!AllowCommands) throw new Exception("must never dispatch");
            return Task.FromResult(new RunPmlCommandResponse { Success = true, Code = "OK", PmlCommand = request.PmlCommand });
        }
    }

    private sealed class Discovery : IAvevaSessionDiscovery
    {
        public AvevaSessionListResponse Discover() => new();
        public AvevaSessionCandidate RequireCandidate(int processId) => new()
        { ProcessId = processId, ProcessStartTimeUtcTicks = 200, PipeDetected = true, PipeName = "synthetic" };
    }
    private sealed class RejectConnections : IPmlRpcConnectionFactory
    {
        public int Calls { get; private set; }
        public IPmlRpcConnection Connect(string pipeName) { Calls++; throw new Exception("No connection should be attempted."); }
    }
}
