using System.Text;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class EvarsEditorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaEvars-" + Guid.NewGuid().ToString("N"));
    public EvarsEditorTests() { Directory.CreateDirectory(root); Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
    private string FileAt(string name, byte[] bytes) { var file = Path.Combine(root, name); File.WriteAllBytes(file, bytes); return file; }

    [Fact]
    public void InitHasPriorityAndE3dNeverFallsBackToBat()
    {
        var bat = FileAt("evars.bat", Encoding.ASCII.GetBytes("@echo off\r\n"));
        Assert.Equal(bat, EvarsEditor.SelectFile(root, "AM"));
        Assert.Throws<IOException>(() => EvarsEditor.SelectFile(root, "E3D"));
        var init = FileAt("evars.init", Encoding.ASCII.GetBytes("@echo off\r\n"));
        Assert.Equal(init, EvarsEditor.SelectFile(root, "AM"));
        Assert.Throws<IOException>(() => EvarsEditor.SelectFile(root, "AM", bat));
    }

    [Theory]
    [InlineData(936)]
    [InlineData(65001)]
    [InlineData(1200)]
    public void BytesNewlinesManagedBlockAndRollbackArePreserved(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var original = encoding.GetPreamble().Concat(encoding.GetBytes("@echo off\r\nrem 中文原文\r\nset pmllib=vendor\r\n\r\n")).ToArray();
        var file = FileAt("evars.init", original);
        var runtime = Path.Combine(root, "PmlTrigger.Yuzuha");
        var plan = EvarsEditor.Preview(file, runtime, "E3D2.1", "net48", codePage);
        Assert.Equal(original, File.ReadAllBytes(file));
        var receipt = EvarsEditor.Apply(plan);
        Assert.Equal(original, File.ReadAllBytes(receipt.BackupFile!));
        var current = EvarsEditor.Preview(file, runtime, "E3D2.1", "net48", codePage);
        Assert.Equal(current.BeforeHash, current.AfterHash);
        Assert.Null(EvarsEditor.Apply(current).BackupFile);
        EvarsEditor.Restore(receipt);
        Assert.Equal(original, File.ReadAllBytes(file));
    }

    [Fact]
    public void StalePlanEarlyExitAndBrokenMarkersCannotWrite()
    {
        var file = FileAt("evars.bat", Encoding.ASCII.GetBytes("set pdmsui=vendor\n"));
        var plan = EvarsEditor.Preview(file, root, "AM", "net35");
        File.AppendAllText(file, "rem changed\n");
        Assert.Throws<IOException>(() => EvarsEditor.Apply(plan));
        File.WriteAllText(file, "@echo off\nexit /b\n");
        Assert.Throws<InvalidDataException>(() => EvarsEditor.Preview(file, root, "AM", "net35"));
        File.WriteAllText(file, "rem >>> Yuzuha managed settings\n");
        Assert.Throws<IOException>(() => EvarsEditor.Preview(file, root, "AM", "net35"));
    }

    [Fact]
    public void ExternalEditsAfterApplyPreventRollbackOverwrite()
    {
        var file = FileAt("evars.bat", Encoding.ASCII.GetBytes("set pdmsui=vendor\n"));
        var receipt = EvarsEditor.Apply(EvarsEditor.Preview(file, root, "AM", "net35"));
        File.AppendAllText(file, "rem external\n");
        Assert.Throws<IOException>(() => EvarsEditor.Restore(receipt));
        Assert.Contains("rem external", File.ReadAllText(file));
    }

    [Theory]
    [InlineData("if exist foo (\nset pmllib=vendor\n)\n")]
    [InlineData("set pmllib=vendor&echo unexpected\n")]
    [InlineData("set unrelated=one^\nset pmllib=vendor\n")]
    [InlineData("goto missing\nset pmllib=vendor\n")]
    public void SharedInstallerSyntaxRulesRejectAmbiguousTargetWrites(string body)
    {
        var file = FileAt("evars.bat", Encoding.ASCII.GetBytes(body));
        Assert.Throws<InvalidDataException>(() => EvarsEditor.Preview(file, root, "AM", "net35"));
        Assert.Equal(body, File.ReadAllText(file));
    }

    [Fact]
    public void ExistingManagedStartupPolicySurvivesDirectoryChange()
    {
        var file = FileAt("evars.bat", Encoding.ASCII.GetBytes("set pmllib=vendor\nrem >>> Yuzuha managed settings\nset YUZUHA_STARTUP_ACTIONS=rehash-all\nrem <<< Yuzuha managed settings\n"));
        var plan = EvarsEditor.Preview(file, root, "AM", "net35");
        Assert.Equal("rehash-all", plan.StartupActions);
        EvarsEditor.Apply(plan);
        Assert.Contains("set YUZUHA_STARTUP_ACTIONS=rehash-all", File.ReadAllText(file));
    }

    [Fact]
    public void AlreadyRestoredFingerprintConfirmsLostReplyWithoutReplayingAndStillRejectsLaterEdits()
    {
        var original = Encoding.ASCII.GetBytes("set pmllib=vendor\r\n");var file = FileAt("evars.init", original);
        var receipt = EvarsEditor.Apply(EvarsEditor.Preview(file, root, "E3D2.1", "net48"));
        EvarsEditor.Restore(receipt);File.Delete(receipt.BackupFile!);
        EvarsEditor.Restore(receipt);Assert.Equal(original, File.ReadAllBytes(file));
        File.AppendAllText(file, "rem later unrelated edit\r\n");
        Assert.Throws<IOException>(() => EvarsEditor.Restore(receipt));Assert.Contains("later unrelated edit", File.ReadAllText(file));
        var unchanged = EvarsEditor.Apply(EvarsEditor.Preview(file, root, "E3D2.1", "net48"));
        var noChange = EvarsEditor.Apply(EvarsEditor.Preview(file, root, "E3D2.1", "net48"));Assert.Equal(noChange.BeforeHash, noChange.AfterHash);
        File.AppendAllText(file, "rem changed after unchanged receipt\r\n");Assert.Throws<IOException>(() => EvarsEditor.Restore(noChange));
    }

    public void Dispose() => Directory.Delete(root, true);
}
