using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Core30.Backend;
using Core30.Core;
using Core30.PmlTwin;
using Core30.Ui;
using Core30.Workbench;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private string _profileId = "";
    private string _quickQuery = "";
    private string _quickProject = "全部项目";
    private string _quickInstall = "全部安装";
    private string _quickModule = "全部模块";
    private string _profileSearch = "";

    private FrameworkElement HomePage()
    {
        var sag = Card("SAG 知识库", Muted($"{_store.Sag.Sources.Count} 个索引目录 · {Data.Documents.Count} 份资料"),
            Actions(Jump("搜索正文", "Sag"), Jump("部署资料", "Docs"), Jump("检查宏", "CheckMacro")));
        var logs = Data.Logs.Take(2).Select(item =>
        {
            var label = $"{item.Time}   {item.Command}   {item.State}";
            var link = Btn(label, "HomeLog" + item.Id, () => ShowLog(item.Id), "Button.Quiet");
            var summary = Views.Text(label, "Text.Meta");
            summary.Foreground = Brush("primary");
            summary.TextWrapping = TextWrapping.NoWrap;
            summary.TextTrimming = TextTrimming.CharacterEllipsis;
            link.Content = summary;
            link.ToolTip = label;
            link.HorizontalContentAlignment = HorizontalAlignment.Left;
            link.MinHeight = 23;
            link.Padding = new Thickness(4, 1, 4, 1);
            return (FrameworkElement)link;
        }).ToArray();
        var recent = Card("执行日志", logs.Length > 0 ? Stack(logs) : Muted("执行 PML 函数后，回执会显示在这里。"), Jump("查看全部", "Logs"));
        return Stack(SideBySide(StatusCard(), QuickLogin(), 1.24, 665, 12),
            Below(SideBySide(sag, recent, 1.24, 665, 12), 10));
    }

    private FrameworkElement StatusCard()
    {
        var current = InstallSession;
        var evars = ReadEvarsConfiguration();
        var installRow = StatusRow("Grid", "AVEVA 安装", Install?.Directory ?? _installationMessage,
            Install is not null ? "已发现" : _scanning ? "检测中" : "未发现", Install is not null ? "success" : "warning",
            Btn("安装列表", "DetectedInstallations", ShowInstallations, "Button.Quiet"));
        var evarsDetail = evars.FilePath.Length == 0 ? "从 AVEVA 软件目录读取" : Path.GetFileName(evars.FilePath);
        if (evars.IsConfigured) evarsDetail += $" · {evars.Profile}";
        var ev = StatusRow("Report", "EVARS", evarsDetail,
            evars.Status, evars.IsConfigured ? "success" : "warning", Jump(evars.IsConfigured ? "查看配置" : "检查 EVARS", "Ev"));
        var connection = CliConnectionState(current);
        var cli = StatusRow("Terminal", "AVEVA 会话", current?.ToString() ?? SessionStatusMessage, connection,
            current is not null ? "success" : _sessionCheckFailed || _pendingSessions > 0 ? "warning" : "primary", Jump("查看状态", "Cli"));
        var content = Stack(InstallationPicker(), Below(installRow, 5), ev, cli);
        if (_latestDiscovery.Length > 0) content.Children.Add(Muted("检测于 " + _latestDiscovery));
        return Card("状态检查", content, AsyncBtn("重新检测", "Discover",
            () => Task.WhenAll(ScanInstallations(), RefreshSessions()), "Button.Toolbar"));
    }

    private static FrameworkElement StatusRow(string glyph, string title, string detail, string status, string tone, FrameworkElement? action)
    {
        var mark = Glyph(glyph, 18);
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.Margin = new Thickness(0, 0, 9, 0);
        var name = Views.Text(title);
        name.FontWeight = FontWeights.SemiBold;
        var caption = Stack(name, Muted(detail));
        caption.VerticalAlignment = VerticalAlignment.Center;
        var badge = Badge(status, tone);
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.Margin = new Thickness(5, 0, 3, 0);
        var row = new DockPanel();
        if (action is not null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(action, Dock.Right);
            row.Children.Add(action);
        }
        DockPanel.SetDock(badge, Dock.Right);
        row.Children.Add(badge);
        row.Children.Add(mark);
        row.Children.Add(caption);
        return new Border { MinHeight = 43, Padding = new Thickness(0, 2, 0, 2), Child = row, BorderBrush = Brush("border"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private static void AutomationName(FrameworkElement item, string caption) =>
        System.Windows.Automation.AutomationProperties.SetName(item, caption);

    private void ShowInstallations()
    {
        var rows = Installations.Select(item => new { item.Name, item.Directory, Status = item.CanLaunch ? "支持启动" : "安装已发现" });
        if (Modal("本机 AVEVA 安装", Stack(Muted(_installationMessage),
            Table(rows, "InstallationTable", ("AVEVA", "Name", 1.35), ("安装位置", "Directory", 1.45), ("状态", "Status", .65))),
            "选择软件目录…", () => true, width: 710)) _ = ChooseInstallation();
    }

    private FrameworkElement QuickLogin()
    {
        if (Data.Profiles.Count == 0)
            return Card("快捷登录", Stack(Muted("保存一份工程配置后可从这里登录。"),
                Below(Actions(AsyncBtn("新建配置", "QuickNewProfile", () => EditProfile(), "Button.Primary")), 8)));

        var search = Input(_quickQuery, "QuickSearch", "搜索登录配置");
        var projects = Data.Profiles.Select(profile => profile.Project).Distinct().Order();
        var project = Choice(new[] { "全部项目" }.Concat(projects), _quickProject, "QuickProject");
        project.Width = 98;
        project.Margin = new Thickness(0, 0, 7, 0);
        AutomationName(project, "按项目筛选");
        var filter = Btn("筛选", "QuickFilter", OpenQuickFilter, "Button.Toolbar");
        var bar = Leading(filter, Leading(project, Prompt(search, "搜索登录配置"), Dock.Right), Dock.Right);
        var list = Picker("最近登录", "QuickProfiles");
        var caption = Muted("最近登录");
        var launch = AsyncBtn("一键登录", "QuickLaunch", async () =>
        {
            if ((list.SelectedItem as ListBoxItem)?.Tag is LoginProfile profile) await Launch(profile);
        }, "Button.Primary");
        var edit = AsyncBtn("编辑当前配置", "QuickEditProfile", async () =>
        {
            if ((list.SelectedItem as ListBoxItem)?.Tag is LoginProfile profile) await EditProfile(profile);
        });
        list.SelectionChanged += (_, _) =>
        {
            var selected = (list.SelectedItem as ListBoxItem)?.Tag as LoginProfile;
            launch.Content = selected is null ? "一键登录" : _launching ? "正在登录…" : "一键登录 " + selected.Module;
            launch.IsEnabled = selected is not null && !_launching;
            edit.IsEnabled = selected is not null;
            if (selected is not null) _profileId = selected.Id;
        };
        void ReloadList()
        {
            var filtering = _quickQuery.Trim().Length > 0 || _quickProject != "全部项目" ||
                _quickInstall != "全部安装" || _quickModule != "全部模块";
            var matches = filtering
                ? Data.Profiles.Where(profile =>
                    (_quickProject == "全部项目" || profile.Project == _quickProject) &&
                    (_quickInstall == "全部安装" || ProfileInstallation(profile)?.Name == _quickInstall) &&
                    (_quickModule == "全部模块" || profile.Module == _quickModule) &&
                    $"{profile.Name} {profile.Project} {profile.Module} {profile.User}".Contains(_quickQuery.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : Data.Recent.Select(id => Data.Profiles.FirstOrDefault(profile => profile.Id == id)).OfType<LoginProfile>()
                    .Concat(Data.Profiles.Where(profile => !Data.Recent.Contains(profile.Id))).ToList();
            caption.Text = filtering ? $"找到 {matches.Count} 个配置" : "最近登录";
            list.Items.Clear();
            var selected = matches.FirstOrDefault(profile => profile.Id == _profileId);
            var visible = matches.Take(3).ToArray();
            if (selected is not null && visible.Length == 3 && !visible.Contains(selected)) visible[^1] = selected;
            foreach (var profile in visible) list.Items.Add(Row(profile, profile.Name, profile.Target, trim: true));
            list.SelectedItem = list.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is LoginProfile profile && profile == selected) ??
                list.Items.OfType<ListBoxItem>().FirstOrDefault();
            launch.IsEnabled = list.Items.Count > 0 && !_launching;
            edit.IsEnabled = list.Items.Count > 0;
        }
        search.TextChanged += (_, _) => { _quickQuery = search.Text; ReloadList(); };
        project.SelectionChanged += (_, _) => { _quickProject = project.SelectedItem as string ?? "全部项目"; ReloadList(); };
        ReloadList();
        var create = AsyncBtn("新建配置", "QuickNewProfile", () => EditProfile());
        return Card("快捷登录", Stack(bar, Below(caption, 5), Below(list, 3),
            Below(Actions(launch, edit, create), 6),
            LaunchFeedback()), Jump("全部配置", "Profiles"));
    }

    private void OpenQuickFilter()
    {
        var install = Choice(new[] { "全部安装" }.Concat(Installations.Select(item => item.Name)), _quickInstall, "QuickInstallFilter");
        var module = Choice(new[] { "全部模块" }.Concat(Data.Profiles.Select(item => item.Module).Distinct()), _quickModule, "QuickModuleFilter");
        if (!Modal("更多登录筛选", Form(Field("AVEVA 安装", install), Field("模块", module)), "应用筛选", () => true)) return;
        _quickInstall = install.SelectedItem as string ?? "全部安装";
        _quickModule = module.SelectedItem as string ?? "全部模块";
        Render();
    }

    private async Task Launch(LoginProfile profile)
    {
        if (_launching) return;
        _launching = true;
        _launchMessage = $"正在准备启动 {profile.Target}。";
        if (ShowsLaunchStatus) Render();
        PmlLog? receipt = null;
        try
        {
            var installation = ProfileInstallation(profile);
            if (_inventory is not { } inventory || installation is not { CanLaunch: true })
                throw new InvalidDataException("未找到当前配置的启动程序。请在配置管理重新选择本机安装。");
            _install = installation.Directory;
            var workflow = LaunchBackend;
            var revision = _profileRevision;
            if (new[] { profile.Project, profile.User, profile.Mdb, profile.Module }.Any(string.IsNullOrWhiteSpace))
            {
                if (Modal("登录配置未完成", Muted("请补齐工程、模块、用户和 MDB。已填值会保留。"), "编辑配置", () => true))
                    await EditProfile(profile);
                return;
            }
            var prepared = await Task.Run(() => workflow.Prepare(inventory, installation, profile.Id, revision,
                inheritWorkstationEnvironment: true));
            var preview = await Task.Run(() => workflow.Preview(prepared));
            if (!await ConfirmLaunchCatalogWarning(profile, inventory, installation, workflow))
            {
                _launchMessage = $"已取消启动 {profile.Target}。";
                return;
            }
            var credentials = new WindowsLaunchCredentials();
            var (target, identity) = Credential(profile);
            string? secret;
            try { secret = credentials.Read(target, identity); }
            catch (Win32Exception) { secret = null; }
            if (string.IsNullOrEmpty(secret) || secret == PasswordExample)
            {
                var password = Id(new PasswordBox { MaxLength = 1024 }, "LaunchPassword");
                var notice = new Notice("LaunchPasswordNotice");
                var content = Stack(Details([("工程／模块", profile.Target), ("程序", Value(preview, "executable"))]),
                    Below(Field("AVEVA 密码", password), 8), Below(notice, 6));
                if (!Modal("登录 " + profile.Project, content, "提交启动", () =>
                {
                    using var typed = password.SecurePassword;
                    if (typed.Length > 0 && password.Password != PasswordExample) return true;
                    notice.Show("请填写 AVEVA 密码。", "warning");
                    return false;
                }, password))
                {
                    password.Clear();
                    _launchMessage = $"已取消启动 {profile.Target}。";
                    return;
                }
                secret = password.Password;
                password.Clear();
            }
            var requestedAt = DateTimeOffset.UtcNow;
            var submitted = await Task.Run(() => workflow.Launch(prepared, () => secret));
            secret = null;
            var process = submitted.DescendantsAndSelf().FirstOrDefault(row => Value(row, "status") == "Submitted");
            if (!int.TryParse(Value(process, "pid"), out var pid) || pid <= 0)
                throw new InvalidDataException("启动器没有返回已提交的进程回执。");
            _ = long.TryParse(Value(process, "startTicks"), out var startTicks);
            _profileId = profile.Id;
            Data.Recent.Remove(profile.Id);
            Data.Recent.Insert(0, profile.Id);
            if (Data.Recent.Count > 5) Data.Recent.RemoveAt(5);
            _launchMessage = $"正在启动 {profile.Target}，等待 AVEVA 会话 · 入口 PID {pid}。";
            receipt = RecordLog("AVEVA 启动", "运行中", null, _launchMessage, "");
            if (ShowsLaunchStatus) Render();
            await ObserveLaunch(profile, pid, startTicks, requestedAt, receipt);
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            if (receipt is not null)
                FinishLaunch(receipt, "待核对", $"无法检查 {profile.Target} 的会话。", error.Message);
            else
            {
                _launchMessage = error.Message;
                RecordLog("AVEVA 启动", "失败", null, "", error.Message);
                Tell(error.Message);
            }
        }
        finally { _launching = false; if (ShowsLaunchStatus) Render(); }
    }

    private async Task ObserveLaunch(LoginProfile profile, int pid, long startTicks, DateTimeOffset requestedAt, PmlLog receipt)
    {
        bool IsNewProcess(long start, string executable) =>
            start >= requestedAt.UtcTicks && InInstallation(executable, profile.Install);
        bool IsEntry(PmlLiveSession session) => session.Pid == pid && session.StartTicks == startTicks;

        var identities = new Dictionary<(int Pid, long StartTicks), PmlLoginIdentity?>();
        var host = new PmlWorkbenchHost(_store.DirectoryPath);
        var elapsed = Stopwatch.StartNew();
        string? identityError = null;
        PmlLiveSession[] candidates = [];
        var ambiguous = false;
        var monitorReported = false;
        var hostReported = false;
        while (IsLoaded && elapsed.Elapsed < TimeSpan.FromSeconds(48))
        {
            await RefreshSessions(redraw: false);
            candidates = _sessions.Where(row => IsNewProcess(row.StartTicks, row.ExecutablePath)).ToArray();
            var possible = candidates.Where(row => IsEntry(row) || SameField(row.Model, profile.Module)).ToArray();
            if (!hostReported && IsLoaded && possible.Length > 0)
            {
                hostReported = true;
                _launchMessage = $"CLI 已连接，正在核对 {profile.Target} 的工程、用户和 MDB。";
                if (ShowsLaunchStatus) Render();
            }
            foreach (var session in possible)
            {
                var key = (session.Pid, session.StartTicks);
                if (identities.ContainsKey(key)) continue;
                try
                {
                    // A lost status handshake can recover while the new Host starts.
                    // Cache actual identity requests, including failures, so PML is never replayed here.
                    if (await Task.Run(() => host.TryReadLoginIdentity(session)) is { } identity)
                        identities[key] = identity;
                }
                catch (Exception error) when (WorkbenchError(error))
                {
                    identities[key] = null;
                    identityError ??= error.Message;
                }
            }

            var matching = possible.Where(session => MatchesLogin(profile, identities.GetValueOrDefault((session.Pid, session.StartTicks)))).ToArray();
            var confirmed = matching.FirstOrDefault(IsEntry) ?? (matching.Length == 1 ? matching[0] : null);
            if (confirmed is not null)
            {
                _session = confirmed;
                FinishLaunch(receipt, "成功", $"已登录 {profile.Target} · MDB {profile.Mdb} · CLI 已连接 · PID {confirmed.Pid}。");
                return;
            }
            ambiguous = matching.Length > 1;
            if (!monitorReported && IsLoaded && candidates.Length == 0 && elapsed.Elapsed.TotalSeconds >= 6 &&
                VisibleMonitor(profile.Install, requestedAt.UtcTicks) is { } monitorPid)
            {
                monitorReported = true;
                _launchMessage = MonitorMessage(profile, monitorPid);
                if (ShowsLaunchStatus) Render();
            }
            await Task.Delay(1600);
        }

        if (!IsLoaded) return;
        if (_sessionCheckFailed)
            FinishLaunch(receipt, "待核对", $"无法检查 {profile.Target} 的会话。", _sessionMessage);
        else if (ambiguous)
            FinishLaunch(receipt, "待核对", $"有多个匹配 {profile.Target} 的新会话，请在会话与 CLI 中选择。");
        else if (candidates.Length > 0)
            FinishLaunch(receipt, "待核对", $"CLI 已连接，但尚未确认 {profile.Target} 的登录身份。", identityError ?? "");
        else if (_pendingHostProcesses.Any(process => IsNewProcess(process.StartTicks, process.ExecutablePath)))
            FinishLaunch(receipt, "待核对", $"发现新的 AVEVA 窗口，CLI 尚未连接。可在会话与 CLI 中刷新。");
        else if (VisibleMonitor(profile.Install, requestedAt.UtcTicks) is { } remainingMonitorPid)
            FinishLaunch(receipt, "待核对", MonitorMessage(profile, remainingMonitorPid));
        else if (EntryExited(pid, startTicks))
            FinishLaunch(receipt, "失败", $"AVEVA 启动进程已退出，未确认 {profile.Target}。请查看 AVEVA 的提示或启动日志。");
        else
            FinishLaunch(receipt, "待核对", $"AVEVA 仍在启动，尚未确认 {profile.Target}。请查看 AVEVA 登录窗口。");
    }

    private static bool MatchesLogin(LoginProfile profile, PmlLoginIdentity? identity) => identity is not null &&
        SameField(identity.Project, profile.Project) && SameField(identity.User, profile.User) &&
        SameField(identity.Mdb.TrimStart('/'), profile.Mdb.Trim().TrimStart('/')) && SameField(identity.Module, profile.Module);

    private static bool SameField(string actual, string expected) =>
        actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string MonitorMessage(LoginProfile profile, int pid) => SameField(profile.Module, "Monitor")
        ? $"AVEVA Monitor 已打开 · PID {pid}。请在原厂窗口核对工程和用户。"
        : $"尚未进入 {profile.Target}。AVEVA Monitor 已打开 · PID {pid}，请在原厂窗口核对 MDB 和模块。";

    private static int? VisibleMonitor(string installation, long requestedTicks)
    {
        // The CLI intentionally discovers engineering sessions only. Monitor can outlive the entry PID.
        var executable = Path.Combine(installation, "mon.exe");
        var processes = Process.GetProcessesByName("mon");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero && process.StartTime.ToUniversalTime().Ticks >= requestedTicks &&
                        process.MainWindowTitle.Contains("Monitor", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                        return process.Id;
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException) { }
            }
            return null;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static bool EntryExited(int pid, long startTicks)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited || (startTicks != 0 && process.StartTime.ToUniversalTime().Ticks != startTicks);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return true; }
        catch (Win32Exception) { return false; }
    }

    private void FinishLaunch(PmlLog receipt, string state, string message, string error = "")
    {
        _launchMessage = message;
        AcceptExecutionLog(receipt with
        {
            Sequence = receipt.Sequence + 1,
            When = DateTimeOffset.Now,
            State = state,
            ExitCode = state == "成功" ? 0 : null,
            Output = receipt.Output + Environment.NewLine + message,
            Error = error
        });
    }

    private FrameworkElement LaunchFeedback() =>
        Below(Id(Muted(_launchMessage), "LaunchStatus"), _launchMessage.Length > 0 ? 6 : 0);

    private bool ShowsLaunchStatus => _page.Id is "Home" or "Launch" or "Profiles";

    private async Task<bool> ConfirmLaunchCatalogWarning(LoginProfile profile, LaunchInventory inventory,
        LaunchInstallation installation, LaunchDeskWorkflow workflow)
    {
        CoreData catalog;
        try { catalog = await Task.Run(() => workflow.Catalog(inventory, installation)); }
        catch (Exception error) when (WorkbenchError(error))
        {
            // The installation and launch parameters have already passed the mandatory launch preview.
            return true;
        }
        var projects = catalog.Children("project").Where(item => (bool?)item.Field("found") == true).ToArray();
        var modules = InstalledLoginModules(catalog);
        var missingProject = projects.Length > 0 &&
            !projects.Any(item => Value(item, "code").Equals(profile.Project, StringComparison.OrdinalIgnoreCase));
        var missingModule = modules.Length > 0 && !modules.Contains(profile.Module, StringComparer.OrdinalIgnoreCase);
        if (!missingProject && !missingModule) return true;

        var kind = (missingProject, missingModule) switch
        {
            (true, true) => "project-module",
            (true, false) => "project",
            _ => "module"
        };
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join("\n", kind, profile.Product, installation.Directory, profile.Project, profile.Module, profile.User, profile.Mdb)
                .ToUpperInvariant())));
        if (Data.DismissedLoginChecks.Contains(key)) return true;

        var item = kind == "project-module" ? "工程和模块" : missingProject ? "工程" : "模块";
        var remember = Id(new CheckBox { Content = "不再提示这组参数的同类问题" }, "MuteLoginCheck");
        var warning = new DialogWindow(this, "启动前提示");
        warning.Body.Children.Add(Stack(Views.Text($"本机启动目录未找到配置中的{item}。你可以取消后修改，或按保存的参数继续。"),
            Below(remember, 10)));
        warning.SetActions("继续登录", () => true);
        if (warning.ShowDialog() != true) return false;
        if (remember.IsChecked == true && Data.DismissedLoginChecks.Add(key)) Save();
        return true;
    }

    private static string[] InstalledLoginModules(CoreData catalog) =>
        catalog.Children("module").Where(item => (bool?)item.Field("defined") != false)
            .Select(item => Value(item, "name"))
            .Where(name => name.Length > 0 && !name.Equals("Monitor", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private (string Target, string Identity) Credential(LoginProfile profile) => (
        LaunchCredentialKeys.Target(_store.DirectoryPath, profile.Id),
        LaunchCredentialKeys.Identity(profile.Product, profile.Install, profile.Project, profile.User));

    private FrameworkElement LaunchPage(bool manage)
    {
        var search = Input(_profileSearch, "ProfileSearch");
        var table = Table(Array.Empty<LoginProfile>(), "LoginProfiles", ("登录配置", "Name", 1.55),
            ("项目／模块", "Target", 1.25), ("用户", "User", 1), ("MDB", "Mdb", 1));
        table.MaxHeight = 280;
        var details = new ContentControl();
        void ShowSelection()
        {
            if (table.SelectedItem is not LoginProfile profile)
            {
                details.Content = Card("当前配置", Muted("请在表中选择配置。"));
                return;
            }
            _profileId = profile.Id;
            var login = AsyncBtn(_launching ? "正在登录…" : "一键登录 " + profile.Module, "LaunchProfile",
                () => Launch(profile), "Button.Primary");
            login.IsEnabled = !_launching;
            var actions = Actions(login, AsyncBtn("编辑", "EditProfile", () => EditProfile(profile)));
            var secondary = manage ? Actions(AsyncBtn("复制", "CopyProfile", () => EditProfile(profile, true)),
                AsyncBtn("归档", "ArchiveProfile", () => ArchiveProfile(profile))) : Actions(Jump("CLI 状态", "Cli"));
            details.Content = Card("当前配置", Stack(Details([
                ("名称", profile.Name), ("AVEVA", ProfileInstallation(profile)?.ToString() ?? profile.Install),
                ("项目／模块", profile.Target), ("用户", profile.User), ("MDB", profile.Mdb)
            ]), Below(actions, 9), Below(secondary, 7), LaunchFeedback()));
        }
        void Filter()
        {
            var rows = Data.Profiles.Where(profile => $"{profile.Name} {profile.Target} {profile.User} {profile.Mdb}"
                .Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            table.ItemsSource = rows;
            table.SelectedItem = rows.FirstOrDefault(profile => profile.Id == _profileId) ?? rows.FirstOrDefault();
            ShowSelection();
        }
        table.SelectionChanged += (_, _) => ShowSelection();
        search.TextChanged += (_, _) => { _profileSearch = search.Text; Filter(); };
        Filter();
        var commands = Actions(AsyncBtn("新建配置", "NewProfile", () => EditProfile(), "Button.Primary"));
        if (manage) commands.Children.Add(Btn("查看归档", "ShowArchivedProfiles", ShowArchivedProfiles, "Button.Quiet"));
        else commands.Children.Add(Jump("管理配置", "Profiles"));
        return SideBySide(Card("登录配置", Stack(Prompt(search, "搜索名称、项目、用户或 MDB"), Below(table, 8)), commands), details, 1.45);
    }

    private const string PasswordExample = "/XXXXXX";

    private sealed record ProfileDraft(LaunchInstallation Installation, string Project, string Module,
        string User, string Mdb, string WorkingDirectory);

    private async Task EditProfile(LoginProfile? profile = null, bool copy = false)
    {
        try
        {
            if (_inventory is not { } inventory) throw new InvalidDataException("请先检测 AVEVA 安装。");
            var choices = Installations.Where(item => item.CanLaunch).ToArray();
            if (choices.Length == 0) throw new InvalidDataException("当前未发现具备启动文件的 AVEVA 安装。");
            var workflow = LaunchBackend;
            var revision = _profileRevision;
            var matched = profile is null ? null : ProfileInstallation(profile);
            var initial = matched is { CanLaunch: true } ? matched : choices.FirstOrDefault(item =>
                item.Directory.Equals(_install, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
            var original = matched == initial ? profile : null;
            var settings = original is null ? null :
                (await Task.Run(() => workflow.Prepare(inventory, initial, original.Id, revision,
                    inheritWorkstationEnvironment: true))).Children().SingleOrDefault();
            var name = Input(copy ? profile!.Name + " 副本" : profile?.Name ?? "", "ProfileName");
            var install = Choice(choices, initial, "ProfileInstall");
            var project = Input(original?.Project ?? "", "ProfileProject", "工程");
            var module = Input(original?.Module ?? "", "ProfileModule", "启动模块");
            var user = Input(original?.User ?? "", "ProfileUser", "用户");
            var mdb = Input(original?.Mdb ?? "", "ProfileMdb", "MDB");
            var password = Id(new PasswordBox { MaxLength = 1024 }, "ProfilePassword");
            var workingDirectory = Input(Value(settings, "workingDirectory") is { Length: > 0 } savedDirectory
                ? savedDirectory : initial.Directory, "ProfileWorkDirectory");
            var notice = new Notice("ProfileEditorNotice");
            var dialog = new DialogWindow(this, profile is null ? "新建登录配置" : copy ? "复制登录配置" : "编辑登录配置", width: 665);
            var closed = false;
            var updating = false;
            var moduleEdited = false;
            var catalogRevision = 0;
            var fieldsRevision = 0;
            var lastProject = project.Text.Trim();
            CoreData? catalog = null;
            string[] availableModules = [];
            var moduleDirectory = Btn("查看本机模块", "ProfileModuleDirectory", () =>
            {
                var directory = new DialogWindow(dialog, "本机启动模块",
                    "启动时还会核对工程权限和许可。需要切换模块时可在配置中直接填写。");
                directory.Body.Children.Add(Views.Text(string.Join("、", availableModules)));
                directory.SetActions("关闭", () => true, showCancel: false);
                directory.ShowDialog();
            }, "Button.Quiet");
            moduleDirectory.Visibility = Visibility.Collapsed;

            static string[] Choices(CoreData data, string node, string field) => data.Children(node)
                .Where(item => (bool?)item.Field("defined") != false && (bool?)item.Field("found") != false)
                .Select(item => Value(item, field)).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            static bool IsFromInstallation(LoginProfile item, LaunchInstallation selected) =>
                item.Product == selected.Product && item.Install.Equals(selected.Directory, StringComparison.OrdinalIgnoreCase);

            LoginProfile? RecentFor(LaunchInstallation selected, string? code = null) =>
                Data.Recent.Select(id => Data.Profiles.FirstOrDefault(item => item.Id == id)).OfType<LoginProfile>()
                    .FirstOrDefault(item => IsFromInstallation(item, selected) &&
                        (code is null || item.Project.Equals(code, StringComparison.OrdinalIgnoreCase)));

            void WithoutChangeNotifications(Action update)
            {
                updating = true;
                try { update(); }
                finally { updating = false; }
            }

            void FillKnownLogin(LaunchInstallation selected)
            {
                var code = project.Text.Trim();
                if (code.Length == 0 || user.Text.Length != 0 || mdb.Text.Length != 0) return;
                if (RecentFor(selected, code) is { User.Length: > 0, Mdb.Length: > 0 } saved)
                {
                    user.Text = saved.User;
                    mdb.Text = saved.Mdb;
                    return;
                }
                var entries = catalog?.Children("project").FirstOrDefault(row =>
                    Value(row, "code").Equals(code, StringComparison.OrdinalIgnoreCase))?.Children("login");
                var known = (entries ?? []).Select(row => (User: Value(row, "user"), Mdb: Value(row, "mdb")))
                    .Where(entry => entry.User.Length > 0 && entry.Mdb.Length > 0)
                    .DistinctBy(entry => (entry.User.ToUpperInvariant(), entry.Mdb.ToUpperInvariant()))
                    .Take(2).ToArray();
                if (known.Length == 1) (user.Text, mdb.Text) = known[0];
            }

            async Task ReadCatalog(bool preserve)
            {
                if (closed || install.SelectedItem is not LaunchInstallation selected) return;
                var request = ++catalogRevision;
                catalog = null;
                var saved = preserve ? original : null;
                var recent = RecentFor(selected);
                WithoutChangeNotifications(() =>
                {
                    if (!preserve)
                    {
                        project.Text = "";
                        module.Text = "";
                        moduleEdited = false;
                        user.Text = "";
                        mdb.Text = "";
                        workingDirectory.Text = selected.Directory;
                        password.Clear();
                    }
                    if (saved is null && project.Text.Length == 0 && recent is not null) project.Text = recent.Project;
                    if (saved is null) FillKnownLogin(selected);
                    lastProject = project.Text.Trim();
                    availableModules = [];
                    moduleDirectory.Visibility = Visibility.Collapsed;
                });
                var unchanged = fieldsRevision;
                notice.Show("正在核对本机项目和启动模块。可继续填写配置。");
                try
                {
                    var result = await Task.Run(() => workflow.Catalog(inventory, selected));
                    if (closed || request != catalogRevision) return;
                    catalog = result;
                    var projects = Choices(result, "project", "code");
                    availableModules = InstalledLoginModules(result);
                    WithoutChangeNotifications(() =>
                    {
                        if (fieldsRevision == unchanged)
                        {
                            if (project.Text.Length == 0 && projects.Length == 1) project.Text = projects[0];
                            if (saved is null) FillKnownLogin(selected);
                        }
                        lastProject = project.Text.Trim();
                        if (!moduleEdited && module.Text.Length == 0)
                        {
                            var standard = selected.Product switch { "E3D" => "Design", "AM" => "Outfitting", _ => null };
                            var savedModules = Data.Profiles.Where(item => IsFromInstallation(item, selected))
                                .Select(item => item.Module).Where(item => availableModules.Contains(item, StringComparer.OrdinalIgnoreCase))
                                .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
                            module.Text = availableModules.FirstOrDefault(item => item.Equals(recent?.Module, StringComparison.OrdinalIgnoreCase)) ??
                                (savedModules.Length == 1 ? savedModules[0] : null) ??
                                availableModules.FirstOrDefault(item => item.Equals(standard, StringComparison.OrdinalIgnoreCase)) ??
                                (availableModules.Length == 1 ? availableModules[0] : "");
                        }
                    });
                    moduleDirectory.Visibility = availableModules.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
                    if (availableModules.Length == 0) notice.Show("未发现本机启动模块。可手填，或先在 AVEVA 登录后检测会话。", "warning");
                    else if (module.Text.Length > 0 && !availableModules.Contains(module.Text.Trim(), StringComparer.OrdinalIgnoreCase))
                        notice.Show("已填模块未出现在本机启动目录。可以查看本机模块后修改。", "warning");
                    else notice.Show("已核对本机启动入口。工程权限和许可在实际登录时验证。");
                }
                catch (Exception error) when (WorkbenchError(error))
                {
                    if (closed || request != catalogRevision) return;
                    notice.Show(error.Message, "warning");
                }
            }

            void ReadProjectChanged()
            {
                if (updating) return;
                fieldsRevision++;
                var code = project.Text.Trim();
                if (code.Equals(lastProject, StringComparison.OrdinalIgnoreCase)) return;
                lastProject = code;
                WithoutChangeNotifications(() =>
                {
                    user.Clear();
                    mdb.Clear();
                    password.Clear();
                });
                notice.Clear();
            }

            ProfileDraft? CurrentDraft()
            {
                if (install.SelectedItem is not LaunchInstallation selected ||
                    new[] { project.Text, module.Text, user.Text, mdb.Text }.Any(string.IsNullOrWhiteSpace)) return null;
                return new(selected, project.Text.Trim().ToUpperInvariant(), module.Text.Trim(), user.Text.Trim(), mdb.Text.Trim(),
                    workingDirectory.Text.Trim());
            }


            project.TextChanged += (_, _) => ReadProjectChanged();
            module.TextChanged += (_, _) => { if (!updating) moduleEdited = true; };
            user.TextChanged += (_, _) => { if (!updating) { fieldsRevision++; password.Clear(); } };
            mdb.TextChanged += (_, _) => { if (!updating) fieldsRevision++; };
            install.SelectionChanged += async (_, _) => await ReadCatalog(false);
            dialog.Loaded += async (_, _) => await ReadCatalog(true);
            dialog.Closed += (_, _) => closed = true;

            var installationField = choices.Length == 1
                ? Field("软件安装", ReadOnlyInput(initial.ToString(), "ProfileFixedInstall"))
                : Field("软件安装", install);
            var advanced = Id(new Expander
            {
                Header = "显示名称和高级选项",
                Content = Stack(Field("配置名称（可自动生成）", name), Below(Field("工作目录", workingDirectory), 8),
                    Below(Field("AVEVA 密码（可选，保存到 Windows）", password), 8),
                    Below(Muted("示例 /XXXXXX 是占位符。可留空，在实际启动时输入密码。"), 5))
            }, "ProfileAdvanced");
            var sample = Btn("填入示例", "ProfileExample", () =>
            {
                WithoutChangeNotifications(() =>
                {
                    project.Text = lastProject = "APS";
                    user.Text = "SYSTEM";
                    mdb.Text = "ALL";
                    password.Clear();
                });
                fieldsRevision++;
                notice.Show("示例仅供预览。请确认工程、用户和 MDB 是否适用于你的环境。");
            });
            dialog.Body.Children.Add(Stack(installationField,
                Below(Form(Field("工程", Prompt(project, "APS")), Field("启动模块", Prompt(module, "按当前安装填写")),
                    Field("用户", Prompt(user, "SYSTEM")), Field("MDB", Prompt(mdb, "ALL"))), 8),
                Below(Actions(sample, moduleDirectory), 3),
                Below(notice, 6), Below(advanced, 7)));
            var id = profile is not null && !copy ? profile.Id : Guid.NewGuid().ToString("N");
            var credentialWarning = "";
            dialog.SetActions("保存配置", () =>
            {
                if (CurrentDraft() is not { } draft)
                {
                    notice.Show("请填写工程、启动模块、用户和 MDB。", "warning");
                    return false;
                }
                var secret = password.Password;
                if (secret == PasswordExample)
                {
                    notice.Show("请清空密码占位符，或填写实际 AVEVA 密码。", "warning");
                    return false;
                }
                var label = name.Text.Trim();
                if (label.Length == 0) label = $"{draft.Installation.Name} · {draft.Project} · {draft.User}";
                try
                {
                    var result = workflow.Save(inventory, draft.Installation, id, label, revision, draft.Project, draft.User,
                        draft.Mdb, draft.Module, DraftWorkDirectory(draft),
                        draft.Installation.Product == "E3D" && Value(settings, "uicDirectory") is { Length: > 0 } uicDirectory ? uicDirectory : null);
                    _profileRevision = Value(result, "revision");
                }
                catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "danger"); return false; }
                if (secret.Length > 0)
                {
                    try
                    {
                        var (target, identity) = Credential(new(id, label, draft.Installation.Directory, draft.Project,
                            draft.Module, draft.User, draft.Mdb, draft.Installation.Product));
                        new WindowsLaunchCredentials().Write(target, identity, secret);
                    }
                    catch (Exception error) when (WorkbenchError(error)) { credentialWarning = "配置已保存。Windows 凭据保存失败，请在登录时输入密码。"; }
                }
                return true;
            });
            dialog.FocusOnOpen(project);
            try { if (dialog.ShowDialog() != true) return; }
            finally { password.Clear(); }
            await ReadProfiles();
            _profileId = id;
            _profileSearch = "";
            Render();
            Tell(credentialWarning.Length > 0 ? credentialWarning : "登录配置已保存。");
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private static TextBox ReadOnlyInput(string text, string id)
    {
        var input = Input(text, id);
        input.IsReadOnly = true;
        return input;
    }

    private static string DraftWorkDirectory(ProfileDraft draft) =>
        draft.WorkingDirectory.Length == 0 ? draft.Installation.Directory : Path.GetFullPath(draft.WorkingDirectory);

    private async Task ArchiveProfile(LoginProfile profile)
    {
        if (!Confirm("归档登录配置", $"将「{profile.Name}」移出当前配置清单。", "归档")) return;
        try
        {
            var revision = _profileRevision;
            await Task.Run(() => LaunchBackend.Archive(profile.Id, revision));
            await ReadProfiles();
            Data.ArchivedProfiles.Insert(0, profile);
            Render();
            Save("已归档登录配置。");
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private void ShowArchivedProfiles()
    {
        var table = Table(Data.ArchivedProfiles, "ArchivedProfiles", ("配置", "Name", 1.4), ("项目／模块", "Target", 1));
        Modal("本机配置归档", Data.ArchivedProfiles.Count == 0 ? Muted("暂无已归档配置。") : table,
            "完成", () => true, cancel: false);
    }
}
