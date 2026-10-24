using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class LaunchOperationTests
{
    [Fact]
    public async Task EveryLaunchOperationUsesTheVersionedServiceAndOnlyExplicitStartCreatesAProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhaytonLaunchOperations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var entry = Path.Combine(root, "mon.exe"); File.WriteAllText(entry, "synthetic; not executable");
        var init = Path.Combine(root, "launch.init"); File.WriteAllText(init, "synthetic");
        var service = new LaunchProfileService(new WorkspaceConfigurationStore(Path.Combine(root, "state")));
        var launched = 0;
        var executor = new OperationExecutor(new OperationRegistry(LaunchOperationCatalog.Registrations(service, startProcess: (profile, password) =>
        {
            Assert.Equal("profile", profile.Id); Assert.Equal("/ALL", profile.Mdb);
            Assert.Equal("fixture-password", password); launched++; return 42;
        })));
        var visited = new HashSet<string>();
        var records = new List<object>();
        var completed = false;
        string? failure = null;
        var context = new OperationContext(AllowWrite: true, AllowLaunch: true);
        async Task<JsonElement> Run(string id, object input)
        {
            var request = JsonSerializer.SerializeToElement(input);
            executor.Registry.TryGet(id, out var registration);
            OperationResult? denied = null;
            if (registration!.Descriptor.RequiresWrite || registration.Descriptor.RequiresLaunch)
            {
                var before = service.Read().Revision;
                denied = await executor.InvokeAsync(id, request, new());
                Assert.Equal(4, denied.ExitCode); Assert.Equal(before, service.Read().Revision);
            }
            var result = await executor.InvokeAsync(id, request, context);
            visited.Add(id);
            records.Add(new { operationId = id, status = result.Success ? "Passed" : "Failed", result,
                permissionRejected = denied?.Error?.Code, environment = id.EndsWith(".start") ? "synthetic-process-factory" : "isolated-files-real-service" });
            Assert.True(result.Success, result.Error?.Message);
            return result.Data!.Value;
        }
        try
        {
            var workspace = await Run("launch.workspace.read", new { });
            var revision = workspace.GetProperty("revision").GetString()!;
            var modules = await Run("launch.modules.list", new { product = "AM", version = "12.1" });
            Assert.DoesNotContain(modules.GetProperty("options").EnumerateArray(), item => item.GetProperty("value").GetString() == "Design");
            var profile = new { id = "profile", name = "Fixture", product = "E3D", version = "2.1", kind = "Monitor",
                executable = entry, initialization = init, workingDirectory = root, project = "TST", user = "SYSTEM", mdb = "/ALL", module = "Design" };
            Assert.True((await Run("launch.profile.validate", new { profile })).GetProperty("isValid").GetBoolean());
            Assert.True((await Run("launch.profile.preview", new { profile, expectedRevision = revision })).GetProperty("validation").GetProperty("isValid").GetBoolean());
            var saved = await Run("launch.profile.save", new { profile, expectedRevision = revision });
            var original = revision = saved.GetProperty("snapshot").GetProperty("revision").GetString()!;
            Assert.Equal("/ALL", (await Run("launch.profile.read", new { profileId = "profile" })).GetProperty("profile").GetProperty("mdb").GetString());
            Assert.Single((await Run("launch.profile.list", new { query = "Fixture", product = "E3D", scope = "all" })).EnumerateArray());
            var changed = await Run("launch.profile.update", new { profileId = "profile", changes = new { mdb = "/REVIEW" }, expectedRevision = revision });
            revision = changed.GetProperty("snapshot").GetProperty("revision").GetString()!;
            var favorite = await Run("launch.profile.favorite", new { profileId = "profile", favorite = true, expectedRevision = revision });
            revision = favorite.GetProperty("snapshot").GetProperty("revision").GetString()!;
            var defaulted = await Run("launch.profile.default", new { profileId = "profile", expectedRevision = revision });
            revision = defaulted.GetProperty("snapshot").GetProperty("revision").GetString()!;
            var duplicated = await Run("launch.profile.duplicate", new { profileId = "profile", expectedRevision = revision });
            revision = duplicated.GetProperty("revision").GetString()!;
            var copyId = duplicated.GetProperty("profile").GetProperty("id").GetString()!;
            Assert.False(duplicated.GetProperty("profile").GetProperty("isDefault").GetBoolean());
            Assert.Equal("", duplicated.GetProperty("profile").GetProperty("credentialKey").GetString());
            Assert.NotEmpty((await Run("launch.profile.history", new { profileId = "profile", limit = 50 })).EnumerateArray());
            var preview = await Run("launch.profile.restore.preview", new { profileId = "profile", revision = original, expectedRevision = revision });
            Assert.Equal("/ALL", preview.GetProperty("profile").GetProperty("mdb").GetString());
            var restored = await Run("launch.profile.restore", new { profileId = "profile", revision = original, expectedRevision = revision });
            revision = restored.GetProperty("snapshot").GetProperty("revision").GetString()!;
            Assert.NotEqual(original, revision);
            Assert.Equal(0, launched);
            var started = await Run("launch.profile.start", new { profileId = "profile", expectedRevision = revision, password = "fixture-password" });
            Assert.Equal(42, started.GetProperty("processId").GetInt32()); Assert.Equal(1, launched);
            await Run("launch.profile.delete", new { profileId = copyId, expectedRevision = revision });
            Assert.Single(service.List());
            Assert.Equal(executor.Registry.Descriptors.Select(item => item.Id).Order(), visited.Order());
            Assert.Equal(16, visited.Count);
            completed = true;
        }
        catch (Exception error)
        {
            failure = error.ToString();
            throw;
        }
        finally
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src"))) current = current.Parent;
            var report = Path.Combine(current!.FullName, "artifacts", "native-cli-migration", "launch-capabilities.json");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, JsonSerializer.Serialize(new { status = completed ? "Passed" : "Failed", completed, error = failure, actualAvevaStarted = false, cases = records,
                missing = executor.Registry.Descriptors.Select(item => item.Id).Except(visited).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
            Directory.Delete(root, true);
        }
    }
}
