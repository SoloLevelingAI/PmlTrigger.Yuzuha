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
    private static bool ProjectReaderAvailable =>
#if BUNDLED_PROJECT_READER
        AvevaProjectReaderPackage.IsInstalled;
#else
        false;
#endif

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
        var cli = StatusRow("Terminal", "PmlTwin CLI", current?.ToString() ?? _sessionMessage, connection,
            current is not null ? "success" : connection == "未连接" ? "warning" : "primary", Jump("查看状态", "Cli"));
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
        list.SelectionChanged += (_, _) =>
        {
            var selected = (list.SelectedItem as ListBoxItem)?.Tag as LoginProfile;
            launch.Content = selected is null ? "一键登录" : "一键登录 " + selected.Module;
            launch.IsEnabled = selected is not null && !_launching;
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
            foreach (var profile in matches.Take(3)) list.Items.Add(Row(profile, profile.Name, profile.Target, trim: true));
            list.SelectedIndex = list.Items.Count > 0 ? 0 : -1;
            launch.IsEnabled = list.Items.Count > 0 && !_launching;
        }
        search.TextChanged += (_, _) => { _quickQuery = search.Text; ReloadList(); };
        project.SelectionChanged += (_, _) => { _quickProject = project.SelectedItem as string ?? "全部项目"; ReloadList(); };
        ReloadList();
        return Card("快捷登录", Stack(bar, Below(caption, 5), Below(list, 3),
            Below(Actions(Jump(Data.Profiles.Count == 0 ? "新建登录配置" : "管理配置", "Profiles"), launch), 6),
            Below(Muted(_launchMessage), _launchMessage.Length > 0 ? 6 : 0)), Jump("打开启动器", "Launch"));
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
        try
        {
            var installation = ProfileInstallation(profile);
            if (_inventory is not { } inventory || installation is not { CanLaunch: true })
                throw new InvalidDataException("未找到当前配置的启动程序。请在配置管理重新选择本机安装。");
            _install = installation.Directory;
            var workflow = LaunchBackend;
            var revision = _profileRevision;
            var prepared = await Task.Run(() => workflow.Prepare(inventory, installation, profile.Id, revision,
                inheritWorkstationEnvironment: true));
            var preview = await Task.Run(() => workflow.Preview(prepared));
            var credentials = new WindowsLaunchCredentials();
            var (target, identity) = Credential(profile);
            string? secret;
            try { secret = credentials.Read(target, identity); }
            catch (System.ComponentModel.Win32Exception) { secret = null; }
            if (string.IsNullOrEmpty(secret))
            {
                var password = Id(new PasswordBox { MaxLength = 1024 }, "LaunchPassword");
                var notice = new Notice("LaunchPasswordNotice");
                var content = Stack(Details([("工程／模块", profile.Target), ("程序", Value(preview, "executable"))]),
                    Below(Field("AVEVA 密码", password), 8), Below(notice, 6));
                if (!Modal("登录 " + profile.Project, content, "提交启动", () =>
                {
                    using var typed = password.SecurePassword;
                    if (typed.Length > 0) return true;
                    notice.Show("请填写 AVEVA 密码。", "warning");
                    return false;
                }, password)) { password.Clear(); return; }
                secret = password.Password;
                password.Clear();
            }
            var requestedAt = DateTimeOffset.UtcNow;
            var submitted = await Task.Run(() => workflow.Launch(prepared, () => secret));
            var process = submitted.DescendantsAndSelf().FirstOrDefault(row => Value(row, "status") == "Submitted");
            if (!int.TryParse(Value(process, "pid"), out var pid) || pid <= 0)
                throw new InvalidDataException("启动器没有返回已提交的进程回执。");
            _ = long.TryParse(Value(process, "startTicks"), out var startTicks);
            _profileId = profile.Id;
            Data.Recent.Remove(profile.Id);
            Data.Recent.Insert(0, profile.Id);
            if (Data.Recent.Count > 5) Data.Recent.RemoveAt(5);
            _launchMessage = $"已提交 {profile.Target} · PID {pid}，正在等待会话。";
            RecordLog("AVEVA 启动", "已提交", 0, _launchMessage, "");
            Render();
            await ObserveLaunch(profile, pid, startTicks, requestedAt);
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            _launchMessage = error.Message;
            RecordLog("AVEVA 启动", "失败", null, "", error.Message);
            Tell(error.Message);
        }
        finally { _launching = false; Render(); }
    }

    private async Task ObserveLaunch(LoginProfile profile, int pid, long startTicks, DateTimeOffset requestedAt)
    {
        bool MatchesProjectModule(PmlLiveSession row) =>
            row.Project.Equals(profile.Project, StringComparison.OrdinalIgnoreCase) &&
            row.Model.Equals(profile.Module, StringComparison.OrdinalIgnoreCase);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(48);
        while (IsLoaded && DateTimeOffset.UtcNow < deadline)
        {
            await RefreshSessions(redraw: false);
            var candidates = _sessions.Where(row => row.StartTicks >= requestedAt.UtcTicks).ToArray();
            var matching = candidates.Where(row => InInstallation(row, profile.Install) && MatchesProjectModule(row)).ToArray();
            var session = candidates.FirstOrDefault(row => row.Pid == pid && (startTicks == 0 || row.StartTicks == startTicks))
                ?? (matching.Length == 1 ? matching[0] : null);
            if (session is not null)
            {
                _session = session;
                _launchMessage = MatchesProjectModule(session)
                    ? $"{profile.Target} 已打开，CLI 已连接 · PID {session.Pid}。"
                    : $"CLI 已连接 · PID {session.Pid} · {session.Model}，请核对实际工程。";
                return;
            }
            await Task.Delay(1600);
        }
        _launchMessage = $"已提交 {profile.Target} · PID {pid}。可在会话与 CLI 刷新核对。";
    }

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
            var actions = manage ? Actions(AsyncBtn("编辑", "EditProfile", () => EditProfile(profile)),
                    AsyncBtn("复制", "CopyProfile", () => EditProfile(profile, true)),
                    AsyncBtn("归档", "ArchiveProfile", () => ArchiveProfile(profile))) :
                Actions(AsyncBtn("一键登录 " + profile.Module, "LaunchProfile", () => Launch(profile), "Button.Primary"),
                    Jump("CLI 状态", "Cli"));
            details.Content = Card("当前配置", Stack(Details([
                ("名称", profile.Name), ("AVEVA", ProfileInstallation(profile)?.ToString() ?? profile.Install),
                ("项目／模块", profile.Target), ("用户", profile.User), ("MDB", profile.Mdb)
            ]), Below(actions, 9), Below(Muted(_launchMessage), _launchMessage.Length > 0 ? 6 : 0)));
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
            var initial = matched ?? choices.FirstOrDefault(item => item.Directory == _install) ?? choices[0];
            var settings = profile is not null && matched is { CanLaunch: true }
                ? (await Task.Run(() => workflow.Prepare(inventory, matched, profile.Id, revision,
                    inheritWorkstationEnvironment: true))).Children().SingleOrDefault()
                : null;
            var name = Input(copy ? profile!.Name + " 副本" : profile?.Name ?? "", "ProfileName");
            var install = Choice(choices, initial, "ProfileInstall");
            var project = Choice(Array.Empty<string>(), null, "ProfileProject", editable: true);
            var module = Choice(Array.Empty<string>(), null, "ProfileModule", editable: true);
            var user = Choice(Array.Empty<string>(), null, "ProfileUser", editable: true);
            var mdb = Choice(Array.Empty<string>(), null, "ProfileMdb", editable: true);
            var password = Id(new PasswordBox { MaxLength = 1024 }, "ProfilePassword");
            var workingDirectory = Input(Value(settings, "workingDirectory") is { Length: > 0 } savedDirectory ? savedDirectory : initial.Directory, "ProfileWorkDirectory");
            var notice = new Notice("ProfileEditorNotice");
            var dialog = new DialogWindow(this, profile is null ? "新建登录配置" : copy ? "复制登录配置" : "编辑登录配置", width: 665);
            var readerAvailable = ProjectReaderAvailable;
            var closed = false;
            var updatingChoices = false;
            var loadingCatalog = false;
            var catalogRevision = 0;
            CancellationTokenSource? activeRead = null;
            var lastProject = profile?.Project ?? "";
            var modules = Array.Empty<string>();
            CoreData? catalog = null;

            bool HasProjectCode() => project.Text.Trim() is { Length: 3 } code && code.All(char.IsAsciiLetterOrDigit);

            static void Choices(ComboBox editor, string[] list, string typed)
            {
                editor.ItemsSource = list;
                editor.SelectedItem = list.FirstOrDefault(item => item.Equals(typed, StringComparison.OrdinalIgnoreCase));
                if (editor.SelectedItem is null) editor.Text = typed;
            }

            static string[] Values(CoreData data, string node, string field) => data.Children(node)
                .Where(item => (bool?)item.Field("defined") != false && (bool?)item.Field("found") != false)
                .Select(item => Value(item, field)).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            void ShowKnownLogins()
            {
                var known = catalog?.Children("project").FirstOrDefault(row =>
                    Value(row, "code").Equals(project.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (known is null) return;
                var users = Values(known, "login", "user");
                var mdbs = Values(known, "login", "mdb");
                Choices(user, users, user.Text.Length == 0 && users.Length == 1 ? users[0] : user.Text);
                Choices(mdb, mdbs, mdb.Text.Length == 0 && mdbs.Length == 1 ? mdbs[0] : mdb.Text);
            }

            void ApplyChoices(Action change)
            {
                updatingChoices = true;
                try { change(); }
                finally { updatingChoices = false; }
            }

            void StopReader()
            {
                var previous = activeRead;
                activeRead = null;
                previous?.Cancel();
            }

            async Task Projects(bool preserve)
            {
                if (closed || install.SelectedItem is not LaunchInstallation selected) return;
                StopReader();
                var request = ++catalogRevision;
                loadingCatalog = true;
                catalog = null;
                modules = [];
                if (!preserve)
                {
                    workingDirectory.Text = selected.Directory;
                    password.Clear();
                }
                lastProject = preserve ? profile?.Project ?? "" : "";
                ApplyChoices(() =>
                {
                    Choices(project, [], lastProject);
                    Choices(module, [], preserve ? profile?.Module ?? "" : "");
                    Choices(user, [], preserve ? profile?.User ?? "" : "");
                    Choices(mdb, [], preserve ? profile?.Mdb ?? "" : "");
                });
                notice.Show("正在读取项目目录。可继续填写配置。");
                try
                {
                    var result = await Task.Run(() => workflow.Catalog(inventory, selected));
                    if (closed || request != catalogRevision) return;
                    catalog = result;
                    var projects = Values(result, "project", "code");
                    modules = Values(result, "module", "name");
                    ApplyChoices(() =>
                    {
                        Choices(project, projects, project.Text.Length == 0 && projects.Length == 1 ? projects[0] : project.Text);
                        lastProject = project.Text.Trim();
                        Choices(module, modules, module.Text.Length == 0 && modules.Length == 1 ? modules[0] : module.Text);
                        ShowKnownLogins();
                    });
                    notice.Show($"已读取 {projects.Length} 个项目与 {modules.Length} 个模块。");
                }
                catch (Exception error) when (WorkbenchError(error))
                {
                    if (!closed && request == catalogRevision) notice.Show(error.Message, "warning");
                    return;
                }
                finally
                {
                    if (!closed && request == catalogRevision) loadingCatalog = false;
                }
                if (request == catalogRevision) await Access();
            }

            async Task Access()
            {
                if (closed || activeRead is not null || !readerAvailable || loadingCatalog || !HasProjectCode() ||
                    install.SelectedItem is not LaunchInstallation selected) return;

                var code = project.Text.Trim();
                using var read = new CancellationTokenSource();
                activeRead = read;
                notice.Show("正在读取用户和 MDB。可继续填写。");

                bool IsCurrent() => !closed && ReferenceEquals(activeRead, read);
                Task<CoreData> ReadCandidates(bool reread) => Task.Run(
                    () => workflow.Access(inventory, selected, code, reread, read.Token), read.Token);

                bool Show(CoreData data)
                {
                    if (!IsCurrent()) return false;
                    if (Value(data, "status") != "project-metadata")
                    {
                        var detail = Value(data, "readerCode") switch
                        {
                            "timeout" => "读取用户和 MDB 超时。",
                            "worker-failed" => "Reader 未能完成读取。",
                            _ => Value(data, "detail")
                        };
                        notice.Show($"{detail}可继续手填。", "warning");
                        return false;
                    }
                    var users = Values(data, "user", "value");
                    var mdbs = Values(data, "mdb", "value");
                    var keptUser = user.Text.Trim();
                    var keptMdb = mdb.Text.Trim();
                    var readerModules = Values(data, "module", "name");
                    ApplyChoices(() =>
                    {
                        Choices(user, users, keptUser.Length == 0 && users.Length == 1 ? users[0] : keptUser);
                        Choices(mdb, mdbs, keptMdb.Length == 0 && mdbs.Length == 1 ? mdbs[0] : keptMdb);
                        if (readerModules.Length > 0) Choices(module, readerModules, module.Text);
                    });
                    notice.Show($"{(Value(data, "cache") == "hit" ? "已显示缓存" : "读取完成")} · {users.Length} 个用户 · {mdbs.Length} 个 MDB");
                    return true;
                }

                static bool Missing(ComboBox editor, string value) => value.Length > 0 && !editor.Items.OfType<string>()
                    .Any(item => item.TrimStart('/').Equals(value.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
                try
                {
                    var result = await ReadCandidates(reread: false);
                    if (Show(result) && Value(result, "cache") == "hit" &&
                        (Missing(user, user.Text.Trim()) || Missing(mdb, mdb.Text.Trim())))
                    {
                        notice.Show("缓存里缺少当前用户或 MDB，正在刷新。");
                        Show(await ReadCandidates(reread: true));
                    }
                }
                catch (OperationCanceledException) when (read.IsCancellationRequested) { }
                catch (Exception error) when (WorkbenchError(error))
                {
                    if (IsCurrent()) notice.Show(error.Message, "warning");
                }
                finally
                {
                    if (IsCurrent()) activeRead = null;
                }
            }

            project.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(async (_, _) =>
            {
                var code = project.Text.Trim();
                if (updatingChoices || code.Equals(lastProject, StringComparison.OrdinalIgnoreCase)) return;
                StopReader();
                lastProject = code;
                ApplyChoices(() =>
                {
                    Choices(user, [], "");
                    Choices(mdb, [], "");
                    Choices(module, modules, module.Text);
                    ShowKnownLogins();
                });
                password.Clear();
                notice.Clear();
                await Access();
            }));
            user.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => { if (!updatingChoices) password.Clear(); }));
            install.SelectionChanged += async (_, _) => await Projects(false);
            dialog.Loaded += async (_, _) => await Projects(true);
            dialog.Closed += (_, _) => { closed = true; StopReader(); };

            dialog.Body.Children.Add(Stack(Field("配置名称（可自动生成）", name), Below(Field("软件安装", install), 8),
                Below(Form(Field("项目", project), Field("模块", module), Field("用户", user), Field("MDB", mdb)), 8),
                Below(notice, 6),
                Below(Field("AVEVA 密码（可选，保存到 Windows）", password), 8),
                Below(Field("工作目录", workingDirectory), 8)));
            var id = profile is not null && !copy ? profile.Id : Guid.NewGuid().ToString("N");
            var credentialWarning = "";
            dialog.SetActions("保存配置", () =>
            {
                if (install.SelectedItem is not LaunchInstallation selected ||
                    new[] { project.Text, user.Text, mdb.Text, module.Text, workingDirectory.Text }.Any(string.IsNullOrWhiteSpace))
                {
                    notice.Show("请补齐项目、模块、用户、MDB 和工作目录。", "warning");
                    return false;
                }
                var label = name.Text.Trim();
                var code = project.Text.Trim().ToUpperInvariant();
                var username = user.Text.Trim();
                if (label.Length == 0) label = $"{selected.Name} · {code} · {username}";
                try
                {
                    var result = workflow.Save(inventory, selected, id, label, revision, code, username, mdb.Text.Trim(),
                        module.Text.Trim(), Path.GetFullPath(workingDirectory.Text.Trim()),
                        selected.Product == "E3D" && Value(settings, "uicDirectory") is { Length: > 0 } uicDirectory ? uicDirectory : null);
                    _profileRevision = Value(result, "revision");
                }
                catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "danger"); return false; }
                var secret = password.Password;
                if (secret.Length > 0)
                {
                    try
                    {
                        var (target, identity) = Credential(new(id, label, selected.Directory, code, module.Text.Trim(), username, mdb.Text.Trim(), selected.Product));
                        new WindowsLaunchCredentials().Write(target, identity, secret);
                    }
                    catch (Exception error) when (WorkbenchError(error)) { credentialWarning = "配置已保存。Windows 凭据保存失败，请在登录时输入密码。"; }
                }
                return true;
            });
            dialog.FocusOnOpen(name);
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
