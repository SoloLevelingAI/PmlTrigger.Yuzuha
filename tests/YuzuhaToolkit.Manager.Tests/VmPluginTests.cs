using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Plugins.Vm;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class VmPluginTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaVmFixture-" + Guid.NewGuid().ToString("N"));
    private string Allowed => Path.Combine(root, "OwnedClones");
    private string Vm => Path.Combine(Allowed, "fixture", "fixture.vmx");
    private string Snapshots => Path.ChangeExtension(Vm, ".vmsd");
    private string Evidence => Path.Combine(root, "evidence");
    private string Vix => Path.Combine(root, "vendor-fixture", "vix.dll");
    public VmPluginTests()
    {
        Write(Vm, "uuid.bios = \"SYNTHETIC UUID\"\nethernet0.present = \"TRUE\"\nethernet0.generatedAddress = \"00:50:56:00:00:01\"\nethernet0.connectionType = \"hostonly\"\n");
        Write(Snapshots, "snapshot0.displayName = \"baseline\"\n"); Write(Vix, "Synthetic DLL must never be loaded.");
    }
    private static void Write(string file, string text) { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); }
    private VmTaskRequest Request(string profile = "AM") => new(Allowed, Vm, "SYNTHETIC UUID", "00:50:56:00:00:01", profile, Evidence,
        [new("revertSnapshot", "baseline"), new("start"), new("probe")], Vix);

    [Theory]
    [InlineData("AM", "net35")]
    [InlineData("E3D2.1", "net48")]
    [InlineData("E3D3.1.6", "net48")]
    public async Task DiscoveryPlanAndSimulationDoNotCreateSdkSessionOutputOrRealConnection(string profile, string framework)
    {
        var calls = 0; var executor = new OperationExecutor(new OperationRegistry(VmPlugin.Registrations((_, _) => { calls++; throw new Exception("must not open SDK"); })));
        Assert.Single(VmPlanning.Discover([Allowed])); var plan = VmPlanning.Preview(Request(profile));
        Assert.Contains("revertSnapshot:baseline", plan.Effects); Assert.False(Directory.Exists(Evidence));
        var result = await executor.InvokeAsync("vm.simulate", JsonSerializer.SerializeToElement(new { profile, scenario = "ok" }), new());
        Assert.True(result.Success, result.Error?.Message); var data = result.Data!.Value;
        Assert.Equal(framework, data.GetProperty("hostFramework").GetString()); Assert.False(data.GetProperty("realConnectionVerified").GetBoolean());
        Assert.Equal("simulation", data.GetProperty("mode").GetString()); Assert.Equal("unverified", data.GetProperty("checkoutState").GetString()); Assert.Equal(0, calls);
        Assert.DoesNotContain(typeof(ManagerOperationCatalog).Assembly.GetReferencedAssemblies(), a => (a.Name ?? "").Contains("Plugin.Vm"));
    }

    [Theory]
    [InlineData("no-session")]
    [InlineData("pipe-missing")]
    [InlineData("protocol-old")]
    [InlineData("identity-reused")]
    [InlineData("module-changed")]
    public async Task SimulatedFailuresNeverBecomeRealHostSuccess(string scenario)
    {
        var executor = new OperationExecutor(new OperationRegistry(VmPlugin.Registrations()));
        var result = await executor.InvokeAsync("vm.simulate", JsonSerializer.SerializeToElement(new { profile = "AM", scenario }), new());
        Assert.True(result.Success); Assert.Equal("simulated-rejection", result.Data!.Value.GetProperty("outcome").GetString()); Assert.False(result.Data.Value.GetProperty("realConnectionVerified").GetBoolean());
    }

    [Fact]
    public void IdentityNetworkSnapshotLibraryAndOutputChangesAreRejectedBeforeAnyRun()
    {
        var plan = VmPlanning.Preview(Request());
        Assert.Throws<InvalidDataException>(() => VmPlanning.Preview(Request() with { ExpectedUuid = "other" }));
        Assert.Throws<ArgumentException>(() => VmPlanning.Preview(Request() with { OutputDirectory = Path.Combine(Allowed, "output") }));
        Assert.Throws<InvalidDataException>(() => VmPlanning.Preview(Request() with { Steps = [new("revertSnapshot", "missing")] }));
        Assert.Throws<IOException>(() => VmPlanning.Verify(plan with { Request = plan.Request with { OutputDirectory = Path.Combine(root, "other-output") } }, plan.Confirmation));
        File.AppendAllText(Vix, "changed"); Assert.Throws<IOException>(() => VmPlanning.Verify(plan, plan.Confirmation));
        Write(Vix, "Synthetic DLL must never be loaded."); File.AppendAllText(Snapshots, "snapshot1.displayName = \"baseline\"\n");
        Assert.Throws<InvalidDataException>(() => VmPlanning.Preview(Request()));
        Write(Snapshots, "snapshot0.displayName = \"baseline\"\n"); File.AppendAllText(Vm, "ethernet1.present = \"TRUE\"\nethernet1.connectionType = \"nat\"\n");
        Assert.Throws<InvalidDataException>(() => VmPlanning.Preview(Request())); Assert.False(Directory.Exists(Evidence));
    }

    [Fact]
    public async Task ExecuteNeedsAllThreeGrantsAndUsesOneAttemptWithoutRepeatingAnyStep()
    {
        var opened = 0; var actions = new List<string>();
        var executor = new OperationExecutor(new OperationRegistry(VmPlugin.Registrations((_, _) => { opened++; return Task.FromResult<IVmwareSession>(new FakeSession(actions)); })));
        var plan = VmPlanning.Preview(Request()); var attemptId = Guid.NewGuid().ToString("N");
        var input = JsonSerializer.SerializeToElement(new { plan, confirmation = plan.Confirmation, attemptId }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(4, (await executor.InvokeAsync("vm.execute", input, new(AllowWrite: true, AllowLaunch: true))).ExitCode); Assert.Equal(0, opened);
        var result = await executor.InvokeAsync("vm.execute", input, new(AllowExecution: true, AllowWrite: true, AllowLaunch: true)); Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(new[] { "revertSnapshot", "start", "probe" }, actions); Assert.Equal(1, opened);
        Assert.True(File.Exists(result.Data!.Value.GetProperty("planFile").GetString())); Assert.True(File.Exists(result.Data.Value.GetProperty("resultFile").GetString()));
        result = await executor.InvokeAsync("vm.execute", input, new(AllowExecution: true, AllowWrite: true, AllowLaunch: true)); Assert.True(result.Success);
        Assert.Equal(1, opened); Assert.Equal(3, actions.Count);
    }

    [Fact]
    public async Task SubmittedTimeoutRemainsUnknownAndRepeatedAttemptOnlyReadsExistingEvidence()
    {
        var actions = new List<string>(); var opened = 0; var plan = VmPlanning.Preview(Request()); var attempt = Guid.NewGuid().ToString("N");
        Task<IVmwareSession> Open(VmTaskPlan _, CancellationToken token) { opened++; return Task.FromResult<IVmwareSession>(new FakeSession(actions, true)); }
        var failed = await VmTaskRunner.ExecuteAsync(plan, plan.Confirmation, attempt, Open);
        Assert.Equal("unknown", failed.State); Assert.True(failed.MayHaveExecuted); Assert.Single(actions); Assert.True(File.Exists(failed.ResultFile));
        var again = await VmTaskRunner.ExecuteAsync(plan, plan.Confirmation, attempt, Open); Assert.Equal("unknown", again.State); Assert.Equal(1, opened); Assert.Single(actions);
    }

    [Fact]
    public void LocalProtectedCredentialIsOwnerAndTargetBoundAndNeverPlaintextInFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        var key = VmCredentials.TargetKey(Vm, Request().ExpectedUuid, Request().ExpectedMac);
        var receipt = VmCredentials.Store(Path.Combine(root, "private"), key, "ONLY_SYNTHETIC_SECRET");
        Assert.DoesNotContain("ONLY_SYNTHETIC_SECRET", File.ReadAllText(receipt.File));
        var bytes = VmCredentials.Read(receipt.File, key);
        try { Assert.Equal("ONLY_SYNTHETIC_SECRET", Encoding.UTF8.GetString(bytes)); } finally { CryptographicOperations.ZeroMemory(bytes); }
        Assert.Throws<InvalidDataException>(() => VmCredentials.Read(receipt.File, new string('0', 64)));
        var wrongOwner = JsonSerializer.Deserialize(File.ReadAllText(receipt.File), VmJsonContext.Default.VmProtectedCredential)! with { Owner = "Other.Application" };
        File.WriteAllText(receipt.File, JsonSerializer.Serialize(wrongOwner, VmJsonContext.Default.VmProtectedCredential));
        Assert.Throws<InvalidDataException>(() => VmCredentials.Read(receipt.File, key));
    }

    private sealed class FakeSession(List<string> actions, bool timeout = false) : IVmwareSession
    {
        public bool HasDispatched { get; private set; }
        public Task<VmStepEvidence> ExecuteAsync(VmTaskStep step, int index, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); actions.Add(step.Action); HasDispatched |= step.Action != "probe";
            if (timeout) throw new TimeoutException("synthetic submitted timeout");
            return Task.FromResult(new VmStepEvidence(index, step.Action, "observed", 8, 1));
        }
        public void Dispose() { }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
