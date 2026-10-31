using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using NUnit.Framework;
namespace LaunchDesk.Next.UnitTests.Launch;

[TestFixture]
public sealed class BackendTests
{
    [Test]
    public async Task CapturedDraftUsesTheSamePipelineWithoutRequiringSeparateProfileSave()
    {
        var saved=Request();var process=new Gateway();var coordinator=Coordinator(saved,process,new Log());
        var result=await coordinator.ExecuteSnapshotAsync(saved with {ExecutablePath="edited-fixture.exe"});
        Assert.That(result.Status,Is.EqualTo(LaunchStatus.Created));Assert.That(process.Count,Is.EqualTo(1));
    }
    [Test]
    public async Task UnsavedLaunchFieldsAreRejectedEvenWhenVersionMatches()
    {
        var saved = Request();
        var process = new Gateway();
        var coordinator = Coordinator(saved, process, new Log());
        var result = await coordinator.ExecuteAsync(saved with { ExecutablePath = "UNSAVED.exe" });
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Rejected));
        Assert.That(result.Message, Does.Contain("save changes"));
        Assert.That(process.Count, Is.Zero);
    }

    [Test]
    public async Task DictionaryInsertionOrderDoesNotMakeSavedFieldsDirty()
    {
        var saved = Request() with { Environment = new Dictionary<string, string> { ["A"] = "1", ["B"] = "2" } };
        var process = new Gateway();
        var coordinator = Coordinator(saved, process, new Log());
        var result = await coordinator.ExecuteAsync(saved with { Environment = new Dictionary<string, string> { ["B"] = "2", ["A"] = "1" } });
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Created));
        Assert.That(process.Count, Is.EqualTo(1));
    }
    private static LaunchRequest Request() => new LaunchInput { DisplayName = "Fixture", ExecutablePath = "fixture.exe", WorkingDirectory = "fixture", Protocol = EntryProtocol.Direct, CheckLicence = false, Version = 1 }.CreateSnapshot(Guid.NewGuid());
    [Test]
    public void InputSnapshotCopiesCollections()
    {
        var input = new LaunchInput();
        input.Environment["TOOL"] = "old";
        var request = input.CreateSnapshot(Guid.NewGuid());
        input.Environment["TOOL"] = "new";
        Assert.That(request.Environment["TOOL"], Is.EqualTo("old"));
    }
    [Test] public void MonitorOrderMatchesVendorProtocol() => Assert.That(StartMonitor.BuildArguments(new LaunchInput { Product = "E3D", InitializationFile = "launch.init", Project = "SAM", User = "SYSTEM", Mdb = "ALL", Module = "DESIGN" }.CreateSnapshot(Guid.NewGuid())), Is.EqualTo(new[] { "PROD", "E3D", "init", "launch.init", "SAM", "SYSTEM/<secret>", "/ALL", "DESIGN" }));
    [Test] public void MarineOrderMatchesVendorProtocol() => Assert.That(StartMarineBatch.BuildArguments(new LaunchInput { Project = "SEA", User = "SYSTEM", Mdb = "/ALL", Module = "Paragon" }.CreateSnapshot(Guid.NewGuid())), Is.EqualTo(new[] { "Paragon", "noconsole", "SEA", "SYSTEM/<secret>", "/ALL" }));
    [TestCase("a&b")]
    [TestCase("a b")]
    [TestCase("a%b")]
    [TestCase("a!b")]
    [TestCase("a\nb")]
    public void UnsafeMarineTokenRejected(string value) => Assert.Throws<ArgumentException>(() => StartMarineBatch.BuildArguments(new LaunchInput { Project = value, User = "USER", Mdb = "ALL", Module = "Design" }.CreateSnapshot(Guid.NewGuid())));
    [Test]
    public async Task PrecreateLogFailureDoesNotStart()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log { FailAt = 1 });
        var result = await coordinator.ExecuteAsync(r);
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Rejected));
        Assert.That(process.Count, Is.Zero);
    }
    [Test]
    public async Task PostcreateLogFailureDoesNotRetry()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log { FailAt = 5 });
        var first = await coordinator.ExecuteAsync(r);
        var second = await coordinator.ExecuteAsync(r);
        Assert.That(first.Status, Is.EqualTo(LaunchStatus.NeedsVerification));
        Assert.That(second, Is.EqualTo(first));
        Assert.That(process.Count, Is.EqualTo(1));
    }
    [Test]
    public async Task CancellationNeverCreatesProcess()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log());
        var result = await coordinator.ExecuteAsync(r, ct: new CancellationToken(true));
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Cancelled));
        Assert.That(process.Count, Is.Zero);
    }
    [Test]
    public async Task DuplicateAttemptsCreateExactlyOnce()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log());
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => coordinator.ExecuteAsync(r)));
        Assert.That(process.Count, Is.EqualTo(1));
        Assert.That(results.All(v => v.RunId == results[0].RunId), Is.True);
    }
    [Test]
    public async Task ReusedAttemptDifferentInputRejected()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log());
        await coordinator.ExecuteAsync(r);
        var result = await coordinator.ExecuteAsync(r with { Project = "changed" });
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Rejected));
        Assert.That(process.Count, Is.EqualTo(1));
    }
    [Test]
    public async Task StaleVersionRejected()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log());
        var result = await coordinator.ExecuteAsync(r with { Version = 2 });
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Rejected));
        Assert.That(process.Count, Is.Zero);
    }
    [Test]
    public async Task BusyHeldUntilObservationThreshold()
    {
        var r = Request();
        var process = new Gateway();
        var coordinator = Coordinator(r, process, new Log());
        await coordinator.ExecuteAsync(r);
        var result = await coordinator.ExecuteAsync(r with { AttemptId = Guid.NewGuid() });
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Busy));
    }
    [Test]
    public async Task CallerDictionaryCannotMutateExecution()
    {
        var r = Request();
        var map = new Dictionary<string, string> { { "TOOL", "before" } };
        r = r with { Environment = map, CheckLicence = true, LicenceServer = "fixture", LicencePort = 5053 };
        var process = new Gateway();
        var licence = new Licence { Pause = true };
        var coordinator = Coordinator(r, process, new Log(), licence);
        var pending = coordinator.ExecuteAsync(r); map["TOOL"] = "after";
        licence.Ready.SetResult();
        await pending;
        Assert.That(process.Command!.Environment["TOOL"], Is.EqualTo("before"));
    }
    [Test]
    public async Task PromptWithoutSecretCancels()
    {
        var r = Request() with { PasswordPolicy = PasswordPolicy.Prompt };
        var process = new Gateway();
        var result = await Coordinator(r, process, new Log()).ExecuteAsync(r);
        Assert.That(result.Status, Is.EqualTo(LaunchStatus.Cancelled));
        Assert.That(process.Count, Is.Zero);
    }
    private static LoginLaunchCoordinator Coordinator(LaunchRequest request, Gateway gateway, Log log, Licence? licence = null) =>
        new(new CheckSavedProfile(new SavedProfiles(new Document(request))),
            new CheckAvevaLicence(licence ?? new Licence(), new Services()), new ResolveLoginSecret(new Credentials()),
            new StartMonitor(gateway, new Files()), new StartMarineBatch(gateway), new StartDirect(gateway), log,
            TimeSpan.FromMinutes(1));
    private sealed class Document(LaunchRequest request) : IJsonDocumentStore
    {
        public Task<T?> ReadAsync<T>(CancellationToken ct = default) => Task.FromResult((T?)(object)new List<LaunchRequest> { request });
        public Task ReplaceAsync<T>(T value, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Licence : ITcpProbe
    {
        public bool Pause;
        public TaskCompletionSource Ready = new();
        public async Task<bool> ConnectAsync(string host, int port, CancellationToken ct)
        {
            if (Pause) await Ready.Task;
            return true;
        }
    }
    private sealed class Services : IWindowsServiceProbe { public bool IsRunning(string name) => true; }
    private sealed class Files : IFileSystem
    {
        public bool FileExists(string path) => true;
        public bool DirectoryExists(string path) => true;
        public IEnumerable<string> EnumerateFiles(string directory) => [];
        public IEnumerable<string> EnumerateDirectories(string directory) => [];
        public bool IsReparsePoint(string path) => false;
        public string ReadText(string path, int maximumBytes) => throw new NotSupportedException();
    }
    private sealed class Credentials : ICredentialStore { public ISecretHandle? Read(string reference) => null; public void Save(string reference, ISecretHandle secret) => throw new NotSupportedException(); }
    private sealed class Gateway : IProcessHost
    {
        public int Count; public ProcessCommand? Command; public IProcessHandle Start(ProcessCommand command, ISecretHandle? secret)
        {
            Count++;
            Command = command;
            return new Handle();
        }
    }
    private sealed class Handle : IProcessHandle { public int Id => 123; public bool HasExited => false; public bool HasWindow => false; public void Dispose() { } }
    private sealed class Log : IRecordLog { private int count; public int FailAt; public IReadOnlyList<Record> Records => []; public Task<bool> AppendAsync(Guid attemptId, Guid? runId, string stage, string outcome, IReadOnlyDictionary<string, string>? snapshot = null) => Task.FromResult(++count != FailAt); }
}
