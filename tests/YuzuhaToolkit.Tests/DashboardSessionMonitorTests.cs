using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Mcp;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class DashboardSessionMonitorTests
{
    private sealed class Probe : IDashboardSessionProbe
    {
        internal AvevaSessionCandidate Candidate = new() { ProcessId = 42, ProcessStartTimeUtcTicks = 200, PipeDetected = true, PipeName = "host-42", Product = "E3D", Project = "TEST" };
        internal Func<CancellationToken, Task<AvevaSessionListResponse>>? Discovery;
        internal Func<AvevaSessionCandidate, string?, CancellationToken, Task<DashboardProbeReply>>? Inspection;
        internal int Calls;
        internal string? LastExpectedModule;
        public Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token) => Discovery?.Invoke(token) ?? Task.FromResult(new AvevaSessionListResponse { Sessions = [Candidate], Count = 1 });
        public Task<DashboardProbeReply> ProbeAsync(AvevaSessionCandidate candidate, string? expectedModule, CancellationToken token)
        { Calls++; LastExpectedModule = expectedModule; return Inspection?.Invoke(candidate, expectedModule, token) ?? Task.FromResult(Reply(candidate)); }
        internal static DashboardProbeReply Reply(AvevaSessionCandidate c, string module = "DESIGN", int form = 1, int array = 1) => new(1, new()
        {
            Connected = true, TargetVerified = true, ExpectedProcessId = c.ProcessId, ExpectedProcessStartTimeUtcTicks = c.ProcessStartTimeUtcTicks, ExpectedModel = module, PipeName = c.PipeName,
            Host = new() { ProcessId = c.ProcessId, ProcessStartTimeUtcTicks = c.ProcessStartTimeUtcTicks, PipeName = c.PipeName, Model = module, FormQueryVersion = form, BoundedArrayReadVersion = array }
        });
    }
    [Fact]
    public async Task EmptyAndNoHostStayNonGreenAndNeverProbeAnAbsentPipe()
    {
        var probe = new Probe { Discovery = _ => Task.FromResult(new AvevaSessionListResponse { Sessions = [], Count = 0 }) };
        using var monitor = new DashboardSessionMonitor(probe); await monitor.PollAsync();
        Assert.Empty(monitor.Snapshot.Sessions); Assert.Equal(DashboardSessionState.None, monitor.Snapshot.State);
        probe.Discovery = null; probe.Candidate.PipeDetected = false; await monitor.PollAsync();
        Assert.Equal(DashboardSessionState.Detected, Assert.Single(monitor.Snapshot.Sessions).State); Assert.Equal(0, probe.Calls);
    }
    [Theory]
    [InlineData("pid")]
    [InlineData("ticks")]
    [InlineData("pipe")]
    [InlineData("empty-module")]
    [InlineData("expected")]
    [InlineData("wire")]
    [InlineData("form")]
    [InlineData("array")]
    public async Task GreenRequiresEveryIdentityAndProtocolField(string changed)
    {
        var probe = new Probe(); probe.Inspection = (c, _, _) =>
        {
            var r = Probe.Reply(c); var host = r.Status.Host!;
            switch (changed) { case "pid": host.ProcessId++; break; case "ticks": host.ProcessStartTimeUtcTicks++; break; case "pipe": host.PipeName = "different"; break; case "empty-module": host.Model = ""; break; case "expected": r.Status.ExpectedProcessId++; break; case "wire": r = r with { ProtocolVersion = 2 }; break; case "form": host.FormQueryVersion = 0; break; case "array": host.BoundedArrayReadVersion = 0; break; }
            return Task.FromResult(r);
        };
        using var monitor = new DashboardSessionMonitor(probe); await monitor.PollAsync(); Assert.Equal(0, monitor.Snapshot.AvailableCount);
    }
    [Fact]
    public async Task ModuleAndPipeArePinnedWhileRecoveryStillRequiresFreshMatchingEvidence()
    {
        var probe = new Probe(); using var monitor = new DashboardSessionMonitor(probe); await monitor.PollAsync(); Assert.Equal(1, monitor.Snapshot.AvailableCount);
        probe.Inspection = (c, _, _) => Task.FromResult(Probe.Reply(c, "PARAGON")); await monitor.PollAsync(); Assert.Equal("DESIGN", probe.LastExpectedModule); Assert.Equal(DashboardSessionState.Failed, Assert.Single(monitor.Snapshot.Sessions).State);
        probe.Inspection = null; await monitor.PollAsync(); Assert.Equal(1, monitor.Snapshot.AvailableCount);
        probe.Candidate.PipeName = "changed"; var calls = probe.Calls; await monitor.PollAsync(); Assert.Equal(0, monitor.Snapshot.AvailableCount); Assert.Equal(calls, probe.Calls);
    }
    [Fact]
    public async Task DisappearancePidReuseAndTransportFailureCannotReuseOldGreen()
    {
        var probe = new Probe(); using var monitor = new DashboardSessionMonitor(probe); await monitor.PollAsync();
        probe.Discovery = _ => Task.FromResult(new AvevaSessionListResponse { Sessions = [], Count = 0 }); await monitor.PollAsync(); Assert.False(Assert.Single(monitor.Snapshot.Sessions).IsAvailable);
        probe.Discovery = null; probe.Candidate.ProcessStartTimeUtcTicks++; probe.Candidate.PipeDetected = false; await monitor.PollAsync(); Assert.Equal(0, monitor.Snapshot.AvailableCount); Assert.Contains(monitor.Snapshot.Sessions, row => row.StartTicks == 200 && !row.Discovered);
        probe.Candidate.ProcessStartTimeUtcTicks = 200; probe.Candidate.PipeDetected = true; probe.Inspection = (_, _, _) => throw new IOException("fixture lost Host"); await monitor.PollAsync(); Assert.Equal(DashboardSessionState.Failed, Assert.Single(monitor.Snapshot.Sessions).State);
    }
    [Fact]
    public async Task ExpiryAndClockRollbackDowngradeEvenWithoutAnotherPoll()
    {
        var probe = new Probe(); var time = DateTimeOffset.UtcNow; using var monitor = new DashboardSessionMonitor(probe, () => time); await monitor.PollAsync();
        time = time.AddSeconds(11); Assert.Equal(DashboardSessionState.Expired, Assert.Single(monitor.Snapshot.Sessions).State);
        time = time.AddSeconds(-20); Assert.Equal(0, monitor.Snapshot.AvailableCount);
    }
    [Fact]
    public async Task SingleFlightTimeoutAndLateCompletionDoNotOverwriteNewGeneration()
    {
        var pending = new TaskCompletionSource<DashboardProbeReply>(TaskCreationOptions.RunContinuationsAsynchronously); var probe = new Probe { Inspection = (_, _, _) => pending.Task };
        using var monitor = new DashboardSessionMonitor(probe, timeout: TimeSpan.FromMilliseconds(40)); var first = monitor.PollAsync();
        Assert.False(await monitor.PollAsync()); await first; Assert.Equal(0, monitor.Snapshot.AvailableCount);
        probe.Candidate.PipeDetected = false; await monitor.PollAsync(); var generation = monitor.Snapshot.Generation; pending.SetResult(Probe.Reply(probe.Candidate)); await Task.Yield();
        Assert.Equal(generation, monitor.Snapshot.Generation); Assert.Equal(0, monitor.Snapshot.AvailableCount);
    }
    [Fact]
    public async Task MultipleCandidatesAreIndependentlyVerifiedAndDisposeRejectsLateEvidence()
    {
        var probe = new Probe(); var second = new AvevaSessionCandidate { ProcessId = 43, ProcessStartTimeUtcTicks = 300, PipeName = "host-43", PipeDetected = true, Product = "E3D" };
        probe.Discovery = _ => Task.FromResult(new AvevaSessionListResponse { Sessions = [probe.Candidate, second], Count = 2 });
        using var monitor = new DashboardSessionMonitor(probe); await monitor.PollAsync(); Assert.Equal(2, monitor.Snapshot.AvailableCount); Assert.Null(probe.LastExpectedModule);
        var waiting = new TaskCompletionSource<DashboardProbeReply>(TaskCreationOptions.RunContinuationsAsynchronously); probe.Inspection = (_, _, _) => waiting.Task; var poll = monitor.PollAsync(); monitor.Dispose(); waiting.TrySetResult(Probe.Reply(probe.Candidate)); await poll;
        Assert.False(await monitor.PollAsync()); Assert.False(monitor.IsPolling);
    }
}
