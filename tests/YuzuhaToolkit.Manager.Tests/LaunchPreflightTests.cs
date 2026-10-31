using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.Plugins.License;
using YuzuhaToolkit.Plugins.Vm;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchPreflightTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaPreflightTests-" + Guid.NewGuid().ToString("N"));
    private readonly LaunchProfileService profiles;
    private readonly ManagerPluginRegistry plugins;
    private readonly LaunchProfile profile;
    public LaunchPreflightTests()
    {
        Directory.CreateDirectory(root); var entry = Path.Combine(root, "mon.exe"); File.WriteAllText(entry, "synthetic");
        var init = Path.Combine(root, "fixture.init"); File.WriteAllText(init, "synthetic");
        profiles = new(new WorkspaceConfigurationStore(Path.Combine(root, "state"))); plugins = new(Path.Combine(root, "plugins-state"));
        profile = new() { Id = "fixture", Name = "fixture", Executable = entry, Initialization = init, WorkingDirectory = root, Project = "TST", User = "SYSTEM" };
    }
    private void Install(string id)
    {
        var source = Path.Combine(root, id); Directory.CreateDirectory(source);
        var capabilities = id == "license" ? LicensePlugin.Capabilities : VmPlugin.Capabilities;
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, id == "license" ? "LicenseFixture" : "VmFixture"))) File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        var hashes = Directory.GetFiles(source).ToDictionary(f => Path.GetFileName(f)!, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant());
        var manifest = new ManagerPluginManifest(1, id, capabilities.Version, 1, id == "license" ? "Yuzuha.Manager.Plugin.License.dll" : "Yuzuha.Manager.Plugin.Vm.dll", hashes, capabilities.Operations);
        File.WriteAllText(Path.Combine(source, "manager-plugin.json"), JsonSerializer.Serialize(manifest, PluginJsonContext.Default.ManagerPluginManifest));
        plugins.Install(plugins.PreviewInstall(source, true));
    }
    private async Task<(OperationResult Result, int Started)> Start()
    {
        var snapshot = profiles.Save(profile, profiles.Read().Revision, true).Snapshot; var started = 0;
        var executor = new OperationExecutor(new OperationRegistry(LaunchOperationCatalog.Registrations(profiles,
            startProcess: (_, _) => { started++; return 42; }, licensePreflight: (p, token) => LaunchLicensePreflight.CheckAsync(p, plugins, token))));
        var result = await executor.InvokeAsync("launch.profile.start", JsonSerializer.SerializeToElement(new { profileId = profile.Id, expectedRevision = snapshot.Revision, password = "synthetic" }), new(AllowLaunch: true));
        return (result, started);
    }

    [Fact]
    public async Task DefaultStartsWithoutPluginAndRequiredPolicyRefusesMissingOrCheckoutEvidence()
    {
        var first = await Start(); Assert.True(first.Result.Success); Assert.Equal(1, first.Started);
        Assert.False(first.Result.Data!.Value.TryGetProperty("licensePrecheck", out _)); Assert.False(Directory.Exists(Path.Combine(root, "plugins-state")));
        profile.LicensePrecheck = new() { Mode = "required", Host = "127.0.0.1", RmsPort = 12345 };
        var missing = await Start(); Assert.Equal("PLUGIN_NOT_INSTALLED", missing.Result.Error?.Code); Assert.Equal(0, missing.Started);
        profile.LicensePrecheck.RequireCheckout = true;
        var checkout = await Start(); Assert.Equal("LICENSE_CHECKOUT_UNVERIFIED", checkout.Result.Error?.Code); Assert.Equal(0, checkout.Started);
        profile.LicensePrecheck.Mode = "off"; Assert.True((await Start()).Result.Success);
    }

    [Fact]
    public async Task RealOptionalWorkerChecksReachabilityAndNeverPromotesItToCheckout()
    {
        Install("license"); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            profile.LicensePrecheck = new() { Mode = "required", Host = "127.0.0.1", RmsPort = port, RulesPort = port };
            var allowed = await Start(); Assert.True(allowed.Result.Success, allowed.Result.Error?.Message); Assert.Equal(1, allowed.Started);
            var precheck = allowed.Result.Data!.Value.GetProperty("licensePrecheck"); Assert.Equal("unverified", precheck.GetProperty("checkoutState").GetString());
            Assert.Equal("entry-process-created-host-not-verified", allowed.Result.Data.Value.GetProperty("state").GetString());
            listener.Stop(); var rejected = await Start(); Assert.Equal("LICENSE_PRECHECK_REJECTED", rejected.Result.Error?.Code); Assert.Equal(0, rejected.Started);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task RevisionIsCheckedAgainAfterAwaitedPreflightBeforeAnyProcess()
    {
        var snapshot = profiles.Save(profile, "missing", true).Snapshot; var started = 0;
        var executor = new OperationExecutor(new OperationRegistry(LaunchOperationCatalog.Registrations(profiles, startProcess: (_, _) => { started++; return 1; },
            licensePreflight: (_, _) => { profiles.SetFavorite(profile.Id, true, snapshot.Revision, true); return Task.FromResult(new LaunchPrecheckResult(true, "fixture", "fixture", "unverified")); })));
        var result = await executor.InvokeAsync("launch.profile.start", JsonSerializer.SerializeToElement(new { profileId = profile.Id, expectedRevision = snapshot.Revision, password = "synthetic" }), new(AllowLaunch: true));
        Assert.Equal("CONFIGURATION_CONFLICT", result.Error?.Code); Assert.Equal(0, started);
    }

    [Fact]
    public void PolicyEditsDuplicateAndHistoryPreserveIndependentUnknownFields()
    {
        profile.LicensePrecheck = new() { Mode = "required", Host = "127.0.0.1", RmsPort = 12345, ExtensionData = new() { ["futureRule"] = JsonSerializer.SerializeToElement("preserved") } };
        var saved = profiles.Save(profile, "missing", true).Snapshot;
        var copy = LaunchProfileCatalog.Duplicate(profile); copy.LicensePrecheck!.Host = "other"; Assert.Equal("127.0.0.1", profile.LicensePrecheck.Host);
        var changed = profiles.Update(profile.Id, JsonSerializer.SerializeToElement(new { licensePrecheck = new { mode = "off" } }), saved.Revision, true);
        Assert.Equal("preserved", changed.Snapshot.Value.Profiles.Single().LicensePrecheck!.ExtensionData!["futureRule"].GetString());
        profile.LicensePrecheck.Mode = "required"; profile.LicensePrecheck.RmsPort = 0; Assert.False(profiles.Validate(profile).IsValid);
    }

    [Fact]
    public async Task PackagedVmWorkerCanSimulateThroughRegistryWithoutLoadingVendorSdk()
    {
        Install("vm"); var result = await plugins.InvokeAsync("vm", "vm.simulate", JsonSerializer.SerializeToElement(new { profile = "AM", scenario = "ok" }), new());
        Assert.True(result.Success, result.Error?.Message); Assert.False(result.Data!.Value.GetProperty("realConnectionVerified").GetBoolean());
        Assert.Equal("simulation", result.Data.Value.GetProperty("mode").GetString());
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
