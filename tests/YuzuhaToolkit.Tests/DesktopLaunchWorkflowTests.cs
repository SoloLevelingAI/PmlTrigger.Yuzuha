using System.Xml.Linq;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

/// <summary>桌面启动接线与共享协调语义；只使用合成目录，不启动产品或读取本机凭据。</summary>
public sealed class DesktopLaunchWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DesktopLaunchWorkflow-" + Guid.NewGuid().ToString("N"));
    private readonly LaunchProfileService profiles;
    private readonly string entry;
    private readonly string revision;
    public DesktopLaunchWorkflowTests()
    {
        Directory.CreateDirectory(root);
        entry = Path.Combine(root, "marine.bat"); File.WriteAllText(entry, "fixture-never-executed");
        File.WriteAllText(Path.Combine(root, "evars.bat"), "fixture-environment");
        profiles = new(new WorkspaceConfigurationStore(Path.Combine(root, "workspace")));
        profiles.Save(new() { Id = "desktop", Name = "合成配置", Product = "AM", Kind = "MarineBatch", Executable = entry,
            WorkingDirectory = root, Project = "APS", User = "SYSTEM", Module = "Outfitting" }, "missing", true);
        revision = profiles.Read().Revision;
    }
    private LaunchWorkflowService Service(Func<LaunchProfile, string, int> start) => new(profiles, Path.Combine(root, "receipts"), start);
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YuzuhaToolkit.Development.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    [Fact]
    public void WpfRoutesExecutionAndRecoveryThroughSingleCoordinator()
    {
        var desktop = Path.Combine(RepositoryRoot(), "src", "YuzuhaToolkit.Desktop");
        var window = File.ReadAllText(Path.Combine(desktop, "MainWindow.xaml.cs"));
        var workflow = File.ReadAllText(Path.Combine(desktop, "MainWindow.LaunchWorkflow.xaml.cs"));
        Assert.DoesNotContain("LaunchService.Start", window + workflow);
        Assert.DoesNotContain("_journal.Begin", workflow);
        Assert.Contains("LaunchWorkflowService", workflow);
        var xaml = XDocument.Load(Path.Combine(desktop, "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var controls = xaml.Descendants().Where(node => node.Attribute(x + "Name") is not null)
            .ToDictionary(node => (string)node.Attribute(x + "Name")!);
        Assert.Equal("ObserveLaunchWorkflow", (string?)controls["ObserveLaunchButton"].Attribute("Click"));
        Assert.True(controls.ContainsKey("LaunchOperationIdBox"));
        Assert.True(controls.ContainsKey("LaunchWorkflowResultText"));
    }

    [Fact]
    public async Task SameOperationReturnsSameAttemptAndNeverPersistsPassword()
    {
        var count = 0;
        var workflow = Service((_, _) => { count++; return int.MaxValue; });
        var plan = workflow.Plan("desktop", revision, "desktop-repeat");
        var receipt = await workflow.ExecuteAsync(plan, "fixture-secret", "desktop-request", true);
        var repeated = await Service((_, _) => throw new Exception("replay")).ExecuteAsync(plan, "", null, true);
        Assert.Equal(1, count); Assert.Equal(receipt.AttemptId, repeated.AttemptId);
        Assert.Equal("success", receipt.State); Assert.Equal("unverified", receipt.HostVerification);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "receipts"), "*.json", SearchOption.AllDirectories))
            Assert.DoesNotContain("fixture-secret", File.ReadAllText(file));
    }

    [Fact]
    public async Task UnknownAfterRestartCanOnlyBeObservedWithoutReplayingBoundary()
    {
        var workflow = Service((_, password) => throw new IOException(password));
        var receipt = await workflow.ExecuteAsync(workflow.Plan("desktop", revision, "desktop-unknown"), "fixture-secret", null, true);
        Assert.Equal("unknown", receipt.State);
        var observed = Service((_, _) => throw new Exception("replay")).Observe(receipt.OperationId);
        Assert.Equal(receipt.AttemptId, observed.AttemptId); Assert.Equal("unknown", observed.State);
        Assert.DoesNotContain("fixture-secret", File.ReadAllText(Path.Combine(root, "receipts", "desktop-unknown.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenFileOrRevisionChangeRejectsBeforeStarting(bool changeRevision)
    {
        var count = 0; var workflow = Service((_, _) => { count++; return 42; });
        var plan = workflow.Plan("desktop", revision, "desktop-changed");
        if (changeRevision)
        {
            var profile = profiles.ReadProfile("desktop").Profile; profile.Name = "changed";
            profiles.Save(profile, revision, true);
        }
        else File.AppendAllText(entry, "changed");
        if (changeRevision)
            await Assert.ThrowsAsync<ConfigurationConflictException>(() => workflow.ExecuteAsync(plan, "", null, true));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => workflow.ExecuteAsync(plan, "", null, true));
        Assert.Equal(0, count);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
