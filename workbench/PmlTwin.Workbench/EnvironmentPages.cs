using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Core30.Backend;
using Core30.Core;
using Core30.Generated;
using Core30.PmlTwin;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private sealed record EvarsConfiguration(string FilePath = "", PmlHostConfiguration? Host = null, bool NeedsReview = false)
    {
        public bool IsConfigured => Host is { DllPath.Length: > 0 } && !NeedsReview;
        public string Status => FilePath.Length == 0 ? "未找到" : IsConfigured ? "已配置" : NeedsReview ? "待核验" : "待配置";
        public string Profile => Host is { Profile.Length: > 0 } ? $"{Host.Profile} / {Host.Framework}" : "";
    }
    private string _evarsOverride = "";
    private string _evarsInstallation = "";
    private static string SetupPath => Path.Combine(AppContext.BaseDirectory, "phayton", "setup", $"Phayton-{PhaytonProgramVersion}-Windows-Setup.exe");

    private string EvarsFileFor(LaunchInstallation installation)
    {
        var names = installation.Product == "E3D"
            ? new[] { "evars.init", "evar.init" }
            : new[] { "evars.bat", "evar.bat", "evars.init", "evar.init" };
        var remembered = _store.Pml.EvarsFiles.GetValueOrDefault(installation.Directory.ToUpperInvariant());
        var custom = _evarsInstallation == installation.Directory ? _evarsOverride : remembered;
        return File.Exists(custom) ? custom! : names.Select(name => Path.Combine(installation.Directory, name)).FirstOrDefault(File.Exists) ?? "";
    }

    private EvarsConfiguration ReadEvarsConfiguration()
    {
        if (Install is not { } installation) return new();
        var path = EvarsFileFor(installation);
        if (path.Length == 0) return new();

        try
        {
            var host = PmlHostConfiguration.Inspect(path, installation.Product, installation.Release);
            var session = InstallSession;
            var needsReview = host.Profile.Length == 0 ||
                session is not null && (host.DllPath.Length == 0 ||
                    session.LoadedHostDll.Length > 0 && !host.DllPath.Equals(session.LoadedHostDll, StringComparison.OrdinalIgnoreCase));
            return new(path, host, needsReview);
        }
        catch (Exception error) when (WorkbenchError(error)) { return new(path, NeedsReview: true); }
    }

    private FrameworkElement EvarsPage()
    {
        var config = ReadEvarsConfiguration();
        var session = InstallSession;
        var activate = AsyncBtn("部署并接入所有版本…", "SwitchPhaytonResources", DeployPhaytonResources, "Button.Primary");
        activate.IsEnabled = !_scanning;
        var body = Stack(Details([
            ("EVARS 文件", config.FilePath.Length == 0 ? "待选择" : config.FilePath),
            ("Host 配置", config.Profile.Length == 0 ? "待核验" : config.Profile),
            ("配置指向的 DLL", config.Host is { DllPath.Length: > 0 } host ? host.DllPath : "待核验"),
            ("配置状态", config.Status)
        ]));
        if (config.NeedsReview)
            body.Children.Add(Below(Muted("请核对 EVARS 中的 PMLLIB、界面目录和当前会话加载的 Host。"), 8));
        var actions = Actions(Btn("选择 EVARS…", "BrowseEvars", ChooseEvars), Jump("部署 PML 资源", "Folders"), activate);
        if (config.FilePath.Length > 0)
        {
            actions.Children.Add(AsyncBtn("解除 Phayton 接入…", "DetachPhayton", DetachPhayton));
            actions.Children.Add(AsyncBtn("追加其他目录…", "RegisterLocalPmlPaths", RegisterLocalPmlPaths));
            actions.Children.Add(Btn("查看当前文件", "InspectEvars", () =>
            {
                try
                {
                    if (new FileInfo(config.FilePath).Length > 1024 * 1024) throw new InvalidDataException("文件超过预览上限。");
                    Modal("当前 EVARS", Code(File.ReadAllText(config.FilePath), "EvarsContent", 300), "完成", () => true, cancel: false, width: 730);
                }
                catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
            }));
        }
        body.Children.Add(Below(actions, 8));
        body.Children.Add(Below(PmlScanNotice("EvarsPmlScanNotice"), 10));
        var addPml = AsyncBtn("添加 PML 目录…", "AddPmlScanFolder", AddPmlFolder);
        addPml.IsEnabled = !_pmlScanning;
        body.Children.Add(Below(Actions(addPml, RefreshPmlButton("RefreshEvarsPml")), 7));
        var installation = Card("本机安装", Stack(InstallationPicker(),
            Below(Btn("选择软件目录…", "ChooseInstallation", () => _ = ChooseInstallation()), 8)));
        var loaded = session is null ? "等待会话" : session.LoadedHostDll.Length == 0 ? "路径待核验" : session.LoadedHostDll;
        var live = Card("实际会话", Details([
            ("会话", session?.ToString() ?? "等待连接"),
            ("Host 版本", session?.HostVersion ?? "待核验"),
            ("已加载 DLL", loaded)
        ]));
        return SideBySide(Card("当前配置", body), Stack(installation, Below(live, 8)));
    }

    private async Task DetachPhayton()
    {
        if (_editingPmlResources || ReadEvarsConfiguration() is not { FilePath.Length: > 0 } config) return;
        _editingPmlResources = true;
        try
        {
            var change = await Task.Run(() => PmlResourceSwitch.PreviewRemoval(config.FilePath));
            if (!change.Changed) { Tell("当前 EVARS 没有由 Phayton 托管的接入配置。"); return; }
            if (!Modal("预览解除 Phayton 接入", Stack(Muted(change.Path), Below(EvarsDiff(change.Lines), 10),
                    Below(Muted("将备份当前版本的 EVARS 并移除下列托管行。其他版本可继续使用共用资源。"), 10)),
                "解除接入并备份", () => true, width: 820)) return;

            var backup = await Task.Run(change.Apply);
            RecordLog("解除 Phayton 接入", "成功", 0, $"EVARS · {change.Path}\n原文件备份 · {backup}", "");
            Render();
            Modal("已解除 Phayton 接入", Stack(Details([("当前文件", change.Path), ("原文件备份", backup)]),
                Below(Muted("重新启动 AVEVA 后生效。"), 8)), "完成", () => true, width: 720, cancel: false);
            _ = ScanPmlFolders();
        }
        catch (UnauthorizedAccessException) { Tell("没有写入 EVARS 或保存备份的权限。请使用有写入权限的 Windows 账户打开 PmlTwin。"); }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        finally { _editingPmlResources = false; }
    }

    private async Task RegisterLocalPmlPaths()
    {
        if (Install is not { } installation || ReadEvarsConfiguration() is not { FilePath.Length: > 0 } config) return;
        var library = Input("", "RegisterLocalPmlLib");
        var interfaceFiles = Input("", "RegisterLocalPmlUi");
        var uiName = installation.Product == "E3D" ? "PMLUI" : "PDMSUI";
        var notice = new Notice("RegisterLocalPmlPathsNotice");
        FrameworkElement ChooseFolder(string label, TextBox field, string id)
        {
            var browse = Btn("选择目录…", id, () =>
            {
                var folder = new OpenFolderDialog { Title = $"选择 {label} 本机目录" };
                if (folder.ShowDialog(this) == true) field.Text = folder.FolderName;
            });
            browse.VerticalAlignment = VerticalAlignment.Bottom;
            return Leading(browse, Field(label, field), Dock.Right);
        }
        string? lib = null, ui = null;
        var reviewDirectory = Btn("使用只读函数目录", "UseReviewedFunctionDirectory", () =>
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YuzuhaToolkit", "ReviewedFunctions", "PMLLIB");
                var folder = new FeatureContext(Directory.CreateDirectory(path).FullName);
                File.WriteAllText(folder.FilePath(".pmltwin-reviewed-library"), "PmlTwin");
                library.Text = folder.Root;
            }
            catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "warning"); }
        });
        if (!Modal("注册本机 PML 目录", Stack(Muted(config.FilePath), Below(ChooseFolder("PMLLIB", library, "BrowseLocalPmlLib"), 8),
                Below(reviewDirectory, 5), Below(ChooseFolder(uiName, interfaceFiles, "BrowseLocalPmlUi"), 8), Below(notice, 8)),
            "预览 EVARS", () =>
            {
                lib = library.Text.Trim() is { Length: > 0 } libPath ? libPath : null;
                ui = interfaceFiles.Text.Trim() is { Length: > 0 } uiPath ? uiPath : null;
                if (lib is null && ui is null || new[] { lib, ui }.OfType<string>().Any(path =>
                    !Path.IsPathFullyQualified(path) || !Directory.Exists(path)))
                {
                    notice.Show("请填写至少一个实际存在的本机目录。", "warning");
                    return false;
                }
                return true;
            }, library, 735))
            return;
        try
        {
            var plan = await Task.Run(() => Run(LocalEvarsRequests.Preview(config.FilePath, installation.Product, lib, ui)));
            if ((bool?)plan.Field("changed") != true) { Tell("所选目录已经在 EVARS 中。"); return; }
            var updates = string.Join("\n", plan.Children("append").Select(item => item.Value));
            if (!Modal("预览 EVARS 追加内容", Stack(Muted(config.FilePath), Below(Code(updates, "LocalEvarsPreview", 110), 8)),
                "写入并备份", () => true, width: 745)) return;
            var applied = await Task.Run(() => Run(LocalEvarsRequests.Apply(config.FilePath, installation.Product, lib, ui,
                Value(plan, "beforeSha256"), Value(plan, "proposedSha256"))));
            var backup = Value(applied, "backup");
            if (backup.Length == 0) throw new InvalidDataException("后端没有返回 EVARS 备份回执。");
            Render();
            Modal("已注册本机目录", Stack(Views.Text("重新启动 AVEVA 后加载这些目录。"), Below(Muted($"原文件备份 · {backup}"), 8)),
                "完成", () => true, cancel: false, width: 740);
            await ScanPmlFolders();
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private FrameworkElement InstallationPicker()
    {
        var picker = Choice(Installations, Install, "EnvironmentInstall");
        AutomationName(picker, "检查的 AVEVA 安装");
        picker.IsEnabled = Installations.Count > 0 && !_scanning;
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is not Core30.Backend.LaunchInstallation selected) return;
            _install = selected.Directory;
            Render();
        };
        return picker;
    }

    private void ChooseEvars()
    {
        if (Install is not { } installation) { Tell("请先选择软件安装。"); return; }
        var dialog = new OpenFileDialog
        {
            Title = "选择当前安装使用的 EVARS", InitialDirectory = installation.Directory,
            Filter = "AVEVA EVARS|*.bat;*.init", CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        if (Path.GetFileNameWithoutExtension(dialog.FileName).ToLowerInvariant() is not ("evars" or "evar"))
        {
            Tell("请选择名为 EVARS 或 EVAR 的启动环境文件。");
            return;
        }
        _evarsOverride = dialog.FileName;
        _evarsInstallation = installation.Directory;
        try { _store.UpdatePmlPreferences(settings => settings.EvarsFiles[installation.Directory.ToUpperInvariant()] = dialog.FileName); }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        Render();
        _ = ScanPmlFolders();
    }

    private async Task ChooseInstallation()
    {
        var dialog = new OpenFolderDialog { Title = "选择 AVEVA 软件根目录" };
        if (dialog.ShowDialog(this) == true) await ScanInstallations(dialog.FolderName);
    }

    private FrameworkElement SessionSelector()
    {
        var refresh = AsyncBtn("刷新会话", "RefreshSessions", () => RefreshSessions());
        refresh.IsEnabled = !_refreshingSessions;
        var actions = Actions(refresh, Jump("打开登录", "Launch"));
        if (_sessions.Count == 0)
            return Stack(Muted(SessionStatusMessage), Below(actions, 8));

        var picker = Choice(_sessions, _session, "AvevaSessions");
        AutomationName(picker, "已核验的会话");
        picker.SelectionChanged += (_, _) =>
        {
            _session = picker.SelectedItem as PmlLiveSession;
            Render();
        };
        return Stack(Field("目标会话", picker), Below(Muted(SessionStatusMessage), 5), Below(actions, 7));
    }

    private Button PackageSetupButton(string label, string id)
    {
        return Btn(label, id, () =>
        {
            try
            {
                var path = SetupPath;
                if (!File.Exists(path))
                {
                    var picker = new OpenFileDialog { Title = "选择 Phayton 安装包", Filter = "Phayton 安装包|Phayton-*-Windows-Setup.exe" };
                    if (picker.ShowDialog(this) != true) return;
                    path = picker.FileName;
                }
                using var process = Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
                if (RegisteredPhaytonCli.FindInstalledWorkbenchRoot() is not null) Close();
            }
            catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        }, "Button.Primary");
    }

    private string CliConnectionState(PmlLiveSession? session) => session is not null ? "已连接" :
        _refreshingSessions ? "检测中" : _sessionCheckFailed ? "检查失败" :
        _sessions.Count > 0 ? "待选择" : _pendingSessions > 0 ? "Host 未连接" : "无运行会话";

    private FrameworkElement CliPage()
    {
        var programs = RegisteredPhaytonCli.FindWorkbenchPrograms();
        var session = _session;
        return SideBySide(Card("本机 AVEVA 会话", SessionSelector()),
            Card("CLI 状态", Stack(Details([
                ("连接", CliConnectionState(session)), ("CLI 路径", programs.Length == 1 ? programs[0] : "待检查"),
                ("实际模块", session?.Model ?? "待连接"),
                ("执行模式", session is not null && !session.EnforcesReadOnly ? "当前会话的只读能力待核对" :
                    _readOnly ? "当前用户已开启只读" : "执行需授权")
            ]), Below(Actions(Btn("Agent 接入…", "OpenAgentConnection", OpenAgentConnection),
                Jump("宏录制", "Record"), Jump("执行日志", "Logs")), 8))));
    }

    private void CopyText(string text)
    {
        try { Clipboard.SetText(text); Tell("已复制。"); }
        catch (System.Runtime.InteropServices.ExternalException) { Tell("无法写入剪贴板，请重试。"); }
    }
}
