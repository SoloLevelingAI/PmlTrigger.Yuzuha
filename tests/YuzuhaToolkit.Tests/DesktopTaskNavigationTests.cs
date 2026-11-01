using Xunit;
using YuzuhaToolkit.Desktop;

namespace YuzuhaToolkit.Tests;

public sealed class DesktopTaskNavigationTests
{
    private static DashboardSessionHealth Session(DashboardSessionState state, bool discovered = true) => new(42, 100, "E3D", "TEST", "pipe-42", "DESIGN", state, "fixture", DateTimeOffset.UtcNow, state == DashboardSessionState.Available ? DateTimeOffset.UtcNow : null, discovered);
    [Fact]
    public void NoProcessOffersStartEvenAfterOldProcessDisappeared()
    {
        Assert.Equal(DesktopDashboardAction.Start, DesktopTaskNavigation.Decide(DashboardHealthSnapshot.Empty, false));
        Assert.Equal(DesktopDashboardAction.Start, DesktopTaskNavigation.Decide(new(1, DateTimeOffset.UtcNow, [Session(DashboardSessionState.Failed, false)]), false));
    }
    [Theory]
    [InlineData(DashboardSessionState.Detected)]
    [InlineData(DashboardSessionState.Checking)]
    [InlineData(DashboardSessionState.Failed)]
    [InlineData(DashboardSessionState.Expired)]
    internal void ProcessWithoutUsableHostOffersDiagnosisNotImplicitExecution(DashboardSessionState state) => Assert.Equal(DesktopDashboardAction.Diagnose, DesktopTaskNavigation.Decide(new(1, DateTimeOffset.UtcNow, [Session(state)]), false));
    [Fact]
    public void DiscoveryFailureDoesNotPretendNoProcessOrOfferNewLaunch()
    {
        Assert.Equal(DesktopDashboardAction.Diagnose, DesktopTaskNavigation.Decide(new(1, DateTimeOffset.UtcNow, [], "discovery failed"), false));
    }
    [Fact]
    public void AvailableHostStillRequiresExplicitTargetAndNeverSelectsFirstCandidate()
    {
        var snapshot = new DashboardHealthSnapshot(1, DateTimeOffset.UtcNow, [Session(DashboardSessionState.Available), Session(DashboardSessionState.Available) with { ProcessId = 43 }]);
        Assert.Equal(DesktopDashboardAction.SelectTarget, DesktopTaskNavigation.Decide(snapshot, false));
        Assert.Equal(DesktopDashboardAction.Tasks, DesktopTaskNavigation.Decide(snapshot, true));
        Assert.Equal(2, snapshot.Sessions.Length);
    }
}
