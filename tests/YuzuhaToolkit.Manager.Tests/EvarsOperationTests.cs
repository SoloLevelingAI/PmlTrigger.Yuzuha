using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class EvarsOperationTests
{
    [Fact]
    public async Task PreviewKeepsOriginalPrivateAndApplyRequiresAuthorizationAndExactPlan()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsOps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");
            var original = "@echo off\nrem PRIVATE_ORIGINAL_FIXTURE\nset pmllib=vendor\n";
            File.WriteAllText(file, original);
            var state = Path.Combine(root, "state");
            var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(state))));
            var input = JsonSerializer.SerializeToElement(new { installationRoot = root, product = "E3D", runtimeRoot = root, profile = "E3D2.1", framework = "net48", codePage = 65001 });
            var preview = await executor.InvokeAsync("evars.preview", input, new());
            Assert.True(preview.Success, preview.Error?.Message);
            Assert.DoesNotContain("PRIVATE_ORIGINAL_FIXTURE", preview.Data!.Value.GetRawText());
            Assert.DoesNotContain("originalBase64", preview.Data.Value.GetRawText());
            Assert.Equal(original, File.ReadAllText(file));
            var reference = preview.Data.Value;
            JsonElement Ref(JsonElement value) => JsonSerializer.SerializeToElement(new {
                planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
            var denied = await executor.InvokeAsync("evars.apply", Ref(reference), new());
            Assert.Equal(4, denied.ExitCode); Assert.Equal(original, File.ReadAllText(file));
            var id = reference.GetProperty("planId").GetString()!;
            var saved = Path.Combine(state, "ManagerPlans", id + ".json");
            File.AppendAllText(saved, " ");
            var stale = await executor.InvokeAsync("evars.apply", Ref(reference), new(AllowWrite: true));
            Assert.False(stale.Success); Assert.Equal(original, File.ReadAllText(file));
            preview = await executor.InvokeAsync("evars.preview", input, new());
            var applied = await executor.InvokeAsync("evars.apply", Ref(preview.Data!.Value), new(AllowWrite: true));
            Assert.True(applied.Success, applied.Error?.Message);
            Assert.Contains("set Yuzuha=E3D2.1", File.ReadAllText(file));
            var restored = await executor.InvokeAsync("evars.restore", Ref(applied.Data!.Value), new(AllowWrite: true));
            Assert.True(restored.Success, restored.Error?.Message);
            Assert.Equal(original, File.ReadAllText(file));
        }
        finally { Directory.Delete(root, true); }
    }
}
