using Xunit;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class DesktopActivityFilterTests
{
    private static ActivityEntry Row(string call, string tool, string phase, int? target = 42) =>
        new(DateTimeOffset.UnixEpoch, "fixture", 7, call, tool, phase, 12, target, "{}", "selection");

    [Fact]
    public void ConditionsIntersectWithoutChangingSourceOrOrdering()
    {
        var rows = new[] { Row("A", "read", "started"), Row("B", "read", "succeeded"), Row("C", "write", "succeeded") };
        Assert.Same(rows[1], Assert.Single(DesktopActivityFilter.Apply(rows, "succeeded", "read", "SELECTION")));
        Assert.Empty(DesktopActivityFilter.Apply(rows, "succeeded", "read", "no-match"));
        Assert.Equal(rows, DesktopActivityFilter.Apply(rows, "all", null, "  "));
        Assert.Equal(3, rows.Length);
    }

    [Fact]
    public void UnknownIncludesLostRepliesAndIdentityIsSearchable()
    {
        var rows = new[] { Row("pending", "read", "cancelled-unknown"), Row("failed", "read", "failed"), Row("unknown", "write", "unknown", null) };
        Assert.Equal(new[] { rows[0], rows[2] }, DesktopActivityFilter.Apply(rows, "unknown", null, ""));
        Assert.Same(rows[0], Assert.Single(DesktopActivityFilter.Apply(rows, "unknown", "read", "42")));
        Assert.Same(rows[2], Assert.Single(DesktopActivityFilter.Apply(rows, "all", null, "UNKNOWN")));
        Assert.Empty(DesktopActivityFilter.Apply([], "all", null, null));
    }
}
