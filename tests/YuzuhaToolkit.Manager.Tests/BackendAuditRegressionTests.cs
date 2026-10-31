using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class BackendAuditRegressionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaBackendAuditTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FailedCopyNeverRestoresAnAlteredBackupOrReportsFullRecovery()
    {
        var source = Path.Combine(root, "source");
        var registered = Path.Combine(root, "registered");
        var target = Path.Combine(registered, "tool");
        var state = Path.Combine(root, "state");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "a.txt"), "new package");
        File.WriteAllText(Path.Combine(target, "a.txt"), "original user file");
        var plan = PluginDeployment.PreviewTool(source, registered, "tool");
        string? changedBackup = null;

        var failure = Assert.Throws<DeploymentApplyException>(() => PluginDeployment.Apply(plan, state, phase =>
        {
            if (phase != "receipt") return;
            changedBackup = Directory.GetFiles(state, "a.txt", SearchOption.AllDirectories)
                .Single(file => Path.GetRelativePath(state, file).Split(Path.DirectorySeparatorChar).Contains("backups"));
            File.WriteAllText(changedBackup, "corrupted backup");
            throw new IOException("synthetic receipt failure");
        }));

        Assert.Equal("new package", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Contains("备份缺失或已变化", Assert.Single(failure.RecoveryErrors), StringComparison.Ordinal);
        Assert.Contains("恢复需人工处理", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("已恢复本次复制的修改", failure.Message, StringComparison.Ordinal);
        Assert.Equal("corrupted backup", File.ReadAllText(changedBackup!));
    }

    [Theory]
    [InlineData("AM", "12.1.SP4")]
    [InlineData("E3D", "3.1.6")]
    public void SelectedLibraryScopeDoesNotReadAnotherProductOrVersion(string otherProduct, string otherVersion)
    {
        var knowledge = Path.Combine(root, "knowledge");
        var selected = Register(knowledge, "E3D", "2.1", "selected");
        var other = Path.Combine(knowledge, "Libraries", otherProduct, otherVersion, "broken");
        Directory.CreateDirectory(other); File.WriteAllText(Path.Combine(other, "registration.json"), "{broken");

        Assert.Equal(selected, Assert.Single(KnowledgeLibraries.List(knowledge, "E3D", "2.1")).Database);
        Assert.Contains(Path.Combine(other, "registration.json"), Assert.Throws<InvalidDataException>(
            () => KnowledgeLibraries.List(knowledge, otherProduct, otherVersion)).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => KnowledgeLibraries.List(knowledge));
    }

    [Fact]
    public void SelectedLibraryScopeStillReportsItsOwnCorruptRegistration()
    {
        var knowledge = Path.Combine(root, "knowledge");
        Register(knowledge, "E3D", "2.1", "selected");
        var bad = Path.Combine(knowledge, "Libraries", "E3D", "2.1", "broken");
        Directory.CreateDirectory(bad); File.WriteAllText(Path.Combine(bad, "registration.json"), "{broken");
        Assert.Contains(Path.Combine(bad, "registration.json"), Assert.Throws<InvalidDataException>(
            () => KnowledgeLibraries.List(knowledge, "E3D", "2.1")).Message, StringComparison.Ordinal);
    }

    private static string Register(string knowledge, string product, string version, string name)
    {
        var directory = Path.Combine(knowledge, "Libraries", product, version, name);
        Directory.CreateDirectory(directory);
        var registration = new KnowledgeLibraryRegistration(1, Guid.NewGuid().ToString("N"), product, version,
            "official", name, new string('0', 64), null, null, [], DateTime.UtcNow.ToString("o"));
        File.WriteAllText(Path.Combine(directory, "registration.json"),
            JsonSerializer.Serialize(registration, KnowledgeLibraryJson.Default.KnowledgeLibraryRegistration));
        var database = Path.Combine(directory, "library.sqlite3");
        File.WriteAllText(database, "synthetic list-only identity; never opened as SQLite");
        return database;
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
