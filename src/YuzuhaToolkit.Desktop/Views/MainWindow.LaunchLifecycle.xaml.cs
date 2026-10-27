using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>正式启动工作台通过同一用例服务编辑配置、比较差异及恢复修订。</summary>
public partial class MainWindow
{
    private readonly LaunchProfileService _launchProfiles = new();
    private string _profileRevision = "";
    private string _draftId = Guid.NewGuid().ToString("N");
    private string _launchBaseline = "";
    private bool _fillingLaunchDraft;
    private bool _refreshingLaunchList;
    private bool _launchLifecycleReady;
    private bool _launchPasswordChanged;
    private bool _launchSmokeRunning;
    private (string ProfileId, string Revision, string ExpectedRevision)? _pendingProfileRestore;
    private string? _pendingReloadProfileId;

    private void InitializeLaunchLifecycle()
    {
        _profileRevision = _launchProfiles.Read().Revision;
        _launchLifecycleReady = true;
        foreach (var field in new[] { NameBox, ExecutableBox, InitializationBox, WorkingBox, ProjectBox, UserBox, LicenseBox })
            field.TextChanged += (_, _) => UpdateLaunchDraftState();
        MdbBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdateLaunchDraftState()));
        ModuleBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdateLaunchDraftState()));
        ProductBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdateLaunchDraftState()));
        StartupPolicyBox.SelectionChanged += (_, _) => UpdateLaunchDraftState();
        RememberPasswordBox.Checked += (_, _) => UpdateLaunchDraftState();
        RememberPasswordBox.Unchecked += (_, _) => UpdateLaunchDraftState();
        PasswordInput.PasswordChanged += (_, _) => { if (!_fillingLaunchDraft) _launchPasswordChanged = true; UpdateLaunchDraftState(); };
        Closing += ConfirmLaunchDraftOnClose;
        RefreshProductModules(_selected?.Module ?? "", chooseDefault: true);
        MarkLaunchDraftClean(); RefreshLaunchProfileList(); LoadLaunchHistory();
    }
    private void ConfirmLaunchDraftOnClose(object? sender, CancelEventArgs e)
    {
        if (!_launchSmokeRunning && !AllowDiscardLaunchDraft()) e.Cancel = true;
    }
    private string LaunchDraftFingerprint() => JsonSerializer.Serialize(ReadProfile());
    private bool IsLaunchDraftDirty() => _launchLifecycleReady && !_fillingLaunchDraft &&
        (_launchPasswordChanged || _launchBaseline != LaunchDraftFingerprint());
    private bool AllowDiscardLaunchDraft() => !IsLaunchDraftDirty() || MessageBox.Show(this,
        "当前启动配置有未保存的更改。继续会放弃这些更改，已保存配置保持原样。", "放弃未保存的更改？",
        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    private void MarkLaunchDraftClean()
    {
        if (!_launchLifecycleReady) return;
        _launchPasswordChanged = false; _launchBaseline = LaunchDraftFingerprint(); UpdateLaunchDraftState();
    }
    private void UpdateLaunchDraftState()
    {
        if (!_launchLifecycleReady || _fillingLaunchDraft) return;
        LaunchDraftState.Text = IsLaunchDraftDirty() ? "有未保存的更改 · 切换配置前可先保存或放弃。" :
            _selected is null ? "新配置草稿 · 尚未写入文件。" : "已读取保存的配置 · 编辑后预览差异再保存。";
    }
    private void ProductIdentityChanged(object sender, RoutedEventArgs e)
    {
        if (!_launchLifecycleReady || _fillingLaunchDraft) return;
        if (ReferenceEquals(sender, ProductBox))
        {
            _fillingLaunchDraft = true;
            try
            {
                if (e is SelectionChangedEventArgs { AddedItems.Count: > 0 } changed && changed.AddedItems[0] is ComboBoxItem item)
                    ProductBox.Text = item.Content?.ToString() ?? "";
                KindBox.SelectedIndex = ProductModuleCatalog.NormalizeProduct(ProductBox.Text) == "AM" ? 1 : 0;
            }
            finally { _fillingLaunchDraft = false; }
        }
        RefreshProductModules(ReadModuleValue(), chooseDefault: true); UpdateLaunchDraftState();
    }
    private string ReadModuleValue() => ModuleBox.SelectedItem is ProductModuleOption option &&
        (ModuleBox.Text == option.Label || ModuleBox.Text.Equals(option.Value, StringComparison.OrdinalIgnoreCase)) ? option.Value : ModuleBox.Text.Trim();
    private void RefreshProductModules(string preferred, bool chooseDefault)
    {
        if (!_launchLifecycleReady || _fillingLaunchDraft) return;
        var modules = _launchProfiles.Validate(ReadProfile()).Modules;
        _fillingLaunchDraft = true;
        try
        {
            ModuleBox.ItemsSource = modules.Options;
            var selected = modules.Options.FirstOrDefault(option => option.Value.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (selected is null && chooseDefault) selected = modules.Options.FirstOrDefault();
            ModuleBox.SelectedItem = selected;
            if (selected is null) ModuleBox.Text = preferred;
            ModuleSourceText.Text = string.IsNullOrWhiteSpace(modules.Warning) ? "模块来源：" + modules.Source : modules.Warning;
        }
        finally { _fillingLaunchDraft = false; }
    }
    private void LifecycleMessage(string message, bool error = false)
    {
        LaunchLifecycleMessage.Text = message;
        LaunchLifecycleMessage.SetResourceReference(TextBlock.ForegroundProperty, error ? "Text.Danger" : "Text.Primary"); Say(message);
    }
    private void RunLaunchAction(Action action)
    {
        try { action(); }
        catch (Exception error) { LifecycleMessage(error.Message + "\n编辑内容已保留。配置冲突时请先重新读取当前版本。", true); }
    }
    private void PreviewProfileChanges(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
        ShowProfilePreview(_launchProfiles.PreviewSave(ReadProfile(), _profileRevision)));
    private void ShowProfilePreview(LaunchProfilePreview preview)
    {
        LaunchChangesBox.Text = preview.Changes.Length == 0 ? "没有配置字段变化。" :
            string.Join("\n\n", preview.Changes.Select(change => $"{change.Path}\n  原值：{change.Before}\n  新值：{change.After}"));
        LaunchChangesExpander.IsExpanded = true;
        LifecycleMessage(preview.Validation.IsValid ? "校验通过。" + preview.Effect :
            string.Join("\n", preview.Validation.Issues.Select(issue => $"{issue.Field}：{issue.Message}")), !preview.Validation.IsValid);
    }
    private void ApplyProfileCommit(ConfigCommit<WorkspaceSettings> commit, string? selectedId)
    {
        _pendingProfileRestore = null; RestoreConfirmationPanel.Visibility = Visibility.Collapsed;
        _settings = commit.Snapshot.Value; _profileRevision = commit.Snapshot.Revision;
        _fillingLaunchDraft = true;
        try { ReloadProfiles(); } finally { _fillingLaunchDraft = false; }
        SelectLaunchProfile(_settings.Profiles.FirstOrDefault(profile => profile.Id == selectedId));
        LifecycleMessage(commit.Effect);
    }
    private void SaveWorkspaceSnapshot(string reason)
    {
        var selectedId = _selected?.Id;
        var commit = new WorkspaceConfigurationStore().Commit(_settings, _profileRevision, new(reason));
        _settings = commit.Snapshot.Value; _profileRevision = commit.Snapshot.Revision;
        _selected = _settings.Profiles.FirstOrDefault(profile => profile.Id == selectedId);
        // Directory settings share the document revision, but must not reload or discard the launch editor's draft.
        LoadLaunchHistory();
    }
    private void SelectLaunchProfile(LaunchProfile? profile)
    {
        _pendingReloadProfileId = null; ReloadConfirmationPanel.Visibility = Visibility.Collapsed;
        _pendingProfileRestore = null; RestoreConfirmationPanel.Visibility = Visibility.Collapsed;
        _selected = profile; _fillingLaunchDraft = true;
        try { ProfilesBox.SelectedItem = profile; LaunchProfilesGrid.SelectedItem = profile; }
        finally { _fillingLaunchDraft = false; }
        FillProfileFields(profile ?? new LaunchProfile()); RefreshLaunchProfileList(); LoadLaunchHistory();
    }
    private void ReloadProfileFromDisk(object sender, RoutedEventArgs e)
    {
        if (IsLaunchDraftDirty())
        {
            _pendingReloadProfileId = _selected?.Id;
            ReloadConfirmationPanel.Visibility = Visibility.Visible;
            LifecycleMessage("重新读取会放弃当前未保存的草稿。请确认后继续。");
            return;
        }
        RunLaunchAction(() => ReloadLaunchSnapshot(_selected?.Id));
    }
    private void ConfirmReloadProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        ReloadLaunchSnapshot(_pendingReloadProfileId); ReloadConfirmationPanel.Visibility = Visibility.Collapsed;
    });
    private void CancelReloadProfile(object sender, RoutedEventArgs e) => ReloadConfirmationPanel.Visibility = Visibility.Collapsed;
    private void ReloadLaunchSnapshot(string? selectedId)
    {
        _pendingProfileRestore = null; RestoreConfirmationPanel.Visibility = Visibility.Collapsed;
        var snapshot = _launchProfiles.Read(); _settings = snapshot.Value; _profileRevision = snapshot.Revision;
        _fillingLaunchDraft = true;
        try { ReloadProfiles(); } finally { _fillingLaunchDraft = false; }
        SelectLaunchProfile(_settings.Profiles.FirstOrDefault(profile => profile.Id == selectedId));
        LifecycleMessage("已从配置文件重新读取。" + snapshot.FilePath);
    }
    private void RefreshLaunchProfileList()
    {
        if (!_launchLifecycleReady || _refreshingLaunchList) return;
        _refreshingLaunchList = true;
        try
        {
            var product = LaunchProductFilter.SelectedItem as string ?? "全部产品";
            var products = _settings.Profiles.Select(profile => profile.Product).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).Prepend("全部产品").ToArray();
            LaunchProductFilter.ItemsSource = products; LaunchProductFilter.SelectedItem = products.Contains(product) ? product : "全部产品";
            var scope = (LaunchScopeFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
            var query = LaunchProfileSearch.Text.Trim();
            var rows = LaunchProfileCatalog.Select(_settings.Profiles, "", product == "全部产品" ? null : product, scope)
                .Where(profile => query.Length == 0 || new[] { profile.Name, profile.Project, profile.Module, profile.User, profile.Executable }
                    .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            LaunchProfilesGrid.ItemsSource = rows; LaunchProfilesGrid.SelectedItem = rows.FirstOrDefault(profile => profile.Id == _selected?.Id);
            LaunchProfileCount.Text = $"显示 {rows.Length} / {_settings.Profiles.Count} 套配置";
            LaunchProfilesEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            LaunchFavoriteButton.Content = _selected?.IsFavorite == true ? "取消收藏" : "收藏";
        }
        finally { _refreshingLaunchList = false; }
    }
    private void LaunchProfileFilterChanged(object sender, RoutedEventArgs e) => RefreshLaunchProfileList();
    private void LaunchProfileGridSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_launchLifecycleReady || _refreshingLaunchList || _fillingLaunchDraft || LaunchProfilesGrid.SelectedItem is not LaunchProfile profile || profile.Id == _selected?.Id) return;
        if (!AllowDiscardLaunchDraft()) { RefreshLaunchProfileList(); return; }
        SelectLaunchProfile(profile);
    }
    private void DuplicateLaunchProfile(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        ProfileLibraryGrid.SelectedItem = _settings.Profiles.FirstOrDefault(profile => profile.Id == _selected.Id); DuplicateLibraryProfile(this, e);
    }
    private void FavoriteLaunchProfile(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        ProfileLibraryGrid.SelectedItem = _settings.Profiles.FirstOrDefault(profile => profile.Id == _selected.Id); ToggleFavoriteProfile(this, e);
    }
    private void DefaultLaunchProfile(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        ProfileLibraryGrid.SelectedItem = _settings.Profiles.FirstOrDefault(profile => profile.Id == _selected.Id); SetDefaultProfile(this, e);
    }
    private void RefreshLaunchHistory(object sender, RoutedEventArgs e) => RunLaunchAction(LoadLaunchHistory);
    private void LoadLaunchHistory()
    {
        if (!_launchLifecycleReady) return;
        var history = _selected is null ? [] : _launchProfiles.History(_selected.Id);
        LaunchHistoryGrid.ItemsSource = history; LaunchHistoryEmpty.Visibility = history.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        LaunchRevisionText.Text = (_selected?.Name ?? "未保存草稿") + " · 编辑基于修订 " + _profileRevision + "\n" + _launchProfiles.Read().FilePath;
        UpdateHistoryActions();
    }
    private void LaunchHistorySelected(object sender, SelectionChangedEventArgs e) => UpdateHistoryActions();
    private void UpdateHistoryActions()
    {
        if (PreviewRestoreProfileButton is null) return;
        PreviewRestoreProfileButton.IsEnabled = _selected is not null && LaunchHistoryGrid.SelectedItem is ConfigHistoryEntry;
        RestoreProfileButton.IsEnabled = PreviewRestoreProfileButton.IsEnabled;
    }
    private void PreviewRestoreProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        if (_selected is null || LaunchHistoryGrid.SelectedItem is not ConfigHistoryEntry revision) return;
        ShowProfilePreview(_launchProfiles.PreviewRestore(_selected.Id, revision.Revision, _profileRevision)); LaunchScroll.ScrollToVerticalOffset(0);
    });
    private void RestoreProfileRevision(object sender, RoutedEventArgs e)
    {
        if (_selected is null || LaunchHistoryGrid.SelectedItem is not ConfigHistoryEntry revision || !AllowDiscardLaunchDraft()) return;
        var id = _selected.Id;
        RunLaunchAction(() =>
        {
            if (IsLaunchDraftDirty()) ReloadLaunchSnapshot(id);
            var preview = _launchProfiles.PreviewRestore(id, revision.Revision, _profileRevision); ShowProfilePreview(preview);
            if (!preview.Validation.IsValid) return;
            _pendingProfileRestore = (id, revision.Revision, _profileRevision);
            RestoreConfirmationText.Text = "确认将“" + _selected.Name + "”恢复为所选内容？将创建新修订，原历史保留，工程不会启动。\n\n" + LaunchChangesBox.Text;
            RestoreConfirmationPanel.Visibility = Visibility.Visible;
            RestoreConfirmationPanel.BringIntoView();
        });
    }
    private void ConfirmRestoreProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        if (_pendingProfileRestore is not { } pending) return;
        if (IsLaunchDraftDirty()) throw new InvalidOperationException("配置草稿已修改，请先保存或重新读取，再重新预览恢复。");
        ApplyProfileCommit(_launchProfiles.Restore(pending.ProfileId, pending.Revision, pending.ExpectedRevision, true), pending.ProfileId);
        LifecycleMessage("已恢复为新的修订。若历史凭据已被删除，请重新输入密码。");
    });
    private void CancelRestoreProfile(object sender, RoutedEventArgs e)
    {
        _pendingProfileRestore = null; RestoreConfirmationPanel.Visibility = Visibility.Collapsed;
    }
}
