using System.IO.Compression;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class CopyOperationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaCopyOps-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "state");
    private string Registered => Path.Combine(root, "registered");
    private string Write(string relative, string text)
    { var file = Path.GetFullPath(Path.Combine(root, relative)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }
    private OperationExecutor Executor(Func<DeploymentPlan, string, DeploymentReceipt>? apply = null) =>
        new(new OperationRegistry(CopyOperations.Registrations(new ManagerPlanStore(State), State, apply)));
    private static JsonElement Ref(OperationResult result) => JsonSerializer.SerializeToElement(new {
        planId = result.Data!.Value.GetProperty("planId").GetString(), sha256 = result.Data.Value.GetProperty("sha256").GetString() });

    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    [InlineData("zip")]
    public async Task AllExistingCopyFormatsUsePreviewAuthorizationBackupAndConditionalRestore(string kind)
    {
        var file = Write("source/sample.pmlfnc", "new synthetic text");
        var previous = Write("registered/Tool/sample.pmlfnc", "original text");
        var source = kind == "file" ? file : Path.GetDirectoryName(file)!;
        if (kind == "zip")
        {
            source = Path.Combine(root, "tool.zip"); using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("Tool/sample.pmlfnc"); using var text = new StreamWriter(entry.Open()); text.Write("new synthetic text");
        }
        var executor = Executor();
        var preview = await executor.InvokeAsync("copy.preview", JsonSerializer.SerializeToElement(new { source, registeredRoot = Registered, name = "Tool" }), new());
        Assert.True(preview.Success, preview.Error?.Message); Assert.Equal("original text", File.ReadAllText(previous));
        Assert.Equal(4, (await executor.InvokeAsync("copy.apply", Ref(preview), new())).ExitCode);
        var applied = await executor.InvokeAsync("copy.apply", Ref(preview), new(AllowWrite: true));
        Assert.True(applied.Success, applied.Error?.Message); Assert.Equal("new synthetic text", File.ReadAllText(previous));
        var same = await executor.InvokeAsync("copy.preview", JsonSerializer.SerializeToElement(new { source, registeredRoot = Registered, name = "Tool" }), new());
        Assert.Equal(0, same.Data!.Value.GetProperty("summary").GetProperty("changed").GetInt32());
        var restored = await executor.InvokeAsync("copy.restore", Ref(applied), new(AllowWrite: true));
        Assert.True(restored.Success, restored.Error?.Message); Assert.Equal("original text", File.ReadAllText(previous));
    }

    [Fact]
    public async Task EmptyMessageStillHasCodePhaseTypeAndReadableDiagnosticLog()
    {
        var source = Write("source/sample.pmlfnc", "new"); Directory.CreateDirectory(Registered);
        var executor = Executor((_, _) => throw new EmptyMessageFailure());
        var preview = await executor.InvokeAsync("copy.preview", JsonSerializer.SerializeToElement(new { source, registeredRoot = Registered, name = "Tool" }), new());
        var failed = await executor.InvokeAsync("copy.apply", Ref(preview), new(AllowWrite: true));
        Assert.Equal(7, failed.ExitCode); Assert.False(string.IsNullOrWhiteSpace(failed.Error?.Message));
        var diagnostic = failed.Data!.Value;
        Assert.Equal("apply", diagnostic.GetProperty("phase").GetString()); Assert.Contains("EmptyMessageFailure", diagnostic.GetProperty("exceptionType").GetString());
        Assert.True(File.Exists(diagnostic.GetProperty("logPath").GetString())); Assert.False(Directory.Exists(Path.Combine(Registered, "Tool")));
    }

    [Fact]
    public void CopyFailureCarriesPartialRecoveryRatherThanOnlyAnExceptionMessage()
    {
        Write("source/one.pmlfnc", "new-one"); Write("source/two.pmlfnc", "new-two");
        Write("registered/Tool/one.pmlfnc", "old-one"); Write("registered/Tool/two.pmlfnc", "old-two");
        var plan = PluginDeployment.Preview(Path.Combine(root, "source"), Registered, "Tool");
        Assert.Equal(2, plan.Files.Length);
        // Directory enumeration order is not a contract. Mutate an already applied
        // target only after the final write, so rollback must preserve that edit.
        var first = Path.Combine(plan.Target, plan.Files[0].RelativePath);
        var last = Path.Combine(plan.Target, plan.Files[^1].RelativePath);
        var originalLast = File.ReadAllText(last);
        var failure = Assert.Throws<DeploymentApplyException>(() => PluginDeployment.Apply(plan, State, phase => {
            if (phase == "after-write:" + plan.Files[^1].RelativePath) { File.WriteAllText(first, "external-edit"); throw new EmptyMessageFailure(); }
        }));
        Assert.NotEmpty(failure.RecoveryErrors); Assert.Contains(plan.Files[0].RelativePath, string.Join(";", failure.RecoveryErrors));
        Assert.Equal("external-edit", File.ReadAllText(first)); Assert.Equal(originalLast, File.ReadAllText(last));
        Assert.True(Directory.Exists(failure.BackupDirectory)); Assert.Contains("EmptyMessageFailure", failure.Message);
    }

    [Fact]
    public void StageCleanupFailureDoesNotReportAnAlreadyCompletedCopyAsFailed()
    {
        var source = Write("source/sample.pmlfnc", "new"); Directory.CreateDirectory(Registered);
        var receipt = PluginDeployment.Apply(PluginDeployment.Preview(source, Registered, "Tool"), State,
            phase => { if (phase == "cleanup") throw new EmptyMessageFailure(); });
        Assert.Equal("new", File.ReadAllText(Path.Combine(Registered, "Tool/sample.pmlfnc")));
        Assert.Contains("EmptyMessageFailure", receipt.CleanupWarning);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(receipt.BackupDirectory)!, "receipt.json")));
    }

    private sealed class EmptyMessageFailure : IOException { public override string Message => ""; }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
