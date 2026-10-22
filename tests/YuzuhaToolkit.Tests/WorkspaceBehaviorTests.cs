using System.IO.Compression;
using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class WorkspaceBehaviorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yuzuha-workspace-test-" + Guid.NewGuid().ToString("N"));
    private string DirectoryPath(string name) { var directory = Path.Combine(_root, name); Directory.CreateDirectory(directory); return directory; }
    private string Write(string relative, string text) { var file = Path.Combine(_root, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }

    [Fact]
    public void InspectorCountsAllFilesAndWarnsAboutDevelopmentArtifactsWithoutDeletingThem()
    {
        Write("registered/a.pmlfnc", "define function !!Example()\nendfunction");
        Write("registered/node_modules/junk.js", new string('x', 2048));
        var result = DirectoryInspector.Scan(DirectoryPath("registered"), warnFiles: 2, warnBytes: 1024);
        Assert.True(result.Complete); Assert.Equal(2, result.Files); Assert.Equal(1, result.PmlFiles); Assert.True(result.NeedsAttention);
        Assert.Contains(result.Warnings, warning => warning.Contains("node_modules")); Assert.True(File.Exists(Path.Combine(_root, "registered/node_modules/junk.js")));
    }

    [Fact]
    public void IncompleteInspectionNeverReportsHealthy()
    {
        for (var index = 0; index < 5; index++) Write("registered/" + index + ".pmlfnc", "source");
        var result = DirectoryInspector.Scan(DirectoryPath("registered"), maxEntries: 1);
        Assert.False(result.Complete); Assert.True(result.NeedsAttention); Assert.True(result.Files <= 1);
    }

    [Theory]
    [InlineData("../outside.pmlfnc")]
    [InlineData("folder/../../outside.pmlfnc")]
    [InlineData("data.pmlfnc:other")]
    public void CopyPathsCannotEscapeTheirRegisteredRoot(string relative) => Assert.Throws<InvalidDataException>(() => PathRules.Inside(DirectoryPath("registered"), relative));

    [Fact]
    public void CopyPreviewSkipsDevelopmentOutputsAndCopiesOnlyFrozenPayload()
    {
        Write("source/tool.pmlfnc", "new source"); Write("source/bin/vendor.dll", "not a deployment payload"); Write("source/pml.index", "stale"); Write("source/.git/config", "not deployed");
        var plan = PluginDeployment.Preview(DirectoryPath("source"), DirectoryPath("registered"), "Plugin");
        Assert.Single(plan.Files); Assert.True(plan.Skipped >= 3); Assert.Equal("tool.pmlfnc", plan.Files[0].RelativePath);
        Assert.False(Directory.Exists(plan.Target));
    }

    [Fact]
    public void CopyWritesPayloadAndKeepsBackupsOutsideTheRegisteredLibrary()
    {
        Write("source/tool.pmlfnc", "new source"); Write("registered/Plugin/tool.pmlfnc", "old source"); Write("registered/Plugin/old.pmlobj", "keep old file");
        var registered = DirectoryPath("registered"); var plan = PluginDeployment.Preview(DirectoryPath("source"), registered, "Plugin");
        var receipt = PluginDeployment.Apply(plan, DirectoryPath("state"));
        Assert.Equal("new source", File.ReadAllText(Path.Combine(plan.Target, "tool.pmlfnc")));
        Assert.Equal("old source", File.ReadAllText(Path.Combine(receipt.BackupDirectory, "tool.pmlfnc")));
        Assert.False(receipt.BackupDirectory.StartsWith(registered, StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(plan.Target, "old.pmlobj")));
    }

    [Fact]
    public void ZipCopyFlattensTheMatchingPluginRootAndPreservesResources()
    {
        var file = Path.Combine(DirectoryPath("source"), "plugin.zip");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create)) { using var writer = new StreamWriter(zip.CreateEntry("PLUGIN/nested/tool.pmlfnc").Open()); writer.Write("source"); }
        var plan = PluginDeployment.Preview(file, DirectoryPath("registered"), "Plugin"); Assert.Equal("nested/tool.pmlfnc", plan.Files.Single().RelativePath);
        PluginDeployment.Apply(plan, DirectoryPath("state")); Assert.Equal("source", File.ReadAllText(Path.Combine(plan.Target, "nested/tool.pmlfnc")));
    }

    [Fact]
    public void ChangedTargetStopsWholeCopyBeforeAnyWrites()
    {
        Write("source/a.pmlfnc", "a"); Write("source/b.pmlfnc", "new b"); var destination = Write("registered/Plugin/b.pmlfnc", "original b");
        var plan = PluginDeployment.Preview(DirectoryPath("source"), DirectoryPath("registered"), "Plugin");
        File.WriteAllText(destination, "user changed b");
        Assert.Throws<IOException>(() => PluginDeployment.Apply(plan));
        Assert.False(File.Exists(Path.Combine(_root, "registered/Plugin/a.pmlfnc"))); Assert.Equal("user changed b", File.ReadAllText(destination));
    }

    [Fact]
    public void ChangedSourceStopsWholeCopyBeforeAnyWrites()
    {
        var source = Write("source/a.pmlfnc", "a"); var plan = PluginDeployment.Preview(DirectoryPath("source"), DirectoryPath("registered"), "Plugin");
        File.WriteAllText(source, "a changed"); Assert.Throws<IOException>(() => PluginDeployment.Apply(plan)); Assert.False(Directory.Exists(plan.Target));
    }

    [Fact]
    public void DevelopmentSourceMustBeOutsideRegisteredLibrary()
    {
        Write("registered/dev/a.pmlfnc", "source");
        Assert.Throws<IOException>(() => PluginDeployment.Preview(DirectoryPath("registered/dev"), DirectoryPath("registered"), "Plugin"));
    }

    [Fact]
    public void ZipSlipIsRejectedDuringPreview()
    {
        var file = Path.Combine(DirectoryPath("source"), "plugin.zip");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create)) { using var writer = new StreamWriter(zip.CreateEntry("../outside.pmlfnc").Open()); writer.Write("invalid"); }
        Assert.Throws<InvalidDataException>(() => PluginDeployment.Preview(file, DirectoryPath("registered"), "Plugin")); Assert.False(File.Exists(Path.Combine(_root, "outside.pmlfnc")));
    }

    [Fact]
    public void NativeLaunchArgumentsPreserveSpacesAndDoNotModifyParentEnvironment()
    {
        var executable = Write("program with spaces/mon.exe", "test fixture; never executed"); var initialization = Write("program with spaces/launch.init", "fixture");
        var profile = new LaunchProfile { Executable = executable, Initialization = initialization, WorkingDirectory = DirectoryPath("working"), Project = "PRJ", User = "USER", Mdb = "ALL", Module = "Design", License = "4545@localhost" };
        var previous = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST"); var start = LaunchService.Build(profile, "secret password");
        Assert.Equal(new[] { "PROD", "E3D", "init", initialization, "PRJ", "USER/secret password", "/ALL", "Design" }, start.ArgumentList);
        Assert.Equal("4545@localhost", start.Environment["AVEVA_LICENSE_SERVER_LIST"]); Assert.Equal(previous, Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST"));
        Assert.DoesNotContain("secret password", LaunchService.Preview(profile));
    }

    [Fact]
    public void MarineRejectsArgumentsThatCannotSurviveVendorBatchParsing()
    {
        var profile = new LaunchProfile { Kind = "MarineBatch", Executable = Write("marine.bat", "fixture"), WorkingDirectory = DirectoryPath("working"), Project = "PRJ", User = "USER", Mdb = "/ALL", Module = "Design" };
        Assert.Throws<ArgumentException>(() => LaunchService.Build(profile, "contains&operator"));
    }

    [Fact]
    public void JournalPersistsCorrelatedStartAndCompletionWithRedactedNamedSecrets()
    {
        var state = DirectoryPath("state");
        using var journal = new RuntimeJournal("test MCP", state);
        var id = journal.Begin("generate_pml_call", "{\"methodName\":\"Example\",\"nested\":{\"password\":\"never-store-this\"}}", 123);
        Assert.Equal("running", journal.Snapshot().State); Assert.Single(journal.Snapshot().ActiveTools);
        journal.Complete(id, "succeeded", "done", 123);
        var events = RuntimeReader.Recent(10, state); Assert.Equal(2, events.Length); Assert.All(events, entry => Assert.Equal(id, entry.CallId));
        Assert.DoesNotContain("never-store-this", string.Join("", events.Select(entry => entry.Arguments))); Assert.Contains("[redacted]", events.Single(entry => entry.Phase == "started").Arguments);
        Assert.Equal("idle", RuntimeReader.Statuses(state).Single().State);
    }

    [Fact]
    public void JournalRotationBoundsEachProcessLogs()
    {
        var state = DirectoryPath("state"); using var journal = new RuntimeJournal("test MCP", state, 1024);
        for (var index = 0; index < 50; index++) { var id = journal.Begin("tool", "{\"text\":\"" + new string('x', 700) + "\"}"); journal.Complete(id, "failed", "expected test failure"); }
        Assert.InRange(Directory.GetFiles(journal.Snapshot().Directory, "events-*.jsonl").Length, 1, 8); Assert.True(RuntimeReader.Recent(200, state).Length <= 200);
    }

    [Fact]
    public void ExitedProcessWithUnfinishedCallIsReportedAsUnknownRatherThanSuccess()
    {
        var state = DirectoryPath("state"); var directory = DirectoryPath("state/Activity/dead");
        var status = new RuntimeStatus("gone", int.MaxValue, 1, DateTimeOffset.UtcNow, "running", ["run_pml_command"], 123, "", null, directory);
        File.WriteAllText(Path.Combine(directory, "status.json"), JsonSerializer.Serialize(status, WorkspaceJsonContext.Default.RuntimeStatus));
        Assert.Equal("exited-result-unknown", RuntimeReader.Statuses(state).Single().State);
    }

    [Fact]
    public void LoggingFailureDoesNotTurnAnAlreadyExecutedOperationIntoAnException()
    {
        var state = Write("file-not-directory", "fixture"); using var journal = new RuntimeJournal("test", state);
        var id = journal.Begin("run_pml_command", "{}"); journal.Complete(id, "succeeded", "finished"); Assert.NotNull(journal.Snapshot().LoggingError);
    }

    [Fact]
    public void MarkDbDescriptionEscapesQuotesAndRejectsCommandSeparators()
    {
        Assert.Equal("MARKDB 'O''Brien plugin'", WorkspaceTools.BuildMarkDb("O'Brien plugin"));
        Assert.Throws<ArgumentException>(() => WorkspaceTools.BuildMarkDb("marker\nDELETE WORLD"));
    }

    public void Dispose() { var resolved = Path.GetFullPath(_root); if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("yuzuha-workspace-test-", StringComparison.Ordinal) && Directory.Exists(resolved)) Directory.Delete(resolved, true); }
}
