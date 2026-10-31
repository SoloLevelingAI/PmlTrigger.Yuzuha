using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class ConnectionEvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly OperationContext Context = new(42, "Design", ExpectedProcessStartTimeUtcTicks: 12345);
    private static CommunicationResponse Response(int pid = 42, long ticks = 12345, string model = "Design", string pipe = "fixture-pipe-42",
        int protocol = 1, string framework = "net48") => new(1, Guid.NewGuid().ToString("N"),
        OperationResult.Ok("aveva.session.status", JsonSerializer.SerializeToElement(new {
            SessionSelected = true, Connected = true, TargetVerified = true, ExpectedProcessId = 42,
            ExpectedProcessStartTimeUtcTicks = 12345L, ExpectedModel = "Design", PipeName = "fixture-pipe-42",
            Host = new { ProcessId = pid, ProcessStartTimeUtcTicks = ticks, Model = model, PipeName = pipe,
                HostFramework = framework, FormQueryVersion = protocol, BoundedArrayReadVersion = protocol }
        })), "fixture/1");

    [Fact]
    public void OnlyCompleteCorrelatedIdentityProtocolAndDeploymentBindingCanBeGreen()
    {
        var evidence = ConnectionEvidence.FromResponse(Response(), Context, "E3D2.1", "hash-a", Now);
        Assert.Equal("green", evidence.Overall.State); Assert.Equal("TARGET_VERIFIED", evidence.Identity.Code);
        Assert.Equal(Now.AddSeconds(60), evidence.ExpiresAtUtc);
        Assert.Equal(evidence, ConnectionEvidence.Revalidate(evidence, 42, 12345, "Design", "hash-a", Now.AddSeconds(10)));
        var unbound = ConnectionEvidence.FromResponse(Response(), Context, null, "unbound", Now);
        Assert.Equal("yellow", unbound.Overall.State); Assert.Equal("PROFILE_UNBOUND", unbound.Protocol.Code);
    }

    [Theory]
    [InlineData(43, 12345, "Design", "fixture-pipe-42", "net48")]
    [InlineData(42, 99999, "Design", "fixture-pipe-42", "net48")]
    [InlineData(42, 12345, "Draw", "fixture-pipe-42", "net48")]
    [InlineData(42, 12345, "Unknown", "fixture-pipe-42", "net48")]
    [InlineData(42, 12345, "Design", "wrong-pipe", "net48")]
    [InlineData(42, 12345, "Design", "fixture-pipe-42", "net35")]
    public void PidReuseModulePipeOrFrameworkMismatchNeverProducesOverallGreen(int pid, long ticks, string model, string pipe, string framework)
    { Assert.Equal("red", ConnectionEvidence.FromResponse(Response(pid, ticks, model, pipe, framework: framework), Context, "E3D2.1", "hash-a", Now).Overall.State); }

    [Fact]
    public void OlderHostProtocolsAndCommunicationUnknownStayYellow()
    {
        var old = ConnectionEvidence.FromResponse(Response(protocol: 0), Context, "E3D2.1", "hash-a", Now);
        Assert.Equal("yellow", old.Overall.State); Assert.Equal("PROTOCOL_INSUFFICIENT", old.Protocol.Code);
        var timeout = Response() with { Result = OperationResult.Fail("aveva.session.status", 7, "CLI_TIMEOUT", "synthetic timeout") };
        Assert.Equal("yellow", ConnectionEvidence.FromResponse(timeout, Context, "E3D2.1", "hash-a", Now).Overall.State);
        var invalid = Response() with { ProtocolVersion = 999 };
        Assert.Equal("red", ConnectionEvidence.FromResponse(invalid, Context, "E3D2.1", "hash-a", Now).Overall.State);
    }

    [Theory]
    [InlineData(null, null, "Design", "hash-a", 1, "PROCESS_EXITED")]
    [InlineData(42, 9999L, "Design", "hash-a", 1, "PROCESS_CHANGED")]
    [InlineData(42, 12345L, "Draw", "hash-a", 1, "MODULE_CHANGED")]
    [InlineData(42, 12345L, "Design", "hash-b", 1, "DEPLOYMENT_CHANGED")]
    [InlineData(42, 12345L, "Design", "hash-a", 60, "EVIDENCE_EXPIRED")]
    public void OldGreenEvidenceIsClearedOnEveryInvalidation(int? pid, long? ticks, string model, string fingerprint, int seconds, string code)
    {
        var original = ConnectionEvidence.FromResponse(Response(), Context, "E3D2.1", "hash-a", Now);
        var current = ConnectionEvidence.Revalidate(original, pid, ticks, model, fingerprint, Now.AddSeconds(seconds));
        Assert.Equal(code, current.Overall.Code); Assert.NotEqual("green", current.Overall.State);
        Assert.All(new[] { current.Identity, current.Pipe, current.Protocol, current.Deployment }, lamp => Assert.NotEqual("green", lamp.State));
    }

    [Fact]
    public void DiscoveryCannotProduceHostSuccessAndRuntimeFingerprintDetectsByteChanges()
    {
        Assert.Equal("NO_SESSIONS", ConnectionEvidence.DiscoveryLamp(0).Code);
        Assert.Equal("HOST_NOT_TESTED", ConnectionEvidence.DiscoveryLamp(1).Code);
        Assert.Equal("TARGET_SELECTION_REQUIRED", ConnectionEvidence.DiscoveryLamp(2).Code);
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaConnectionBinding-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = Path.Combine(root, "runtime/profiles/AM/net35/YuzuhaToolkit.PmlHost.Net35.dll");
            var bootstrap = Path.Combine(root, "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc");
            Directory.CreateDirectory(Path.GetDirectoryName(host)!); Directory.CreateDirectory(Path.GetDirectoryName(bootstrap)!);
            File.WriteAllText(host, "synthetic-a"); File.WriteAllText(bootstrap, "synthetic bootstrap");
            WriteStartupFiles(root);
            var hash = ConnectionEvidence.Fingerprint(root, "AM"); File.WriteAllText(host, "synthetic-b");
            Assert.NotEqual(hash, ConnectionEvidence.Fingerprint(root, "AM"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    internal static void WriteStartupFiles(string root)
    {
        foreach (var relative in new[] { "PMLLIB/Addins/YuzuhaRpc.pmlobj", "PMLLIB/Addins/YuzuhaAddin.pmlobj", "PMLLIB/pml.index" })
        {
            var file = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "synthetic startup never executed: " + relative);
        }
    }

    [Theory]
    [InlineData("PMLLIB/Addins/YuzuhaRpc.pmlobj")]
    [InlineData("PMLLIB/Addins/YuzuhaAddin.pmlobj")]
    [InlineData("PMLLIB/pml.index")]
    public void StartupByteChangesClearEveryOldGreenLampAndMissingFilesFailClosed(string relative)
    {
        var root = Path.Combine(Path.GetTempPath(), "YuzuhaStartupBinding-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = Path.Combine(root, "runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll");
            var bootstrap = Path.Combine(root, "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc");
            Directory.CreateDirectory(Path.GetDirectoryName(host)!); Directory.CreateDirectory(Path.GetDirectoryName(bootstrap)!);
            File.WriteAllText(host, "synthetic host"); File.WriteAllText(bootstrap, "synthetic resolver");
            WriteStartupFiles(root);
            var originalHash = ConnectionEvidence.Fingerprint(root, "E3D2.1");
            Assert.Equal(originalHash, ConnectionEvidence.Fingerprint(root, "E3D2.1"));
            var original = ConnectionEvidence.FromResponse(Response(), Context, "E3D2.1", originalHash, Now);
            Assert.Equal("green", original.Overall.State);
            var file = Path.Combine(root, relative);
            File.AppendAllText(file, "changed");
            var changedHash = ConnectionEvidence.Fingerprint(root, "E3D2.1");
            Assert.NotEqual(originalHash, changedHash);
            var changed = ConnectionEvidence.Revalidate(original, 42, 12345, "Design", changedHash, Now.AddSeconds(1));
            Assert.Equal("DEPLOYMENT_CHANGED", changed.Overall.Code);
            Assert.All(new[] { changed.Overall, changed.Identity, changed.Pipe, changed.Protocol, changed.Deployment }, lamp => Assert.NotEqual("green", lamp.State));
            File.Delete(file);
            Assert.Throws<FileNotFoundException>(() => ConnectionEvidence.Fingerprint(root, "E3D2.1"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ManagerRequiresModuleBeforeStartingCommunicationProcess()
    {
        var executor = ManagerOperationCatalog.Create(communication: () => throw new Exception("must not start"));
        var result = await executor.InvokeAsync("connection.test", JsonSerializer.SerializeToElement(new { }), new(42, ExpectedProcessStartTimeUtcTicks: 12345));
        Assert.Equal("MODULE_IDENTITY_REQUIRED", result.Error?.Code);
    }
}
