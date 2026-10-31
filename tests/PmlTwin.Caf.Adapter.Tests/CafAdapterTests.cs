using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PmlTwin.Caf.Adapter;
using Xunit;

public sealed class CafAdapterTests
{
    [Fact]
    public void LifecycleRepeatedStartStopKeepsSnapshotsAndNeverRequestsLaunch()
    {
        var lifecycle = new CafLifecycle();
        Assert.False(lifecycle.Describe().Started);
        lifecycle.Stop();
        lifecycle.Start("1.2.2.0", "1.2.2.0", "17c64733a9775004", true);
        var snapshot = lifecycle.Describe();
        lifecycle.Start("1.2.2.0", "1.2.2.0", "17c64733a9775004", true);
        lifecycle.Stop(); lifecycle.Stop();
        Assert.True(snapshot.Started);
        Assert.False(lifecycle.Describe().Started);
        Assert.False(snapshot.AutomaticManagerLaunch);
        Assert.True(snapshot.ReadOnly);
    }

    [Theory]
    [InlineData("4.0.0.0", "17c64733a9775004")]
    [InlineData("1.2.2.0", "0000000000000000")]
    public void IncompatibleOrSubstitutedSdkRefusesBeforeStart(string version, string token)
    {
        var lifecycle = new CafLifecycle();
        Assert.Throws<NotSupportedException>(() => lifecycle.Start("1.2.2.0", version, token, true));
        Assert.False(lifecycle.Describe().Started);
    }

    [Fact]
    public void MissingHostServicesRefusesAndKeepsEntryInactive()
    {
        var lifecycle = new CafLifecycle();
        Assert.Throws<ArgumentNullException>(() => lifecycle.Start("1.2.2.0", "1.2.2.0", "17c64733a9775004", false));
        Assert.False(lifecycle.Describe().Started);
    }

    [Theory]
    [InlineData("E3D21", "1.2.2.0")]
    [InlineData("E3D31", "4.0.0.0")]
    [InlineData("AM121", "1.1.20.0")]
    public void RealAdapterMetadataUsesVendorInterfaceWithoutDatabaseOrDeploymentDependencies(string profile, string version)
    {
        var root = FindRoot();
        var file = Path.Combine(root, "src/PmlTwin.Caf.Adapter/bin/Release", profile, "net48/PmlTwin.Caf.Adapter.dll");
        Assert.True(File.Exists(file), "Build real SDK profile before this isolated metadata test: " + profile);
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var references = md.AssemblyReferences.Select(h => md.GetAssemblyReference(h)).ToArray();
        Assert.All(references, r => Assert.Contains(md.GetString(r.Name), new[] { "mscorlib", "Aveva.ApplicationFramework" }));
        var caf = Assert.Single(references, r => md.GetString(r.Name) == "Aveva.ApplicationFramework");
        Assert.Equal(version, caf.Version.ToString());
        Assert.Equal("17C64733A9775004", Convert.ToHexString(md.GetBlobBytes(caf.PublicKeyOrToken)));
        var type = Assert.Single(md.TypeDefinitions.Select(h => md.GetTypeDefinition(h)), t => md.GetString(t.Name) == "PmlTwinAddin");
        var interfaces = type.GetInterfaceImplementations().Select(h => md.GetInterfaceImplementation(h))
            .Select(i => md.GetTypeReference((TypeReferenceHandle)i.Interface)).ToArray();
        Assert.All(interfaces, i => Assert.Equal("Aveva.ApplicationFramework", md.GetString(i.Namespace)));
        Assert.Equal(profile == "E3D31" ? 2 : 1, interfaces.Length);
        Assert.Contains(interfaces, i => md.GetString(i.Name) == "IAddin");
        if (profile == "E3D31") Assert.Contains(interfaces, i => md.GetString(i.Name) == "IAddinInjected");
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "Aveva.ApplicationFramework.dll")), "Private=false must prevent SDK redistribution.");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "YuzuhaToolkit.Development.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }
}
