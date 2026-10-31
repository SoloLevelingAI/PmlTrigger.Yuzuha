using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
using NUnit.Framework;

namespace LaunchDesk.Next.UnitTests.Launch;

public sealed class FeatureScriptsTests
{
    [Test]
    public async Task DisabledLicenceDoesNotProbeNetworkOrServices()
    {
        var tcp = new Tcp();
        var services = new Services();
        var result = await new CheckAvevaLicence(tcp, services).ExecuteAsync(Profile(), default);
        Assert.That(result, Is.True);
        Assert.That(tcp.Ports, Is.Empty);
        Assert.That(services.Names, Is.Empty);
    }

    [Test]
    public async Task LocalRmsUsesServiceAndOptionalRulesStillUsesTcp()
    {
        var tcp = new Tcp();
        var services = new Services { Running = false };
        var profile = Profile() with
        {
            CheckLicence = true,
            UseLocalRmsService = true,
            LocalRmsServiceName = "fixture-rms",
            RequireRules = true,
            RulesPort = 5054
        };
        var result = await new CheckAvevaLicence(tcp, services).ExecuteAsync(profile, default);
        Assert.That(result, Is.False);
        Assert.That(services.Names, Is.EqualTo(new[] { "fixture-rms" }));
        Assert.That(tcp.Ports, Is.EqualTo(new[] { 5054 }));
    }

    [Test]
    public async Task RemoteRmsAndRulesAreBothCheckedEvenWhenRmsFails()
    {
        var tcp = new Tcp { Reachable = false };
        var services = new Services();
        var profile = Profile() with { CheckLicence = true, LicencePort = 5093, RequireRules = true, RulesPort = 5054 };
        Assert.That(await new CheckAvevaLicence(tcp, services).ExecuteAsync(profile, default), Is.False);
        Assert.That(tcp.Ports, Is.EqualTo(new[] { 5093, 5054 }));
        Assert.That(services.Names, Is.Empty);
    }

    [Test]
    public void MissingMonitorInitializationNeverCallsProcessHost()
    {
        var host = new Host();
        Assert.Throws<ArgumentException>(() => new StartMonitor(host, new Files { Exists = false }).Execute(Profile(), null));
        Assert.That(host.Command, Is.Null);
    }

    [Test]
    public void MarineRejectsNativeExeBeforeCallingProcessHost()
    {
        var host = new Host();
        Assert.Throws<ArgumentException>(() => new StartMarineBatch(host).Execute(Profile(), null));
        Assert.That(host.Command, Is.Null);
    }

    [Test]
    public void MonitorBuildsDetachedNeutralCommandAndKeepsSecretOutOfArguments()
    {
        var host = new Host();
        var environment = new Dictionary<string, string> { ["TOOL"] = "before" };
        var profile = Profile() with { Environment = environment, LicenceServer = "licence-fixture" };
        using var secret = new Secret();
        using var process = new StartMonitor(host, new Files()).Execute(profile, secret);
        environment["TOOL"] = "after";
        var command = host.Command!;
        Assert.That(command.Environment["TOOL"], Is.EqualTo("before"));
        Assert.That(command.Environment["AVEVA_LICENSE_SERVER_LIST"], Is.EqualTo("licence-fixture"));
        Assert.That(command.Arguments[5], Is.EqualTo("SYSTEM/<secret>"));
        Assert.That(command.Invocation, Is.EqualTo(ProcessInvocation.Native));
        Assert.That(command.Observation, Is.EqualTo(ProcessObservation.DescendantsMayDetach));
        Assert.That(host.Secret, Is.SameAs(secret));
        Assert.That(secret.ReadCount, Is.Zero, "Only the OS host may resolve the secret during execution.");
    }

    private static LaunchRequest Profile() => new LaunchInput
    {
        DisplayName = "fixture",
        ExecutablePath = "fixture.exe",
        WorkingDirectory = "fixture",
        Product = "E3D",
        InitializationFile = "launch.init",
        Project = "SAM",
        User = "SYSTEM",
        Mdb = "ALL",
        Module = "DESIGN",
        CheckLicence = false
    }.CreateSnapshot(Guid.NewGuid());

    private sealed class Tcp : ITcpProbe
    {
        public bool Reachable = true;
        public List<int> Ports { get; } = [];
        public Task<bool> ConnectAsync(string host, int port, CancellationToken ct)
        {
            Ports.Add(port);
            return Task.FromResult(Reachable);
        }
    }
    private sealed class Services : IWindowsServiceProbe
    {
        public bool Running = true;
        public List<string> Names { get; } = [];
        public bool IsRunning(string name) { Names.Add(name); return Running; }
    }
    private sealed class Files : IFileSystem
    {
        public bool Exists = true;
        public bool FileExists(string path) => Exists;
        public bool DirectoryExists(string path) => Exists;
        public IEnumerable<string> EnumerateFiles(string directory) => [];
        public IEnumerable<string> EnumerateDirectories(string directory) => [];
        public bool IsReparsePoint(string path) => false;
        public string ReadText(string path, int maximumBytes) => throw new NotSupportedException();
    }
    private sealed class Host : IProcessHost
    {
        public ProcessCommand? Command;
        public ISecretHandle? Secret;
        public IProcessHandle Start(ProcessCommand command, ISecretHandle? secret)
        {
            Command = command; Secret = secret; return new Handle();
        }
    }
    private sealed class Handle : IProcessHandle
    {
        public int Id => 123;
        public bool HasExited => false;
        public bool HasWindow => false;
        public void Dispose() { }
    }
    private sealed class Secret : ISecretHandle
    {
        public int ReadCount;
        public string Reveal() { ReadCount++; return "fixture-secret"; }
        public void Dispose() { }
    }
}
