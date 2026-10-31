using System.Diagnostics;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Manager.LaunchDesk;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class InstallationAndLaunchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaInstallationTests-" + Guid.NewGuid().ToString("N"));
    private string Write(string relative, string text = "synthetic")
    { var file = Path.GetFullPath(Path.Combine(root, relative)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }

    [Fact]
    public void NestedProductsHaveSeparateContentIdentitiesAndEffectiveEnvironmentSelection()
    {
        Write("Everything3D2.10/mon.exe"); var init = Write("Everything3D2.10/evars.init", "set module=DESIGN");
        Write("Everything3D2.10/evars.bat", "set module=WRONG");
        Write("Marine/OH12.1.SP4/marine.bat", "@echo off"); Write("Marine/OH12.1.SP4/evars.bat", "set module=OUTFITTING");
        var report = ManagerInstallationDiscovery.Scan([root]); Assert.True(report.Complete); Assert.Equal(2, report.Candidates.Length);
        var e3d = Assert.Single(report.Candidates, c => c.Identity.Product == "E3D");
        var am = Assert.Single(report.Candidates, c => c.Identity.Product == "AM");
        Assert.Equal(init, e3d.Identity.EnvironmentFile); Assert.NotEqual(e3d.Identity.Id, am.Identity.Id);
        Assert.Equal("MarineBatch", am.Identity.Kind); Assert.Equal("OH12.1.SP4", am.Identity.VersionHint);
        File.AppendAllText(init, "\nrem changed");
        Assert.NotEqual(e3d.Identity.Id, ManagerInstallationDiscovery.Identify(e3d.Identity.Executable, "E3D").Id);
    }

    [Fact]
    public void ProjectParserReadsIncludesWithoutExecutingThemOrCrawlingDatabaseContents()
    {
        var project = Path.Combine(root, "projects", "ABC000"); Directory.CreateDirectory(project);
        var marker = Path.Combine(root, "NEVER_EXECUTED");
        Write("Everything3D/evars.init", "call \"%~dp0projects.bat\"\nset module=DESIGN\necho bad >\"" + marker + "\"\n");
        Write("Everything3D/projects.bat", "set \"ABC000=" + project + "\"\n");
        Write("projects/ABC000/database.dat", "never parse this");
        var report = new FindAvevaProjectsAndModules(new DiscoveryFileSystem()).Execute(Path.Combine(root, "Everything3D"));
        var found = Assert.Single(report.Projects, p => p.Code == "ABC"); Assert.Equal(project, found.Directory); Assert.True(found.DirectoryExists);
        Assert.Contains(report.Modules, m => m.Name == "DESIGN"); Assert.False(File.Exists(marker));
        Assert.DoesNotContain(report.FilesRead, f => f.EndsWith("database.dat", StringComparison.Ordinal));
    }

    [Fact]
    public void BoundedDiscoveryIsNotReportedCompleteAndAmbiguousInitIsExplicit()
    {
        Write("Everything3D/mon.exe"); Write("Everything3D/evars.init"); Write("Everything3D/evar.init");
        var limited = ManagerInstallationDiscovery.Scan([root], maximumFiles: 1); Assert.False(limited.Complete);
        var ambiguous = ManagerInstallationDiscovery.Scan([root]);
        Assert.Empty(ambiguous.Candidates); Assert.Contains(ambiguous.Diagnostics, d => d.Code == "InstallationUnreadable");
    }

    [Fact]
    public void ExplicitSelectionBindsOneVersionAndUnreadableInitNeverFallsBackToBat()
    {
        var entry21 = Write("Everything3D2.10/mon.exe"); var init = Write("Everything3D2.10/evars.init");
        Write("Everything3D2.10/evars.bat"); Write("Everything3D3.1.6/mon.exe"); Write("Everything3D3.1.6/evars.init");
        var marine = Write("Marine/OH12.1.SP4/marine.bat"); Write("Marine/OH12.1.SP4/evars.bat");
        var report = ManagerInstallationDiscovery.Scan([root]); Assert.Equal(3, report.Candidates.Length);
        var selected = ManagerInstallationDiscovery.Select(Path.GetDirectoryName(entry21)!, "E3D", entry21);
        Assert.Equal(entry21, selected.Executable); Assert.Equal(init, selected.EnvironmentFile);
        using (var held = new FileStream(init, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => ManagerInstallationDiscovery.Select(Path.GetDirectoryName(entry21)!, "E3D"));
        Write("Marine/OH12.1.SP4/marine.cmd");
        Assert.Throws<IOException>(() => ManagerInstallationDiscovery.Select(Path.GetDirectoryName(marine)!, "AM"));
        Assert.Equal(marine, ManagerInstallationDiscovery.Select(Path.GetDirectoryName(marine)!, "AM", marine).Executable);
        Assert.Throws<ArgumentException>(() => ManagerInstallationDiscovery.Select(Path.GetDirectoryName(marine)!, "AM", entry21));
    }

    [Fact]
    public async Task ManagerStdinSaveAndStartReachVendorBatchWithoutExternalLaunchDeskOrHostClaim()
    {
        var output = Path.Combine(root, "manager-cli-output.txt");
        var batch = Write("AM cli fixture/marine.bat", "@echo off\r\n>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %1 %2 %3 %4 %5\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo completed\r\n");
        var service = new LaunchProfileService(new WorkspaceConfigurationStore(Path.Combine(root, "isolated-manager-state")));
        var executor = ManagerOperationCatalog.Create(profiles: service, communication: () => throw new Exception("must not contact Host"));
        var profile = new LaunchProfile { Id = "cli-fixture", Name = "Synthetic", Executable = batch, WorkingDirectory = Path.GetDirectoryName(batch)!, Product = "AM", Kind = "MarineBatch",
            Project = "TST", User = "SYSTEM", Module = "Outfitting", Environment = new() { ["YUZUHA_LAUNCH_FIXTURE_OUTPUT"] = output } };
        using var saveOutput = new StringWriter(); using var saveInput = new StringReader(JsonSerializer.Serialize(new { profile = new { id = profile.Id, name = profile.Name, executable = profile.Executable,
            workingDirectory = profile.WorkingDirectory, product = profile.Product, kind = profile.Kind, project = profile.Project, user = profile.User, module = profile.Module, environment = profile.Environment }, expectedRevision = "missing" }));
        var saveCode = await ManagerCliHost.RunAsync(["op", "invoke", "launch.profile.save", "--input-stdin", "--allow-write"], executor, saveOutput, inputReader: saveInput);
        Assert.True(saveCode == 0, saveOutput.ToString());
        using var startOutput = new StringWriter(); using var startInput = new StringReader(JsonSerializer.Serialize(new { profileId = profile.Id, expectedRevision = service.Read().Revision, password = "only-synthetic" }));
        Assert.Equal(0, await ManagerCliHost.RunAsync(["op", "invoke", "launch.profile.start", "--input-stdin", "--allow-launch"], executor, startOutput, inputReader: startInput));
        using var result = JsonDocument.Parse(startOutput.ToString()); var receipt = result.RootElement.GetProperty("data");
        Assert.Equal("entry-process-created-host-not-verified", receipt.GetProperty("state").GetString()); Assert.True(receipt.GetProperty("processId").GetInt32() > 0);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(output) || !File.ReadAllText(output).Contains("completed")) await Task.Delay(25, deadline.Token);
        try { using var child = Process.GetProcessById(receipt.GetProperty("processId").GetInt32()); await child.WaitForExitAsync(deadline.Token); }
        catch (ArgumentException) { } // The owned entry already exited.
        Assert.Contains("Outfitting noconsole TST SYSTEM/only-synthetic /ALL", File.ReadAllText(output));
        Assert.DoesNotContain("only-synthetic", startOutput.ToString());
    }

    [Fact]
    public async Task ManagerCliRejectsInlineLaunchSecretBeforeDispatchWithoutEchoingIt()
    {
        var executor = ManagerOperationCatalog.Create(communication: () => throw new Exception("must stay offline"));
        using var output = new StringWriter();
        Assert.Equal(2, await ManagerCliHost.RunAsync(["op", "invoke", "launch.profile.start", "--input", "{\"password\":\"SYNTHETIC_SECRET\"}"], executor, output));
        Assert.DoesNotContain("SYNTHETIC_SECRET", output.ToString());
        using var parsed = JsonDocument.Parse(output.ToString()); Assert.Equal("SECRET_INPUT_SOURCE_REQUIRED", parsed.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MonitorArgumentsAndWorkingDirectoryReachRealOwnedChildWithIsolatedEnvironment()
    {
        var output = Path.Combine(root, "monitor-receipt.json"); var init = Write("configuration with spaces/launch.init");
        var work = Path.Combine(root, "work with spaces"); Directory.CreateDirectory(work);
        var fixture = Path.Combine(AppContext.BaseDirectory, "ProcessFixture", "YuzuhaToolkit.Manager.ProcessFixture.exe");
        var profile = new LaunchProfile { Executable = fixture, Initialization = init, WorkingDirectory = work, Product = "E3D", Kind = "Monitor",
            Project = "TST", User = "SYSTEM", Mdb = "/ALL", Module = "Design", License = "fixture-license-server", Environment = new() {
                ["YUZUHA_LAUNCH_FIXTURE_OUTPUT"] = output, ["YUZUHA_TEST_ONLY"] = "child-only" } };
        var parent = Environment.GetEnvironmentVariable("YUZUHA_TEST_ONLY");
        var start = LaunchService.Build(profile, "test-only-password"); start.CreateNoWindow = true;
        using var process = Process.Start(start)!; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await process.WaitForExitAsync(deadline.Token);
        Assert.Equal(0, process.ExitCode);
        using var receipt = JsonDocument.Parse(File.ReadAllText(output)); var data = receipt.RootElement;
        Assert.Equal(new[] { "PROD", "E3D", "init", init, "TST", "SYSTEM/test-only-password", "/ALL", "Design" }, data.GetProperty("arguments").EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.Equal(work, data.GetProperty("workingDirectory").GetString()); Assert.Equal("child-only", data.GetProperty("selectedEnvironment").GetString());
        Assert.Equal(parent, Environment.GetEnvironmentVariable("YUZUHA_TEST_ONLY"));
    }

    [Fact]
    public async Task MarineBatchUsesVendorOrderedArgumentsInActualCmdFixture()
    {
        var output = Path.Combine(root, "marine-receipt.txt");
        var batch = Write("AM fixture with spaces/marine.bat", "@echo off\r\n>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %1\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %2\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %3\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %4\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %5\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %CD%\r\n>>\"%YUZUHA_LAUNCH_FIXTURE_OUTPUT%\" echo %YUZUHA_TEST_ONLY%\r\n");
        var work = Path.Combine(root, "marine work"); Directory.CreateDirectory(work);
        var profile = new LaunchProfile { Executable = batch, WorkingDirectory = work, Product = "AM", Kind = "MarineBatch", Project = "TST", User = "SYSTEM", Mdb = "ALL", Module = "Outfitting",
            Environment = new() { ["YUZUHA_LAUNCH_FIXTURE_OUTPUT"] = output, ["YUZUHA_TEST_ONLY"] = "child-only" } };
        var start = LaunchService.Build(profile, "test-only-password");
        using var process = Process.Start(start)!; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await process.WaitForExitAsync(deadline.Token);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(new[] { "Outfitting", "noconsole", "TST", "SYSTEM/test-only-password", "/ALL", work, "child-only" }, File.ReadAllLines(output));
    }

    [Theory]
    [InlineData("simple-password", "launch.init")]
    [InlineData("space quote\" slash\\", "configuration with spaces/launch.init")]
    public async Task DetachedMonitorPreservesProcessStartArgumentListRawCommandLine(string password, string initialization)
    {
        var output = Path.Combine(root, "raw-command-line.json");
        var init = Write(initialization);
        var fixture = Path.Combine(AppContext.BaseDirectory, "ProcessFixture", "YuzuhaToolkit.Manager.ProcessFixture.exe");
        var profile = new LaunchProfile { Executable = fixture, Initialization = init, WorkingDirectory = root,
            Product = "E3D", Kind = "Monitor", Project = "TST", User = "SYSTEM", Mdb = "ALL", Module = "Design",
            Environment = new() { ["YUZUHA_LAUNCH_FIXTURE_OUTPUT"] = output } };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var original = LaunchService.Build(profile, password); original.CreateNoWindow = true;
        using (var process = Process.Start(original)!) await process.WaitForExitAsync(deadline.Token);
        using var previous = JsonDocument.Parse(File.ReadAllText(output));
        var expected = previous.RootElement.GetProperty("commandLine").GetString();
        File.Delete(output);
        Assert.True(LaunchService.Start(profile, password) > 0);
        JsonDocument? actual = null;
        while (actual is null)
        {
            try { actual = JsonDocument.Parse(File.ReadAllText(output)); }
            catch (Exception error) when (error is IOException or JsonException) { await Task.Delay(20, deadline.Token); }
        }
        using (actual)
        {
            Assert.Equal(expected, actual.RootElement.GetProperty("commandLine").GetString());
            Assert.Equal(previous.RootElement.GetProperty("arguments").ToString(), actual.RootElement.GetProperty("arguments").ToString());
        }
    }

    public void Dispose()
    {
        for (var attempt = 0; ; attempt++)
        { try { if (Directory.Exists(root)) Directory.Delete(root, true); return; } catch (IOException) when (attempt < 20) { Thread.Sleep(50); } }
    }
}
