using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class DesktopFunctionalCorrectionsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "yuzuha-desktop-corrections-" + Guid.NewGuid().ToString("N"));
    private string Write(string path, string text = "fixture only")
    {
        var file = Path.GetFullPath(Path.Combine(root, path)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file;
    }

    [Theory]
    [InlineData("LaunchDesk · AVEVA 多版本工作台", "LaunchDesk", "D:/VirtualMachines/LaunchDesk/LaunchDesk.exe", false)]
    [InlineData("AVEVA 使用说明", "notepad", "C:/Windows/notepad.exe", false)]
    [InlineData("PDMS Design - Project - SAMPLE", "des", "C:/AVEVA/PDMS12.1/des.exe", true)]
    [InlineData("Paragon - Project - SAMPLE", "paragon", "C:/AVEVA/Marine/OH12.1.SP4/paragon.exe", true)]
    [InlineData("mar | ALL_NO_MDS | Model", "des", "C:/Program Files/AVEVA/Everything3D2.10/des.exe", true)]
    [InlineData("AVEVA Marine", "PDMSConsole", "C:/AVEVA/Marine/OH12.1.SP4/PDMSConsole.exe", false)]
    public void DiscoveryRequiresEngineeringProcessInsteadOfAvevaWord(string title, string process, string path, bool expected) =>
        Assert.Equal(expected, AvevaSessionDiscovery.IsEngineeringWindow(title, process, path));

    [Fact]
    public void InstallationDiscoveryFindsNestedMarineAndPdmsWithoutLaunchingScripts()
    {
        var marine = Write("Marine/OH12.1.SP4/marine.bat", "@echo NEVER_EXECUTE");
        var pdms = Write("PDMS12.1/pdms.bat", "@echo NEVER_EXECUTE");
        var e3d = Write("Everything3D2.10/mon.exe");
        Write("LaunchDesk/LaunchDesk.exe");
        var found = RegistrationDiscovery.FindApplications([root]);
        Assert.Contains(found, x => x.Path == marine); Assert.Contains(found, x => x.Path == pdms); Assert.Contains(found, x => x.Path == e3d);
        Assert.DoesNotContain(found, x => x.Path.Contains("LaunchDesk"));
        Assert.Equal(found.Length, found.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void SavedCredentialSurvivesBlankEditAndNeverEntersWorkspaceJson()
    {
        const string password = "isolated-fixture-password";
        var key = WindowsCredentialStore.Prepare("", password, true);
        try
        {
            Assert.StartsWith("YuzuhaToolkit/Profile/", key);
            Assert.Equal(password, WindowsCredentialStore.Read(key));
            Assert.Equal(key, WindowsCredentialStore.Prepare(key, "", true));
            Assert.Equal("", WindowsCredentialStore.Prepare(key, "", false));
            var json = JsonSerializer.Serialize(new LaunchProfile { CredentialKey = key }, WorkspaceJsonContext.Default.LaunchProfile);
            Assert.DoesNotContain(password, json);
            Assert.Equal(key, JsonSerializer.Deserialize(json, WorkspaceJsonContext.Default.LaunchProfile)!.CredentialKey);
        }
        finally { WindowsCredentialStore.Delete(key); }
        Assert.Throws<InvalidOperationException>(() => WindowsCredentialStore.Read(key));
    }

    [Fact]
    public void CredentialStoreCannotReadOrDeleteAnotherApplicationsCredential()
    {
        Assert.Throws<ArgumentException>(() => WindowsCredentialStore.Read("LaunchDesk/Profile/not-ours"));
        Assert.Throws<ArgumentException>(() => WindowsCredentialStore.Delete("LaunchDesk/Profile/not-ours"));
    }

    [Theory]
    [InlineData("current", "", "", "!!YuzuhaReadCurrentElement(30,2,'')")]
    [InlineData("element", "/SAMPLE/PIPE", "owner", "!!YuzuhaReadDbref(30,2,'owner','/SAMPLE/PIPE')")]
    [InlineData("element", "=123/456", "", "!!YuzuhaReadDbref(30,2,'','=123/456')")]
    [InlineData("global", "!!fmsys", "forms.1", "!!YuzuhaReadGlobal(30,2,'forms.1','fmsys')")]
    public void ObjectQueriesReuseBuiltinsWithSeparateTargetAndMemberPath(string kind, string target, string member, string expected) =>
        Assert.Equal(expected, DesktopObjectQuery.Build(kind, target, member, 30, 2).Expression);

    [Fact]
    public void ObjectQueriesRejectExpressionsAndInvalidBudgetsBeforeDispatch()
    {
        Assert.Throws<ArgumentException>(() => DesktopObjectQuery.Build("global", "fmsys.Reset()", "", 30, 2));
        Assert.Throws<ArgumentException>(() => DesktopObjectQuery.Build("global", "fmsys", "forms.Delete()", 30, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => DesktopObjectQuery.Build("current", "", "", 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => DesktopObjectQuery.Build("current", "", "", 30, 99));
    }

    [Fact]
    public void GuiKnowledgeSearchReadsFullBuiltinWithoutCreatingDatabase()
    {
        var directory = Path.Combine(root, "empty-knowledge");
        var browser = new DesktopKnowledgeBrowser(directory);
        var rows = browser.Search("当前对象", false);
        var row = Assert.Single(rows, x => x.GuideId == "read-current-element");
        Assert.Equal("builtin", row.Role); Assert.Equal("", row.Database);
        Assert.Contains("YuzuhaReadCurrentElement", browser.Read(row).Content);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void GuiKnowledgeDetailsKeepDatabaseIdentityAndSourceLocation()
    {
        var source = Write("pmllib/FixtureProbe.pmlfnc", "define function !!FixtureProbe() is STRING\nreturn 'fixture_token'\nendfunction");
        var directory = Path.Combine(root, "knowledge");
        new KnowledgeRepository(directory).Build(Path.GetDirectoryName(source), null, null, "custom-fixture", directory, false, 0);
        var browser = new DesktopKnowledgeBrowser(directory);
        var row = Assert.Single(browser.Search("FixtureProbe", true), x => x.ChunkId is not null);
        var detail = browser.Read(row);
        Assert.Equal(Path.Combine(directory, "custom-fixture.sqlite3"), row.Database);
        Assert.Equal(source, detail.SourcePath); Assert.True(detail.StartLine > 0);
        Assert.Contains("fixture_token", detail.Content);
        Assert.Equal("未关联实测证据", detail.Verification);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
