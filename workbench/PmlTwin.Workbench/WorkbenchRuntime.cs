using System.ComponentModel;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using Core30.Backend;
using Core30.Core;
using Core30.LaunchDesk;
using Core30.PmlTwin;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private readonly CoreHost _backend = new("PmlTwin",
    [
        new("pml", "PmlTwin", "PML 与会话", PmlTwinFeatures.All),
        new("launcher", "启动器", "运行环境", LaunchDeskFeatures.All)
    ]);
    private LaunchDeskWorkflow LaunchBackend => new(_backend, _store.DirectoryPath);
    private LaunchInventory? _inventory;
    private string _profileRevision = "missing";
    private bool _scanning;
    private bool _refreshingSessions;
    private bool _launching;
    private int _pendingSessions;
    private IReadOnlyList<PmlSessionProcess> _pendingHostProcesses = [];
    private bool _sessionCheckFailed;
    private string _sessionMessage = "正在读取本机 AVEVA 会话。";
    private string _installationMessage = "正在识别本机 AVEVA 安装。";
    private string _launchMessage = "";
    private PmlLiveSession? _session;
    private IReadOnlyList<PmlLiveSession> _sessions = [];
    private IReadOnlyList<LaunchInstallation> Installations => _inventory?.Installations ?? [];
    private LaunchInstallation? Install => Installations.FirstOrDefault(item =>
        item.Directory.Equals(_install, StringComparison.OrdinalIgnoreCase)) ?? Installations.FirstOrDefault();
    private PmlLiveSession? InstallSession => Install is { } installation
        ? _session is { } selected && InInstallation(selected, installation.Directory) ? selected :
            _sessions.FirstOrDefault(row => InInstallation(row, installation.Directory))
        : null;

    private static bool InInstallation(PmlLiveSession session, string directory) => InInstallation(session.ExecutablePath, directory);

    private static bool InInstallation(string executable, string directory) =>
        executable.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private PmlWorkbenchHost LiveHost() => new(_store.DirectoryPath, receipt =>
    {
        if (receipt.Success && receipt.Operation is ("aveva.sessions.list" or "aveva.session.status")) return;
        _ = Dispatcher.InvokeAsync(() => RecordLog(receipt.Operation,
            receipt.Success ? "成功" : receipt.MayHaveExecuted ? "待核对" : "失败",
            receipt.ExitCode, receipt.Output, receipt.Error));
    });

    private CoreData Run(CoreRequest request) => _backend.Run(_store.DirectoryPath, request).Result;

    private static string Value(CoreData? data, string field) => data?.Field(field)?.Value ?? "";

    private static bool WorkbenchError(Exception error) =>
        CoreRunner.IsOperationalError(error) || error is Win32Exception or SecurityException;

    private async Task ScanInstallations(string? customDirectory = null)
    {
        if (_scanning) return;
        _scanning = true;
        _installationMessage = "正在识别本机 AVEVA 安装…";
        Render();
        try
        {
            Directory.CreateDirectory(_store.DirectoryPath);
            var directory = customDirectory ?? (Data.ExtraInstallDirectory.Length > 0 ? Data.ExtraInstallDirectory : null);
            var workflow = LaunchBackend;
            var scan = await Task.Run(() => workflow.Scan(directory));
            if (customDirectory is not null && scan.Installations.Count == 0)
                throw new InvalidDataException("该目录没有可识别的 AVEVA 安装，请选择软件根目录。");
            _inventory = scan;
            if (customDirectory is not null)
            {
                Data.ExtraInstallDirectory = customDirectory;
                Save();
            }
            _latestDiscovery = DateTime.Now.ToString("HH:mm:ss");
            _installationMessage = scan.Installations.Count == 0 ? "未发现 AVEVA 安装，可手动选择软件目录。" : "";
            await ReadProfiles();
            if (_install.Length == 0 || customDirectory is not null)
            {
                var preferred = Data.Recent.Select(id => Data.Profiles.FirstOrDefault(row => row.Id == id)).OfType<LoginProfile>()
                    .Concat(Data.Profiles).Select(ProfileInstallation).OfType<LaunchInstallation>().FirstOrDefault();
                _install = (customDirectory is null ? preferred : null)?.Directory ?? Install?.Directory ?? "";
            }
        }
        catch (Exception error) when (WorkbenchError(error)) { _installationMessage = error.Message; }
        finally
        {
            _scanning = false;
            Render();
        }
    }

    private async Task ReadProfiles()
    {
        var receipt = await Task.Run(() => LaunchBackend.Profiles());
        _profileRevision = Value(receipt, "revision");
        Data.Profiles = receipt.Children("profile").Select(row => new LoginProfile(
            Value(row, "id"), Value(row, "name"), Value(row, "installation"), Value(row, "project"),
            Value(row, "module"), Value(row, "user"), Value(row, "mdb"), Value(row, "product"))).ToList();
        Data.Recent.RemoveAll(id => Data.Profiles.All(row => row.Id != id));
    }

    private LaunchInstallation? ProfileInstallation(LoginProfile profile) => Installations.FirstOrDefault(item =>
        item.Product == profile.Product && item.Directory.Equals(profile.Install, StringComparison.OrdinalIgnoreCase));

    private async Task RefreshSessions(bool redraw = true)
    {
        if (_refreshingSessions) return;
        _refreshingSessions = true;
        try
        {
            Directory.CreateDirectory(_store.DirectoryPath);
            var status = await Task.Run(() => LiveHost().DiscoverSessions());
            var selected = _session;
            _sessions = status.Verified;
            _session = selected is null ? null : _sessions.FirstOrDefault(row =>
                row.Pid == selected.Pid && row.StartTicks == selected.StartTicks && row.Model == selected.Model);
            if (_session is null && _sessions.Count == 1) _session = _sessions[0];
            _pendingSessions = status.PendingHostCount;
            _pendingHostProcesses = status.Pending;
            _sessionCheckFailed = false;
            _sessionMessage = _sessions.Count != 0 ? $"已连接 {_sessions.Count} 个会话。" :
                _pendingSessions != 0 ? $"找到 {_pendingSessions} 个 AVEVA 进程，CLI 暂未连接。" : "当前没有正在运行的 AVEVA 会话。";
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            _sessions = [];
            _session = null;
            _pendingSessions = 0;
            _pendingHostProcesses = [];
            _sessionCheckFailed = true;
            _sessionMessage = error.Message;
        }
        finally
        {
            _refreshingSessions = false;
            if (redraw) Render();
        }
    }

    private static Button AsyncBtn(string label, string id, Func<Task> action, string style = "Button.Base")
    {
        var button = Btn(label, id, () => { }, style);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }
}
