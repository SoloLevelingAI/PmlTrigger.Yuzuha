using System.Security.Cryptography;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.PluginFixtures;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class PluginBoundaryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaPluginTests-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(root, "selected-package");
    private string State => Path.Combine(root, "manager-state");
    private string Manifest => Path.Combine(Source, "manager-plugin.json");
    private ManagerPluginRegistry Registry() => new(State);
    private void Package(string version = "1.0.0", int contract = 1)
    {
        Directory.CreateDirectory(Source);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "ProcessFixture"))) File.Copy(file, Path.Combine(Source, Path.GetFileName(file)));
        var hashes = Directory.GetFiles(Source).ToDictionary(f => Path.GetFileName(f)!, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant(), StringComparer.Ordinal);
        var manifest = new ManagerPluginManifest(1, "fixture", version, contract, "YuzuhaToolkit.Manager.ProcessFixture.dll", hashes!, FixturePluginDefinition.Capabilities.Operations);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(manifest, PluginJsonContext.Default.ManagerPluginManifest));
    }
    private ManagerPluginRegistrySnapshot Install(bool enabled = false)
    { var registry = Registry(); return registry.Install(registry.PreviewInstall(Source, enabled)); }
    private static JsonElement Echo => JsonSerializer.SerializeToElement(new { text = "插件 中文 & $()" });

    [Fact]
    public async Task NoPluginDisabledAndUnregisteredStatesCannotBlockBaseMetadataOrRunWorker()
    {
        var registry = Registry(); Assert.Empty(registry.List()); Assert.False(Directory.Exists(State));
        Assert.Equal("PLUGIN_NOT_INSTALLED", (await registry.InvokeAsync("fixture", "fixture.echo", Echo, new())).Error?.Code);
        Package(); var installed = Install(); Assert.Equal("disabled", Assert.Single(registry.List()).State);
        Assert.Equal("PLUGIN_DISABLED", (await registry.InvokeAsync("fixture", "fixture.echo", Echo, new())).Error?.Code);
        var ready = registry.Change("fixture", installed.Revision, true); Assert.Equal("ready", Assert.Single(registry.List()).State);
        var invoked = await registry.InvokeAsync("fixture", "fixture.echo", Echo, new()); Assert.True(invoked.Success, invoked.Error?.Message);
        Assert.Equal("插件 中文 & $()", invoked.Data!.Value.GetProperty("text").GetString());
        var disabled = registry.Change("fixture", ready.Revision, false);
        Assert.Equal("PLUGIN_DISABLED", (await registry.InvokeAsync("fixture", "fixture.echo", Echo, new())).Error?.Code);
        registry.Change("fixture", disabled.Revision, null); Assert.Empty(registry.List());
        using var output = new StringWriter(); Assert.Equal(0, await ManagerCliHost.RunAsync(["op", "list"], ManagerOperationCatalog.Create(), output));
        Assert.Contains("launch.profile.start", output.ToString()); Assert.Contains("knowledge.import.preview", output.ToString());
        Assert.True(File.Exists(Manifest));
    }

    [Fact]
    public async Task ManifestAuthorizationIsCheckedBeforeDispatchAndLostWriteReplyIsUnknownWithoutRetry()
    {
        Package(); Install(true); var registry = Registry(); var marker = Path.Combine(root, "marker.txt");
        var input = JsonSerializer.SerializeToElement(new { marker });
        Assert.Equal(4, (await registry.InvokeAsync("fixture", "fixture.launch", input, new(AllowWrite: true))).ExitCode); Assert.False(File.Exists(marker));
        Assert.Equal(4, (await registry.InvokeAsync("fixture", "fixture.commit-crash", input, new())).ExitCode); Assert.False(File.Exists(marker));
        var lost = await registry.InvokeAsync("fixture", "fixture.commit-crash", input, new(AllowWrite: true));
        Assert.False(lost.Success); Assert.True(lost.Error?.MayHaveExecuted); Assert.Equal("INVALID_CLI_RESPONSE", lost.Error?.Code);
        Assert.Single(File.ReadAllLines(marker));
        var missing = await registry.InvokeAsync("fixture", "fixture.not-declared", input, new(AllowWrite: true)); Assert.Equal(3, missing.ExitCode); Assert.Single(File.ReadAllLines(marker));
    }

    [Fact]
    public async Task ActualWorkerVersionMustMatchFixedManifestBeforeOperation()
    {
        Package("2.0.0"); Install(true);
        var rejected = await Registry().InvokeAsync("fixture", "fixture.echo", Echo, new());
        Assert.Equal("PLUGIN_HANDSHAKE_FAILED", rejected.Error?.Code); Assert.Equal(6, rejected.ExitCode);
    }

    [Fact]
    public void ChangedSourceOrUndeclaredDependencyIsRejectedWithoutReplacingRegistration()
    {
        Package(); var registry = Registry(); var plan = registry.PreviewInstall(Source);
        File.AppendAllText(Path.Combine(Source, "YuzuhaToolkit.Manager.ProcessFixture.dll"), "changed");
        Assert.Throws<InvalidDataException>(() => registry.Install(plan)); Assert.Equal("missing", registry.Read().Revision);
        var manifest = JsonSerializer.Deserialize(File.ReadAllText(Manifest), PluginJsonContext.Default.ManagerPluginManifest)!;
        manifest.Files["YuzuhaToolkit.Manager.ProcessFixture.dll"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Source, "YuzuhaToolkit.Manager.ProcessFixture.dll"))));
        File.WriteAllText(Manifest, JsonSerializer.Serialize(manifest, PluginJsonContext.Default.ManagerPluginManifest));
        File.WriteAllText(Path.Combine(Source, "undeclared.dll"), "never execute");
        Assert.Throws<InvalidDataException>(() => registry.PreviewInstall(Source));
    }

    [Fact]
    public async Task MissingAndFutureContractPackagesAreExplicitlyUnavailable()
    {
        Package(); var installed = Install(true); var registration = installed.Document.Registrations.Single();
        File.Delete(Path.Combine(registration.Directory, "YuzuhaToolkit.Manager.ProcessFixture.dll"));
        Assert.Equal("unavailable", Assert.Single(Registry().List()).State);
        Assert.Equal("PLUGIN_UNAVAILABLE", (await Registry().InvokeAsync("fixture", "fixture.echo", Echo, new())).Error?.Code);
        // Represents a newer host's retained registration when an older manager reads it.
        File.Copy(Path.Combine(Source, "YuzuhaToolkit.Manager.ProcessFixture.dll"), Path.Combine(registration.Directory, "YuzuhaToolkit.Manager.ProcessFixture.dll"));
        var manifestPath = Path.Combine(registration.Directory, "manager-plugin.json");
        var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), PluginJsonContext.Default.ManagerPluginManifest)! with { ContractVersion = 2 };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, PluginJsonContext.Default.ManagerPluginManifest));
        registration = registration with { ManifestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant() };
        var registryFile = Path.Combine(State, "Plugins", "registry.json"); File.WriteAllText(registryFile,
            JsonSerializer.Serialize(installed.Document with { Registrations = [registration] }, ManagerJsonContext.Default.ManagerPluginRegistryDocument));
        Assert.Equal("incompatible", Assert.Single(Registry().List()).State);
        Assert.Equal("PLUGIN_INCOMPATIBLE", (await Registry().InvokeAsync("fixture", "fixture.echo", Echo, new())).Error?.Code);
    }

    [Fact]
    public void RegistryRejectsStaleEditorsAndRetainsUnknownFields()
    {
        Package(); var snapshot = Install(); var file = Path.Combine(State, "Plugins", "registry.json");
        var document = snapshot.Document with { ExtensionData = new() { ["futureField"] = JsonSerializer.SerializeToElement(new { preserve = true }) } };
        File.WriteAllText(file, JsonSerializer.Serialize(document, ManagerJsonContext.Default.ManagerPluginRegistryDocument));
        var registry = Registry(); var before = registry.Read(); var changed = registry.Change("fixture", before.Revision, true);
        Assert.True(changed.Document.ExtensionData!["futureField"].GetProperty("preserve").GetBoolean());
        Assert.Throws<ConfigurationConflictException>(() => registry.Change("fixture", before.Revision, false));
        Assert.Equal(changed.Revision, registry.Read().Revision);
    }

    [Fact]
    public async Task ManagerOperationsShareFrozenInstallPlanEnableRevisionAndInvocation()
    {
        Package(); var registry = Registry(); var executor = new OperationExecutor(new OperationRegistry(PluginOperations.Registrations(registry, new ManagerPlanStore(State))));
        var preview = await executor.InvokeAsync("plugin.install.preview", JsonSerializer.SerializeToElement(new { source = Source }), new()); Assert.True(preview.Success, preview.Error?.Message);
        var value = preview.Data!.Value; var reference = JsonSerializer.SerializeToElement(new { planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
        Assert.Equal(4, (await executor.InvokeAsync("plugin.install.apply", reference, new())).ExitCode);
        var installed = await executor.InvokeAsync("plugin.install.apply", reference, new(AllowWrite: true)); Assert.True(installed.Success, installed.Error?.Message);
        var enabled = await executor.InvokeAsync("plugin.enable", JsonSerializer.SerializeToElement(new { id = "fixture", expectedRevision = registry.Read().Revision, enabled = true }), new(AllowWrite: true)); Assert.True(enabled.Success, enabled.Error?.Message);
        var invoked = await executor.InvokeAsync("plugin.invoke", JsonSerializer.SerializeToElement(new { pluginId = "fixture", operation = "fixture.echo", input = new { text = "roundtrip" } }), new());
        Assert.True(invoked.Success, invoked.Error?.Message); Assert.Equal("plugin.invoke", invoked.OperationId);
        Assert.Equal("fixture.echo", invoked.Data!.Value.GetProperty("result").GetProperty("operationId").GetString());
        Assert.Equal("roundtrip", invoked.Data.Value.GetProperty("result").GetProperty("data").GetProperty("text").GetString());
    }

    public void Dispose()
    {
        for (var attempt = 0; ; attempt++)
        { try { if (Directory.Exists(root)) Directory.Delete(root, true); return; } catch (IOException) when (attempt < 20) { Thread.Sleep(50); } }
    }
}
