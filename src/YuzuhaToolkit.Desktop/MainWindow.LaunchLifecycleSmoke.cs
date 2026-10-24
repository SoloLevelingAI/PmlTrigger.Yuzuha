using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>通过正式控件事件验证隔离启动配置流程；不创建 AVEVA 进程。</summary>
public partial class MainWindow
{
    /// <summary>运行正式 WPF 保存、模块联动、筛选、冲突和恢复流程，并保存截图及逐项结果。</summary>
    /// <param name="outputDirectory">调用者选择的隔离输出目录。</param>
    public async Task RunLaunchLifecycleSmokeAsync(string outputDirectory)
    {
        outputDirectory = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(outputDirectory);
        _launchSmokeRunning = true; _timer.Stop();
        var checks = new List<string>(); var started = DateTimeOffset.UtcNow;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("启动工作台验证失败：" + name + "；界面提示：" + LaunchLifecycleMessage.Text);
            checks.Add(name);
        }
        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Receipt(string status, Exception? error = null) => File.WriteAllText(Path.Combine(outputDirectory, "launch-lifecycle-ui.json"),
            JsonSerializer.Serialize(new { status, scope = "Actual WPF controls and application service with isolated files; no AVEVA process started",
                startedUtc = started, finishedUtc = DateTimeOffset.UtcNow, checks, error = error?.ToString(),
                stateDirectory = WorkspaceSettings.StateDirectory, hostOperations = 0,
                version = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(MainWindow).Assembly.Location).FileVersion,
                binarySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(MainWindow).Assembly.Location))) }, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            MaintainerText.Text = "隔离 UI 验证";
            var fixtures = Path.Combine(outputDirectory, "fixtures"); Directory.CreateDirectory(fixtures);
            var monitor = Path.Combine(fixtures, "mon.exe"); File.WriteAllBytes(monitor, []);
            var init = Path.Combine(fixtures, "launch.init"); File.WriteAllText(init, "synthetic UI fixture only");
            var marine = Path.Combine(fixtures, "marine.cmd"); File.WriteAllText(marine, "@echo off\nrem synthetic UI fixture; never executed\n");
            FillProfileFields(new LaunchProfile { Name = "E3D · 配置验证", Product = "E3D", Version = "2.1", Kind = "Monitor",
                Executable = monitor, Initialization = init, WorkingDirectory = fixtures, Project = "SAMPLE", User = "UI_TEST",
                Mdb = "/ALL", Module = "Design", RememberPassword = false });
            RefreshProductModules("Design", true); MarkLaunchDraftClean();
            Check(ModuleBox.Items.Cast<ProductModuleOption>().Any(item => item.Value == "Design") &&
                !ModuleBox.Items.Cast<ProductModuleOption>().Any(item => item.Value == "Outfitting"), "E3D module list is product-specific");
            Click(SaveProfileButton);
            Check(_selected is not null && _launchProfiles.Read().Value.Profiles.Count == 1, "save button persists a new profile");
            var e3dId = _selected!.Id; var initialRevision = _profileRevision;
            Check(File.Exists(Path.Combine(WorkspaceSettings.StateDirectory, "workspace.json")), "configuration is written to the real isolated file");
            MdbBox.Text = "/REVIEW"; Click(PreviewProfileButton);
            Check(LaunchChangesBox.Text.Contains("/REVIEW", StringComparison.Ordinal) && _launchProfiles.Read().Revision == initialRevision,
                "preview shows field differences without writing");
            WarnFilesBox.Text = "3100"; SaveWorkspaceFields();
            Check(_profileRevision == _launchProfiles.Read().Revision && MdbBox.Text == "/REVIEW" && IsLaunchDraftDirty() &&
                _launchProfiles.ReadProfile(e3dId).Profile.Mdb == "/ALL" && _settings.WarnFiles == 3100,
                "saving other workbench settings advances revision without discarding the launch draft");
            Click(SaveProfileButton); var revised = _profileRevision;
            Check(new LaunchProfileService().ReadProfile(e3dId).Profile.Mdb == "/REVIEW" && revised != initialRevision,
                "saved values are read by a new service instance with a new revision");
            Click(ReloadProfileButton);
            Check(MdbBox.Text == "/REVIEW" && !IsLaunchDraftDirty(), "reload button restores the persisted editor values");

            NewProfile(this, new());
            _fillingLaunchDraft = true;
            try
            {
                NameBox.Text = "AM · 舾装配置"; ExecutableBox.Text = marine; WorkingBox.Text = fixtures;
                InitializationBox.Text = ""; VersionBox.Text = "12.1"; ProjectBox.Text = "SHIP"; UserBox.Text = "UI_TEST";
            }
            finally { _fillingLaunchDraft = false; }
            ProductBox.SelectedIndex = 1;
            Check(ProductBox.Text == "AM" && ((ComboBoxItem)KindBox.SelectedItem).Content.ToString() == "MarineBatch" &&
                ModuleBox.Items.Cast<ProductModuleOption>().Any(item => item.Value == "Outfitting") &&
                !ModuleBox.Items.Cast<ProductModuleOption>().Any(item => item.Value.Equals("Design", StringComparison.OrdinalIgnoreCase)),
                "changing product updates AM modules and entry kind");
            ModuleBox.SelectedItem = null; ModuleBox.Text = "Design"; var beforeInvalid = _launchProfiles.Read().Revision;
            Click(SaveProfileButton);
            Check(_launchProfiles.Read().Revision == beforeInvalid && LaunchLifecycleMessage.Text.Contains("module", StringComparison.OrdinalIgnoreCase),
                "AM Design mismatch is rejected without writing");
            ModuleBox.SelectedValue = "Outfitting"; Click(SaveProfileButton);
            Check(_selected?.Product == "AM" && _launchProfiles.Read().Value.Profiles.Count == 2, "AM profile saves with its native module value");
            var amId = _selected!.Id;
            Click(LaunchFavoriteButton);
            LaunchProductFilter.SelectedItem = "AM"; LaunchScopeFilter.SelectedIndex = 1; LaunchProfileSearch.Text = "舾装";
            Check(LaunchProfilesGrid.Items.Count == 1 && ((LaunchProfile)LaunchProfilesGrid.Items[0]).Id == amId,
                "product dropdown favorite dropdown and text search combine");
            LaunchProfileSearch.Text = "no-match";
            Check(LaunchProfilesGrid.Items.Count == 0 && LaunchProfilesEmpty.Visibility == Visibility.Visible, "empty filter state is visible");
            LaunchProfileSearch.Clear(); LaunchScopeFilter.SelectedIndex = 0; LaunchProductFilter.SelectedItem = "全部产品";
            Width = 1600; Height = 1040; Tabs.SelectedIndex = 0; LaunchScroll.ScrollToTop();
            PageSubtitleText.Text = "隔离数据的真实 WPF 流程验证 · 未启动 AVEVA";
            await CaptureSmokeAsync(Path.Combine(outputDirectory, "01-am-workbench.png"));

            LaunchProfilesGrid.SelectedItem = _settings.Profiles.Single(profile => profile.Id == e3dId);
            var external = new LaunchProfileService(); var current = external.ReadProfile(e3dId);
            current.Profile.Name = "E3D · 外部已修改";
            external.Save(current.Profile, current.Revision, true, "UI 验证中的第二编辑者");
            MdbBox.Text = "/UNSAVED_UI"; Click(SaveProfileButton);
            Check(MdbBox.Text == "/UNSAVED_UI" && LaunchLifecycleMessage.Text.Contains("修改", StringComparison.Ordinal) &&
                external.ReadProfile(e3dId).Profile.Mdb == "/REVIEW", "stale save keeps draft and refuses to overwrite the second editor");
            Click(ReloadProfileButton);
            Check(ReloadConfirmationPanel.Visibility == Visibility.Visible && MdbBox.Text == "/UNSAVED_UI", "reload asks before discarding an unsaved draft");
            await CaptureSmokeAsync(Path.Combine(outputDirectory, "02-conflict-keeps-draft.png"));
            Click(ConfirmReloadProfileButton);
            Check(NameBox.Text == "E3D · 外部已修改" && MdbBox.Text == "/REVIEW" && !IsLaunchDraftDirty(), "confirmed reload loads the current revision");

            LaunchHistoryGrid.SelectedItem = LaunchHistoryGrid.Items.Cast<ConfigHistoryEntry>().Single(entry => entry.Revision == initialRevision);
            Click(PreviewRestoreProfileButton);
            Check(LaunchChangesBox.Text.Contains("/ALL", StringComparison.Ordinal), "history selection previews restoration differences");
            var beforeRestore = _profileRevision; Click(RestoreProfileButton);
            Check(RestoreConfirmationPanel.Visibility == Visibility.Visible && _launchProfiles.Read().Revision == beforeRestore,
                "restore preview awaits an explicit confirmation");
            Click(ConfirmRestoreProfileButton);
            Check(MdbBox.Text == "/ALL" && _profileRevision != beforeRestore && _profileRevision != initialRevision &&
                _launchProfiles.History(e3dId).Any(entry => entry.Revision == beforeRestore),
                "confirmed restore creates a new revision and preserves later history");
            Check(_launchProfiles.ReadProfile(amId).Profile.Product == "AM", "restore preserves the other product profile");
            LaunchScroll.ScrollToTop(); LaunchChangesExpander.IsExpanded = false;
            foreach (var theme in new[] { "Light", "Dark" })
            {
                ChooseTheme(theme == "Light" ? LightThemeButton : DarkThemeButton, new());
                await CaptureSmokeAsync(Path.Combine(outputDirectory, "03-restored-" + theme + ".png"));
            }
            LaunchScroll.ScrollToBottom();
            await CaptureSmokeAsync(Path.Combine(outputDirectory, "04-history.png"));
            var reopened = new MainWindow { _launchSmokeRunning = true };
            try
            {
                reopened.Show(); reopened._timer.Stop(); reopened.SelectLaunchProfile(reopened._settings.Profiles.Single(profile => profile.Id == e3dId));
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(reopened.MdbBox.Text == "/ALL" && reopened._settings.Profiles.Count == 2 && reopened.LaunchHistoryGrid.Items.Count > 1,
                    "a newly opened workbench reads saved profiles and their history");
                await reopened.CaptureSmokeAsync(Path.Combine(outputDirectory, "05-reopened-window.png"));
            }
            finally { reopened.Close(); }
            Receipt("Passed");
        }
        catch (Exception error) { Receipt("Failed", error); throw; }
    }
}
