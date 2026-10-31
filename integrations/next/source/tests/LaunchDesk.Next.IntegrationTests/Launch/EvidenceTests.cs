using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Platform.Storage;
using LaunchDesk.Next.Platform.Files;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Credentials;
using LaunchDesk.Next.Platform.Diagnostics;
using LaunchDesk.Next.Platform.Processes;
using NUnit.Framework;
namespace LaunchDesk.Next.IntegrationTests.Launch;

[TestFixture]
public sealed class EvidenceTests
{
    [Test]
    public async Task RestartLoadsHistoryAndContinuesSequence()
    {
        var path = Path.Combine(root, "restart.jsonl");
        var oldRun = Guid.NewGuid();
        using (var first = new NLogRecordLog(path))
            Assert.That(await first.AppendAsync(Guid.NewGuid(), oldRun, "Created", "Created"), Is.True);
        using (var second = new NLogRecordLog(path))
        {
            Assert.That(second.Records.Single().RunId, Is.EqualTo(oldRun));
            Assert.That(await second.AppendAsync(Guid.NewGuid(), null, "Restarted", "Valid"), Is.True);
            Assert.That(second.Records.Select(record => record.Sequence), Is.EqualTo(new long[] { 1, 2 }));
        }
        Assert.That(NLogRecordLog.Read(path).Records.Select(record => record.Sequence), Is.EqualTo(new long[] { 1, 2 }));
    }

    [Test]
    public async Task RestartPreservesCorruptTailAndValidLaterHistory()
    {
        var path = Path.Combine(root, "damaged-restart.jsonl");
        using (var first = new NLogRecordLog(path))
            Assert.That(await first.AppendAsync(Guid.NewGuid(), null, "Before", "Valid"), Is.True);
        await File.AppendAllTextAsync(path, "{truncated");
        using (var second = new NLogRecordLog(path))
        {
            Assert.That(second.HasDamagedHistory, Is.True);
            Assert.That(second.Records.Count, Is.EqualTo(1));
            Assert.That(await second.AppendAsync(Guid.NewGuid(), null, "After", "Valid"), Is.True);
        }
        using (var third = new NLogRecordLog(path))
        {
            Assert.That(third.HasDamagedHistory, Is.True);
            Assert.That(third.Records.Select(record => record.Sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(await third.AppendAsync(Guid.NewGuid(), null, "AfterAgain", "Valid"), Is.True);
        }
        Assert.That(File.ReadAllText(path), Does.Contain("{truncated"));
        var read = NLogRecordLog.Read(path);
        Assert.That(read.DamagedTail, Is.True);
        Assert.That(read.Records.Select(record => record.Sequence), Is.EqualTo(new long[] { 1, 2, 3 }));
    }
    private string root = "";
    [SetUp]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "LaunchDesk.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }
    [TearDown] public void Cleanup() { Directory.Delete(root, true); }
    [Test]
    public async Task NLogConcurrentWritesAreOrderedAndReReadable()
    {
        var path = Path.Combine(root, "records.jsonl");
        var input = new Dictionary<string, string> { { "Project", "A\nB\"C" }, { "Password", "FAKE-SECRET-NEVER-LOG" }, { "ToolToken", "FAKE-SECRET-NEVER-LOG" } };
        using (var log = new NLogRecordLog(path))
        {
            Assert.That(await log.AppendAsync(Guid.NewGuid(), null, "Submit", "Valid", input), Is.True); input["Project"] = "changed";
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 100).Select(i => log.AppendAsync(Guid.NewGuid(), null, "Parallel", i.ToString())));
            Assert.That(outcomes.All(x => x), Is.True);
            Assert.That(log.Records[0].Snapshot["Project"], Is.EqualTo("A\nB\"C"));
        }
        var read = NLogRecordLog.Read(path);
        Assert.That(read.Records.Count, Is.EqualTo(101));
        Assert.That(read.Records.Select(r => r.Sequence), Is.EqualTo(Enumerable.Range(1, 101).Select(i => (long)i)));
        Assert.That(File.ReadAllText(path), Does.Not.Contain("FAKE-SECRET-NEVER-LOG"));
        Assert.That(read.DamagedTail, Is.False);
        await File.AppendAllTextAsync(path, "{truncated");
        var damaged = NLogRecordLog.Read(path);
        Assert.That(damaged.Records.Count, Is.EqualTo(101));
        Assert.That(damaged.DamagedTail, Is.True);
    }
    [Test]
    public async Task NLogTargetDirectoryFailureIsReported()
    {
        var path = Path.Combine(root, "is-directory");
        Directory.CreateDirectory(path); using var log = new NLogRecordLog(path);
        Assert.That(await log.AppendAsync(Guid.NewGuid(), null, "Gate", "Required"), Is.False);
        Assert.That(log.Records.Count, Is.EqualTo(1));
    }
    [Test]
    public async Task IndependentConfigRoundtripAndVersionConflict()
    {
        var store = new SavedProfiles(new JsonDocumentStore(Path.Combine(root, "profiles.json")));
        var r = new LaunchInput { DisplayName = "safe", ExecutablePath = "fixture.exe", WorkingDirectory = root, Protocol = EntryProtocol.Direct, CheckLicence = false }.CreateSnapshot(Guid.NewGuid());
        var saved = await store.SaveAsync(r);
        Assert.That(saved.Success, Is.True);
        Assert.That(saved.Configuration!.Version, Is.EqualTo(1));
        Assert.That((await store.ReadAsync(r.ConfigurationId))!.DisplayName, Is.EqualTo("safe"));
        Assert.That((await store.SaveAsync(r)).Success, Is.False);
    }
    [Test]
    public async Task ActualIsolatedProcessReceivesChildEnvironment()
    {
        var output = Path.Combine(root, "child.txt");
        var script = Path.Combine(root, "child.cmd");
        await File.WriteAllTextAsync(script, "@echo off\r\necho %LD_NEXT_FIXTURE%>\"" + output + "\"\r\n");
        // A batch fixture exercises the real cmd handoff; it never invokes a vendor binary.
        var r = new LaunchInput { DisplayName = "isolated", ExecutablePath = script, WorkingDirectory = root, Protocol = EntryProtocol.Marine, Project = "SAM", User = "SYSTEM", Mdb = "ALL", Module = "Design", CheckLicence = false, Environment = new() { { "LD_NEXT_FIXTURE", "child-only" } } }.CreateSnapshot(Guid.NewGuid());
        var original = System.Environment.GetEnvironmentVariable("LD_NEXT_FIXTURE"); using var secret = new SecretHandle("fixture-password"); using var handle = new StartMarineBatch(new WindowsProcessHost()).Execute(r, secret);
        for (var i = 0; i < 50 && !File.Exists(output); i++) await Task.Delay(100);
        Assert.That(File.Exists(output), Is.True);
        Assert.That((await File.ReadAllTextAsync(output)).Trim(), Is.EqualTo("child-only"));
        Assert.That(System.Environment.GetEnvironmentVariable("LD_NEXT_FIXTURE"), Is.EqualTo(original));
    }
    [Test]
    public void SecretHandleDisposalRejectsFurtherReads()
    {
        var secret = new SecretHandle("not-for-evidence");
        secret.Dispose();
        Assert.Throws<ObjectDisposedException>(() => secret.Reveal());
    }
    [Test]
    public async Task NativeMonitorPreservesEightArgumentsAndSpaces()
    {
        var repository = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));
        var receiver = Path.Combine(repository, "tests/LaunchDesk.Next.ProcessFixture/bin/Debug/net10.0/LaunchDesk.Next.ProcessFixture.exe");
        Assert.That(File.Exists(receiver), Is.True, "Build the approved isolated ProcessFixture before this test.");
        var output = Path.Combine(root, "native.json");
        var init = Path.Combine(root, "init with spaces.init");
        await File.WriteAllTextAsync(init, "");
        var r = new LaunchInput { DisplayName = "native fixture", ExecutablePath = receiver, WorkingDirectory = root, Protocol = EntryProtocol.Monitor, Product = "E3D", InitializationFile = init, Project = "SAM", User = "SYSTEM", Mdb = "ALL", Module = "Design", CheckLicence = false, LicenceServer = "fixture-server", Environment = new() { { "LD_NEXT_OUTPUT", output }, { "LD_NEXT_FIXTURE", "child-only" } } }.CreateSnapshot(Guid.NewGuid());
        using var secret = new SecretHandle("fixture password"); using var handle = new StartMonitor(new WindowsProcessHost(), new WindowsFileSystem()).Execute(r, secret);
        for (var i = 0; i < 50 && !File.Exists(output); i++) await Task.Delay(100);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(output));
        Assert.That(json.RootElement.GetProperty("Arguments").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "PROD", "E3D", "init", init, "SAM", "SYSTEM/fixture password", "/ALL", "Design" }));
        Assert.That(json.RootElement.GetProperty("Licence").GetString(), Is.EqualTo("fixture-server"));
    }
}
