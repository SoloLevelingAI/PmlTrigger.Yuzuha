using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

[CollectionDefinition("Manager data environment", DisableParallelization = true)]
public sealed class ManagerDataEnvironmentCollection;

[Collection("Manager data environment")]
public sealed class UserDataOperationTests
{
    [Fact]
    public async Task SharedMigrationRequiresFrozenPlanAndAuthorizationAndRetainsPersonalData()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaManagerData-" + Guid.NewGuid().ToString("N"));
        var saved = new Dictionary<string, string?>();
        foreach (var name in new[] { "YUZUHA_TOOLKIT_ROOT", "YUZUHA_STATE_DIR", "YUZUHA_USER_DIR", "YUZUHA_KNOWLEDGE_DIR", "YUZUHA_WEBAPPS_DIR" })
        { saved[name] = Environment.GetEnvironmentVariable(name); Environment.SetEnvironmentVariable(name, null); }
        try
        {
            var old = Path.Combine(root, "old"); var next = Path.Combine(root, "next");
            Environment.SetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT", Path.Combine(root, "install"));
            Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", Path.Combine(root, "state"));
            new WorkspaceSettings { UserDataRoot = old }.Save();
            var repository = new KnowledgeRepository(Path.Combine(old, "Knowledge"));
            var note = repository.SaveNote(null, 0, "Keep my note", "synthetic personal text", "offline fixture");
            var executor = new OperationExecutor(new OperationRegistry(UserDataOperations.Registrations()));
            var request = JsonSerializer.SerializeToElement(new { root = next });
            var preview = await executor.InvokeAsync("data.migration.preview", request, new());
            Assert.True(preview.Success, preview.Error?.Message); Assert.False(Directory.Exists(next));
            var value = preview.Data!.Value;
            var reference = JsonSerializer.SerializeToElement(new { planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
            Assert.Equal(4, (await executor.InvokeAsync("data.migration.apply", reference, new())).ExitCode);
            var applied = await executor.InvokeAsync("data.migration.apply", reference, new(AllowWrite: true));
            Assert.True(applied.Success, applied.Error?.Message); Assert.Equal(next, UserDataPaths.Root);
            Assert.Equal(note, Assert.Single(new KnowledgeRepository().ListNotes())); Assert.Equal(note, Assert.Single(repository.ListNotes()));
            Environment.SetEnvironmentVariable("YUZUHA_USER_DIR", next);
            var refused = await executor.InvokeAsync("data.migration.preview", JsonSerializer.SerializeToElement(new { root = Path.Combine(root, "third") }), new());
            Assert.False(refused.Success); Assert.False(string.IsNullOrWhiteSpace(refused.Error?.Message));
        }
        finally
        {
            foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
