using YuzuhaToolkit.Workspace;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class LaunchProfileCatalogTests
{
    [Fact]
    public void SearchCombinesProjectPathProductAndFavoriteFilters()
    {
        LaunchProfile[] profiles = [
            new() { Name = "日常设计", Project = "工厂一", Product = "E3D", Executable = @"D:\AVEVA\mon.exe", IsFavorite = true },
            new() { Name = "结构检查", Project = "工厂一", Product = "PDMS" },
            new() { Name = "沙箱", Product = "E3D" }
        ];
        Assert.Same(profiles[0], Assert.Single(LaunchProfileCatalog.Select(profiles, "工厂", "e3d", "favorites")));
        Assert.Same(profiles[0], Assert.Single(LaunchProfileCatalog.Select(profiles, @"aveva\MON", null, "all")));
        Assert.Empty(LaunchProfileCatalog.Select(profiles, "不存在", null, "all"));
    }

    [Fact]
    public void RecentUsesActualStartHistoryAndOrdersNewestFirst()
    {
        var yesterday = new LaunchProfile { Name = "昨天", LastStartedUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        var today = new LaunchProfile { Name = "今天", LastStartedUtc = DateTimeOffset.UtcNow };
        var never = new LaunchProfile { Name = "未启动", IsDefault = true };
        Assert.Equal(new[] { today, yesterday }, LaunchProfileCatalog.Select([yesterday, never, today], "", null, "recent"));
        Assert.Same(never, LaunchProfileCatalog.Select([yesterday, never, today], "", null, "all")[0]);
    }

    [Fact]
    public void CopyHasIndependentEnvironmentAndNeverInheritsCredentialsDefaultOrHistory()
    {
        var original = new LaunchProfile { Name = "日常", CredentialKey = "Yuzuha/credential", IsDefault = true, IsFavorite = true, LastStartedUtc = DateTimeOffset.UtcNow, Environment = new() { ["PROJECT_ROOT"] = "one" } };
        var copy = LaunchProfileCatalog.Duplicate(original);
        Assert.NotEqual(original.Id, copy.Id); Assert.Empty(copy.CredentialKey); Assert.False(copy.IsDefault); Assert.False(copy.IsFavorite); Assert.Null(copy.LastStartedUtc);
        copy.Environment["PROJECT_ROOT"] = "two";
        Assert.Equal("one", original.Environment["PROJECT_ROOT"]);
    }
}
