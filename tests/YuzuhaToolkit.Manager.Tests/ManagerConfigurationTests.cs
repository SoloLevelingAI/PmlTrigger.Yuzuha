using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class ManagerConfigurationTests
{
    [Fact]
    public async Task ManagerAndDesktopServiceRejectStaleEditorsAndPreserveExtensions()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaManagerConfig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var entry = Path.Combine(root, "mon.exe"); File.WriteAllText(entry, "fixture, not executable");
            var init = Path.Combine(root, "launch.init"); File.WriteAllText(init, "fixture");
            var store = new WorkspaceConfigurationStore(Path.Combine(root, "state"));
            var settings = store.Read();
            settings.Value.ExtensionData = new() { ["future-setting"] = JsonSerializer.SerializeToElement(new { preserved = true }) };
            settings.Value.Profiles.Add(new LaunchProfile { Id = "fixture", Name = "Fixture", Product = "E3D", Version = "2.1",
                Kind = "Monitor", Executable = entry, Initialization = init, WorkingDirectory = root,
                Project = "TST", User = "SYSTEM", Mdb = "/ALL", Module = "Design" });
            var initial = store.Commit(settings.Value, settings.Revision, new ConfigurationChangeReason("fixture")).Snapshot;
            var desktopService = new LaunchProfileService(store);
            var manager = ManagerOperationCatalog.Create(desktopService);
            var request = JsonSerializer.SerializeToElement(new { profileId = "fixture", changes = new { name = "Changed" }, expectedRevision = initial.Revision });
            var denied = await manager.InvokeAsync("launch.profile.update", request, new());
            Assert.Equal(4, denied.ExitCode); Assert.Equal(initial.Revision, store.Read().Revision);
            var updated = await manager.InvokeAsync("launch.profile.update", request, new(AllowWrite: true));
            Assert.True(updated.Success, updated.Error?.Message);
            var current = store.Read();
            Assert.Equal("Changed", current.Value.Profiles.Single().Name);
            Assert.True(current.Value.ExtensionData!["future-setting"].GetProperty("preserved").GetBoolean());
            var stale = await manager.InvokeAsync("launch.profile.update", request, new(AllowWrite: true));
            Assert.Equal(10, stale.ExitCode); Assert.Equal("CONFIGURATION_CONFLICT", stale.Error?.Code);
            Assert.Throws<ConfigurationConflictException>(() => desktopService.Update("fixture", request.GetProperty("changes"), initial.Revision, true));
            Assert.Equal(current.Revision, store.Read().Revision);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CliMetaCommandsReturnJsonWithoutResolvingCommunicationClient()
    {
        var manager = ManagerOperationCatalog.Create(communication: () => throw new Exception("Must stay offline"));
        using var output = new StringWriter();
        Assert.Equal(0, await OperationCli.RunAsync(["op", "list"], manager, output));
        using var result = JsonDocument.Parse(output.ToString());
        Assert.Contains(result.RootElement.GetProperty("data").EnumerateArray(),
            d => d.GetProperty("id").GetString() == "connection.test");
        output.GetStringBuilder().Clear();
        Assert.Equal(0, await OperationCli.RunAsync(["op", "describe", "launch.profile.start"], manager, output));
        using var described = JsonDocument.Parse(output.ToString());
        Assert.True(described.RootElement.GetProperty("data").GetProperty("requiresLaunch").GetBoolean());
    }
}
