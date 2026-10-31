using System.Text;
using Xunit;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class DeploymentSelectionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaSelectedPayload-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(root, "source");
    private string Target => Path.Combine(root, "target");
    private string Write(string basePath, string relative, string text)
    { var file = Path.Combine(basePath, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }

    [Fact]
    public void ExplicitRuntimeFilesAndBootstrapTransformationRetainSourceAndCanRestore()
    {
        var source = Write(Source, "runtime/cli/bridge.exe", "synthetic EXE; never executed");
        var bootstrap = Write(Source, "PMLLIB/Bootstrap/resolve.pmlfnc", "!folderName = 'PMLTRI'\n");
        Write(Target, "runtime/cli/bridge.exe", "old version");
        var plan = PluginDeployment.PreviewSelection(Source, Target, ["runtime/cli/bridge.exe", "PMLLIB/Bootstrap/resolve.pmlfnc"]);
        plan = plan with { Files = plan.Files.Select(f => f.RelativePath.EndsWith(".pmlfnc")
            ? f with { ReplacementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("!folderName = 'YUZUHA'\n")) } : f).ToArray() };
        var receipt = PluginDeployment.Apply(plan, Path.Combine(root, "state"));
        Assert.Equal(File.ReadAllText(source), File.ReadAllText(Path.Combine(Target, "runtime/cli/bridge.exe")));
        Assert.Contains("YUZUHA", File.ReadAllText(Path.Combine(Target, "PMLLIB/Bootstrap/resolve.pmlfnc")));
        Assert.Contains("PMLTRI", File.ReadAllText(bootstrap));
        var restored = PluginDeployment.Restore(plan, receipt);
        Assert.Empty(restored.Unresolved);
        Assert.Equal("old version", File.ReadAllText(Path.Combine(Target, "runtime/cli/bridge.exe")));
        Assert.False(File.Exists(Path.Combine(Target, "PMLLIB/Bootstrap/resolve.pmlfnc")));
        Assert.Empty(PluginDeployment.Restore(plan, receipt).Unresolved);
    }

    [Fact]
    public void ChangedSourcesTargetsAndTraversalAreRejected()
    {
        var file = Write(Source, "one.dll", "one");
        var plan = PluginDeployment.PreviewSelection(Source, Target, ["one.dll"]);
        File.WriteAllText(file, "changed");
        Assert.Throws<IOException>(() => PluginDeployment.Apply(plan, Path.Combine(root, "state")));
        Assert.Throws<InvalidDataException>(() => PluginDeployment.PreviewSelection(Source, Target, ["../escape.dll"]));
        var fresh = PluginDeployment.PreviewSelection(Source, Target, ["one.dll"]);
        Write(Target, "one.dll", "external");
        Assert.Throws<IOException>(() => PluginDeployment.Apply(fresh, Path.Combine(root, "state")));
        Assert.Equal("external", File.ReadAllText(Path.Combine(Target, "one.dll")));
    }

    [Fact]
    public void RestoreKeepsAnExternalEditAndReportsTheUnresolvedFile()
    {
        Write(Source, "one.dll", "new"); Write(Target, "one.dll", "old");
        var plan = PluginDeployment.PreviewSelection(Source, Target, ["one.dll"]);
        var receipt = PluginDeployment.Apply(plan, Path.Combine(root, "state"));
        Write(Target, "one.dll", "external");
        var restored = PluginDeployment.Restore(plan, receipt);
        Assert.Single(restored.Unresolved);
        Assert.Equal("external", File.ReadAllText(Path.Combine(Target, "one.dll")));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
