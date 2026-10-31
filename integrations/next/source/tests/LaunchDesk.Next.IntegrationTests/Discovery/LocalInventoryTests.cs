using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Files;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using NUnit.Framework;
namespace LaunchDesk.Next.IntegrationTests.Discovery;

public sealed class LocalInventoryTests
{
    [Test, Category("LocalInventory")]
    public async Task KnownInstalledMonitorInventoryMatchesActualCSharpDiscovery()
    {
        var expected = new[] { @"C:\Program Files (x86)\AVEVA\Everything3D2.10\mon.exe", @"C:\Program Files (x86)\AVEVA\Everything3D3.1\mon.exe", @"C:\AVEVA\Marine\OH12.1.SP4\mon.exe" }.Where(File.Exists).ToArray();
        if (expected.Length == 0) Assert.Ignore("No known local AVEVA installations; real inventory unverified on this host.");
        var report = await new FindInstalledAveva(new WindowsFileSystem()).DiscoverAsync(FindInstalledAveva.DefaultOptions);
        foreach (var path in expected) { var candidate = report.Candidates.SingleOrDefault(c => string.Equals(c.ExecutablePath, path, StringComparison.OrdinalIgnoreCase)); Assert.That(candidate, Is.Not.Null, path); Assert.That(candidate!.Protocol, Is.EqualTo(EntryProtocol.Monitor)); Assert.That(candidate.Prefill.PasswordPolicy, Is.EqualTo(PasswordPolicy.Prompt)); }
    }
}
