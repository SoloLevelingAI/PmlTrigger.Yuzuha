using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Xunit;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Manager;
using PmlTwin.Phayton;
using System.Text.Json;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class KnowledgeImportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaKnowledgeTests-" + Guid.NewGuid().ToString("N"));
    private string Sources => Path.Combine(root, "selected-pml");
    private string Knowledge => Path.Combine(root, "knowledge");
    private string FileName => Path.Combine(Sources, "sample.pmlfnc");
    public KnowledgeImportTests()
    {
        Directory.CreateDirectory(Sources);
        File.WriteAllText(FileName, "define function !!YuzuhaSample() is STRING\n  return 'Yuzuha alpha reference'\nendfunction\n");
    }
    private KnowledgeImportRequest Request(string version = "2.1", string name = "reference") => new("E3D", version, "official", name, PmlLib: Sources);
    private KnowledgeImportService Service(Action<string>? phase = null) => new(Knowledge, phase);
    private string BuildOriginal()
    {
        var dir = Path.Combine(root, "original");
        return new KnowledgeRepository(dir).Build(Sources, null, null, "sample", dir, false, 0).Database;
    }
    private static void Sql(string db, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    [Fact]
    public void PreviewDoesNotBuildAndTwoVersionsPublishAndSearchIndependently()
    {
        var plan = Service().Preview(Request()); Assert.False(Directory.Exists(Knowledge)); Assert.Equal(1, plan.SourceFiles);
        var e3d = Service().Apply(plan);
        var am = Service().Apply(Service().Preview(Request("12.1.SP4") with { Product = "AM" }));
        Assert.True(e3d.Report.CanEnable); Assert.All(e3d.Report.Items, i => Assert.NotEqual("error", i.Status));
        Assert.NotEmpty(e3d.Report.SampleChunkIds);
        Assert.NotEqual(e3d.Library.Database, am.Library.Database);
        Assert.Single(KnowledgeLibraries.List(Knowledge, "E3D", "2.1"));
        var hits = new KnowledgeRepository(Knowledge).Search("Yuzuha", null, e3d.Library.Database, 5, null, null, null);
        Assert.NotEmpty(hits.Hits); Assert.Equal(e3d.Library.Database, hits.Database);
        Assert.True(File.Exists(FileName));
    }

    [Fact]
    public void SameLengthChangeIsDetectedByBytesAndStaleBuildPlanIsRefused()
    {
        var before = Service().Preview(Request()); var db = BuildOriginal();
        var length = new FileInfo(FileName).Length;
        File.WriteAllText(FileName, File.ReadAllText(FileName).Replace("alpha", "omega"));
        Assert.Equal(length, new FileInfo(FileName).Length);
        Assert.Equal("changed", Assert.Single(KnowledgeInspector.Inspect(db).SourceEvidence).Status);
        Assert.Throws<IOException>(() => Service().Apply(before)); Assert.Empty(KnowledgeLibraries.List(Knowledge));
    }

    [Fact]
    public void LegacyPathSizeDigestDoesNotClaimContentFreshness()
    {
        var db = BuildOriginal(); Sql(db, "DELETE FROM meta WHERE key='source_hash_algorithm'");
        var report = KnowledgeInspector.Inspect(db);
        Assert.True(report.CanEnable); Assert.Equal("unverified-legacy-fingerprint", Assert.Single(report.SourceEvidence).Status);
    }

    [Fact]
    public void MissingSourcesCanBeMappedWithoutChangingRecordedPathsOrOriginalDatabase()
    {
        var db = BuildOriginal(); var before = Hash(db); var moved = Path.Combine(root, "mapped"); Directory.Move(Sources, moved);
        Assert.Equal("missing", Assert.Single(KnowledgeInspector.Inspect(db).SourceEvidence).Status);
        var mappings = new[] { new KnowledgeSourceMapping(Sources, moved) };
        var mapped = KnowledgeInspector.Inspect(db, mappings);
        Assert.True(mapped.CanEnable); var source = Assert.Single(mapped.SourceEvidence);
        Assert.Equal(FileName, source.RecordedPath); Assert.Equal(Path.Combine(moved, "sample.pmlfnc"), source.ResolvedPath); Assert.Equal("current", source.Status);
        var request = Request() with { Database = db, PmlLib = null, Mappings = mappings };
        var receipt = Service().Apply(Service().Preview(request));
        Assert.Equal(before, Hash(db)); Assert.Equal(before.ToLowerInvariant(), receipt.Library.Registration.ImportedDatabaseSha256);
        Assert.Equal(FileName, Assert.Single(receipt.Report.SourceEvidence).RecordedPath);
        Assert.Throws<IOException>(() => Service().Preview(request));
    }

    [Theory]
    [InlineData("UPDATE meta SET value='999' WHERE key='schema_version'", "schema.version")]
    [InlineData("DROP TABLE call_refs", "schema.tables")]
    [InlineData("UPDATE chunks_fts SET content='different text'", "fts.rows")]
    [InlineData("PRAGMA foreign_keys=OFF; UPDATE semantic_chunks SET source_id=999", "rows.relationships")]
    public void UnsupportedOrInconsistentDatabaseCannotBeEnabled(string change, string code)
    {
        var db = BuildOriginal(); Sql(db, change); var before = Hash(db);
        var report = KnowledgeInspector.Inspect(db);
        Assert.False(report.CanEnable); Assert.Contains(report.Items, i => i.Code == code && i.Status == "error");
        Assert.Equal(before, Hash(db));
        Assert.Throws<InvalidDataException>(() => Service().Preview(Request() with { Database = db, PmlLib = null }));
        Assert.False(Directory.Exists(Knowledge));
    }

    [Fact]
    public void CorruptDatabaseReturnsReportRatherThanSuccessOrUnhandledException()
    {
        var db = Path.Combine(root, "bad.sqlite3"); File.WriteAllText(db, "synthetic broken database");
        var report = KnowledgeInspector.Inspect(db); Assert.False(report.CanEnable); Assert.Contains(report.Items, i => i.Status == "error");
    }

    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void ActiveSidecarsAreNeverBareCopied(string sidecar)
    {
        var db = BuildOriginal(); File.WriteAllText(db + sidecar, "synthetic active marker");
        Assert.False(KnowledgeInspector.Inspect(db).CanEnable);
        Assert.Throws<InvalidDataException>(() => Service().Preview(Request() with { Database = db, PmlLib = null }));
    }

    [Fact]
    public void ActiveWriterAndApplicationWriterLockAreRefused()
    {
        var db = BuildOriginal();
        using (var writer = new FileStream(db, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            Assert.False(KnowledgeInspector.Inspect(db).CanEnable);
        using (var writer = new FileStream(db + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.False(KnowledgeInspector.Inspect(db).CanEnable);
        Assert.True(KnowledgeInspector.Inspect(db).CanEnable);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("validate")]
    [InlineData("registration")]
    [InlineData("publish")]
    public void InjectedFailuresKeepExistingLibrariesAndActivationUnchanged(string failAt)
    {
        var previous = Service().Apply(Service().Preview(Request())); var before = Hash(previous.Library.Database);
        var service = Service(phase => { if (phase == failAt) throw new IOException("injected " + phase); });
        var plan = service.Preview(Request(name: "second"));
        Assert.Throws<IOException>(() => service.Apply(plan));
        Assert.Single(KnowledgeLibraries.List(Knowledge)); Assert.Equal(before, Hash(previous.Library.Database));
        Assert.False(Directory.Exists(plan.Target)); Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(plan.Target)!, ".import-*"));
    }

    [Fact]
    public void ManifestAndDatabaseChangeRequireNewPreviewAndReservedNamesAreProtected()
    {
        var db = BuildOriginal(); var manifest = Path.ChangeExtension(db, ".manifest.json");
        var request = Request() with { Database = db, PmlLib = null, Manifest = manifest };
        var plan = Service().Preview(request); File.AppendAllText(manifest, " ");
        Assert.Throws<IOException>(() => Service().Apply(plan));
        foreach (var name in new[] { "personal", "experience", "project", "../escape", "bad:name" })
            Assert.Throws<ArgumentException>(() => Service().Preview(Request(name: name)));
    }

    [Fact]
    public async Task ManagerAndCliUseSameFrozenPlanAuthorizationAndVersionIdentity()
    {
        var executor = new OperationExecutor(new OperationRegistry(KnowledgeOperations.Registrations(Knowledge, new ManagerPlanStore(Path.Combine(root, "state")))));
        // Omit optional null properties just as schema-based callers do.
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { product = "E3D", version = "2.1", role = "official", name = "reference", pmlLib = Sources }));
        var preview = await executor.InvokeAsync("knowledge.import.preview", document.RootElement, new());
        Assert.True(preview.Success, preview.Error?.Message); Assert.False(Directory.Exists(Knowledge));
        var data = preview.Data!.Value;
        var reference = JsonSerializer.SerializeToElement(new { planId = data.GetProperty("planId").GetString(), sha256 = data.GetProperty("sha256").GetString() });
        Assert.Equal(4, (await executor.InvokeAsync("knowledge.import.apply", reference, new())).ExitCode);
        using var output = new StringWriter();
        Assert.Equal(0, await OperationCli.RunAsync(["op", "invoke", "knowledge.import.apply", "--input-stdin", "--allow-write"], executor, output, inputReader: new StringReader(reference.GetRawText())));
        var library = Assert.Single(KnowledgeLibraries.List(Knowledge));
        using var applied = JsonDocument.Parse(output.ToString());
        Assert.Equal(library.Registration.Id, applied.RootElement.GetProperty("data").GetProperty("library").GetProperty("registration").GetProperty("id").GetString());
        var query = JsonSerializer.SerializeToElement(new { product = "E3D", version = "2.1", name = "reference", query = "Yuzuha" });
        var searched = await executor.InvokeAsync("knowledge.library.search", query, new()); Assert.True(searched.Success, searched.Error?.Message);
        var chunkId = searched.Data!.Value.GetProperty("result").GetProperty("hits")[0].GetProperty("chunkId").GetInt64();
        var read = await executor.InvokeAsync("knowledge.library.chunk", JsonSerializer.SerializeToElement(new { product = "E3D", version = "2.1", name = "reference", chunkId }), new());
        Assert.True(read.Success, read.Error?.Message); Assert.Equal(FileName, read.Data!.Value.GetProperty("chunk").GetProperty("recordedPath").GetString());
        Assert.Equal(library.Registration.Id, read.Data.Value.GetProperty("library").GetProperty("registration").GetProperty("id").GetString());
    }

    [Fact]
    public void ManifestContradictionsAndFutureVersionsCannotEnableLibrary()
    {
        var database = BuildOriginal(); var manifest = Path.ChangeExtension(database, ".manifest.json");
        var json = File.ReadAllText(manifest); File.WriteAllText(manifest, json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999"));
        Assert.Contains(KnowledgeInspector.Inspect(database, manifest: manifest).Items, i => i.Code == "manifest" && i.Status == "error");
        File.WriteAllText(manifest, json.Replace("\"totalSources\": 1", "\"totalSources\": 900"));
        Assert.False(KnowledgeInspector.Inspect(database, manifest: manifest).CanEnable);
    }

    [Fact]
    public void HtmlAndPmlAreOnlyReadFromExplicitSourcesAndOtherProductIsNotScanned()
    {
        var html = Path.Combine(root, "selected-help"); Directory.CreateDirectory(html);
        File.WriteAllText(Path.Combine(html, "topic.html"), "<html><head><title>Selected Reference</title></head><body><h1>Yuzuha guide</h1><p>Selected HTML documentation about engineering functions. This synthetic paragraph contains enough reference text for the existing section parser to retain it.</p></body></html>");
        var unselected = Path.Combine(root, "unselected-product"); Directory.CreateDirectory(unselected);
        File.WriteAllText(Path.Combine(unselected, "do-not-import.pmlfnc"), "UNSELECTED UNIQUE SECRET SYNTHETIC");
        var receipt = Service().Apply(Service().Preview(Request() with { WebHelp = html }));
        Assert.Equal(2, receipt.Report.Sources);
        Assert.DoesNotContain(receipt.Report.SourceEvidence, s => s.RecordedPath.Contains("unselected-product", StringComparison.Ordinal));
        Assert.Contains(receipt.Report.SourceEvidence, s => s.RecordedPath.EndsWith("topic.html", StringComparison.Ordinal));
    }

    [Fact]
    public void CancellationBeforePublishKeepsPreviousLibraryAndDeletesPrivateStage()
    {
        var initial = Service().Apply(Service().Preview(Request())); var before = Hash(initial.Library.Database);
        using var cancellation = new CancellationTokenSource();
        var service = new KnowledgeImportService(Knowledge, phase => { if (phase == "publish") cancellation.Cancel(); }, cancellation.Token);
        var plan = service.Preview(Request(name: "cancelled"));
        Assert.Throws<OperationCanceledException>(() => service.Apply(plan));
        Assert.Single(KnowledgeLibraries.List(Knowledge)); Assert.Equal(before, Hash(initial.Library.Database)); Assert.False(Directory.Exists(plan.Target));
    }

    [Fact]
    public async Task SearchExplicitlyDistinguishesUncheckedAndCurrentSourceEvidenceAndRefusesReplacedDatabase()
    {
        var library = Service().Apply(Service().Preview(Request())).Library;
        var executor = new OperationExecutor(new OperationRegistry(KnowledgeOperations.Registrations(Knowledge, new ManagerPlanStore(Path.Combine(root, "state")))));
        var request = JsonSerializer.SerializeToElement(new { product = "E3D", version = "2.1", name = "reference", query = "Yuzuha" });
        var result = await executor.InvokeAsync("knowledge.library.search", request, new());
        Assert.Equal("not-rechecked", result.Data!.Value.GetProperty("sourceEvidenceState").GetString());
        File.WriteAllText(FileName, File.ReadAllText(FileName).Replace("alpha", "omega"));
        request = JsonSerializer.SerializeToElement(new { product = "E3D", version = "2.1", name = "reference", query = "Yuzuha", checkSources = true });
        result = await executor.InvokeAsync("knowledge.library.search", request, new());
        Assert.True(result.Success, result.Error?.Message); Assert.Equal("checked-now", result.Data!.Value.GetProperty("sourceEvidenceState").GetString());
        Assert.Equal("changed", result.Data.Value.GetProperty("inspection").GetProperty("sourceEvidence")[0].GetProperty("status").GetString());
        Sql(library.Database, "UPDATE meta SET value='changed' WHERE key='generator'");
        result = await executor.InvokeAsync("knowledge.library.search", request, new()); Assert.False(result.Success);
    }

    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
