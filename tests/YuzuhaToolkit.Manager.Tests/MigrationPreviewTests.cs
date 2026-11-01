using System.Text.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class MigrationPreviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "migration-preview-" + Guid.NewGuid().ToString("N"));
    private string Put(string name, string json)
    {
        var file = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, json); return file;
    }
    private static string Text(MigrationPreviewReport report) => JsonSerializer.Serialize(report, MigrationPreviewJsonContext.Default.MigrationPreviewReport);

    [Fact]
    public void RewriteGenerationPreservesIdentityUnknownFieldsAndHistoryWithoutCredentials()
    {
        var source = Put("generation.json", """
            {"SchemaVersion":3,"Snapshot":{"WorkspaceId":"original-id","Applications":[],"Profiles":[{"Id":"profile-id","Credential":{"Target":"SECRET-REFERENCE"},"Fields":{"Project":"APS","Unknown":{"Keep":7},"Environment":{"X":"SECRET-ENV"}}}]},"History":{"commit-id":{"RequestDigest":"original-digest","Workspace":{"CredentialRef":"SECRET-HISTORY"},"UnknownHistory":[1,2]}}}
            """);
        var before = File.ReadAllBytes(source); var report = MigrationPreview.Preview("rewrite", source);
        Assert.True(report.Complete); Assert.False(report.Activate);
        var content = Assert.Single(report.Materials).Content;
        Assert.Equal("original-id", content.GetProperty("Snapshot").GetProperty("WorkspaceId").GetString());
        Assert.Equal(7, content.GetProperty("Snapshot").GetProperty("Profiles")[0].GetProperty("Fields").GetProperty("Unknown").GetProperty("Keep").GetInt32());
        Assert.Equal(2, content.GetProperty("History").GetProperty("commit-id").GetProperty("UnknownHistory").GetArrayLength());
        Assert.DoesNotContain("SECRET-", Text(report)); Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(3, report.Diagnostics.Count(d => d.Code == "FIELD_REDACTED"));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void NextReadsOnlyKnownLevelsRetainsDirectAndHistoryWhileRemovingSecrets()
    {
        Put("profiles.json", """[{"ConfigurationId":"next-id","Version":4,"Protocol":2,"ExecutablePath":"fake.exe","CredentialReference":"SECRET-REF","Unknown":true}]""");
        Put("draft.json", """{"SchemaVersion":1,"ConfigurationId":"draft-id","Protocol":0,"ExecutablePath":"fake.exe","Password":"SECRET-PASSWORD"}""");
        Put("snapshots/snapshot.json", """{"AttemptId":"old-attempt","Revision":2,"CapturedUtc":"2026-01-01T00:00:00Z","Model":{"SchemaVersion":1,"ConfigurationId":"snapshot-id","Protocol":"Direct","ExecutablePath":"fake.exe"}}""");
        Put("logs/records.jsonl", """{"Sequence":1,"Timestamp":"2026-01-01T00:00:00Z","AttemptId":"old-attempt","Snapshot":{"ConfigurationId":"next-id","CredentialKey":"SECRET-KEY"},"UnknownHistory":42}""");
        Put("ignored/arbitrary.json", "broken should never be read");
        var report = MigrationPreview.Preview("next", root);
        Assert.True(report.Complete); Assert.False(report.Activate); Assert.Equal(4, report.Materials.Length);
        Assert.Equal(2, report.Diagnostics.Count(d => d.Code == "DIRECT_UNSUPPORTED"));
        Assert.Equal(2, report.Materials.Single(m => m.File == "profiles.json").Content[0].GetProperty("Protocol").GetInt32());
        Assert.Equal(42, report.Materials.Single(m => m.File.EndsWith("records.jsonl", StringComparison.Ordinal)).Content[0].GetProperty("UnknownHistory").GetInt32());
        Assert.DoesNotContain("SECRET-", Text(report));
    }

    [Fact]
    public void RibbonPreservesVersionSequenceAndProjectWithoutFollowingArtifactPaths()
    {
        var id = Guid.NewGuid().ToString("D"); var version = Guid.NewGuid().ToString("D");
        Put($"documents/{id}/document.json", $$"""{"id":"{{id}}","lastVersion":"{{version}}","bindings":[],"Unknown":9}""");
        Put($"documents/{id}/versions/{version}.json", """{"id":"VERSION","sequence":12,"project":{"schemaVersion":1,"documentId":"DOCUMENT","buttons":[],"tabs":[],"unknownXml":"<keep/>"},"artifacts":{"path":"C:/must-not-read"}}""".Replace("VERSION", version).Replace("DOCUMENT", id));
        var report = MigrationPreview.Preview("ribbon", root);
        Assert.True(report.Complete); Assert.Equal(2, report.Materials.Length);
        Assert.Contains(report.Materials, m => m.Content.TryGetProperty("sequence", out var value) && value.GetInt32() == 12);
        Assert.Contains("<keep/>", report.Materials.Single(m => m.File.Contains("versions", StringComparison.Ordinal)).Content.GetProperty("project").GetProperty("unknownXml").GetString());
    }

    [Theory]
    [InlineData("{\"SchemaVersion\":99,\"Applications\":[],\"Profiles\":[]}", "FORMAT_OR_VERSION_UNSUPPORTED")]
    [InlineData("{\"SchemaVersion\":3,\"Generation\":\"../escape.json\",\"Sha256\":\"fake\"}", "GENERATION_POINTER_INVALID")]
    [InlineData("{broken", "JSON_INVALID")]
    [InlineData("{\"SchemaVersion\":2,\"schemaVersion\":2,\"Applications\":[],\"Profiles\":[]}", "DUPLICATE_FIELD")]
    public void RejectedSourceHasNoExportedBody(string json, string code)
    {
        var report = MigrationPreview.Preview("rewrite", Put("workspace.json", json));
        Assert.False(report.Complete); Assert.Empty(report.Materials); Assert.Contains(report.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void LegacyV2AndFutureNextAreDistinct()
    {
        Assert.True(MigrationPreview.Preview("rewrite", Put("workspace.json", """{"SchemaVersion":2,"Applications":[],"Profiles":[],"UnknownHistory":[1]}""")).Complete);
        var report = MigrationPreview.Preview("next", Put("draft.json", """{"SchemaVersion":2,"ConfigurationId":"future","ExecutablePath":"fake.exe","Protocol":0}"""));
        Assert.False(report.Complete); Assert.Empty(report.Materials);
    }

    [Fact]
    public void ByteAndFileBudgetsRejectWithoutWritingOrActivating()
    {
        var large = Put("large.json", new string(' ', 2 * 1024 * 1024 + 1));
        Assert.Contains(MigrationPreview.Preview("next", large).Diagnostics, d => d.Code == "BYTE_BUDGET");
        for (var i = 0; i < 257; i++) Put($"snapshots/{i}.json", """{"ConfigurationId":"id","ExecutablePath":"fake.exe","Protocol":0}""");
        var report = MigrationPreview.Preview("next", root);
        Assert.False(report.Complete); Assert.False(report.Activate); Assert.Equal(256, report.Materials.Length);
        Assert.Contains(report.Diagnostics, d => d.Code == "FILE_COUNT_BUDGET");
    }

    [Fact]
    public void RelativeNetworkAndUnknownFormatAreRejected()
    {
        Assert.False(MigrationPreview.Preview("next", "../draft.json").Complete);
        Assert.False(MigrationPreview.Preview("next", @"\\server\share\draft.json").Complete);
        Assert.False(MigrationPreview.Preview("guess", root).Complete);
    }

    [Fact]
    public void RewritePointerReadsOnlyBoundGenerationAndRejectsChangedBytes()
    {
        var generation = Guid.NewGuid().ToString("N") + ".json";
        var file = Put(".control/generations/" + generation, """{"SchemaVersion":3,"Snapshot":{"WorkspaceId":"kept","Applications":[],"Profiles":[]},"History":{"old":{"Unknown":5,"Credential":"SECRET"}}}""");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        var pointer = Put("workspace.json", $$"""{"SchemaVersion":3,"Generation":"{{generation}}","Sha256":"{{hash}}"}""");
        var report = MigrationPreview.Preview("rewrite", root);
        Assert.True(report.Complete); Assert.Equal(2, report.Materials.Length); Assert.DoesNotContain("SECRET", Text(report));
        Assert.Contains(report.Materials, m => m.Content.TryGetProperty("History", out var history) && history.GetProperty("old").GetProperty("Unknown").GetInt32() == 5);
        File.AppendAllText(file, " ");
        report = MigrationPreview.Preview("rewrite", pointer);
        Assert.False(report.Complete); Assert.Empty(report.Materials);
        Assert.Contains(report.Diagnostics, d => d.Code == "GENERATION_HASH_MISMATCH");
    }

    [Fact]
    public void InvalidUtf8ReturnsStableDiagnostic()
    {
        var file = Put("draft.json", ""); File.WriteAllBytes(file, [0xff, 0xfe, 0xff]);
        var report = MigrationPreview.Preview("next", file);
        Assert.False(report.Complete); Assert.Empty(report.Materials);
        Assert.Contains(report.Diagnostics, d => d.Code == "UTF8_INVALID");
    }

    [Fact]
    public async Task RegisteredPreviewRequiresNoWriteAndRejectsUnknownInputAndIncompleteMaterial()
    {
        var file = Put("workspace.json", """{"SchemaVersion":2,"Applications":[],"Profiles":[]}""");
        var catalog = ManagerOperationCatalog.Create();
        var result = await catalog.InvokeAsync("migration.preview", JsonSerializer.SerializeToElement(new { format = "rewrite", source = file }), new());
        Assert.True(result.Success, result.Error?.Message); Assert.Equal(0, result.ExitCode);
        Assert.False(result.Data!.Value.GetProperty("activate").GetBoolean());
        result = await catalog.InvokeAsync("migration.preview", JsonSerializer.SerializeToElement(new { format = "rewrite", source = file, activate = true }), new());
        Assert.False(result.Success); Assert.Equal(2, result.ExitCode);
        File.WriteAllText(file, "broken");
        result = await catalog.InvokeAsync("migration.preview", JsonSerializer.SerializeToElement(new { format = "rewrite", source = file }), new());
        Assert.False(result.Success); Assert.Equal(2, result.ExitCode); Assert.Equal("MIGRATION_PREVIEW_INCOMPLETE", result.Error?.Code);
        Assert.False(result.Data!.Value.GetProperty("complete").GetBoolean());
        Assert.Equal(0, result.Data.Value.GetProperty("materials").GetArrayLength());
    }

    [Fact]
    public void LinkedSourceDirectoryIsRejectedBeforeReading()
    {
        var target = Path.Combine(root, "target"); var link = Path.Combine(root, "linked");
        Put("target/draft.json", """{"SchemaVersion":1,"ConfigurationId":"id","Protocol":0,"ExecutablePath":"fake.exe"}""");
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!; process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, target);
        try
        {
            var report = MigrationPreview.Preview("next", link);
            Assert.False(report.Complete); Assert.Empty(report.Materials);
            Assert.Contains(report.Diagnostics, d => d.Code == "SOURCE_READ_REJECTED");
            Assert.True(File.Exists(Path.Combine(target, "draft.json")));
        }
        finally { Directory.Delete(link); }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
