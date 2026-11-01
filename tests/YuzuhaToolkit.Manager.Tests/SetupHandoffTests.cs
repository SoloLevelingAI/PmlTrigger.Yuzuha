using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PmlTwin.Phayton;
using Xunit;

namespace YuzuhaToolkit.Manager.Tests;

/// <summary>用隔离的真实 v2 标记和旧环境块验证交接、拒绝漂移及未知执行状态。</summary>
public sealed class SetupHandoffTests : IDisposable
{
    private readonly string area = Path.Combine(Path.GetTempPath(), "YuzuhaSetupHandoff-" + Guid.NewGuid().ToString("N"));
    private string Package => Path.Combine(area, "PmlTrigger.Package");
    private string State => Path.Combine(area, "state");
    private string Target => Path.Combine(area, "vendor", "evars.init");
    private string Other => Path.Combine(area, "client", "SKILL.md");
    private string Marker => Path.Combine(Package, ".yuzuha-inno.json");
    private string LegacyBody => "@echo off\r\nrem vendor text remains\r\nset pmllib=vendor\r\nset pmlui=vendor\r\nset SECRET_TOKEN=PRIVATE_FIXTURE\r\n\r\n" +
        "rem >>> Yuzuha managed settings\r\nset pmllib=" + Path.Combine(Package, "PMLLIB") + ";%pmllib%\r\nset pmlui=" + Path.Combine(Package, "PMLUI") + ";%pmlui%\r\nset Yuzuha=E3D2.1\r\nset YUZUHA_STARTUP_ACTIONS=rehash-all\r\nrem <<< Yuzuha managed settings\r\n";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private JsonObject ReadMarker() => JsonNode.Parse(File.ReadAllBytes(Marker))!.AsObject();
    private void WriteMarker(JsonObject marker) => File.WriteAllText(Marker, marker.ToJsonString());
    /// <summary>创建不涉及已安装产品或真实厂商配置的临时安装夹具。</summary>
    public SetupHandoffTests()
    {
        Directory.CreateDirectory(Package);Directory.CreateDirectory(Path.GetDirectoryName(Target)!);Directory.CreateDirectory(Path.GetDirectoryName(Other)!);
        File.WriteAllText(Path.Combine(Package, "payload.txt"), "fixed package bytes");File.WriteAllText(Target, LegacyBody);File.WriteAllText(Other, "unrelated skill");
        File.WriteAllText(Path.Combine(Package, ".setup-payload.json"), JsonSerializer.Serialize(new { version = "fixture", files = new[] { new { path = "payload.txt", sha256 = Hash("fixed package bytes") } } }));
        WriteMarker(new JsonObject { ["owner"] = "Yuzuha.Inno.Plan.v2", ["root"] = Package,
            ["request"] = new JsonObject { ["root"] = Package, ["integrationMode"] = "aveva", ["environments"] = new JsonArray(new JsonObject { ["path"] = Target, ["profile"] = "E3D2.1" }) },
            ["managed"] = new JsonArray(new JsonObject { ["path"] = Target, ["kind"] = "environment", ["original"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("set pmllib=vendor\n")), ["afterSha256"] = Hash(File.ReadAllBytes(Target)) },
                new JsonObject { ["path"] = Other, ["kind"] = "skill", ["original"] = null, ["afterSha256"] = Hash(File.ReadAllBytes(Other)) }),
            ["lastReport"] = "historical report", ["unknownExtension"] = "keep this field" });
    }

    /// <summary>逐项核对载荷，证明检查和预览不修改标记、环境或状态目录。</summary>
    [Fact]
    public void InspectAndPreviewVerifyActualPayloadWithoutWritingMarkerOrEnvironment()
    {
        var service = new SetupHandoffService(State, checkProcesses: _ => { });var marker = File.ReadAllBytes(Marker);var target = File.ReadAllBytes(Target);
        var inspection = service.Inspect(Package);Assert.Equal("aveva", inspection.IntegrationMode);Assert.Equal(1, inspection.FilesVerified);Assert.Equal(1, inspection.LegacyEnvironmentCount);
        var plan = service.PreviewLegacy(Package, Target);Assert.Equal(Hash(marker), plan.MarkerHash);Assert.Equal(Hash(target), plan.EnvironmentHash);
        Assert.Equal(marker, File.ReadAllBytes(Marker));Assert.Equal(target, File.ReadAllBytes(Target));Assert.False(Directory.Exists(State));
        File.WriteAllText(Path.Combine(Package, "payload.txt"), "tampered");Assert.Throws<IOException>(() => service.Inspect(Package));
    }

    /// <summary>保留历史记录和其他条目，并按旧卸载算法证明已移交目标不再被恢复。</summary>
    [Fact]
    public void TransferRetainsOriginalAndOtherEntriesAndLegacyUninstallAlgorithmNoLongerRestoresTarget()
    {
        var service = new SetupHandoffService(State, checkProcesses: _ => { });var originalMarker = File.ReadAllBytes(Marker);var target = File.ReadAllBytes(Target);
        var plan = service.PreviewLegacy(Package, Target);var receipt = service.Transfer(plan);Assert.Equal("transferred", receipt.State);
        Assert.Equal(originalMarker, File.ReadAllBytes(receipt.BackupFile));Assert.True(File.Exists(receipt.ReceiptFile));Assert.Equal(target, File.ReadAllBytes(Target));
        var marker = ReadMarker();Assert.Equal("keep this field", marker["unknownExtension"]!.GetValue<string>());Assert.Empty(marker["request"]!["environments"]!.AsArray());
        // Same legacy uninstall ownership loop: only current marker.managed entries
        // are restored/deleted. The transferred environment is absent from that set.
        foreach (var entry in marker["managed"]!.AsArray())
        {
            var file = entry!["path"]!.GetValue<string>();Assert.NotEqual(Target, file);Assert.Equal(entry["afterSha256"]!.GetValue<string>(), Hash(File.ReadAllBytes(file)));
            if (entry["original"] is null) File.Delete(file);else File.WriteAllBytes(file, Convert.FromBase64String(entry["original"]!.GetValue<string>()));
        }
        Assert.Equal(target, File.ReadAllBytes(Target));Assert.False(File.Exists(Other));Assert.Throws<IOException>(() => service.Transfer(plan));
    }

    /// <summary>拒绝未知所有权版本、环境漂移和重复目标，不覆盖外部修改。</summary>
    [Fact]
    public void UnknownFormatDriftDuplicateOwnershipAndInterveningChangesRejectWithoutEnvironmentWrites()
    {
        var service = new SetupHandoffService(State, checkProcesses: _ => { });var marker = ReadMarker();marker["owner"] = "Yuzuha.Inno.v1";WriteMarker(marker);
        Assert.Throws<IOException>(() => service.Inspect(Package));marker["owner"] = "Yuzuha.Inno.Plan.v2";WriteMarker(marker);
        var plan = service.PreviewLegacy(Package, Target);File.AppendAllText(Target, "external edit");Assert.Throws<IOException>(() => service.Transfer(plan));
        Assert.EndsWith("external edit", File.ReadAllText(Target));
        File.WriteAllText(Target, LegacyBody);marker = ReadMarker();marker["managed"]!.AsArray().Add(marker["managed"]![0]!.DeepClone());WriteMarker(marker);
        Assert.Throws<IOException>(() => service.PreviewLegacy(Package, Target));
    }

    /// <summary>旧安装块交给管理器后只替换一块，保留无关字节并限制公开预览变量。</summary>
    [Fact]
    public async Task InstallerManagedBlockTransfersThenManagerReplacesOneBlockWithoutPublishingUnrelatedVariables()
    {
        var service = new SetupHandoffService(State, checkProcesses: _ => { });var original = File.ReadAllBytes(Target);service.Transfer(service.PreviewLegacy(Package, Target));
        var runtime = Path.Combine(area, "PmlTrigger.ManagerRuntime");var plan = EvarsEditor.Preview(Target, runtime, "E3D2.1", "net48", 65001);
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(plan.UpdatedBase64));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "rem >>> Yuzuha managed settings"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "rem <<< Yuzuha managed settings"));
        Assert.StartsWith(LegacyBody[..LegacyBody.IndexOf("rem >>>", StringComparison.Ordinal)], text);Assert.Contains("set YUZUHA_STARTUP_ACTIONS=rehash-all", text);Assert.Contains(Path.Combine(runtime, "PMLLIB"), text);
        var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(State))));
        var preview = await executor.InvokeAsync("evars.preview", JsonSerializer.SerializeToElement(new { installationRoot = Path.GetDirectoryName(Target), product = "E3D", runtimeRoot = runtime, profile = "E3D2.1", framework = "net48", codePage = 65001 }), new());
        Assert.True(preview.Success, preview.Error?.Message);var summary = preview.Data!.Value.GetProperty("summary");Assert.DoesNotContain("SECRET_TOKEN", summary.GetRawText());Assert.DoesNotContain("PRIVATE_FIXTURE", summary.GetRawText());
        Assert.Contains(summary.GetProperty("beforeManagedSettings").EnumerateArray(), x => x.GetString()!.Contains(Package, StringComparison.Ordinal));
        Assert.Contains(summary.GetProperty("managedSettings").EnumerateArray(), x => x.GetString()!.Contains(runtime, StringComparison.Ordinal));Assert.Equal(original, File.ReadAllBytes(Target));
        var receipt = EvarsEditor.Apply(plan);EvarsEditor.Restore(receipt);Assert.Equal(original, File.ReadAllBytes(Target));
    }

    /// <summary>即使目标摘要匹配，也拒绝无法解释的旧托管块格式。</summary>
    [Fact]
    public void UnrecognizedLegacyBlockCannotTransferEvenWithMatchingRecordedHash()
    {
        File.WriteAllText(Target, "rem >>> Unrecognized settings\nset Yuzuha=E3D2.1\nrem <<< Unrecognized settings\n");
        var marker = ReadMarker();marker["managed"]![0]!["afterSha256"] = Hash(File.ReadAllBytes(Target));WriteMarker(marker);
        Assert.Throws<IOException>(() => new SetupHandoffService(State).PreviewLegacy(Package, Target));
    }

    /// <summary>覆盖活动安装拒绝、替换前并发漂移和替换后回执未知三条边界。</summary>
    [Fact]
    public void GuardedTransferRefusesActiveSetupAndBeforeWriteDriftAndReportsUnknownAfterReplacement()
    {
        var original = File.ReadAllBytes(Marker);var target = File.ReadAllBytes(Target);
        var blocked = new SetupHandoffService(State, checkProcesses: _ => throw new IOException("active setup fixture"));var plan = blocked.PreviewLegacy(Package, Target);
        Assert.Throws<IOException>(() => blocked.Transfer(plan));Assert.Equal(original, File.ReadAllBytes(Marker));
        var changed = new SetupHandoffService(State, phase => { if (phase == "before-marker") File.AppendAllText(Marker, " "); }, _ => { });
        Assert.Throws<IOException>(() => changed.Transfer(plan));Assert.Equal(target, File.ReadAllBytes(Target));File.WriteAllBytes(Marker, original);
        var unknown = new SetupHandoffService(State, phase => { if (phase == "after-marker") throw new IOException("receipt fixture failure"); }, _ => { });
        var error = Assert.Throws<SetupTransferUnknownException>(() => unknown.Transfer(plan));Assert.Equal("unknown", error.Receipt.State);Assert.Equal(target, File.ReadAllBytes(Target));
        Assert.Equal(original, File.ReadAllBytes(error.Receipt.BackupFile));Assert.Empty(Directory.GetFiles(Package, "*.tmp"));
    }

    /// <summary>操作目录要求显式写入授权和冻结引用，摘要不公开原环境字节。</summary>
    [Fact]
    public async Task OperationsRequireWriteAuthorizationAndFreezeExactPlanWithoutPublishingOriginalBytes()
    {
        var service = new SetupHandoffService(State, checkProcesses: _ => { });var executor = new OperationExecutor(new OperationRegistry(SetupHandoffOperations.Registrations(new ManagerPlanStore(State), service)));
        var inspect = await executor.InvokeAsync("setup.handoff.inspect", JsonSerializer.SerializeToElement(new { packageRoot = Package }), new());Assert.True(inspect.Success, inspect.Error?.Message);
        var result = await executor.InvokeAsync("setup.legacy.preview", JsonSerializer.SerializeToElement(new { packageRoot = Package, environmentFile = Target }), new());Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;Assert.DoesNotContain("originalBase64", data.GetRawText());Assert.DoesNotContain("set pmllib", data.GetRawText());
        var reference = JsonSerializer.SerializeToElement(new { planId = data.GetProperty("planId").GetString(), sha256 = data.GetProperty("sha256").GetString() });
        var denied = await executor.InvokeAsync("setup.legacy.transfer", reference, new());Assert.Equal(4, denied.ExitCode);
        var applied = await executor.InvokeAsync("setup.legacy.transfer", reference, new(AllowWrite: true));Assert.True(applied.Success, applied.Error?.Message);Assert.Equal("transferred", applied.Data!.Value.GetProperty("state").GetString());
    }

    /// <summary>标记替换后的失败保留可能执行语义和可人工核对的未知回执。</summary>
    [Fact]
    public async Task TransferOperationPreservesMayHaveExecutedAndUnknownReceiptAfterMarkerReplacement()
    {
        var service = new SetupHandoffService(State, phase => { if (phase == "after-marker") throw new IOException("post-replacement fixture"); }, _ => { });
        var executor = new OperationExecutor(new OperationRegistry(SetupHandoffOperations.Registrations(new ManagerPlanStore(State), service)));
        var preview = await executor.InvokeAsync("setup.legacy.preview", JsonSerializer.SerializeToElement(new { packageRoot = Package, environmentFile = Target }), new());Assert.True(preview.Success);
        var data = preview.Data!.Value;var reference = JsonSerializer.SerializeToElement(new { planId = data.GetProperty("planId").GetString(), sha256 = data.GetProperty("sha256").GetString() });
        var result = await executor.InvokeAsync("setup.legacy.transfer", reference, new(AllowWrite: true));Assert.False(result.Success);Assert.Equal("SETUP_TRANSFER_UNKNOWN", result.Error!.Code);Assert.True(result.Error.MayHaveExecuted);Assert.Equal("unknown", result.Data!.Value.GetProperty("state").GetString());
        Assert.Equal(LegacyBody, File.ReadAllText(Target));
    }

    /// <summary>安装器拥有的环境禁止两类预览；显式移交后才可应用并恢复原环境。</summary>
    [Fact]
    public async Task OwnedEnvironmentBlocksBothPreviewsUntilExplicitTransferThenAllowsApplyAndRestore()
    {
        PrepareRuntimePayload();var original = File.ReadAllBytes(Target);var marker = File.ReadAllBytes(Marker);var runtime = Path.Combine(area, "managed-runtime");
        var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(State))));
        var input = JsonSerializer.SerializeToElement(new { packageRoot = Package, installationRoot = Path.GetDirectoryName(Target), environmentFile = Target, product = "E3D", runtimeRoot = runtime, profile = "E3D2.1", framework = "net48", codePage = 65001 });
        var refused = await executor.InvokeAsync("evars.preview", input, new());Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", refused.Error?.Code);Assert.False(refused.Error!.MayHaveExecuted);
        var deployment = new RuntimeDeploymentService(State, Path.Combine(area, "user"));var request = new RuntimeDeploymentRequest(Package, runtime, "copy", Path.GetDirectoryName(Target)!, "E3D", "E3D2.1", "net48", Target, 65001);
        Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", Assert.Throws<SetupEnvironmentOwnershipException>(() => deployment.Preview(request)).Code);
        var deploymentExecutor = new OperationExecutor(new OperationRegistry(RuntimeDeploymentOperations.Registrations(new ManagerPlanStore(State), deployment)));
        var deploymentRefused = await deploymentExecutor.InvokeAsync("deployment.preview", JsonSerializer.SerializeToElement(new { packageRoot = Package, runtimeRoot = runtime, mode = "copy", installationRoot = Path.GetDirectoryName(Target), product = "E3D", profile = "E3D2.1", framework = "net48", environmentFile = Target, codePage = 65001 }), new());Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", deploymentRefused.Error?.Code);Assert.False(deploymentRefused.Error!.MayHaveExecuted);
        Assert.False(Directory.Exists(State));Assert.False(Directory.Exists(runtime));Assert.Equal(marker, File.ReadAllBytes(Marker));Assert.Equal(original, File.ReadAllBytes(Target));
        var handoff = new SetupHandoffService(State, checkProcesses: _ => { });handoff.Transfer(handoff.PreviewLegacy(Package, Target));
        var preview = await executor.InvokeAsync("evars.preview", input, new());Assert.True(preview.Success, preview.Error?.Message);
        var applied = await executor.InvokeAsync("evars.apply", Reference(preview.Data!.Value), new(AllowWrite: true));Assert.True(applied.Success, applied.Error?.Message);
        var restored = await executor.InvokeAsync("evars.restore", Reference(applied.Data!.Value), new(AllowWrite: true));Assert.True(restored.Success, restored.Error?.Message);Assert.Equal(original, File.ReadAllBytes(Target));
        var plan = deployment.Preview(request);var receipt = deployment.Apply(plan);Assert.Empty(deployment.Restore(receipt).RecoveryErrors);Assert.Equal(original, File.ReadAllBytes(Target));
    }

    /// <summary>预览后旧安装器重新持有目标时，两类应用均在修改环境前拒绝。</summary>
    [Fact]
    public async Task OwnershipReclaimedAfterPreviewBlocksBothApplyWithoutEnvironmentWrites()
    {
        PrepareRuntimePayload();var original = File.ReadAllBytes(Target);var marker = File.ReadAllBytes(Marker);var runtime = Path.Combine(area, "managed-runtime");
        var handoff = new SetupHandoffService(State, checkProcesses: _ => { });handoff.Transfer(handoff.PreviewLegacy(Package, Target));
        var executor = new OperationExecutor(new OperationRegistry(EvarsOperations.Registrations(new ManagerPlanStore(State))));
        var preview = await executor.InvokeAsync("evars.preview", JsonSerializer.SerializeToElement(new { packageRoot = Package, installationRoot = Path.GetDirectoryName(Target), environmentFile = Target, product = "E3D", runtimeRoot = runtime, profile = "E3D2.1", framework = "net48", codePage = 65001 }), new());Assert.True(preview.Success, preview.Error?.Message);
        var service = new RuntimeDeploymentService(State, Path.Combine(area, "user"));var plan = service.Preview(new(Package, runtime, "copy", Path.GetDirectoryName(Target)!, "E3D", "E3D2.1", "net48", Target, 65001));
        File.WriteAllBytes(Marker, marker);
        var applied = await executor.InvokeAsync("evars.apply", Reference(preview.Data!.Value), new(AllowWrite: true));Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", applied.Error?.Code);Assert.False(applied.Error!.MayHaveExecuted);
        Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", Assert.Throws<SetupEnvironmentOwnershipException>(() => service.Apply(plan)).Code);Assert.False(Directory.Exists(runtime));Assert.Equal(original, File.ReadAllBytes(Target));
        handoff.Transfer(handoff.PreviewLegacy(Package, Target));
        var late = new RuntimeDeploymentService(State, Path.Combine(area, "user"), phase => { if (phase == "environment") File.WriteAllBytes(Marker, marker); });
        var lateExecutor = new OperationExecutor(new OperationRegistry(RuntimeDeploymentOperations.Registrations(new ManagerPlanStore(State), late)));
        var latePreview = await lateExecutor.InvokeAsync("deployment.preview", JsonSerializer.SerializeToElement(plan.Request, ManagerJsonContext.Default.RuntimeDeploymentRequest), new());Assert.True(latePreview.Success, latePreview.Error?.Message);
        var lateResult = await lateExecutor.InvokeAsync("deployment.apply", Reference(latePreview.Data!.Value), new(AllowWrite: true));Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", lateResult.Error?.Code);Assert.Empty(lateResult.Data!.Value.GetProperty("recoveryErrors").EnumerateArray());Assert.Equal(original, File.ReadAllBytes(Target));
    }

    /// <summary>没有显式包参数也核对已登记根；未知或重复记录必须人工核对而不能放行。</summary>
    [Fact]
    public void RegisteredOwnershipAndUnknownRecordsCannotBeBypassedByOmittingPackageRoot()
    {
        var guard = new SetupEnvironmentOwnershipGuard(() => [Package]);
        Assert.Equal("SETUP_OWNERSHIP_TRANSFER_REQUIRED", Assert.Throws<SetupEnvironmentOwnershipException>(() => guard.EnsureAvailable(Target, Path.Combine(area, "other-runtime"))).Code);
        var marker = ReadMarker();marker["owner"] = "unknown";WriteMarker(marker);
        Assert.Equal("SETUP_OWNERSHIP_UNRESOLVED", Assert.Throws<SetupEnvironmentOwnershipException>(() => guard.EnsureAvailable(Target)).Code);
        marker["owner"] = "Yuzuha.Inno.Plan.v2";marker["managed"]!.AsArray().Add(marker["managed"]![0]!.DeepClone());WriteMarker(marker);
        Assert.Equal("SETUP_OWNERSHIP_UNRESOLVED", Assert.Throws<SetupEnvironmentOwnershipException>(() => guard.EnsureAvailable(Target)).Code);
    }

    private static JsonElement Reference(JsonElement value) => JsonSerializer.SerializeToElement(new { planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
    private void PrepareRuntimePayload()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Target)!, "mon.exe"), "synthetic installation, never executed");
        foreach (var (path, text) in new[] { ("PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc", "define function !!YuzuhaResolveRuntimePath()\n  !folderName = 'PMLTRI'\nendfunction\n"), ("PMLUI/bootstrap.pmlfnc", "synthetic PML, never executed"), ("runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll", "synthetic Host, never loaded"), ("runtime/cli/PmlTwin.Phayton.Cli.exe", "synthetic CLI, never executed") })
        { var file = Path.Combine(Package, path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file, text); }
        var files = Directory.GetFiles(Package, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) is not (".setup-payload.json" or ".yuzuha-inno.json"))
            .Select(f => new { path = Path.GetRelativePath(Package, f).Replace('\\', '/'), sha256 = Hash(File.ReadAllBytes(f)) }).ToArray();
        File.WriteAllText(Path.Combine(Package, ".setup-payload.json"), JsonSerializer.Serialize(new { version = "fixture", files }));
    }
    /// <summary>仅删除本测试实例的隔离临时目录。</summary>
    public void Dispose() { if (Directory.Exists(area)) Directory.Delete(area, true); }
}
