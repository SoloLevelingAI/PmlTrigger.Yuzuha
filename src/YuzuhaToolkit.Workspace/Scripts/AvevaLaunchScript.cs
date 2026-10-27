using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>配置与凭据操作的已提交结果；清理失败作为警告，不能冒充保存失败或诱发重复提交。</summary>
public sealed record LaunchProfileActionResult(ConfigCommit<WorkspaceSettings> Commit, string[] Warnings);

/// <summary>启动前的无秘密数据；目录提醒由界面询问，脚本不弹窗或创建进程。</summary>
public sealed record LaunchPreflight(string ProfileId, string Revision, string Preview, DirectoryHealth? PmlLibHealth);

/// <summary>入口创建回执；可附最近启动时间的新提交，日志或时间记录失败不撤销已创建的进程。</summary>
public sealed record AvevaLaunchResult(int ProcessId, string ProfileId, string Revision, string State,
    ConfigCommit<WorkspaceSettings>? Configuration, string[] Warnings);

/// <summary>具体启动脚本：保存配置与凭据、检查目录、解析本次密码、创建一次进程并记录结果；不依赖 UI。</summary>
public sealed class AvevaLaunchScript
{
    private readonly LaunchProfileService profiles;
    private readonly RuntimeJournal? journal;
    private readonly Func<LaunchProfile, string, int>? startProcess;
    private readonly Func<string, string, bool, string> prepareCredential;
    private readonly Func<string, string> readCredential;
    private readonly Action<string> deleteCredential;

    /// <summary>复用配置服务与日志；仅在 Windows 凭据及进程边界提供委托，供无界面测试替换副作用。</summary>
    public AvevaLaunchScript(LaunchProfileService? profiles = null, RuntimeJournal? journal = null,
        Func<LaunchProfile, string, int>? startProcess = null,
        Func<string, string, bool, string>? prepareCredential = null,
        Func<string, string>? readCredential = null, Action<string>? deleteCredential = null)
    {
        this.profiles = profiles ?? new(); this.journal = journal; this.startProcess = startProcess;
        this.prepareCredential = prepareCredential ?? WindowsCredentialStore.Prepare;
        this.readCredential = readCredential ?? WindowsCredentialStore.Read;
        this.deleteCredential = deleteCredential ?? WindowsCredentialStore.Delete;
    }

    /// <summary>先检查授权、修订及字段，再保存凭据与配置；空密码沿用，取消记住不删除旧凭据。</summary>
    public LaunchProfileActionResult SaveProfile(LaunchProfile draft, string expectedRevision,
        string enteredPassword, bool allowWrite)
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision);
        var profile = Copy(draft);
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name == "新启动配置") profile.Name = LaunchDrafts.SuggestName(profile);
        var oldKey = current.Value.Profiles.SingleOrDefault(p => p.Id == profile.Id)?.CredentialKey ?? "";
        profile.CredentialKey = oldKey; // References belong to saved state, never to an untrusted UI/JSON draft.
        var preview = profiles.PreviewSave(profile, expectedRevision);
        if (!preview.Validation.IsValid) throw new LaunchProfileValidationException(preview.Validation);
        profile.CredentialKey = !profile.RememberPassword && oldKey.Length > 0 ? oldKey
            : prepareCredential(oldKey, enteredPassword, profile.RememberPassword);
        ConfigCommit<WorkspaceSettings> commit;
        try { commit = profiles.Save(profile, expectedRevision, true); }
        catch
        {
            // A storage exception may occur after replacement. Never delete a key still referenced on disk.
            if (profile.CredentialKey != oldKey) RemoveUnusedCredential(profile.CredentialKey, []);
            throw;
        }
        var warnings = new List<string>();
        if (oldKey != profile.CredentialKey) RemoveUnusedCredential(oldKey, warnings);
        return new(commit, warnings.ToArray());
    }

    /// <summary>清除指定配置的凭据关联；提交成功后仅删除已无其他配置引用的凭据。</summary>
    public LaunchProfileActionResult ClearSavedPassword(string profileId, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision);
        var profile = Copy(Find(current.Value, profileId)); var oldKey = profile.CredentialKey;
        profile.CredentialKey = ""; profile.RememberPassword = false;
        var commit = profiles.Save(profile, expectedRevision, true, "清除配置凭据关联");
        var warnings = new List<string>(); RemoveUnusedCredential(oldKey, warnings);
        return new(commit, warnings.ToArray());
    }

    /// <summary>删除配置并保留历史；提交后清理独占凭据，不操作工程文件或已运行进程。</summary>
    public LaunchProfileActionResult DeleteProfile(string profileId, string expectedRevision, bool allowWrite)
    {
        DemandWrite(allowWrite);
        var current = Current(expectedRevision); var oldKey = Find(current.Value, profileId).CredentialKey;
        var commit = profiles.Delete(profileId, expectedRevision, true);
        var warnings = new List<string>(); RemoveUnusedCredential(oldKey, warnings);
        return new(commit, warnings.ToArray());
    }

    /// <summary>核验保存的配置并返回脱敏预览和目录预算提醒；不读取密码、不启动。</summary>
    public LaunchPreflight InspectBeforeLaunch(string profileId, string expectedRevision)
    {
        var current = Current(expectedRevision); var profile = Find(current.Value, profileId); RequireValid(profile);
        var health = string.IsNullOrWhiteSpace(current.Value.RegisteredPmlLib) ? null
            : DirectoryInspector.Scan(current.Value.RegisteredPmlLib, current.Value.WarnFiles, current.Value.WarnBytes);
        return new(profileId, current.Revision, LaunchService.Preview(profile), health);
    }

    /// <summary>桌面启动：空输入由后端解析已存凭据；创建后尝试记录最近时间，失败只返回警告。</summary>
    public AvevaLaunchResult StartSavedProfile(string profileId, string expectedRevision,
        string enteredPassword, bool allowLaunch, bool allowWrite) =>
        Start(profileId, expectedRevision, enteredPassword, allowLaunch, allowWrite, useSavedCredential: true, recordStarted: true);

    /// <summary>CLI 启动：只使用调用者本次密码，包括明确的空值；不写配置或读取已存凭据。</summary>
    public AvevaLaunchResult StartWithPassword(string profileId, string expectedRevision,
        string password, bool allowLaunch) =>
        Start(profileId, expectedRevision, password, allowLaunch, false, useSavedCredential: false, recordStarted: false);

    private AvevaLaunchResult Start(string profileId, string expectedRevision, string enteredPassword,
        bool allowLaunch, bool allowWrite, bool useSavedCredential, bool recordStarted)
    {
        if (!allowLaunch) throw new UnauthorizedAccessException("启动进程需要显式许可。");
        if (recordStarted) DemandWrite(allowWrite);
        var current = Current(expectedRevision); var profile = Copy(Find(current.Value, profileId)); RequireValid(profile);
        var password = useSavedCredential && enteredPassword.Length == 0 && profile.CredentialKey.Length > 0
            ? readCredential(profile.CredentialKey) : enteredPassword;
        var start = LaunchService.Build(profile, password);
        var warnings = new List<string>(); string? callId = null;
        TryLog(() => callId = journal?.Begin("launch_aveva", JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["profile"] = profile.Name, ["executable"] = profile.Executable,
            ["startupPolicy"] = start.Environment.TryGetValue(StartupPolicy.Variable, out var policy) ? policy ?? "none" : "none",
            ["startupId"] = start.Environment.TryGetValue("YUZUHA_STARTUP_ID", out var startupId) ? startupId ?? "" : "",
            ["startupLog"] = start.Environment.TryGetValue("YUZUHA_STARTUP_LOG", out var startupLog) ? startupLog ?? "" : "",
            ["phase"] = "requested-before-process-start"
        }, WorkspaceJsonContext.Default.DictionaryStringString)), warnings);
        int pid;
        try
        {
            if (startProcess is not null) pid = startProcess(profile, password);
            else { using var process = Process.Start(start) ?? throw new IOException("未创建启动入口进程。"); pid = process.Id; }
            if (pid <= 0) throw new IOException("启动入口返回了无效 PID。");
        }
        catch (Exception error)
        {
            var code = error is Win32Exception win32 ? "；系统错误 " + win32.NativeErrorCode : "";
            var message = "启动入口创建失败：" + error.GetType().Name + code;
            TryLog(() => { if (callId is not null) journal?.Complete(callId, "failed", message); }, warnings);
            throw new IOException(message, error);
        }
        TryLog(() => { if (callId is not null) journal?.Complete(callId, "succeeded", "启动入口进程 PID " + pid); }, warnings);
        ConfigCommit<WorkspaceSettings>? commit = null;
        if (recordStarted)
        {
            profile.LastStartedUtc = DateTimeOffset.UtcNow;
            try { commit = profiles.Save(profile, current.Revision, true, "记录启动入口创建时间"); }
            catch (Exception error) { warnings.Add("启动入口已创建；最近启动时间未保存：" + error.Message); }
        }
        return new(pid, profileId, commit?.Snapshot.Revision ?? current.Revision,
            "entry-process-created-host-not-verified", commit, warnings.ToArray());
    }

    private ConfigSnapshot<WorkspaceSettings> Current(string expectedRevision)
    {
        var current = profiles.Read();
        if (current.Revision != expectedRevision) throw new ConfigurationConflictException(current.Revision);
        return current;
    }
    private void RequireValid(LaunchProfile profile)
    {
        var validation = profiles.Validate(profile);
        if (!validation.IsValid) throw new LaunchProfileValidationException(validation);
    }
    private void RemoveUnusedCredential(string key, List<string> warnings)
    {
        if (key.Length == 0) return;
        try { if (!profiles.Read().Value.Profiles.Any(p => p.CredentialKey == key)) deleteCredential(key); }
        catch (Exception error) { warnings.Add("配置状态已保留；未完成旧凭据清理：" + error.GetType().Name); }
    }
    private static void TryLog(Action action, List<string> warnings)
    {
        try { action(); } catch (Exception error) { warnings.Add("启动日志写入失败：" + error.GetType().Name); }
    }
    private static void DemandWrite(bool allowWrite)
    {
        if (!allowWrite) throw new UnauthorizedAccessException("修改配置或凭据需要显式写入许可。");
    }
    private static LaunchProfile Find(WorkspaceSettings settings, string id) => settings.Profiles.SingleOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException("未找到指定启动配置。");
    private static LaunchProfile Copy(LaunchProfile profile) => JsonSerializer.Deserialize(
        JsonSerializer.SerializeToUtf8Bytes(profile, LaunchLifecycleJsonContext.Default.LaunchProfile), LaunchLifecycleJsonContext.Default.LaunchProfile)!;
}
