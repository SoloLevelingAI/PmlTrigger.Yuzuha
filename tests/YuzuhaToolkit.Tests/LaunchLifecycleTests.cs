using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

[Collection("UserData environment")]
public sealed class LaunchLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PhaytonLaunchLifecycle-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "state");
    private string FilePath => Path.Combine(State, "workspace.json");
    private WorkspaceConfigurationStore Store => new(State);
    private LaunchProfileService Service => new(Store);

    private LaunchProfile Profile(string product = "E3D", string id = "sample")
    {
        var folder = Path.Combine(root, product + "2.1"); Directory.CreateDirectory(folder);
        var entry = Path.Combine(folder, product == "AM" ? "marine.bat" : "mon.exe");
        File.WriteAllText(entry, "test fixture; never execute");
        var init = Path.Combine(folder, "launch.init"); File.WriteAllText(init, "fixture");
        return new() { Id = id, Name = product + " test", Product = product, Version = product == "AM" ? "12.1" : "2.1",
            Kind = product == "AM" ? "MarineBatch" : "Monitor", Executable = entry, Initialization = product == "AM" ? "" : init,
            WorkingDirectory = folder, Project = "DEMO", User = "TEST", Mdb = "/ALL", Module = product == "AM" ? "Outfitting" : "Design" };
    }
    private ConfigCommit<WorkspaceSettings> Seed(LaunchProfile? profile = null) => Service.Save(profile ?? Profile(), "missing", true);
    private static JsonElement Json(string source) { using var document = JsonDocument.Parse(source); return document.RootElement.Clone(); }

    [Fact]
    public void SavedProfileIsReadByNewServiceAndDraftDoesNotMutateFile()
    {
        var saved = Seed(); var reopened = new LaunchProfileService(new WorkspaceConfigurationStore(State)).ReadProfile("sample");
        Assert.Equal(saved.Snapshot.Revision, reopened.Revision); Assert.Equal("/ALL", reopened.Profile.Mdb);
        reopened.Profile.Mdb = "/DRAFT";
        Assert.Equal("/ALL", Service.ReadProfile("sample").Profile.Mdb);
        var next = Service.Save(reopened.Profile, reopened.Revision, true);
        Assert.NotEqual(saved.Snapshot.Revision, next.Snapshot.Revision);
        Assert.Equal("/DRAFT", new WorkspaceConfigurationStore(State).Read().Value.Profiles.Single().Mdb);
        Assert.Equal(FilePath, next.Snapshot.FilePath); Assert.Contains("下次启动", next.Effect);
    }

    [Theory]
    [InlineData("AM", "Design")]
    [InlineData("AM", "Draw")]
    [InlineData("E3D", "Outfitting")]
    [InlineData("E3D", "HullDesign")]
    public void InvalidProductModuleRejectsBeforePersistence(string product, string module)
    {
        var profile = Profile(product); var saved = Seed(profile); var bytes = File.ReadAllBytes(FilePath);
        profile.Module = module;
        var error = Assert.Throws<LaunchProfileValidationException>(() => Service.Save(profile, saved.Snapshot.Revision, true));
        Assert.Contains(error.Validation.Issues, i => i.Code == "MODULE_PRODUCT_MISMATCH" && i.Field == "module");
        Assert.Equal(bytes, File.ReadAllBytes(FilePath)); Assert.Single(Service.History());
    }

    [Fact]
    public void ProductCatalogUsesActualMarineValuesAndNeverClaimsMissingInstallationWasVerified()
    {
        var catalog = new ProductModuleCatalog();
        var am = catalog.Get(new(Path.Combine(root, "missing/marine.bat"), "AM", "12.1", "MarineBatch"));
        Assert.Contains(am.Options, o => o.Value == "OutfittingDraft");
        Assert.Contains(am.Options, o => o.Value == "MarineDrafting");
        Assert.DoesNotContain(am.Options, o => o.Value is "Design" or "Draw" or "HullDraft");
        Assert.All(am.Options, o => Assert.False(o.Verified));
        Assert.NotEmpty(am.Warning);
        var e3d = catalog.Get(new("", "E3D", "2.1"));
        Assert.Contains(e3d.Options, o => o.Value == "Design"); Assert.Contains(e3d.Options, o => o.Value == "Draw");
        Assert.DoesNotContain(e3d.Options, o => o.Value == "Outfitting");
        Assert.Empty(catalog.Get(new("", "unknown")).Options);
    }

    [Fact]
    public void ModuleMetadataIsResolvedFromSelectedInstallationOnly()
    {
        var am = Profile("AM");
        File.WriteAllText(am.Executable, "if /i not \"%moduleArg%\"==\"Outfitting\" goto next\n");
        var modules = new ProductModuleCatalog().Get(new(am.Executable, "AM", "12.1", "MarineBatch"));
        Assert.True(modules.Options.Single(m => m.Value == "Outfitting").Verified);
        Assert.False(modules.Options.Single(m => m.Value == "HullDesign").Verified);
        Assert.Contains(am.Executable, modules.Source);
        var e3d = Profile(); File.WriteAllText(Path.Combine(e3d.WorkingDirectory, "DesignAddins.xml"), "<Addins/>");
        var design = new ProductModuleCatalog().Get(new(e3d.Executable, "E3D", "2.1"));
        Assert.True(design.Options.Single(m => m.Value == "Design").Verified);
        Assert.False(design.Options.Single(m => m.Value == "Draw").Verified);
    }

    [Fact]
    public void JsonUpdatePreservesUnknownWorkspaceAndProfileFields()
    {
        Seed();
        var node = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        node["futureWorkspace"] = JsonNode.Parse("{\"nested\":[1,2]}");
        node["profiles"]![0]!["futureProfile"] = JsonNode.Parse("{\"custom\":true}");
        File.WriteAllText(FilePath, node.ToJsonString());
        var before = Service.Read();
        var saved = Service.Update("sample", Json("{\"mdb\":\"/REVIEW\"}"), before.Revision, true);
        var document = JsonDocument.Parse(File.ReadAllBytes(FilePath));
        Assert.Equal(2, document.RootElement.GetProperty("futureWorkspace").GetProperty("nested").GetArrayLength());
        Assert.True(document.RootElement.GetProperty("profiles")[0].GetProperty("futureProfile").GetProperty("custom").GetBoolean());
        Assert.Equal("/REVIEW", saved.Snapshot.Value.Profiles.Single().Mdb);
    }

    [Fact]
    public void FullProfileEditorAlsoPreservesExistingUnknownFields()
    {
        var profile = Profile(); profile.ExtensionData = new() { ["customData"] = Json("{\"keep\":9}") };
        var seed = Seed(profile);
        var replacement = Profile(); replacement.Name = "edited"; replacement.ExtensionData = null;
        Service.Save(replacement, seed.Snapshot.Revision, true);
        Assert.Equal(9, Service.ReadProfile("sample").Profile.ExtensionData!["customData"].GetProperty("keep").GetInt32());
    }

    [Fact]
    public void ConcurrentEditorsCannotOverwriteTheFirstSavedChange()
    {
        Seed(); var first = Service.ReadProfile("sample"); var second = Service.ReadProfile("sample");
        first.Profile.Mdb = "/FIRST"; var saved = Service.Save(first.Profile, first.Revision, true);
        second.Profile.Mdb = "/SECOND";
        var error = Assert.Throws<ConfigurationConflictException>(() => Service.Save(second.Profile, second.Revision, true));
        Assert.Equal(saved.Snapshot.Revision, error.ActualRevision);
        Assert.Equal("/FIRST", Service.ReadProfile("sample").Profile.Mdb);
    }

    [Fact]
    public async Task SimultaneousCommitsHaveExactlyOneWinner()
    {
        var initial = Seed();
        async Task<bool> Attempt(string name)
        {
            await Task.Yield();
            try { Service.Update("sample", Json("{\"name\":\"" + name + "\"}"), initial.Snapshot.Revision, true); return true; }
            catch (ConfigurationConflictException) { return false; }
        }
        var attempts = await Task.WhenAll(Task.Run(() => Attempt("first")), Task.Run(() => Attempt("second")));
        Assert.Single(attempts, value => value); Assert.Equal(2, Service.History().Length);
    }

    [Fact]
    public void ExternalFileEditInvalidatesPreviouslyReadRevision()
    {
        var seed = Seed(); var node = JsonNode.Parse(File.ReadAllText(FilePath))!;
        node["pluginName"] = "external-change"; File.WriteAllText(FilePath, node.ToJsonString());
        Assert.Throws<ConfigurationConflictException>(() => Service.Update("sample", Json("{\"mdb\":\"/NEW\"}"), seed.Snapshot.Revision, true));
        Assert.Equal("external-change", Store.Read().Value.PluginName);
    }

    [Fact]
    public void RestoreCreatesNewRevisionAndKeepsOtherProfilesAndOldHistory()
    {
        var initial = Seed();
        var second = Service.Save(Profile("AM", "other"), initial.Snapshot.Revision, true);
        var changed = Service.Update("sample", Json("{\"mdb\":\"/UPDATED\"}"), second.Snapshot.Revision, true);
        var preview = Service.PreviewRestore("sample", initial.Snapshot.Revision, changed.Snapshot.Revision);
        Assert.True(preview.Validation.IsValid); Assert.NotEmpty(preview.Changes);
        Assert.Equal("/UPDATED", Service.ReadProfile("sample").Profile.Mdb);
        var restored = Service.Restore("sample", initial.Snapshot.Revision, changed.Snapshot.Revision, true);
        Assert.Equal("/ALL", Service.ReadProfile("sample").Profile.Mdb); Assert.Equal("AM", Service.ReadProfile("other").Profile.Product);
        Assert.NotEqual(initial.Snapshot.Revision, restored.Snapshot.Revision);
        Assert.Contains(Service.History(), h => h.Revision == initial.Snapshot.Revision);
        Assert.Equal(initial.Snapshot.Revision, Service.History()[0].RestoredFrom);
    }

    [Fact]
    public void RestoreRevalidatesCurrentPathsAndCannotSilentlyAcceptMissingEntry()
    {
        var initial = Seed(); File.Delete(initial.Snapshot.Value.Profiles.Single().Executable);
        Assert.Throws<LaunchProfileValidationException>(() => Service.Restore("sample", initial.Snapshot.Revision, initial.Snapshot.Revision, true));
        Assert.Equal(initial.Snapshot.Revision, Store.Read().Revision);
    }

    [Fact]
    public void DeletedProfileCanBeRecoveredFromPreviousRevision()
    {
        var initial = Seed(); var deleted = Service.Delete("sample", initial.Snapshot.Revision, true);
        Assert.Empty(Service.List());
        Service.Restore("sample", initial.Snapshot.Revision, deleted.Snapshot.Revision, true);
        Assert.Equal("sample", Service.ReadProfile("sample").Profile.Id);
    }

    [Theory]
    [InlineData("prepared", false)]
    [InlineData("replaced", true)]
    public void InterruptedCommitRecoversToOldOrCommittedStateWithoutFalseHistory(string stage, bool committed)
    {
        var initial = Seed();
        var faulting = new LaunchProfileService(new WorkspaceConfigurationStore(State, point =>
        { if (point == stage) throw new IOException("injected interruption"); }));
        Assert.Throws<IOException>(() => faulting.Update("sample", Json("{\"mdb\":\"/RECOVERED\"}"), initial.Snapshot.Revision, true));
        var reopened = Service.ReadProfile("sample");
        Assert.Equal(committed ? "/RECOVERED" : "/ALL", reopened.Profile.Mdb);
        Assert.Equal(committed ? 2 : 1, Service.History().Length);
        Assert.False(File.Exists(Path.Combine(State, "workspace.history/pending.json")));
    }

    [Fact]
    public void CorruptHistoricalSnapshotIsRejectedBeforeRestore()
    {
        var initial = Seed(); var bytes = File.ReadAllBytes(FilePath);
        File.WriteAllText(Path.Combine(State, "workspace.history", initial.Snapshot.Revision + ".json"), "{}");
        Assert.Throws<InvalidDataException>(() => Service.Restore("sample", initial.Snapshot.Revision, initial.Snapshot.Revision, true));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void HigherSchemaAndDuplicateFieldsCannotBeSilentlyRewritten()
    {
        Directory.CreateDirectory(State); File.WriteAllText(FilePath, "{\"schemaVersion\":2}");
        Assert.Throws<InvalidDataException>(() => Store.Read());
        Assert.Equal("{\"schemaVersion\":2}", File.ReadAllText(FilePath));
        File.WriteAllText(FilePath, "{\"schemaVersion\":1,\"schemaVersion\":1}");
        Assert.Throws<InvalidDataException>(() => Store.Read());
    }

    [Fact]
    public void LegacyWorkspaceWithoutSchemaOrRevisionIsArchivedBeforeFirstSave()
    {
        var profile = Profile(); var settings = new WorkspaceSettings { Profiles = [profile] };
        var node = JsonSerializer.SerializeToNode(settings, WorkspaceJsonContext.Default.WorkspaceSettings)!.AsObject();
        node.Remove("schemaVersion"); node.Remove("configurationRevision");
        Directory.CreateDirectory(State); File.WriteAllText(FilePath, node.ToJsonString());
        var original = Service.Read();
        Service.Update("sample", Json("{\"name\":\"migrated\"}"), original.Revision, true);
        Assert.Equal(1, Store.Read().SchemaVersion);
        Assert.Contains(Service.History(), h => h.Revision == original.Revision && h.Source == "external-snapshot");
    }

    [Fact]
    public void PermissionDenialAndUnknownPatchFieldsLeaveFileUntouched()
    {
        var initial = Seed(); var bytes = File.ReadAllBytes(FilePath);
        Assert.Throws<UnauthorizedAccessException>(() => Service.Update("sample", Json("{\"mdb\":\"/NO\"}"), initial.Snapshot.Revision, false));
        Assert.Throws<ArgumentException>(() => Service.Update("sample", Json("{\"mddb\":\"/TYPO\"}"), initial.Snapshot.Revision, true));
        Assert.Throws<ArgumentException>(() => Service.Update("sample", Json("{\"mdb\":\"/A\",\"mdb\":\"/B\"}"), initial.Snapshot.Revision, true));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void DuplicateDropsCredentialsAndDefaultButPreservesVersionAndExtensions()
    {
        var profile = Profile(); profile.CredentialKey = "reference-only"; profile.IsDefault = true; profile.LastStartedUtc = DateTimeOffset.UtcNow;
        profile.ExtensionData = new() { ["future"] = Json("true") };
        var initial = Seed(profile); var duplicate = Service.Duplicate("sample", initial.Snapshot.Revision, true);
        Assert.NotEqual("sample", duplicate.Profile.Id); Assert.Equal("", duplicate.Profile.CredentialKey);
        Assert.False(duplicate.Profile.IsDefault); Assert.Null(duplicate.Profile.LastStartedUtc);
        Assert.Equal(profile.Version, duplicate.Profile.Version); Assert.True(duplicate.Profile.ExtensionData!["future"].GetBoolean());
    }

    [Fact]
    public void FiltersFavoritesDefaultsAndHistoryUseSameService()
    {
        var one = Seed(); var two = Service.Save(Profile("AM", "am"), one.Snapshot.Revision, true);
        var favorite = Service.SetFavorite("am", true, two.Snapshot.Revision, true);
        Service.SetDefault("am", favorite.Snapshot.Revision, true);
        Assert.Equal("am", Assert.Single(Service.List("test", "AM", "favorites")).Profile.Id);
        Assert.Equal("am", Service.List()[0].Profile.Id);
        Assert.Single(Store.Read().Value.Profiles, p => p.IsDefault);
        Assert.All(Service.History("am"), entry => Assert.True(entry.ProfileId is null or "am"));
    }

    [Fact]
    public void LegacyLoadSaveParticipatesInRevisionHistoryAndDetectsStaleEdit()
    {
        var previous = Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR");
        Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", State);
        try
        {
            new WorkspaceSettings().Save(); var first = WorkspaceSettings.Load(); var stale = WorkspaceSettings.Load();
            first.PluginName = "one"; first.Save(); first.PluginName = "two"; first.Save();
            Assert.Throws<ConfigurationConflictException>(() => stale.Save());
            Assert.Equal("two", WorkspaceSettings.Load().PluginName); Assert.Equal(3, Store.History().Length);
            Assert.Equal(FilePath, new LaunchProfileService().Read().FilePath);
        }
        finally { Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", previous); }
    }

    public void Dispose()
    {
        var target = Path.GetFullPath(root);
        if (target.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(target).StartsWith("PhaytonLaunchLifecycle-", StringComparison.Ordinal) && Directory.Exists(target)) Directory.Delete(target, true);
    }
}
