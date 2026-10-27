using Xunit;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

[CollectionDefinition("UserData environment", DisableParallelization = true)]
public sealed class UserDataEnvironmentCollection { }

[Collection("UserData environment")]
public sealed class UserDataManagementTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "YuzuhaUserData-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> environment = new();
    private string Old => Path.Combine(root, "old");
    private string Next => Path.Combine(root, "next");
    private KnowledgeRepository Repository => new(Path.Combine(Old, "Knowledge"));
    public UserDataManagementTests()
    {
        foreach (var name in new[] { "YUZUHA_TOOLKIT_ROOT", "YUZUHA_STATE_DIR", "YUZUHA_USER_DIR", "YUZUHA_KNOWLEDGE_DIR", "YUZUHA_WEBAPPS_DIR" })
        { environment[name] = Environment.GetEnvironmentVariable(name); Environment.SetEnvironmentVariable(name, null); }
        Environment.SetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT", Path.Combine(root, "install"));
        Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", Path.Combine(root, "state"));
        new WorkspaceConfigurationStore().Commit(new WorkspaceSettings { UserDataRoot = Old, PluginName = "KeepMyPlugin", Profiles = [new() { Name = "KeepMyProject", CredentialKey = "reference-only" }] }, "missing", new("创建迁移夹具"));
    }
    private string Write(string relative, string text)
    { var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); return path; }
    [Fact]
    public void SharedPathsHonorSavedRootAndExplicitOverridesWithoutCreatingData()
    {
        Assert.Equal(Path.Combine(Old, "Knowledge"), new KnowledgeRepository().DefaultDirectory);
        Assert.Equal(Path.Combine(Old, "Apps"), UserDataPaths.AppsDirectory);
        Assert.False(Directory.Exists(Old));
        Environment.SetEnvironmentVariable("YUZUHA_KNOWLEDGE_DIR", Path.Combine(root, "explicit"));
        Assert.Equal(Path.Combine(root, "explicit"), new KnowledgeRepository().DefaultDirectory);
        Assert.Throws<IOException>(() => UserDataMigrationService.Preview(Next));
    }
    [Fact]
    public void UnconfiguredExistingKnowledgeRetainsItsLegacyLocation()
    {
        var store = new WorkspaceConfigurationStore(); var snapshot = store.Read();
        snapshot.Value.UserDataRoot = ""; snapshot.Value.KnowledgeDirectory = "";
        store.Commit(snapshot.Value, snapshot.Revision, new("未配置目录夹具"));
        var legacy = Path.Combine(root, "install", "knowledge"); Directory.CreateDirectory(legacy);
        Assert.Equal(legacy, new KnowledgeRepository().DefaultDirectory);
        Assert.False(Directory.Exists(UserDataPaths.RecommendedRoot) && UserDataPaths.RecommendedRoot.StartsWith(root));
    }
    [Fact]
    public void MigrationCopiesAndVerifiesAllDataRetainsSourcesAndSettings()
    {
        var note = Repository.SaveNote(null, 0, "迁移笔记", "quartz fixture", "offline");
        Write("old/Apps/apps.json", "saved button binding"); Write("old/Projects/model.txt", "project");
        var plan = UserDataMigrationService.Preview(Next); Assert.False(Directory.Exists(Next));
        UserDataMigrationService.Apply(plan);
        var settings = new WorkspaceConfigurationStore().Read().Value; Assert.Equal(Next, settings.UserDataRoot); Assert.Equal("KeepMyPlugin", settings.PluginName);
        Assert.Equal("reference-only", Assert.Single(settings.Profiles).CredentialKey);
        Assert.Equal(note, Assert.Single(new KnowledgeRepository().ListNotes())); Assert.Equal(note, Assert.Single(Repository.ListNotes()));
        Assert.Equal("saved button binding", File.ReadAllText(Path.Combine(Next, "Apps/apps.json")));
        Assert.True(File.Exists(Path.Combine(Next, UserDataPaths.MarkerName)));
    }
    [Fact]
    public void MigrationRefusesConflictsAndChangedSourcesAndAddedFiles()
    {
        var source = Write("old/Apps/apps.json", "source"); Write("next/Apps/apps.json", "keep target");
        Assert.Throws<IOException>(() => UserDataMigrationService.Preview(Next));
        File.Delete(Path.Combine(Next, "Apps/apps.json")); var plan = UserDataMigrationService.Preview(Next);
        File.WriteAllText(source, "changed"); Assert.Throws<IOException>(() => UserDataMigrationService.Apply(plan));
        plan = UserDataMigrationService.Preview(Next); Write("old/Apps/new.json", "new");
        Assert.Throws<IOException>(() => UserDataMigrationService.Apply(plan)); Assert.Equal(Old, new WorkspaceConfigurationStore().Read().Value.UserDataRoot);
    }
    [Fact]
    public void MigrationFailureAfterFirstCopyRollsBackNewFilesAndRetainsExistingTargets()
    {
        Write("old/Apps/a.json", "new"); Write("old/Apps/nested/b.json", "second");
        var conflict = Write("next/Apps/nested", "existing user file");
        var preview = UserDataMigrationService.Preview(Next);
        var ordered = preview with { Files = preview.Files.OrderBy(file => file.Source).ToArray() };
        Assert.Throws<IOException>(() => UserDataMigrationService.Apply(ordered));
        Assert.False(File.Exists(Path.Combine(Next, "Apps/a.json")));
        Assert.Equal("existing user file", File.ReadAllText(conflict));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Old, "Apps/a.json")));
        Assert.Equal(Old, new WorkspaceConfigurationStore().Read().Value.UserDataRoot);
    }
    [Fact]
    public void MigrationRefusesSettingsChangesAndActiveSqliteWriters()
    {
        Repository.SaveNote(null, 0, "note", "text", ""); var plan = UserDataMigrationService.Preview(Next);
        using (var held = new FileStream(Path.Combine(Old, "Knowledge/personal.sqlite3.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => UserDataMigrationService.Apply(plan));
        Write("old/Knowledge/personal.sqlite3-wal", "busy"); Assert.Throws<IOException>(() => UserDataMigrationService.Preview(Next));
        File.Delete(Path.Combine(Old, "Knowledge/personal.sqlite3-wal")); var store = new WorkspaceConfigurationStore(); var snapshot = store.Read();
        snapshot.Value.PluginName = "changed"; store.Commit(snapshot.Value, snapshot.Revision, new("并发编辑夹具"));
        Assert.Throws<IOException>(() => UserDataMigrationService.Apply(plan)); Assert.Equal(Old, new WorkspaceConfigurationStore().Read().Value.UserDataRoot);
    }
    [Fact]
    public void UserRootCannotOverlapInstallationOrInternalStateOrExistingData()
    {
        Assert.Throws<IOException>(() => UserDataMigrationService.Preview(Path.Combine(root, "install", "user")));
        Assert.Throws<IOException>(() => UserDataMigrationService.Preview(root));
        Write("old/Apps/app.json", "data");
        Assert.Throws<IOException>(() => UserDataMigrationService.Preview(Path.Combine(Old, "Apps", "child")));
    }
    [Fact]
    public void NotesCreateEditSearchDeleteAndRestoreKeepEveryRevision()
    {
        var repository = Repository; var first = repository.SaveNote(null, 0, "QuartzNote", "zephyrtoken", "fixture");
        Assert.Contains(repository.SearchLayers("zephyrtoken", 10, true), layer => layer.Role == "personal" && layer.Result!.Hits.Count > 0);
        var second = repository.SaveNote(first.Id, 1, "Revised", "saffrontoken", "verified");
        Assert.Equal(first, repository.NoteHistory(first.Id).Last());
        Assert.DoesNotContain(repository.SearchLayers("zephyrtoken", 10, true), layer => layer.Role == "personal" && layer.Result!.Hits.Count > 0);
        repository.SaveNote(first.Id, 2, second.Title, second.Content, second.Context, true);
        Assert.Empty(repository.ListNotes()); Assert.True(Assert.Single(repository.ListNotes(true)).Deleted);
        Assert.DoesNotContain(repository.SearchLayers("saffrontoken", 10, true), layer => layer.Role == "personal" && layer.Result!.Hits.Count > 0);
        repository.SaveNote(first.Id, 3, second.Title, second.Content, second.Context);
        Assert.Equal(4, Assert.Single(repository.ListNotes()).Revision); Assert.Equal(4, repository.NoteHistory(first.Id).Length);
        Assert.Equal(1, Assert.Single(repository.ListLibraries()).Chunks);
    }
    [Fact]
    public void StaleEditorCannotOverwriteLatestRevision()
    {
        var first = Repository.SaveNote(null, 0, "Title", "body", ""); Repository.SaveNote(first.Id, 1, "Newest", "body", "");
        Assert.Throws<KnowledgeException>(() => Repository.SaveNote(first.Id, 1, "stale", "body", ""));
        Assert.Equal("Newest", Assert.Single(Repository.ListNotes()).Title); Assert.Equal(2, Repository.NoteHistory(first.Id).Length);
    }
    [Fact]
    public void LibraryRebuildArchiveAndRestoreNeverChangeOriginalSource()
    {
        var source = Write("source/sample.pmlfnc", "define function !!Sample()\n-- quartz\nendfunction\n");
        var repository = Repository; repository.RegisterSource("custom", "fixture", Path.GetDirectoryName(source), null, null, false);
        File.AppendAllText(source, "-- updated\n"); var expected = File.ReadAllText(source); repository.RebuildLibrary("custom-fixture");
        Assert.Single(Directory.GetDirectories(Path.Combine(repository.DefaultDirectory, ".history")));
        var archived = repository.ArchiveLibrary("custom-fixture"); Assert.Empty(repository.ListLibraries()); Assert.Single(repository.ArchivedLibraries());
        Assert.Equal(expected, File.ReadAllText(source)); repository.RestoreLibrary(archived.Id);
        Assert.Single(repository.ListLibraries()); Assert.Empty(repository.ArchivedLibraries()); Assert.Equal(expected, File.ReadAllText(source));
    }
    [Fact]
    public void RestoreRefusesCollisionAndReservedLibrariesRemainProtected()
    {
        var source = Write("source/sample.pmlfnc", "define function !!Sample()\nendfunction\n");
        Repository.RegisterSource("official", "fixture", Path.GetDirectoryName(source), null, null, false);
        var archived = Repository.ArchiveLibrary("official-fixture");
        var target = Write("old/Knowledge/official-fixture.sqlite3", "do not overwrite");
        Assert.Throws<KnowledgeException>(() => Repository.RestoreLibrary(archived.Id)); Assert.Equal("do not overwrite", File.ReadAllText(target)); Assert.Single(Repository.ArchivedLibraries());
        foreach (var name in new[] { "personal", "experience", "project", "../outside" })
            Assert.Throws<KnowledgeException>(() => Repository.ArchiveLibrary(name));
        Assert.Throws<KnowledgeException>(() => Repository.Build(Path.GetDirectoryName(source), null, null, "personal", null, true, 0));
    }
    public void Dispose()
    {
        foreach (var item in environment) Environment.SetEnvironmentVariable(item.Key, item.Value);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
