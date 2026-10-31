using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Files;
using NUnit.Framework;
namespace LaunchDesk.Next.UnitTests.Discovery;

public class DiscoveryTests
{
    [Test] public async Task MarineMonitorUsesNativeMonitorProtocolAndPresetsAreMarked() { var root = Path.Combine(Path.GetTempPath(), "LaunchDeskNext", Guid.NewGuid().ToString("N"), "Marine"); Directory.CreateDirectory(root); try { File.WriteAllText(Path.Combine(root, "mon.exe"), ""); var result = await new FindInstalledAveva(new WindowsFileSystem()).DiscoverAsync(new(new[] { root }, Presets: new Dictionary<string, string> { { "Project", "ABC" } })); Assert.That(result.Candidates, Has.Count.EqualTo(1)); var c = result.Candidates[0]; Assert.That(c.Protocol, Is.EqualTo(EntryProtocol.Monitor)); Assert.That(c.DisplayName, Does.Contain("\u00B7")); Assert.That(c.Prefill.Project, Is.EqualTo("ABC")); Assert.That(c.Sources["Project"], Is.EqualTo("Preset")); } finally { Directory.Delete(root, true); } }
    [Test] public async Task MissingRootIsVisible() { var result = await new FindInstalledAveva(new WindowsFileSystem()).DiscoverAsync(new(new[] { "Z:/LaunchDeskNextMissing" })); Assert.That(result.Diagnostics.Any(d => d.Code == "RootMissing"), Is.True); }
    [Test] public async Task FileLimitIsVisible() { var root = Path.Combine(Path.GetTempPath(), "LaunchDeskNext", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); try { File.WriteAllText(Path.Combine(root, "a"), ""); File.WriteAllText(Path.Combine(root, "b"), ""); var r = await new FindInstalledAveva(new WindowsFileSystem()).DiscoverAsync(new(new[] { root }, MaximumFiles: 1)); Assert.That(r.Diagnostics.Any(d => d.Code == "FileLimit"), Is.True); } finally { Directory.Delete(root, true); } }
}
