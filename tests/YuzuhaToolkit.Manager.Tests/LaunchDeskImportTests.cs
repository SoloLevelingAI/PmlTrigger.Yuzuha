using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchDeskImportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaPrototypeImport-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "manager-state");
    private string Write(string relative, string text)
    { var file = Path.GetFullPath(Path.Combine(root, relative)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); return file; }
    private string Next(string file = "next/profiles.json") => Write(file, JsonSerializer.Serialize(new[] { new {
        ConfigurationId = "source-profile", DisplayName = "Selected profile", ExecutablePath = Path.Combine(root, "mon.exe"), WorkingDirectory = Path.Combine(root, "work"),
        Protocol = 0, Product = "E3D", InitializationFile = Path.Combine(root, "launch.init"), Project = "TST", User = "SYSTEM", Mdb = "ALL", Module = "Design",
        LicenceServer = "fixture-server", Password = "NEVER_IMPORT_PASSWORD", CredentialReference = "OTHER_APP_CREDENTIAL_REFERENCE",
        Environment = new { API_KEY = "NEVER_IMPORT_ENV" } } }));

    [Fact]
    public void PortableRelativeDataRootIsResolvedBesideSelectedApplicationAndSecretsAreNotCopied()
    {
        var app = Path.Combine(root, "app"); var data = Path.Combine(app, "Data");
        var entry = Write("app/portable.json", "{\"DataDirectory\":\"Data\"}");
        var executable = Path.Combine(root, "legacy-mon.exe");
        var workspace = Write("app/Data/workspace.json", JsonSerializer.Serialize(new {
            SchemaVersion = 1, Applications = new[] { new { Id = "app", Kind = "Monitor", ExecutablePath = executable, InitializationPath = "launch.init", WorkingDirectory = root, ProductCode = "E3D" } },
            Profiles = new[] { new { Id = "old", AppId = "app", Name = "Legacy", Project = "TST", Username = "SYSTEM", Mdb = "ALL", Module = "Design", Password = "NEVER_IMPORT_PASSWORD", CredentialReference = "OTHER_APP_CREDENTIAL_REFERENCE" } },
            Licenses = new[] { new { Password = "NEVER_READ_LICENSE_SECRET" } } }));
        var before = File.ReadAllBytes(workspace);
        Write("app/Data/credentials.json", "this must never be parsed"); Next("unselected/profiles.json");
        var plan = LaunchDeskImport.Preview(app); Assert.Equal(data, plan.DataRoot); Assert.Single(plan.Drafts);
        var json = JsonSerializer.Serialize(plan, ManagerJsonContext.Default.LaunchDeskImportPlan);
        Assert.DoesNotContain("NEVER_IMPORT", json); Assert.DoesNotContain("OTHER_APP_CREDENTIAL_REFERENCE", json); Assert.DoesNotContain("NEVER_READ_LICENSE_SECRET", json);
        var receipt = LaunchDeskImport.Apply(plan, State); Assert.True(File.Exists(Path.Combine(receipt.Directory, "import.json")));
        Assert.Equal(before, File.ReadAllBytes(workspace)); Assert.Equal(receipt, LaunchDeskImport.Apply(plan, State));
        Assert.All(plan.Sources, source => Assert.StartsWith(app, source.File));
        Assert.Equal("", Assert.Single(plan.Drafts).Profile.CredentialKey); Assert.Empty(plan.Drafts[0].Profile.Environment);
        Assert.True(File.Exists(entry));
    }

    [Fact]
    public void NextProfilesDraftSnapshotsAndHistoryAreSanitizedAndDamagedTailIsExplicit()
    {
        var profiles = Next(); using var parsed = JsonDocument.Parse(File.ReadAllText(profiles));
        var model = parsed.RootElement[0].GetRawText();
        Write("next/draft.json", "{\"Generation\":3,\"Model\":" + model + "}"); Write("next/snapshots/one.json", "{\"Model\":" + model + "}");
        var record = JsonSerializer.Serialize(new { Sequence = 1, Timestamp = "2026-10-30T00:00:00Z", AttemptId = Guid.NewGuid(), Stage = "Submitted", Outcome = "Checking",
            Snapshot = new { Project = "TST", Module = "Design", CredentialReference = "OTHER_APP_CREDENTIAL_REFERENCE", InputSnapshot = "NEVER_IMPORT_NESTED", ArgumentPlan = "NEVER_IMPORT_ARGUMENTS", Reason = "NEVER_IMPORT_REASON" } });
        Write("next/logs/records.jsonl", record + "\ninvalid-tail");
        var plan = LaunchDeskImport.Preview(Path.Combine(root, "next")); Assert.Equal(3, plan.Drafts.Length); Assert.Single(plan.History);
        Assert.Contains(plan.Warnings, w => w.Contains("损坏", StringComparison.Ordinal));
        var json = JsonSerializer.Serialize(plan, ManagerJsonContext.Default.LaunchDeskImportPlan);
        Assert.DoesNotContain("OTHER_APP_CREDENTIAL_REFERENCE", json); Assert.DoesNotContain("NEVER_IMPORT", json);
        Assert.Equal("TST", plan.History[0].Fields["Project"]);
        Assert.Equal(3, plan.Drafts.Select(d => d.Profile.Id).Distinct().Count());
    }

    [Fact]
    public void InvalidPortableOrChangedSourceCannotFallbackOrPublish()
    {
        var portable = Write("bad/portable.json", "{}");
        Assert.Throws<InvalidDataException>(() => LaunchDeskImport.Preview(Path.GetDirectoryName(portable)!));
        var source = Next(); var plan = LaunchDeskImport.Preview(Path.GetDirectoryName(source)!);
        File.AppendAllText(source, " ");
        Assert.Throws<IOException>(() => LaunchDeskImport.Apply(plan, State)); Assert.False(Directory.Exists(State));
    }

    [Fact]
    public async Task ManagerImportRequiresWriteAndReadVerifiesPublishedContent()
    {
        var source = Next();
        var executor = new OperationExecutor(new OperationRegistry(LaunchDeskImportOperations.Registrations(new ManagerPlanStore(State), State)));
        var preview = await executor.InvokeAsync("launchdesk.import.preview", JsonSerializer.SerializeToElement(new { source = Path.GetDirectoryName(source) }), new());
        Assert.True(preview.Success, preview.Error?.Message);
        var value = preview.Data!.Value;
        var reference = JsonSerializer.SerializeToElement(new { planId = value.GetProperty("planId").GetString(), sha256 = value.GetProperty("sha256").GetString() });
        Assert.Equal(4, (await executor.InvokeAsync("launchdesk.import.apply", reference, new())).ExitCode);
        var applied = await executor.InvokeAsync("launchdesk.import.apply", reference, new(AllowWrite: true)); Assert.True(applied.Success, applied.Error?.Message);
        var id = applied.Data!.Value.GetProperty("id").GetString(); var readInput = JsonSerializer.SerializeToElement(new { id });
        Assert.True((await executor.InvokeAsync("launchdesk.import.read", readInput, new())).Success);
        var importedFile = Path.Combine(applied.Data.Value.GetProperty("directory").GetString()!, "import.json"); File.AppendAllText(importedFile, " ");
        Assert.False((await executor.InvokeAsync("launchdesk.import.read", readInput, new())).Success);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
