using System.Text;
using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Files;
using NUnit.Framework;

namespace LaunchDesk.Next.UnitTests.Discovery;

public sealed class ProjectModuleTests
{
    private string root = "";
    private readonly WindowsFileSystem files = new();
    [SetUp] public void SetUp() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LaunchDesk Project Modules", Guid.NewGuid().ToString("N"))).FullName;
    [TearDown] public void TearDown() => Directory.Delete(root, true);
    private string Write(string relative, string contents)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }
    private ProjectModuleReport Scan(ProjectModuleOptions? options = null, CancellationToken ct = default) =>
        new FindAvevaProjectsAndModules(files).Execute(root, options, ct);

    [Test]
    public void InitDummyArgumentAndNestedQuotedCallFindActualProjectCode()
    {
        Directory.CreateDirectory(Path.Combine(root, "Projects", "Plant", "APS000"));
        Write("launch.init", "set aveva_design_installed_dir=%0\\\ncall \"%aveva_design_installed_dir%evars.init\"\n");
        Write("evars.init", "set projects_dir=%aveva_design_installed_dir%Projects\\\nif exist \"%projects_dir%custom_evars.bat\" call \"%projects_dir%custom_evars.bat\"\n");
        Write("Projects/custom_evars.bat", "call \"%projects_dir%Plant\\evarsPlant.bat\"\n");
        var declaration = Write("Projects/Plant/evarsPlant.bat", "set APS000=%projects_dir%Plant\\APS000\nset APS000ID=NotTheProjectCode\n");
        var report = Scan();
        Assert.That(report.Projects.Any(value => value.Code == "APS" && value.DirectoryExists && value.Origin.File == declaration && value.Origin.Line == 1), Is.True);
        Assert.That(report.Projects.All(value => value.Code != "NotTheProjectCode"), Is.True);
        Assert.That(report.ProjectRoots.Any(value => value.Directory == Path.Combine(root, "Projects")), Is.True);
    }

    [Test]
    public void ChildrenAreInspectedAndDatabaseDirectoryIsNotCrawled()
    {
        Directory.CreateDirectory(Path.Combine(root, "Projects", "Plant", "XYZ000"));
        Write("evars.bat", "set projects_dir=%~dp0Projects\n");
        var projectFile = Write("Projects/Plant/evarsPlant.bat", "set XYZ000=%~dp0XYZ000\n");
        var databaseScript = Write("Projects/Plant/XYZ000/evarsBad.bat", "set BAD000=C:\\not-real\n");
        var report = Scan();
        Assert.That(report.FilesRead, Does.Contain(projectFile).And.Not.Contain(databaseScript));
        Assert.That(report.Projects.Any(value => value.Code == "XYZ" && value.Origin.Kind == "DirectoryHint"), Is.True);
    }

    [Test]
    public void ModuleDeclarationsAndLaunchDispatchHaveSourceLocations()
    {
        Write("evars.init", "set module=DESIGN\n");
        var launcher = Write("launch.bat", "if /i not \"%moduleArg%\"==\"Paragon\" goto other\nif /i \"%moduleArg%\"==\"Admin\" set executable=adm\n");
        var report = Scan();
        Assert.That(report.Modules.Any(value => value.Name == "DESIGN" && value.Origin.Kind == "Assignment"), Is.True);
        Assert.That(report.Modules.Any(value => value.Name == "PARAGON" && value.Origin.File == launcher && value.Origin.Line == 1), Is.True);
        Assert.That(report.Modules.Any(value => value.Name == "ADMIN"), Is.True);
        Assert.That(report.Diagnostics.Any(value => value.Code == "ControlFlowNotEvaluated"), Is.True);
    }

    [Test]
    public void CommentsUnknownExpansionAndSecretAssignmentsCannotInventProjectsOrLeak()
    {
        Write("evars.init", "rem set BAD000=C:\\fake\n:: set ALSO000=C:\\fake\nset UNKNOWN000=%missing%\\database\nset PASSWORD=never-persist-this\nset module=!dynamic!\n");
        var report = Scan();
        Assert.That(report.Projects, Is.Empty);
        Assert.That(report.Modules, Is.Empty);
        Assert.That(report.Diagnostics.Any(value => value.Code == "UnresolvedExpression"), Is.True);
        Assert.That(JsonSerializer.Serialize(report), Does.Not.Contain("never-persist-this"));
    }

    [Test]
    public void CircularIncludesStopWithDiagnostic()
    {
        Write("evars.init", "call \"%~dp0other.init\"\n");
        Write("other.init", "call \"%~dp0evars.init\"\n");
        Assert.That(Scan().Diagnostics.Any(value => value.Code == "IncludeCycle"), Is.True);
    }

    [Test]
    public void FalseExistenceConditionDoesNotReadItsTarget()
    {
        Write("evars.init", "if exist \"%~dp0missing\" call \"%~dp0evarsIgnored.bat\"\n");
        var ignored = Write("evarsIgnored.bat", "set BAD000=C:\\fake\n");
        Assert.That(Scan().FilesRead, Does.Not.Contain(ignored));
    }

    [Test]
    public void ReadLimitsAndInvalidEncodingAreReported()
    {
        var path = Write("evars.init", new string('x', 100));
        Assert.That(Scan(new(MaximumFileBytes: 16)).Diagnostics.Any(value => value.Code == "ConfigurationReadFailed"), Is.True);
        File.WriteAllBytes(path, [0xff, 0x80, 0x81]);
        Assert.That(Scan().Diagnostics.Any(value => value.Code == "ConfigurationReadFailed"), Is.True);
    }

    [Test]
    public void CancellationDoesNotBecomeAnEmptySuccessfulReport() =>
        Assert.Throws<OperationCanceledException>(() => Scan(ct: new CancellationToken(true)));

    [Test]
    public async Task SingleDeclaredProjectPrefillsButExplicitPresetWins()
    {
        var installation = Path.Combine(root, "Everything3D fixture");
        Directory.CreateDirectory(Path.Combine(installation, "Data", "XYZ000"));
        Write("Everything3D fixture/mon.exe", "");
        Write("Everything3D fixture/evars.init", "set XYZ000=%~dp0Data\\XYZ000\nset module=DESIGN\n");
        var discovery = new FindInstalledAveva(files);
        var first = (await discovery.DiscoverAsync(new([installation]))).Candidates.Single();
        Assert.That(first.Prefill.Project, Is.EqualTo("XYZ"));
        Assert.That(first.Sources["Project"], Is.EqualTo("StaticCandidate"));
        Assert.That(first.Prefill.Module, Is.EqualTo("DESIGN"));
        var second = (await discovery.DiscoverAsync(new([installation], Presets: new Dictionary<string, string> { ["Project"] = "MANUAL", ["Module"] = "ADMIN" }))).Candidates.Single();
        Assert.That(second.Prefill.Project, Is.EqualTo("MANUAL"));
        Assert.That(second.Prefill.Module, Is.EqualTo("ADMIN"));
        Assert.That(second.ProjectModules!.Projects.Any(value => value.Code == "XYZ"), Is.True);
    }

    [Test]
    public async Task MultipleProjectCodesStayCandidatesWithoutChoosingFirst()
    {
        var installation = "PDMS fixture";
        Write(installation + "/mon.exe", "");
        Directory.CreateDirectory(Path.Combine(root, installation, "ABC000"));
        Directory.CreateDirectory(Path.Combine(root, installation, "XYZ000"));
        Write(installation + "/evars.init", "set ABC000=%~dp0ABC000\nset XYZ000=%~dp0XYZ000\n");
        var candidate = (await new FindInstalledAveva(files).DiscoverAsync(new([Path.Combine(root, installation)]))).Candidates.Single();
        Assert.That(candidate.Prefill.Project, Is.Empty);
        Assert.That(candidate.ProjectModules!.Projects.Select(value => value.Code).Distinct(), Is.EquivalentTo(new[] { "ABC", "XYZ" }));
        Assert.That(candidate.DisplayName, Does.StartWith("AVEVA PDMS"));
    }

    [Test]
    public void SharedOlderVersionProjectRootIsKeptInsteadOfRewritten()
    {
        var shared = Directory.CreateDirectory(Path.Combine(root, "E3D2.1", "Projects")).FullName;
        Directory.CreateDirectory(Path.Combine(shared, "APS000"));
        var installation = Directory.CreateDirectory(Path.Combine(root, "E3D3.1")).FullName;
        Write("E3D3.1/evars.init", $"set projects_dir={shared}\nset APS000=%projects_dir%\\APS000\n");
        var report = new FindAvevaProjectsAndModules(files).Execute(installation);
        Assert.That(report.ProjectRoots.Select(value => value.Directory), Does.Contain(shared));
        Assert.That(report.Projects.Single(value => value.Origin.Kind == "Assignment").Directory,
            Is.EqualTo(Path.Combine(shared, "APS000")));
    }

    [Test]
    public void MissingProjectDirectoryIsReportedAsAHintInsteadOfBeingDeclaredUsable()
    {
        Write("evars.init", "set ABS000=%~dp0Missing\\ABS000\n");
        var project = Scan().Projects.Single();
        Assert.That(project.Code, Is.EqualTo("ABS"));
        Assert.That(project.DirectoryExists, Is.False);
    }
}
