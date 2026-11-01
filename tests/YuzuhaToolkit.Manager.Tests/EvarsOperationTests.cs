using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

/// <summary>验证环境操作的冻结计划、写入授权及部分失败后的显式恢复证据。</summary>
public sealed class EvarsOperationTests
{
    /// <summary>拒绝跨产品或未知版本配置，环境与计划目录均不发生写入。</summary>
    [Theory]
    [InlineData("AM", "PDMS", "net35")]
    [InlineData("PDMS", "AM", "net35")]
    [InlineData("E3D", "AM", "net48")]
    [InlineData("E3D", "E3D3.1.0", "net48")]
    public async Task WrongProductProfileIsRejectedWithoutWritingOrFreezingPlan(string product, string profile, string framework)
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsProfileRejected-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");var original = "@echo off\nset pmllib=vendor\n";File.WriteAllText(file, original);var state = Path.Combine(root, "state");
            var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(state))));
            var result = await executor.InvokeAsync("evars.preview", JsonSerializer.SerializeToElement(new { installationRoot = root, environmentFile = file, product, runtimeRoot = root, profile, framework, codePage = 65001 }), new());
            Assert.False(result.Success);Assert.Equal(2, result.ExitCode);Assert.False(result.Error!.MayHaveExecuted);Assert.Null(result.Data);Assert.Equal(original, File.ReadAllText(file));Assert.False(Directory.Exists(state));Assert.Single(Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>四种已交付产品配置均可预览、明确应用和恢复，预览自身只冻结本机计划。</summary>
    [Theory]
    [InlineData("AM", "AM", "net35")]
    [InlineData("PDMS", "PDMS", "net35")]
    [InlineData("E3D", "E3D2.1", "net48")]
    [InlineData("E3D", "E3D3.1.6", "net48")]
    public async Task SupportedProductProfileCanPreviewApplyAndRestore(string product, string profile, string framework)
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsProfileAccepted-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");var original = "@echo off\nset pmllib=vendor\n";File.WriteAllText(file, original);var state = Path.Combine(root, "state");
            var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(state))));
            var preview = await executor.InvokeAsync("evars.preview", JsonSerializer.SerializeToElement(new { installationRoot = root, environmentFile = file, product, runtimeRoot = root, profile, framework, codePage = 65001 }), new());
            Assert.True(preview.Success, preview.Error?.Message);Assert.Equal(original, File.ReadAllText(file));Assert.Single(Directory.GetFiles(Path.Combine(state, "ManagerPlans")));Assert.Equal(profile, preview.Data!.Value.GetProperty("summary").GetProperty("profile").GetString());
            var applied = await executor.InvokeAsync("evars.apply", Ref(preview.Data.Value), new(AllowWrite: true));Assert.True(applied.Success, applied.Error?.Message);Assert.Contains("set Yuzuha=" + profile + "\n", File.ReadAllText(file));Assert.Contains("set YuzuhaFramework=" + framework + "\n", File.ReadAllText(file));
            var restored = await executor.InvokeAsync("evars.restore", Ref(applied.Data!.Value), new(AllowWrite: true));Assert.True(restored.Success, restored.Error?.Message);Assert.Equal(original, File.ReadAllText(file));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>旧冻结编辑计划缺少所有权上下文时要求重新预览，不能直接应用。</summary>
    [Fact]
    public async Task LegacyFrozenEditWithoutOwnershipContextRequiresNewPreview()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsOldPlan-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");var original = "@echo off\nset pmllib=vendor\n";File.WriteAllText(file, original);var store = new ManagerPlanStore(Path.Combine(root, "state"));
            var previous = store.Save("evars.edit", EvarsEditor.Preview(file, root, "E3D2.1", "net48", 65001), ManagerJsonContext.Default.EvarsEditPlan, JsonSerializer.SerializeToElement(new { }));
            var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(store)));
            var result = await executor.InvokeAsync("evars.apply", Ref(JsonSerializer.SerializeToElement(previous, ManagerJsonContext.Default.ManagerPlanReference)), new(AllowWrite: true));Assert.False(result.Success);Assert.Contains("重新预览", result.Error?.Message);Assert.False(result.Error!.MayHaveExecuted);Assert.Equal(original, File.ReadAllText(file));Assert.Empty(Directory.GetFiles(root, "*.bak"));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>写后失败返回未知和有效恢复引用，允许明确恢复原字节。</summary>
    [Fact]
    public async Task PartialApplyFailureReturnsUnknownWithFrozenRecoveryReferenceAndExplicitRestore()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsPartial-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");File.WriteAllText(file, "@echo off\nset pmllib=vendor\n");var original = File.ReadAllBytes(file);
            var store = new ManagerPlanStore(Path.Combine(root, "state"));var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(store, plan =>
            { var receipt = EvarsEditor.Apply(plan);throw new EvarsApplyException("synthetic post-write failure", receipt, new IOException("fixture")); })));
            var preview = await executor.InvokeAsync("evars.preview", Input(root, file), new());Assert.True(preview.Success);var applied = await executor.InvokeAsync("evars.apply", Ref(preview.Data!.Value), new(AllowWrite: true));
            Assert.False(applied.Success);Assert.Equal("EVARS_APPLY_UNKNOWN", applied.Error!.Code);Assert.True(applied.Error.MayHaveExecuted);Assert.Equal("evars.receipt", applied.Data!.Value.GetProperty("kind").GetString());Assert.DoesNotContain("originalBase64", applied.Data.Value.GetRawText());
            var restore = await executor.InvokeAsync("evars.restore", Ref(applied.Data.Value), new(AllowWrite: true));Assert.True(restore.Success, restore.Error?.Message);Assert.Equal(original, File.ReadAllBytes(file));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>回执与补偿同时失败仍保留备份定位，禁止误报为未执行。</summary>
    [Fact]
    public async Task FailedReceiptPersistenceAndFailedCompensationKeepManualLocatorAndMayHaveExecuted()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsReceipt-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");File.WriteAllText(file, "@echo off\nset pmllib=vendor\n");var state = Path.Combine(root, "state");
            var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(state), plan =>
            {
                var receipt = EvarsEditor.Apply(plan);Directory.Move(Path.Combine(state, "ManagerPlans"), Path.Combine(state, "ManagerPlans.before"));File.WriteAllText(Path.Combine(state, "ManagerPlans"), "persistent store fixture blockage");return receipt;
            }, _ => throw new IOException("compensation fixture failure"))));
            var preview = await executor.InvokeAsync("evars.preview", Input(root, file), new());var applied = await executor.InvokeAsync("evars.apply", Ref(preview.Data!.Value), new(AllowWrite: true));
            Assert.False(applied.Success);Assert.Equal("EVARS_APPLY_UNKNOWN", applied.Error!.Code);Assert.True(applied.Error.MayHaveExecuted);Assert.True(File.Exists(applied.Data!.Value.GetProperty("backupFile").GetString()));Assert.False(applied.Data.Value.TryGetProperty("planId", out _));Assert.Contains("set Yuzuha=E3D2.1", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>恢复结果未知时保留同一恢复引用，防止界面丢失待核对状态。</summary>
    [Fact]
    public async Task RestoreOutcomeFailureRetainsOriginalRecoveryReferenceAndUnknownState()
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaEvarsRestore-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "evars.init");File.WriteAllText(file, "@echo off\nset pmllib=vendor\n");var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(Path.Combine(root, "state")), apply: null, restore: receipt => { EvarsEditor.Restore(receipt);throw new IOException("post-restore fixture failure"); })));
            var preview = await executor.InvokeAsync("evars.preview", Input(root, file), new());var applied = await executor.InvokeAsync("evars.apply", Ref(preview.Data!.Value), new(AllowWrite: true));Assert.True(applied.Success);
            var result = await executor.InvokeAsync("evars.restore", Ref(applied.Data!.Value), new(AllowWrite: true));Assert.False(result.Success);Assert.Equal("EVARS_RESTORE_UNKNOWN", result.Error!.Code);Assert.True(result.Error.MayHaveExecuted);Assert.Equal(applied.Data.Value.GetProperty("planId").GetString(), result.Data!.Value.GetProperty("planId").GetString());
        }
        finally { Directory.Delete(root, true); }
    }

    private static JsonElement Input(string root, string file) => JsonSerializer.SerializeToElement(new { installationRoot = root, environmentFile = file, product = "E3D", runtimeRoot = root, profile = "E3D2.1", framework = "net48", codePage = 65001 });
    private static JsonElement Ref(JsonElement value) => JsonSerializer.SerializeToElement(new { planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
    /// <summary>预览不公开原文，应用需要写入授权和未漂移的精确计划引用。</summary>
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
