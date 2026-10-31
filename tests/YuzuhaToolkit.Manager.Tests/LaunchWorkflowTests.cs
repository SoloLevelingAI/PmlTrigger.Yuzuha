using System.Text.Json;
using System.Security.Cryptography;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LaunchWorkflow-" + Guid.NewGuid().ToString("N"));
    private readonly LaunchProfileService profiles;
    private readonly string revision;
    private readonly string entry;
    public LaunchWorkflowTests()
    {
        Directory.CreateDirectory(root);
        entry = Path.Combine(root, "marine.bat"); File.WriteAllText(entry, "synthetic-never-run");
        File.WriteAllText(Path.Combine(root, "evars.bat"), "synthetic-environment-never-run");
        profiles = new(new WorkspaceConfigurationStore(Path.Combine(root, "workspace")));
        profiles.Save(new() { Id = "test", Name = "合成测试", Product = "AM", Kind = "MarineBatch", Executable = entry,
            WorkingDirectory = root, Project = "APS", User = "SYSTEM", Module = "Outfitting" }, "missing", true);
        revision = profiles.Read().Revision;
    }
    private LaunchWorkflowService Service(Func<LaunchProfile, string, int> start) => new(profiles, Path.Combine(root, "receipts"), start);

    [Fact]
    public async Task AdmissionPrecedesBoundaryAndRestartReturnsSameAttemptWithoutPersistingSecret()
    {
        var count = 0;
        var service = Service((_, _) =>
        {
            count++;
            var persisted = File.ReadAllText(Path.Combine(root, "receipts", "operation-1.json"));
            Assert.Contains("accepted", persisted); Assert.DoesNotContain("synthetic-password", persisted);
            return int.MaxValue;
        });
        var plan = service.Plan("test", revision, "operation-1");
        var first = await service.ExecuteAsync(plan, "synthetic-password", "request-1", true);
        var second = await Service((_, _) => throw new Exception("must not replay")).ExecuteAsync(plan, "another-password", "request-2", true);
        Assert.Equal(first, second); Assert.Equal(1, count); Assert.Equal("unverified", first.HostVerification);
        Assert.DoesNotContain("synthetic-password", File.ReadAllText(Path.Combine(root, "receipts", "operation-1.json")));
    }

    [Fact]
    public async Task ChangedTargetAndTamperedPlanNeverReachBoundary()
    {
        var count = 0; var service = Service((_, _) => { count++; return 42; });
        var plan = service.Plan("test", revision, "changed");
        File.AppendAllText(entry, "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(plan, "", null, true));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(plan with { ProfileId = "elsewhere" }, "", null, true));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UnknownOutcomeRemainsUnknownAcrossRestartAndDoesNotLeakException()
    {
        var service = Service((_, password) => throw new IOException(password));
        var plan = service.Plan("test", revision, "unknown");
        var receipt = await service.ExecuteAsync(plan, "synthetic-password", null, true);
        Assert.Equal("unknown", receipt.State);
        var restart = Service((_, _) => throw new Exception("must not replay"));
        var repeated = await restart.ExecuteAsync(plan, "", null, true);
        Assert.Equal(receipt.AttemptId, repeated.AttemptId); Assert.Equal(receipt.State, repeated.State);
        Assert.Equal(receipt.RecoveryActions, repeated.RecoveryActions);
        Assert.Equal("unknown", restart.Observe("unknown").State);
        Assert.DoesNotContain("synthetic-password", File.ReadAllText(Path.Combine(root, "receipts", "unknown.json")));
    }

    [Fact]
    public async Task CrossInstanceLeaseAndStorageFailurePreventAdmission()
    {
        var service = Service((_, _) => throw new Exception("must not launch"));
        var plan = service.Plan("test", revision, "locked");
        Directory.CreateDirectory(Path.Combine(root, "receipts"));
        using (var lease = new FileStream(Path.Combine(root, "receipts", "locked.lease"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(plan, "", null, true));
        File.WriteAllText(Path.Combine(root, "blocked"), "file");
        var blocked = new LaunchWorkflowService(profiles, Path.Combine(root, "blocked"), (_, _) => throw new Exception("must not launch"));
        await Assert.ThrowsAsync<IOException>(() => blocked.ExecuteAsync(plan, "", null, true));
    }

    [Fact]
    public void ExitedEntryReportsOnlyConstrainedModuleCandidatesWithoutClaimingHostOrProjectExit()
    {
        var started = DateTime.UtcNow.AddMinutes(-1).Ticks;
        const int pid = int.MaxValue;
        var valid = new LaunchChildEvidence(1234, pid, started + TimeSpan.FromSeconds(5).Ticks, Path.Combine(root, "des.exe"), true, 1);
        var service = new LaunchWorkflowService(profiles, Path.Combine(root, "receipts"), childSnapshot: _ =>
            [valid, valid with { ParentProcessId = 99 }, valid with { ProcessStartUtcTicks = started - 1 },
             valid with { ProcessStartUtcTicks = started + TimeSpan.FromMinutes(3).Ticks },
             valid with { Executable = Path.Combine(root, "other", "des.exe") }, valid with { Executable = Path.Combine(root, "unknown.exe") }]);
        var plan = service.Plan("test", revision, "handoff");
        var receipt = new LaunchReceipt(plan.OperationId, plan.PlanDigest, "attempt", null, "success", "entry-process-created", "ENTRY_PROCESS_CREATED", "entry created", DateTimeOffset.UtcNow,
            pid, started, EntryExecutable: entry);
        File.WriteAllBytes(Path.Combine(root, "receipts", "handoff.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, LaunchWorkflowJsonContext.Default.LaunchReceipt));
        var observed = service.Observe("handoff");
        Assert.Equal("entry-process-absent", observed.ProcessObservation);
        Assert.Equal(valid, Assert.Single(observed.ChildCandidates!));
        Assert.Contains("模块候选", observed.Message); Assert.Equal("unverified", observed.HostVerification);
    }

    [Fact]
    public void ExitedLegacyEntryWithoutFrozenIdentityLeavesHandoffUnverified()
    {
        var service = Service((_, _) => 42);
        var plan = service.Plan("test", revision, "unverified-handoff");
        var receipt = new LaunchReceipt(plan.OperationId, plan.PlanDigest, "attempt", null, "success", "entry-process-created", "ENTRY_PROCESS_CREATED", "entry created", DateTimeOffset.UtcNow, int.MaxValue);
        File.WriteAllBytes(Path.Combine(root, "receipts", "unverified-handoff.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, LaunchWorkflowJsonContext.Default.LaunchReceipt));
        var observed = service.Observe("unverified-handoff");
        Assert.Contains("模块交接待核验", observed.Message); Assert.Empty(observed.ChildCandidates!);
        Assert.Equal("unverified", observed.HostVerification);
    }

    [Fact]
    public async Task EnvironmentContentAndInitPrecedenceChangesInvalidatePlanWithoutLaunching()
    {
        var count = 0; var service = Service((_, _) => { count++; return 42; });
        var plan = service.Plan("test", revision, "environment-changed");
        var bat = Path.Combine(root, "evars.bat");
        Assert.Contains(plan.Files, file => file.Path == bat);
        File.AppendAllText(bat, "changed");
        var changed = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(plan, "", null, true));
        Assert.StartsWith("TARGET_CHANGED", changed.Message);
        var beforeInit = service.Plan("test", revision, "init-added");
        File.WriteAllText(Path.Combine(root, "evars.init"), "preferred-init-never-run");
        var precedence = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(beforeInit, "", null, true));
        Assert.StartsWith("TARGET_CHANGED", precedence.Message);
        var fresh = service.Plan("test", revision, "init-selected");
        Assert.Contains(fresh.Files, file => file.Path == Path.Combine(root, "evars.init"));
        Assert.DoesNotContain(fresh.Files, file => file.Path == bat);
        Assert.Equal(0, count);
    }

    [Fact]
    public void MissingOrAmbiguousEnvironmentCannotProduceFrozenPlan()
    {
        var service = Service((_, _) => throw new Exception("must not launch"));
        File.Delete(Path.Combine(root, "evars.bat"));
        Assert.StartsWith("EVARS_SELECTION_FAILED", Assert.Throws<InvalidDataException>(() => service.Plan("test", revision, "missing-environment")).Message);
        File.WriteAllText(Path.Combine(root, "evars.init"), "one");
        File.WriteAllText(Path.Combine(root, "evar.init"), "two");
        Assert.StartsWith("EVARS_SELECTION_FAILED", Assert.Throws<InvalidDataException>(() => service.Plan("test", revision, "ambiguous-environment")).Message);
    }

    [Fact]
    public async Task FinalReceiptStorageFailureReturnsUnknownAndOperationFailureWithPossibleExecution()
    {
        var count = 0;
        var service = new LaunchWorkflowService(profiles, Path.Combine(root, "receipts"), (_, _) => { count++; return 42; },
            beforeReceiptWrite: receipt => { if (receipt.State != "accepted") throw new IOException("synthetic disk failure"); });
        var plan = service.Plan("test", revision, "disk-failure");
        var registration = LaunchWorkflowOperations.Registrations(service).Single(item => item.Descriptor.Id == "launch.execute");
        using var json = JsonDocument.Parse("{\"plan\":" + JsonSerializer.Serialize(plan, LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan) + ",\"password\":\"\"}");
        var result = await registration.Handler.InvokeAsync("launch.execute", json.RootElement, new PmlTwin.Phayton.OperationContext(AllowLaunch: true), default);
        Assert.False(result.Success); Assert.True(result.Error!.MayHaveExecuted);
        Assert.Equal("RECEIPT_PERSISTENCE_FAILED", result.Error.Code);
        Assert.Equal("unknown", result.Data!.Value.GetProperty("state").GetString());
        Assert.Equal(1, count);
        var restarted = Service((_, _) => throw new Exception("must not replay"));
        Assert.Equal("unknown", (await restarted.ExecuteAsync(plan, "", null, true)).State);
    }

    [Fact]
    public async Task BoundaryFailureAndFailedUnknownPersistenceStillReturnUnknown()
    {
        var service = new LaunchWorkflowService(profiles, Path.Combine(root, "receipts"), (_, _) => throw new IOException("synthetic secret"),
            beforeReceiptWrite: receipt => { if (receipt.State != "accepted") throw new UnauthorizedAccessException(); });
        var plan = service.Plan("test", revision, "unknown-save-failure");
        var receipt = await service.ExecuteAsync(plan, "", null, true);
        Assert.Equal("unknown", receipt.State); Assert.Equal("RECEIPT_PERSISTENCE_FAILED", receipt.Code);
        Assert.DoesNotContain("synthetic secret", receipt.Message);
    }

    [Fact]
    public void CacheHasHardCapacityAndRejectsOversizedIdentifiers()
    {
        var service = Service((_, _) => 42);
        service.Plan("test", revision, "capacity");
        var cache = Path.Combine(root, "receipts", "plans");
        for (var i = 0; i < 255; i++) File.WriteAllText(Path.Combine(cache, "placeholder-" + i + ".json"), "{}");
        Assert.StartsWith("PLAN_CACHE_FULL", Assert.Throws<InvalidDataException>(() => service.Plan("test", revision, "overflow")).Message);
        Assert.Throws<ArgumentException>(() => service.Plan("test", revision, new string('x', 129)));
        Assert.Equal(256, Directory.EnumerateFiles(cache).Count());
    }

    [Fact]
    public async Task RecomputedClientDigestCannotExtendFrozenExpiryOrDropTargets()
    {
        var service = Service((_, _) => throw new Exception("must not launch"));
        var plan = service.Plan("test", revision, "forged");
        var modified = plan with { ExpiresAt = plan.ExpiresAt.AddYears(1), PlanDigest = "" };
        modified = modified with { PlanDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(modified, LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan))) };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(modified, "", null, true));
        Assert.StartsWith("PLAN_NOT_FOUND", error.Message);
        Assert.False(File.Exists(Path.Combine(root, "receipts", "forged.json")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(plan with { Files = [] }, "", null, true));
    }

    [Fact]
    public async Task DifferentDigestConflictsAndObservationUsesCreationTimeWithoutClaimingLogin()
    {
        var service = Service((_, _) => Environment.ProcessId);
        var plan = service.Plan("test", revision, "observed");
        var receipt = await service.ExecuteAsync(plan, "", null, true);
        Assert.NotNull(receipt.ProcessStartUtcTicks);
        var observed = service.Observe("observed");
        Assert.Equal("same-entry-process-present", observed.ProcessObservation);
        Assert.Equal("unverified", observed.HostVerification);
        var conflicting = service.Plan("test", revision, "observed");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(conflicting, "", null, true));
    }

    [Fact]
    public async Task InterruptedAdmissionBecomesUnknownInsteadOfReplaying()
    {
        var service = Service((_, _) => throw new Exception("must not launch"));
        var plan = service.Plan("test", revision, "interrupted");
        Directory.CreateDirectory(Path.Combine(root, "receipts"));
        var accepted = new LaunchReceipt(plan.OperationId, plan.PlanDigest, "owned-attempt", "request", "accepted", "admitted", "LAUNCH_ADMITTED", "accepted", DateTimeOffset.UtcNow);
        File.WriteAllBytes(Path.Combine(root, "receipts", "interrupted.json"), JsonSerializer.SerializeToUtf8Bytes(accepted, LaunchWorkflowJsonContext.Default.LaunchReceipt));
        var result = await service.ExecuteAsync(plan, "", null, true);
        Assert.Equal("unknown", result.State); Assert.Equal("owned-attempt", result.AttemptId);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
