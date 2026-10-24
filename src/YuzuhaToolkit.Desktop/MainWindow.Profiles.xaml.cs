using System.IO;
using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>v08 启动配置库；复用工作区持久化与现有启动编辑器。</summary>
public partial class MainWindow
{
    private string _profileScope = "all";
    private bool _refreshingProfileLibrary;

    private void OpenProfileLibrary(object sender, RoutedEventArgs e)
    {
        RefreshProfileLibrary(); Tabs.SelectedIndex = 8; SetPage("启动配置");
    }

    private void RefreshProfileLibrary()
    {
        if (_settings is null || ProfileLibraryGrid is null || ProfileLibrarySearch is null || ProfileProductFilter is null || _refreshingProfileLibrary) return;
        _refreshingProfileLibrary = true;
        try
        {
            var selected = (ProfileLibraryGrid.SelectedItem as LaunchProfile)?.Id;
            var product = ProfileProductFilter.SelectedItem as string ?? "全部产品";
            var products = _settings.Profiles.Select(profile => profile.Product).Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).Prepend("全部产品").ToArray();
            if (!ProfileProductFilter.Items.Cast<string>().SequenceEqual(products))
            {
                ProfileProductFilter.ItemsSource = products;
                ProfileProductFilter.SelectedItem = products.Contains(product) ? product : "全部产品";
                product = (string)ProfileProductFilter.SelectedItem;
            }
            var rows = LaunchProfileCatalog.Select(_settings.Profiles, ProfileLibrarySearch.Text, product == "全部产品" ? null : product, _profileScope);
            ProfileLibraryGrid.ItemsSource = rows;
            ProfileLibraryGrid.SelectedItem = rows.FirstOrDefault(profile => profile.Id == selected) ?? rows.FirstOrDefault();
            ProfileLibraryCount.Text = $"已保存 {_settings.Profiles.Count} 套配置 · 当前显示 {rows.Length} 套";
            ProfileLibraryEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfileLibraryEmpty.Text = _settings.Profiles.Count == 0 ? "还没有启动配置。新建一套，或从启动工程页选择本机程序入口。" : "没有匹配配置。请调整关键词、产品或收藏筛选。";
        }
        finally { _refreshingProfileLibrary = false; }
        ShowProfileLibrarySelection();
        RefreshLaunchProfileList();
    }

    private void ProfileLibraryFilterChanged(object sender, RoutedEventArgs e) => RefreshProfileLibrary();
    private void ChooseProfileScope(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string scope }) return;
        _profileScope = scope;
        foreach (var button in new[] { AllProfilesButton, FavoriteProfilesButton, RecentProfilesButton })
        {
            var selected = Equals(button.Tag, scope);
            button.SetResourceReference(BackgroundProperty, selected ? "Surface.Selected" : "Surface.Card");
            button.SetResourceReference(ForegroundProperty, selected ? "Text.Brand" : "Text.Secondary");
        }
        RefreshProfileLibrary();
    }

    private LaunchProfile? LibrarySelection(object sender) => (sender as FrameworkElement)?.DataContext as LaunchProfile ?? ProfileLibraryGrid.SelectedItem as LaunchProfile;
    private void EditLibraryProfile(object sender, RoutedEventArgs e)
    {
        if (LibrarySelection(sender) is not { } profile) return;
        if (!AllowDiscardLaunchDraft()) return;
        SelectLaunchProfile(profile); Tabs.SelectedIndex = 0; NameBox.Focus();
    }

    private void DuplicateLibraryProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        if (LibrarySelection(sender) is not { } profile) return;
        if (!AllowDiscardLaunchDraft()) return;
        var copy = _launchProfiles.Duplicate(profile.Id, _profileRevision, true);
        ReloadLaunchSnapshot(copy.Profile.Id);
        ProfileLibraryGrid.SelectedItem = _settings.Profiles.FirstOrDefault(item => item.Id == copy.Profile.Id);
        LifecycleMessage("已保存独立副本并记录修订。副本不复制密码，请单独设置本机凭据。");
    });

    private void ToggleFavoriteProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        if (LibrarySelection(sender) is not { } profile) return;
        if (!AllowDiscardLaunchDraft()) return;
        ApplyProfileCommit(_launchProfiles.SetFavorite(profile.Id, !profile.IsFavorite, _profileRevision, true), _selected?.Id);
    });

    private void SetDefaultProfile(object sender, RoutedEventArgs e) => RunLaunchAction(() =>
    {
        if (LibrarySelection(sender) is not { } profile) return;
        if (!AllowDiscardLaunchDraft()) return;
        ApplyProfileCommit(_launchProfiles.SetDefault(profile.Id, _profileRevision, true), _selected?.Id);
        LifecycleMessage("默认配置已更新；下次打开工作台时优先选中，不会自动启动工程。");
    });

    private void DeleteLibraryProfile(object sender, RoutedEventArgs e) => Try(() =>
    {
        if (LibrarySelection(sender) is not { } profile) return;
        if (!AllowDiscardLaunchDraft()) return;
        var dialog = new ProfileDeleteDialog(profile, _settings.Profiles.Count - 1) { Owner = this };
        if (dialog.ShowDialog() == true) DeleteSavedProfile(profile);
    });

    private void DeleteSavedProfile(LaunchProfile profile)
    {
        var selected = _selected?.Id == profile.Id ? null : _selected?.Id;
        ApplyProfileCommit(_launchProfiles.Delete(profile.Id, _profileRevision, true), selected);
        LifecycleMessage("已删除本机配置并保留历史。工程文件和运行中的会话保持原状。");
        if (profile.CredentialKey.Length > 0 && !_settings.Profiles.Any(item => item.CredentialKey == profile.CredentialKey))
        {
            try { WindowsCredentialStore.Delete(profile.CredentialKey); }
            catch (Exception error) { Say("配置已删除；解除本机凭据失败：" + error.Message); }
        }
    }

    private void ProfileLibrarySelected(object sender, SelectionChangedEventArgs e) => ShowProfileLibrarySelection();
    private void ShowProfileLibrarySelection()
    {
        if (ProfileLibraryDetailTitle is null) return;
        var profile = ProfileLibraryGrid.SelectedItem as LaunchProfile;
        ProfileLibraryDetailTitle.Text = profile?.Name ?? "选择一套配置";
        ProfileLibraryDetail.Text = profile is null ? "查看程序入口、项目和最近启动记录。" :
            $"{profile.Product} · {profile.Project} · {profile.Module}\n\n程序入口\n{profile.Executable}\n\n工作目录\n{profile.WorkingDirectory}\n\n最近创建启动入口\n{(profile.LastStartedUtc is { } at ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "尚无记录")}\n\n{(profile.IsDefault ? "默认配置" : "普通配置")} · {(profile.IsFavorite ? "已收藏" : "未收藏")}";
        foreach (var button in new[] { ProfileEditButton, ProfileDuplicateButton, ProfileDefaultButton, ProfileFavoriteButton, ProfileDeleteButton }) button.IsEnabled = profile is not null;
        ProfileFavoriteButton.Content = profile?.IsFavorite == true ? "取消收藏" : "收藏配置";
    }

    private void VerifyProfileLibrarySmoke()
    {
        MarkLaunchDraftClean();
        var original = _settings.Profiles[0];
        RefreshProfileLibrary(); ProfileLibraryGrid.SelectedItem = original;
        ToggleFavoriteProfile(this, new()); SetDefaultProfile(this, new());
        var saved = WorkspaceSettings.Load().Profiles.Single(profile => profile.Id == original.Id);
        if (!saved.IsFavorite || !saved.IsDefault) throw new InvalidOperationException("配置收藏或默认项没有持久保存。");
        DuplicateLibraryProfile(this, new());
        var copy = _settings.Profiles.Single(profile => profile.Id != original.Id);
        if (copy.CredentialKey.Length != 0 || copy.IsDefault) throw new InvalidOperationException("配置副本继承了凭据或默认标记。");
        ProfileLibrarySearch.Text = "不存在的配置";
        if (ProfileLibraryGrid.Items.Count != 0 || ProfileLibraryEmpty.Visibility != Visibility.Visible) throw new InvalidOperationException("配置库空结果没有显示。");
        ProfileLibrarySearch.Text = copy.Name;
        if (ProfileLibraryGrid.Items.Count != 1) throw new InvalidOperationException("配置搜索没有定位独立副本。");
        var confirmation = new ProfileDeleteDialog(copy, _settings.Profiles.Count - 1);
        confirmation.ConfirmationBox.Text = copy.Name + "x";
        if (confirmation.DeleteButton.IsEnabled) throw new InvalidOperationException("错误的配置名称仍允许删除。");
        confirmation.ConfirmationBox.Text = copy.Name;
        if (!confirmation.DeleteButton.IsEnabled) throw new InvalidOperationException("完整配置名称未解除删除确认。");
        confirmation.Close(); DeleteSavedProfile(copy);
        if (WorkspaceSettings.Load().Profiles.Any(profile => profile.Id == copy.Id)) throw new InvalidOperationException("被删除的配置仍残留在持久设置。");
        ProfileLibrarySearch.Clear(); ProfilesBox.SelectedItem = _settings.Profiles.Single(profile => profile.Id == original.Id); NameBox.Text = "未保存的离线预览草稿";
    }
}
