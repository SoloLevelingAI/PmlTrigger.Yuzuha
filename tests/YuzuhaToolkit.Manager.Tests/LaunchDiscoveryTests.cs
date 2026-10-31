using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LaunchDiscovery-" + Guid.NewGuid().ToString("N"));
    private string Write(string name, string content = "synthetic")
    {
        var file = Path.GetFullPath(Path.Combine(root, name)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content); return file;
    }
    private string Installation(string name)
    {
        var project = Path.Combine(root, name, "projects", "APS000"); Directory.CreateDirectory(project);
        var executable = Write(name + "/mon.exe"); Write(name + "/launch.init");
        Write(name + "/evars.init", "set APS000=" + project + "\nset module=DESIGN\n"); return executable;
    }

    [Fact]
    public void UniqueInstallationFromDefaultSourcePrefillsPathsAndApsWithoutWriting()
    {
        var executable = Installation("Everything3D2.10");
        var service = new LaunchDiscoveryService(() => [new(root, "default-fixture")]);
        var result = service.Discover(new(Overrides: new() { ["user"] = "TEST", ["mdb"] = "/ALL" }));
        Assert.Equal("resolved", result.State); Assert.Empty(result.NeedsInput);
        Assert.Equal(executable, result.Draft!.Executable); Assert.Equal("APS", result.Draft.Project);
        Assert.True(Assert.Single(result.Candidates).ApsAvailable); Assert.Contains("default-fixture", result.Candidates[0].Sources);
        Assert.Equal("static-not-session-verified", result.EvidenceKind); Assert.True(result.ExpiresAt > result.ObservedAt);
        var duplicateSources = new LaunchDiscoveryService().Discover(new([root, Path.Combine(root, "Everything3D2.10") + Path.DirectorySeparatorChar]));
        Assert.Single(duplicateSources.Candidates);
    }

    [Fact]
    public void MultipleCandidatesRequireInputAndExplicitTargetAndOverrideWin()
    {
        Installation("Everything3D2.10"); Installation("Everything3D3.1");
        var service = new LaunchDiscoveryService(); var request = new LaunchDiscoveryRequest([root]);
        var ambiguous = service.Discover(request);
        Assert.Equal("needsInput", ambiguous.State); Assert.Null(ambiguous.Draft); Assert.Contains("candidateId", ambiguous.NeedsInput);
        var selected = ambiguous.Candidates[1].Installation.Identity;
        var result = service.Discover(request with { CandidateId = selected.Id, Overrides = new() { ["module"] = "INVALID" } });
        Assert.Equal(selected.Executable, result.Draft!.Executable); Assert.Equal("INVALID", result.Draft.Module);
        Assert.Contains(result.Fields, f => f.Field == "module" && f.UserOverride && f.NeedsInput);
        Assert.Equal(selected.Executable, service.Discover(request with { PreferredExecutable = selected.Executable }).Draft!.Executable);
        var stale = service.Discover(request with { PreferredExecutable = Path.Combine(root, "missing.exe") });
        Assert.Null(stale.Draft); Assert.Contains("candidateId", stale.NeedsInput);
    }

    [Fact]
    public void IncompleteAndUnavailableRemainDistinctFromAResolvedEnvironment()
    {
        Installation("Everything3D2.10");
        var limited = new LaunchDiscoveryService().Discover(new([root], MaximumFiles: 1));
        Assert.False(limited.Coverage.Complete); Assert.Equal("needsInput", limited.State);
        var missing = new LaunchDiscoveryService().Discover(new([Path.Combine(root, "missing")]));
        Assert.Empty(missing.Candidates); Assert.Contains(missing.Diagnostics, d => d.Code == "RootMissing");
    }

    [Fact]
    public async Task HeadlessOperationReturnsStructuredNeedsInputAndValidatesBudgets()
    {
        Installation("Everything3D2.10"); Installation("Everything3D3.1");
        var executor = new OperationExecutor(new OperationRegistry(LaunchDiscoveryOperations.Registrations()));
        var input = JsonSerializer.SerializeToElement(new { roots = new[] { root } });
        var result = await executor.InvokeAsync("environment.resolve", input, new());
        Assert.False(result.Success); Assert.Equal("ENVIRONMENT_NEEDS_INPUT", result.Error!.Code);
        Assert.Equal(2, result.Data!.Value.GetProperty("candidates").GetArrayLength());
        var invalid = await executor.InvokeAsync("environment.discover", JsonSerializer.SerializeToElement(new { maximumFiles = 0 }), new());
        Assert.False(invalid.Success);
    }

    [Theory]
    [InlineData("AM", "Marine/OH12.1.SP4", "marine.bat", "DESIGN", "Outfitting")]
    [InlineData("E3D", "Everything3D3.1", "mon.exe", "DRAFT", "Draw")]
    public void ModuleChoicesEqualTheExistingLaunchCatalogAndPreserveRawStaticEvidence(string product, string folder, string entry, string rawModule, string launchModule)
    {
        var executable = Write(folder + "/" + entry);
        var project = Path.Combine(root, "projects", "APS000"); Directory.CreateDirectory(project);
        Write(folder + (product == "AM" ? "/evars.bat" : "/evars.init"), "set APS000=" + project + "\nset module=" + rawModule);
        if (product == "E3D") Write(folder + "/launch.init");
        var service = new LaunchDiscoveryService();
        var unresolved = service.Discover(new([root]));
        var candidate = Assert.Single(unresolved.Candidates);
        var expected = new ProductModuleCatalog().Get(new(executable, product, "", product == "AM" ? "MarineBatch" : "Monitor"));
        Assert.Equal(expected.Options.Select(m => m.Value), candidate.ModuleOptions.Options.Select(m => m.Value));
        Assert.Contains(candidate.Installation.ProjectModules.Modules, m => m.Name == rawModule);
        Assert.DoesNotContain(rawModule, Assert.Single(unresolved.Fields, f => f.Field == "module").Alternatives);
        Assert.Contains(launchModule, Assert.Single(unresolved.Fields, f => f.Field == "module").Alternatives);
        var resolved = service.Discover(new([root], Overrides: new() { ["module"] = launchModule, ["user"] = "TEST", ["mdb"] = "/ALL" }));
        Assert.Equal("resolved", resolved.State);
        Assert.Equal("product-mapped-unverified", Assert.Single(resolved.Fields, f => f.Field == "module").Validation);
        Assert.Contains(launchModule, LaunchService.Preview(resolved.Draft!));
    }

    [Fact]
    public async Task ResolvedDraftPassesDirectlyThroughRealPreviewAndSaveOperations()
    {
        Installation("Everything3D2.10");
        var profiles = new LaunchProfileService(new WorkspaceConfigurationStore(Path.Combine(root, "state")));
        var executor = new OperationExecutor(new OperationRegistry(LaunchDiscoveryOperations.Registrations().Concat(LaunchOperationCatalog.Registrations(profiles))));
        var discovery = await executor.InvokeAsync("environment.resolve", JsonSerializer.SerializeToElement(new { roots = new[] { root }, overrides = new { user = "TEST", mdb = "/ALL" } }), new());
        Assert.True(discovery.Success, discovery.Error?.Message);
        var draft = discovery.Data!.Value.GetProperty("draft");
        Assert.False(draft.TryGetProperty("lastStartedUtc", out _));
        Assert.False(draft.TryGetProperty("licensePrecheck", out _));
        // Pass the returned JSON unchanged; the client performs no field filtering.
        var request = JsonSerializer.SerializeToElement(new { profile = draft, expectedRevision = profiles.Read().Revision });
        var preview = await executor.InvokeAsync("launch.profile.preview", request, new());
        Assert.True(preview.Success, preview.Error?.Message);
        var save = await executor.InvokeAsync("launch.profile.save", request, new(AllowWrite: true));
        Assert.True(save.Success, save.Error?.Message);
        Assert.Equal("APS", Assert.Single(profiles.Read().Value.Profiles).Project);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
