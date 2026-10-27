using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

[Collection("UserData environment")]
public sealed class AvevaLaunchScriptTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PhaytonBackend-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> credentials = new();
    private readonly string? previousState = Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR");
    private readonly string? previousActions = Environment.GetEnvironmentVariable("YUZUHA_STARTUP_ACTIONS");
    private int prepares, reads, deletes, starts;
    private string? dispatchedPassword;
    private WorkspaceConfigurationStore Store => new(Path.Combine(root, "state"));
    private LaunchProfileService Profiles => new(Store);
    private string Revision => Store.Read().Revision;

    public AvevaLaunchScriptTests()
    {
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", Path.Combine(root, "state"));
        Environment.SetEnvironmentVariable("YUZUHA_STARTUP_ACTIONS", "none");
    }
    private LaunchProfile Draft(string id = "one")
    {
        var exe = Path.Combine(root, "mon.exe"); File.WriteAllText(exe, "synthetic; never executed");
        var init = Path.Combine(root, "launch.init"); File.WriteAllText(init, "fixture");
        return new() { Id = id, Name = "Fixture", Product = "E3D", Version = "2.1", Kind = "Monitor",
            Executable = exe, Initialization = init, WorkingDirectory = root, Project = "TST", User = "SYSTEM", Module = "Design" };
    }
    private string Prepare(string current, string password, bool remember)
    {
        prepares++;
        if (!remember) return "";
        if (password.Length == 0) return current;
        var key = "YuzuhaToolkit/Profile/" + Guid.NewGuid().ToString("N"); credentials.Add(key, password); return key;
    }
    private string Read(string key) { reads++; return credentials[key]; }
    private void Delete(string key) { deletes++; credentials.Remove(key); }
    private int Start(LaunchProfile profile, string password) { starts++; dispatchedPassword = password; return 4242; }
    private AvevaLaunchScript Script(Func<LaunchProfile, string, int>? start = null,
        Func<string, string, bool, string>? prepare = null, Action<string>? delete = null, RuntimeJournal? journal = null) =>
        new(Profiles, journal, start ?? Start, prepare ?? Prepare, Read, delete ?? Delete);
    private void NoSecretOnDisk(params string[] secrets)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            foreach (var secret in secrets) Assert.DoesNotContain(secret, File.ReadAllText(file));
    }

    [Fact]
    public void WorkspaceSettingsHasOnlyInstanceDataAndRoundTripsWithoutPersistenceBehavior()
    {
        var methods = typeof(WorkspaceSettings).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, m => !m.IsSpecialName);
        Assert.Empty(typeof(WorkspaceSettings).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        var data = JsonSerializer.Deserialize("{\"pluginName\":\"fixture\",\"future\":{\"x\":1}}", WorkspaceJsonContext.Default.WorkspaceSettings)!;
        var json = JsonSerializer.Serialize(data, WorkspaceJsonContext.Default.WorkspaceSettings);
        Assert.Contains("future", json); Assert.Equal("fixture", data.PluginName);
        Assert.False(Directory.Exists(Path.Combine(root, "state")));
    }

    [Fact]
    public void BlankPasswordAndDisablingRememberPreserveTheSavedCredentialWithoutMutatingDraft()
    {
        var script = Script(); var draft = Draft();
        var saved = script.SaveProfile(draft, "missing", "stored-secret", true);
        Assert.Empty(draft.CredentialKey); var key = Assert.Single(saved.Commit.Snapshot.Value.Profiles).CredentialKey;
        var edit = Profiles.ReadProfile(draft.Id).Profile; edit.RememberPassword = false;
        var next = script.SaveProfile(edit, Revision, "temporary-secret", true);
        Assert.Equal(key, Assert.Single(next.Commit.Snapshot.Value.Profiles).CredentialKey);
        Assert.Equal("stored-secret", credentials[key]); Assert.Equal(0, deletes); Assert.Equal(1, prepares);
        NoSecretOnDisk("stored-secret", "temporary-secret");
    }

    [Fact]
    public void DenialStaleRevisionAndInvalidProductModuleHaveNoCredentialOrProcessSideEffects()
    {
        var script = Script(); var draft = Draft();
        Assert.Throws<UnauthorizedAccessException>(() => script.SaveProfile(draft, "missing", "secret", false));
        var saved = Profiles.Save(draft, "missing", true);
        Assert.Throws<ConfigurationConflictException>(() => script.SaveProfile(draft, "missing", "secret", true));
        draft.Product = "AM"; draft.Kind = "MarineBatch";
        Assert.Throws<LaunchProfileValidationException>(() => script.SaveProfile(draft, saved.Snapshot.Revision, "secret", true));
        Assert.Throws<UnauthorizedAccessException>(() => script.StartSavedProfile("one", Revision, "secret", false, true));
        Assert.Throws<UnauthorizedAccessException>(() => script.StartSavedProfile("one", Revision, "secret", true, false));
        Assert.Equal((0, 0, 0, 0), (prepares, reads, deletes, starts));
    }

    [Fact]
    public void ConflictAfterCredentialPreparationCleansOnlyTheUncommittedNewKey()
    {
        Script().SaveProfile(Draft(), "missing", "old-secret", true);
        var key = Profiles.ReadProfile("one").Profile.CredentialKey; var before = Revision;
        var script = Script(prepare: (current, password, remember) =>
        {
            var next = Prepare(current, password, remember);
            var snapshot = Store.Read(); snapshot.Value.PluginName = "external-edit";
            Store.Commit(snapshot.Value, snapshot.Revision, new("并发编辑夹具")); return next;
        });
        Assert.Throws<ConfigurationConflictException>(() => script.SaveProfile(Draft(), before, "new-secret", true));
        Assert.Equal(key, Profiles.ReadProfile("one").Profile.CredentialKey);
        Assert.Equal("old-secret", Assert.Single(credentials).Value); Assert.Equal(1, deletes);
        NoSecretOnDisk("old-secret", "new-secret");
    }

    [Fact]
    public void StorageExceptionAfterReplacementDoesNotDeleteTheCredentialNowReferencedOnDisk()
    {
        var failingStore = new WorkspaceConfigurationStore(Path.Combine(root, "state"), phase =>
        { if (phase == "replaced") throw new IOException("simulated interruption after replacement"); });
        var script = new AvevaLaunchScript(new(failingStore), startProcess: Start,
            prepareCredential: Prepare, readCredential: Read, deleteCredential: Delete);
        Assert.Throws<IOException>(() => script.SaveProfile(Draft(), "missing", "recoverable-secret", true));
        var recovered = Profiles.ReadProfile("one");
        Assert.Equal("recoverable-secret", credentials[recovered.Profile.CredentialKey]); Assert.Equal(0, deletes);
        NoSecretOnDisk("recoverable-secret");
    }

    [Fact]
    public void ClearingAndDeletingProfilesPreserveSharedKeysUntilTheLastReferenceIsRemoved()
    {
        var script = Script(); script.SaveProfile(Draft(), "missing", "shared-secret", true);
        var first = Profiles.ReadProfile("one").Profile; var second = Draft("two"); second.CredentialKey = first.CredentialKey;
        Profiles.Save(second, Revision, true);
        var cleared = script.ClearSavedPassword("one", Revision, true);
        Assert.Empty(cleared.Commit.Snapshot.Value.Profiles.Single(p => p.Id == "one").CredentialKey);
        Assert.Equal(0, deletes); Assert.Single(credentials);
        script.DeleteProfile("two", Revision, true); Assert.Empty(credentials); Assert.Equal(1, deletes);
    }

    [Fact]
    public void CredentialCleanupFailureReturnsTheCommittedStateAndAWarning()
    {
        Script().SaveProfile(Draft(), "missing", "old-secret", true);
        var result = Script(delete: _ => throw new IOException("unavailable credential store")).ClearSavedPassword("one", Revision, true);
        Assert.Empty(Profiles.ReadProfile("one").Profile.CredentialKey); Assert.Single(result.Warnings);
        Assert.Equal(result.Commit.Snapshot.Revision, Revision);
    }

    [Fact]
    public void DesktopLaunchResolvesSavedPasswordOrExplicitOverrideAndRecordsTimeWithoutPersistingSecrets()
    {
        using var journal = new RuntimeJournal("launch-test", Path.Combine(root, "state"));
        var script = Script(journal: journal); script.SaveProfile(Draft(), "missing", "saved-secret", true);
        var first = script.StartSavedProfile("one", Revision, "", true, true);
        Assert.Equal("saved-secret", dispatchedPassword); Assert.Equal(1, reads); Assert.NotNull(first.Configuration);
        Assert.NotNull(Profiles.ReadProfile("one").Profile.LastStartedUtc); Assert.Empty(first.Warnings);
        var second = script.StartSavedProfile("one", Revision, "once-secret", true, true);
        Assert.Equal("once-secret", dispatchedPassword); Assert.Equal(1, reads); Assert.Equal(2, starts);
        Assert.Equal("entry-process-created-host-not-verified", second.State);
        NoSecretOnDisk("saved-secret", "once-secret");
    }

    [Fact]
    public void CliExplicitEmptyPasswordDoesNotReadSavedCredentialsOrWriteARevision()
    {
        var script = Script(); script.SaveProfile(Draft(), "missing", "stored-secret", true); var before = Revision;
        var result = script.StartWithPassword("one", before, "", true);
        Assert.Equal("", dispatchedPassword); Assert.Equal(0, reads); Assert.Null(result.Configuration);
        Assert.Equal(before, Revision); Assert.Null(Profiles.ReadProfile("one").Profile.LastStartedUtc);
    }

    [Fact]
    public void ProcessFailureIsAttemptedOnceLoggedWithoutSecretsAndDoesNotRecordStartTime()
    {
        using var journal = new RuntimeJournal("failure-test", Path.Combine(root, "state"));
        Profiles.Save(Draft(), "missing", true); var before = Revision;
        var script = Script(start: (_, _) => { starts++; throw new Win32Exception(5, "password-that-must-not-enter-log"); }, journal: journal);
        var failure = Assert.Throws<IOException>(() => script.StartWithPassword("one", before, "temporary-secret", true));
        Assert.Contains("系统错误 5", failure.Message); Assert.Equal(1, starts); Assert.Equal(before, Revision);
        NoSecretOnDisk("password-that-must-not-enter-log", "temporary-secret");
    }

    [Fact]
    public void RevisionConflictAfterProcessCreationReturnsItsPidAndWarningWithoutRetry()
    {
        Profiles.Save(Draft(), "missing", true);
        var script = Script(start: (_, _) =>
        {
            starts++; var current = Profiles.ReadProfile("one"); current.Profile.Name = "external-edit";
            Profiles.Save(current.Profile, current.Revision, true); return 8675;
        });
        var result = script.StartSavedProfile("one", Revision, "fixture", true, true);
        Assert.Equal(8675, result.ProcessId); Assert.Equal(1, starts); Assert.Null(result.Configuration);
        Assert.Single(result.Warnings); Assert.Null(Profiles.ReadProfile("one").Profile.LastStartedUtc);
    }

    [Fact]
    public void PreflightReturnsDirectoryWarningAndMaskedPreviewWithoutReadingCredentialsOrStarting()
    {
        Profiles.Save(Draft(), "missing", true); var snapshot = Store.Read();
        var directory = Path.Combine(root, "pmllib"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "one.pmlfnc"), "fixture"); File.WriteAllText(Path.Combine(directory, "two.pmlfnc"), "fixture");
        snapshot.Value.RegisteredPmlLib = directory; snapshot.Value.WarnFiles = 1;
        Store.Commit(snapshot.Value, snapshot.Revision, new("目录预算夹具"));
        var result = Script().InspectBeforeLaunch("one", Revision);
        Assert.True(result.PmlLibHealth!.NeedsAttention); Assert.Contains("本机凭据或本次输入", result.Preview);
        Assert.Equal((0, 0, 0), (prepares, reads, starts));
    }

    [Fact]
    public void DefaultBackendCreatesARealHarmlessBatchProcessWithoutOpeningWpfOrAveva()
    {
        Assert.True(OperatingSystem.IsWindows());
        var entry = Path.Combine(root, "marine.cmd"); File.WriteAllText(entry, "@echo off\r\n>\"%~dp0created.txt\" echo created\r\nexit /b 0\r\n");
        var profile = Draft(); profile.Product = "AM"; profile.Kind = "MarineBatch"; profile.Module = "Outfitting"; profile.Executable = entry;
        Profiles.Save(profile, "missing", true);
        var result = new AvevaLaunchScript(Profiles).StartWithPassword("one", Revision, "fixture", true);
        Assert.True(result.ProcessId > 0); Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "created.txt")), 5000));
        try { using var process = System.Diagnostics.Process.GetProcessById(result.ProcessId); Assert.True(process.WaitForExit(5000)); }
        catch (ArgumentException) { } // The harmless process may already have exited after writing its marker.
        Assert.Null(result.Configuration); Assert.Equal("entry-process-created-host-not-verified", result.State);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", previousState);
        Environment.SetEnvironmentVariable("YUZUHA_STARTUP_ACTIONS", previousActions);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
