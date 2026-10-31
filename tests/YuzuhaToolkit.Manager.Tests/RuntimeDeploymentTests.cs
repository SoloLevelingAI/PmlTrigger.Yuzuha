using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class RuntimeDeploymentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaRuntimeTests-" + Guid.NewGuid().ToString("N"));
    private string Package => Path.Combine(root, "PmlTrigger.Package");
    private string Target => Path.Combine(root, "YuzuhaRuntime");
    private string Installation => Path.Combine(root, "aveva");
    private string State => Path.Combine(root, "state");
    private string EnvironmentFile => Path.Combine(Installation, "evars.init");
    private const string Bootstrap = "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc";
    public RuntimeDeploymentTests()
    {
        Write(Installation, "mon.exe", "synthetic entry, never executed");
        Write(Installation, "evars.init", "@echo off\nset pmllib=vendor\nset pmlui=vendor\n");
        Write(Package, Bootstrap, "define function !!YuzuhaResolveRuntimePath()\n  !folderName = 'PMLTRI'\nendfunction\n");
        Write(Package, "PMLUI/bootstrap.pmlfnc", "synthetic PML, never executed");
        Write(Package, "runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll", "synthetic Host, never loaded");
        Write(Package, "runtime/cli/PmlTwin.Phayton.Cli.exe", "synthetic CLI, never executed");
        Manifest();
    }
    private static string Write(string directory, string name, string text)
    { var file = Path.Combine(directory, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }
    private void Manifest()
    {
        var files = Directory.GetFiles(Package, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != ".setup-payload.json")
            .Select(f => new { path = Path.GetRelativePath(Package, f).Replace('\\', '/'), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant() }).ToArray();
        File.WriteAllText(Path.Combine(Package, ".setup-payload.json"), JsonSerializer.Serialize(new { version = "synthetic", files }));
    }
    private RuntimeDeploymentRequest Request(string mode = "copy") => new(Package, mode == "copy" ? Target : Package,
        mode, Installation, "E3D", "E3D2.1", "net48", CodePage: 65001);
    private RuntimeDeploymentService Service(Action<string>? phase = null) => new(State, Path.Combine(root, "user"), phase);

    [Fact]
    public void DeploymentFreezesInstallationEntryAndDiscoveryIdentityBeforeWriting()
    {
        var service = Service(); var plan = service.Preview(Request());
        Assert.Equal(plan.Installation.Id, plan.Request.ExpectedInstallationId);
        Assert.Equal(EnvironmentFile, plan.Installation.EnvironmentFile);
        File.AppendAllText(Path.Combine(Installation, "mon.exe"), "replacement installation");
        Assert.Throws<IOException>(() => service.Apply(plan)); Assert.False(Directory.Exists(Target));
        Assert.Throws<IOException>(() => service.Preview(Request() with { ExpectedInstallationId = plan.Installation.Id }));
    }

    [Fact]
    public void CopyAdaptsCustomFolderWithoutChangingPackageAndRestoresWholeTransaction()
    {
        var before = File.ReadAllBytes(EnvironmentFile); var source = File.ReadAllBytes(Path.Combine(Package, Bootstrap));
        var service = Service(); var plan = service.Preview(Request());
        Assert.False(Directory.Exists(Target)); Assert.Equal(before, File.ReadAllBytes(EnvironmentFile));
        Assert.Equal("YUZUHA", plan.BootstrapToken);
        var receipt = service.Apply(plan);
        Assert.Equal(source, File.ReadAllBytes(Path.Combine(Package, Bootstrap)));
        Assert.Contains("'YUZUHA'", File.ReadAllText(Path.Combine(Target, Bootstrap)));
        Assert.Contains(Target, File.ReadAllText(EnvironmentFile));
        Assert.True(File.Exists(Path.Combine(Target, ".yuzuha-runtime.json")));
        Assert.Empty(service.Restore(receipt).RecoveryErrors);
        Assert.Equal(before, File.ReadAllBytes(EnvironmentFile));
        Assert.False(File.Exists(Path.Combine(Target, Bootstrap)));
        Assert.False(File.Exists(Path.Combine(Target, ".yuzuha-runtime.json")));
    }

    [Fact]
    public void ReferenceModeOnlyUpdatesEnvironment()
    {
        var before = File.ReadAllBytes(EnvironmentFile); var source = File.ReadAllBytes(Path.Combine(Package, Bootstrap));
        var service = Service(); var plan = service.Preview(Request("reference"));
        Assert.Null(plan.Copy); Assert.Equal("PMLTRI", plan.BootstrapToken);
        var receipt = service.Apply(plan);
        Assert.Null(receipt.CopyReceipt);
        Assert.Equal(source, File.ReadAllBytes(Path.Combine(Package, Bootstrap)));
        Assert.False(Directory.Exists(Target));
        Assert.Empty(service.Restore(receipt).RecoveryErrors);
        Assert.Equal(before, File.ReadAllBytes(EnvironmentFile));
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("ownership")]
    [InlineData("receipt")]
    public void FailureAfterCopyOrEnvironmentRestoresAppliedChanges(string failAt)
    {
        var before = File.ReadAllBytes(EnvironmentFile);
        var observedCopy = false;
        var service = Service(phase => {
            if (phase != failAt) return;
            observedCopy = File.Exists(Path.Combine(Target, Bootstrap));
            throw new IOException("Synthetic failure at " + phase);
        });
        var error = Assert.Throws<RuntimeDeploymentException>(() => service.Apply(service.Preview(Request())));
        Assert.True(observedCopy); Assert.Equal(failAt, error.Diagnostic.Phase);
        Assert.Empty(error.Diagnostic.RecoveryErrors);
        Assert.Equal(before, File.ReadAllBytes(EnvironmentFile));
        Assert.False(File.Exists(Path.Combine(Target, Bootstrap)));
        Assert.False(File.Exists(Path.Combine(Target, ".yuzuha-runtime.json")));
    }

    [Fact]
    public void UpgradesRefuseLocalEditsUnknownTargetsAndSourceTampering()
    {
        var service = Service(); var plan = service.Preview(Request());
        Write(Package, "runtime/cli/PmlTwin.Phayton.Cli.exe", "tampered");
        Assert.Throws<IOException>(() => service.Apply(plan));
        Manifest();
        var receipt = service.Apply(service.Preview(Request()));
        Write(Target, Bootstrap, "user edited source");
        Assert.Throws<IOException>(() => service.Preview(Request()));
        Assert.NotEmpty(service.Restore(receipt).RecoveryErrors);
        Assert.Equal("user edited source", File.ReadAllText(Path.Combine(Target, Bootstrap)));
        var unknown = Path.Combine(root, "unowned"); Write(unknown, "notes.txt", "keep");
        Assert.Throws<IOException>(() => service.Preview(Request() with { RuntimeRoot = unknown }));
    }

    [Fact]
    public void DatabasePayloadAndWrongProfileAreRejected()
    {
        Write(Package, "PMLLIB/private.sqlite3", "user data"); Manifest();
        Assert.Throws<IOException>(() => Service().Preview(Request()));
        Assert.Throws<ArgumentException>(() => Service().Preview(Request() with { Framework = "net35" }));
    }

    [Fact]
    public void EnvironmentLockAfterPayloadCopyRollsBackTheCopiedFiles()
    {
        FileStream? held = null;
        var before = File.ReadAllBytes(EnvironmentFile);
        try
        {
            var service = Service(phase => { if (phase == "environment") held = new FileStream(EnvironmentFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            var plan = service.Preview(Request());
            var failed = Assert.Throws<RuntimeDeploymentException>(() => service.Apply(plan));
            Assert.Equal("environment", failed.Diagnostic.Phase);
            Assert.Empty(failed.Diagnostic.RecoveryErrors);
            Assert.False(File.Exists(Path.Combine(Target, Bootstrap)));
        }
        finally { held?.Dispose(); }
        Assert.Equal(before, File.ReadAllBytes(EnvironmentFile));
    }

    [Fact]
    public void APartialInnerFileRecoveryCannotBeReportedAsFullyRestored()
    {
        var service = Service(phase => { if (phase == "copy") throw new YuzuhaToolkit.Workspace.DeploymentApplyException("synthetic partial copy", State,
            ["unresolved fixture file"], new IOException("synthetic")); });
        var failed = Assert.Throws<RuntimeDeploymentException>(() => service.Apply(service.Preview(Request())));
        Assert.Contains(failed.Diagnostic.RecoveryErrors, e => e.Contains("unresolved fixture file", StringComparison.Ordinal));
        Assert.Contains("恢复待处理", failed.Message);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
