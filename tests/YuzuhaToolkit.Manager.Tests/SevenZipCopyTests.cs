using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class SevenZipFactAttribute : FactAttribute
{
    public SevenZipFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("YUZUHA_TEST_7ZR_PATH") is not { Length: > 0 } file || !File.Exists(file))
            Skip = "Optional native-format matrix requires an explicitly selected YUZUHA_TEST_7ZR_PATH.";
    }
}

public sealed class SevenZipCopyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaSevenZip-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "state");
    private string Registered => Path.Combine(root, "registered");
    private string Write(string relative, string text)
    { var file = Path.GetFullPath(Path.Combine(root, relative)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }

    [Theory]
    [InlineData("Path = ../escape.pmlfnc\nSize = 1")]
    [InlineData("Path = C:/escape.pmlfnc\nSize = 1")]
    [InlineData("Path = Tool/a.pmlfnc\n\nPath = tool/A.pmlfnc")]
    [InlineData("Path = Tool/a.\nSize = 1")]
    [InlineData("Path = Tool/a \nSize = 1")]
    [InlineData("Path = Tool/CON.txt\nSize = 1")]
    [InlineData("Path = Tool/NUL\nSize = 1")]
    [InlineData("Path = Tool/file\nSymbolic Link = ../escape")]
    [InlineData("Path = Tool/file\nHard Link = elsewhere")]
    [InlineData("Path = Tool/file\nEncrypted = +")]
    [InlineData("Path = Tool/file\nSize = 134217729")]
    [InlineData("Path = Tool/file\nSize = -1")]
    public void UnsafeUnsupportedOrOversizeListingIsRejectedBeforeExtraction(string body)
    { Assert.Throws<InvalidDataException>(() => SevenZipCopy.ValidateListing("archive header\n----------\n" + body, root)); }

    [Fact]
    public void MissingFormatDependencyFailsPreviewBeforeAnyCopyOrStaging()
    {
        var source = Write("source.7z", "synthetic invalid bytes"); Directory.CreateDirectory(Registered);
        Assert.Throws<FileNotFoundException>(() => SevenZipCopy.Preview(source, Registered, "Tool", Path.Combine(root, "missing", "7zr.exe"), State));
        Assert.False(Directory.Exists(State)); Assert.False(Directory.Exists(Path.Combine(Registered, "Tool")));
        Assert.Throws<InvalidDataException>(() => SevenZipCopy.ValidateListing("----------\n", root));
    }

    [Fact]
    public void ToolPolicyCopiesProgramFilesAndRefusesDatabasePayloadWithoutChangingLegacyPmlPolicy()
    {
        Write("source/tool.exe", "synthetic executable never run"); Write("source/start.bat", "synthetic script never run");
        var pml = Write("source/plugin.pmlfnc", "synthetic function never run"); Directory.CreateDirectory(Registered);
        var legacy = PluginDeployment.Preview(Path.GetDirectoryName(pml)!, Registered, "Tool"); Assert.Single(legacy.Files);
        var tool = PluginDeployment.PreviewTool(Path.GetDirectoryName(pml)!, Registered, "Tool"); Assert.Equal(3, tool.Files.Length);
        var receipt = PluginDeployment.Apply(tool, State);
        Assert.True(File.Exists(Path.Combine(Registered, "Tool/tool.exe"))); Assert.Empty(PluginDeployment.Restore(tool, receipt).Unresolved);
        Write("source/private.sqlite3", "synthetic private data");
        Assert.Throws<IOException>(() => PluginDeployment.PreviewTool(Path.GetDirectoryName(pml)!, Registered, "Tool"));
    }

    [SevenZipFact]
    public void ActualStandaloneToolValidatesCopiesAndRestoresSyntheticPayloadAndRejectsChangedPackageOrTool()
    {
        var originalTool = Environment.GetEnvironmentVariable("YUZUHA_TEST_7ZR_PATH")!;
        var tool = Path.Combine(root, "Tools", "7zr.exe"); Directory.CreateDirectory(Path.GetDirectoryName(tool)!); File.Copy(originalTool, tool);
        Write("source/Tool/app.exe", "synthetic executable never run"); Write("source/Tool/plugin.pmlfnc", "new synthetic function never run");
        var previous = Write("registered/Tool/plugin.pmlfnc", "old synthetic function");
        var archive = Path.Combine(root, "package.7z");
        Create(tool, archive, "Tool");
        var before = File.ReadAllBytes(archive); var plan = SevenZipCopy.Preview(archive, Registered, "Tool", tool, State);
        Assert.Equal(2, plan.Files.Files.Length); Assert.Equal("old synthetic function", File.ReadAllText(previous));
        var receipt = SevenZipCopy.Apply(plan, State);
        Assert.Equal(before, File.ReadAllBytes(archive)); Assert.Equal("new synthetic function never run", File.ReadAllText(previous));
        Assert.Empty(PluginDeployment.Restore(receipt.Plan, receipt.Receipt).Unresolved); Assert.Equal("old synthetic function", File.ReadAllText(previous));
        var corruptBytes = before.ToArray(); var at = corruptBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("new synthetic function never run"));
        Assert.True(at >= 0); corruptBytes[at] ^= 1;
        var corruptPayload = Path.Combine(root, "bad-crc.7z"); File.WriteAllBytes(corruptPayload, corruptBytes);
        Assert.Throws<InvalidDataException>(() => SevenZipCopy.Preview(corruptPayload, Registered, "Other", tool, State));
        File.AppendAllText(archive, "changed"); Assert.Throws<IOException>(() => SevenZipCopy.Apply(plan, State));
        File.WriteAllBytes(archive, before); File.AppendAllText(tool, "changed"); Assert.Throws<IOException>(() => SevenZipCopy.Apply(plan, State));
        var corrupt = Write("bad.7z", "this is not a 7z container");
        var error = Record.Exception(() => SevenZipCopy.Preview(corrupt, Registered, "Other", originalTool, State));
        Assert.True(error is InvalidDataException or IOException, error?.ToString()); Assert.False(Directory.Exists(Path.Combine(Registered, "Other")));
        Directory.CreateDirectory(Path.Combine(root, "source", "Empty")); var empty = Path.Combine(root, "empty.7z"); Create(originalTool, empty, "Empty");
        Assert.Throws<IOException>(() => SevenZipCopy.Preview(empty, Registered, "Empty", originalTool, State));
    }

    [SevenZipFact]
    public async Task FormatAndFrozenSevenZipPlanAreAvailableThroughManagerOperations()
    {
        var tool = Environment.GetEnvironmentVariable("YUZUHA_TEST_7ZR_PATH")!;
        Write("source/Tool/app.exe", "synthetic executable never run"); Directory.CreateDirectory(Registered);
        var archive = Path.Combine(root, "manager.7z"); Create(tool, archive, "Tool");
        var executor = new OperationExecutor(new OperationRegistry(CopyOperations.Registrations(new ManagerPlanStore(State), State)));
        var formats = await executor.InvokeAsync("copy.formats", JsonSerializer.SerializeToElement(new { }), new()); Assert.True(formats.Success);
        Assert.Contains(formats.Data!.Value.EnumerateArray(), f => f.GetProperty("format").GetString() == "7z");
        Assert.False(Directory.Exists(State));
        var preview = await executor.InvokeAsync("copy.preview", JsonSerializer.SerializeToElement(new { source = archive, registeredRoot = Registered, name = "Tool", sevenZipTool = tool }), new());
        Assert.True(preview.Success, preview.Error?.Message);
        JsonElement Ref(OperationResult result) => JsonSerializer.SerializeToElement(new { planId = result.Data!.Value.GetProperty("planId").GetString(), sha256 = result.Data.Value.GetProperty("sha256").GetString() });
        var applied = await executor.InvokeAsync("copy.apply", Ref(preview), new(AllowWrite: true)); Assert.True(applied.Success, applied.Error?.Message);
        Assert.True(File.Exists(Path.Combine(Registered, "Tool/app.exe")));
        var restored = await executor.InvokeAsync("copy.restore", Ref(applied), new(AllowWrite: true)); Assert.True(restored.Success, restored.Error?.Message);
        Assert.False(File.Exists(Path.Combine(Registered, "Tool/app.exe")));
    }

    private void Create(string tool, string archive, string folder)
    {
        var start = new ProcessStartInfo(tool) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.Combine(root, "source"), RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "a", "-t7z", "-mx=0", "-y", archive, folder }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000)) { process.Kill(true); throw new IOException("Synthetic archive creation timed out."); }
        Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
