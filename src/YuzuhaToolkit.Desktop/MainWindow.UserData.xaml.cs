using System.IO;
using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>统一用户目录、个人笔记与资料库管理的本机界面。</summary>
public partial class MainWindow
{
    private UserDataMigration? directoryMigration;
    private PersonalNote? editingNote;
    private string? editingNoteDirectory;
    private string? displayedKnowledgeDirectory;
    private bool knowledgeManagementBusy;
    private KnowledgeRepository ManagementRepository() => new(Path.GetFullPath(KnowledgeDirectoryBox.Text));
    private void ScrollKnowledgeTo(FrameworkElement element) => KnowledgeScroll.ScrollToVerticalOffset(KnowledgeScroll.VerticalOffset + element.TranslatePoint(new Point(), KnowledgeScroll).Y - 85);
    private void OpenPersonalNotes(object sender, RoutedEventArgs e) { KnowledgePages.SelectedIndex = 1; KnowledgeScroll.ScrollToTop(); }
    private void OpenKnowledgeLibraries(object sender, RoutedEventArgs e) { KnowledgePages.SelectedIndex = 2; KnowledgeScroll.ScrollToTop(); }
    private void UserDirectoryChanged(object sender, TextChangedEventArgs e) { directoryMigration = null; if (ApplyUserDirectoryButton is not null) ApplyUserDirectoryButton.IsEnabled = false; }
    private void BrowseUserDirectory(object sender, RoutedEventArgs e) { var selected = Folder(); if (selected is not null) UserDirectoryBox.Text = selected; }
    private void RecommendUserDirectory(object sender, RoutedEventArgs e) => UserDirectoryBox.Text = UserDataPaths.RecommendedRoot;
    private async void PreviewUserDirectory(object sender, RoutedEventArgs e)
    {
        directoryMigration = null; ApplyUserDirectoryButton.IsEnabled = false;
        try
        {
            var root = UserDirectoryBox.Text.Trim(); UserDirectoryStatus.Text = "正在核对旧数据与目标文件…";
            var plan = await Task.Run(() => UserDataMigrationService.Preview(root));
            if (!string.Equals(root, UserDirectoryBox.Text.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            directoryMigration = plan;
            UserDirectoryStatus.Text = $"目标：{plan.Root}\n复制/核对 {plan.Files.Length} 个文件；旧数据保留。\n" + string.Join("\n", plan.Files.Take(20).Select(file => file.Source + " → " + file.Target))
                + (plan.Files.Length > 20 ? "\n其余文件按相同目录映射复制。" : "") + "\n切换前请关闭知识 CLI/MCP 和 HTML 宿主，避免继续写入旧目录。";
            ApplyUserDirectoryButton.IsEnabled = true;
        }
        catch (Exception error) { UserDirectoryStatus.Text = error.Message; }
    }
    private async void ApplyUserDirectory(object sender, RoutedEventArgs e)
    {
        if (directoryMigration is not { } plan) return;
        if (MessageBox.Show($"将核验并复制 {plan.Files.Length} 个文件到 {plan.Root}，保留旧目录，然后切换设置。\n请确认已关闭使用旧数据的知识服务和 HTML 宿主。", "切换用户目录", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        ApplyUserDirectoryButton.IsEnabled = false;
        try
        {
            await Task.Run(() => UserDataMigrationService.Apply(plan));
            _settings.UserDataRoot = plan.Root; _settings.KnowledgeDirectory = "";
            KnowledgeDirectoryBox.Text = UserDataPaths.KnowledgeDirectory(); directoryMigration = null;
            UserDirectoryStatus.Text = "切换完成，旧文件保留。知识库：" + KnowledgeDirectoryBox.Text + "\n应用目录：" + UserDataPaths.AppsDirectory + "\nUserPmlLib 尚需按实际 AVEVA 环境登记，不会自动修改 EVAR。";
            NewPersonalNote(this, new()); await RefreshKnowledgeManagementAsync();
        }
        catch (Exception error) { UserDirectoryStatus.Text = "未切换：" + error.Message; }
    }
    private async Task RefreshKnowledgeManagementAsync()
    {
        if (knowledgeManagementBusy) return;
        knowledgeManagementBusy = true; KnowledgeManagementPanel.IsEnabled = false;
        try
        {
            var repository = ManagementRepository(); var deleted = IncludeDeletedNotesBox.IsChecked == true;
            var result = await Task.Run(() => (Notes: repository.ListNotes(deleted), Libraries: repository.ListLibraries(), Trash: repository.ArchivedLibraries()));
            if (!string.Equals(repository.DefaultDirectory, Path.GetFullPath(KnowledgeDirectoryBox.Text), StringComparison.OrdinalIgnoreCase)) return;
            displayedKnowledgeDirectory = repository.DefaultDirectory;
            PersonalNotesGrid.ItemsSource = result.Notes; KnowledgeLibrariesGrid.ItemsSource = result.Libraries; KnowledgeTrashGrid.ItemsSource = result.Trash;
            KnowledgeManagementStatus.Text = $"{result.Notes.Length} 条个人笔记 · {result.Libraries.Length} 个资料库 · {result.Trash.Length} 个已移除资料库。当前目录：{repository.DefaultDirectory}";
        }
        catch (Exception error) { KnowledgeManagementStatus.Text = "读取失败：" + error.Message; }
        finally { knowledgeManagementBusy = false; KnowledgeManagementPanel.IsEnabled = true; }
    }
    private async void RefreshKnowledgeManagement(object sender, RoutedEventArgs e) => await RefreshKnowledgeManagementAsync();
    private void NewPersonalNote(object sender, RoutedEventArgs e)
    {
        editingNote = null; editingNoteDirectory = null; NoteTitleBox.Clear(); NoteContextBox.Clear(); NoteContentBox.Clear(); NoteHistoryBox.ItemsSource = null; NoteRevisionText.Text = "新笔记";
    }
    private void SelectPersonalNote(object sender, SelectionChangedEventArgs e)
    {
        if (PersonalNotesGrid.SelectedItem is not PersonalNote note || displayedKnowledgeDirectory is null) return;
        editingNote = note; editingNoteDirectory = displayedKnowledgeDirectory; ShowNote(note);
        try { NoteHistoryBox.ItemsSource = new KnowledgeRepository(editingNoteDirectory).NoteHistory(note.Id); }
        catch (Exception error) { KnowledgeManagementStatus.Text = error.Message; }
    }
    private void ShowNote(PersonalNote note) { NoteTitleBox.Text = note.Title; NoteContextBox.Text = note.Context; NoteContentBox.Text = note.Content; NoteRevisionText.Text = $"修订 {note.Revision} · {(note.Deleted ? "回收站" : "当前正文")} · {note.UpdatedUtc}"; }
    private void SelectNoteHistory(object sender, SelectionChangedEventArgs e) { if (NoteHistoryBox.SelectedItem is PersonalNote note) ShowNote(note); }
    private async void SavePersonalNote(object sender, RoutedEventArgs e) => await WritePersonalNoteAsync(false);
    private async void DeletePersonalNote(object sender, RoutedEventArgs e)
    {
        if (editingNote is null || editingNote.Deleted) return;
        if (MessageBox.Show("将此笔记移入回收站？历史版本保留，可恢复。", "移除个人笔记", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes) await WritePersonalNoteAsync(true);
    }
    private async void RestorePersonalNote(object sender, RoutedEventArgs e) { if (editingNote?.Deleted == true) await WritePersonalNoteAsync(false); }
    private async Task WritePersonalNoteAsync(bool deleted)
    {
        if (knowledgeManagementBusy) return;
        KnowledgeRepository repository;
        try { repository = ManagementRepository(); } catch (Exception error) { KnowledgeManagementStatus.Text = error.Message; return; }
        var current = editingNote; var title = NoteTitleBox.Text; var body = NoteContentBox.Text; var context = NoteContextBox.Text;
        if (current is not null && !string.Equals(editingNoteDirectory, repository.DefaultDirectory, StringComparison.OrdinalIgnoreCase))
        { KnowledgeManagementStatus.Text = "知识目录已变化，请刷新并重新选择笔记。编辑内容仍保留。"; return; }
        knowledgeManagementBusy = true; KnowledgeManagementPanel.IsEnabled = false;
        try { editingNote = await Task.Run(() => repository.SaveNote(current?.Id, current?.Revision ?? 0, title, body, context, deleted)); editingNoteDirectory = repository.DefaultDirectory; ShowNote(editingNote); NoteHistoryBox.ItemsSource = repository.NoteHistory(editingNote.Id); }
        catch (Exception error) { KnowledgeManagementStatus.Text = "保存失败：" + error.Message; return; }
        finally { knowledgeManagementBusy = false; KnowledgeManagementPanel.IsEnabled = true; }
        await RefreshKnowledgeManagementAsync();
    }
    private void CopyResultToNote(object sender, RoutedEventArgs e)
    {
        if (KnowledgeResultsGrid.SelectedItem is not KnowledgeBrowseRow row || knowledgeBrowser is null) return;
        try { var detail = knowledgeBrowser.Read(row); NewPersonalNote(this, new()); OpenPersonalNotes(this, new()); NoteTitleBox.Text = row.Title; NoteContentBox.Text = detail.Content; NoteContextBox.Text = "来源：" + detail.SourcePath + "\n" + detail.Verification; }
        catch (Exception error) { KnowledgeManagementStatus.Text = error.Message; }
    }
    private void BrowseKnowledgeSource(object sender, RoutedEventArgs e) { var directory = Folder(); if (directory is not null) KnowledgeSourcePathBox.Text = directory; }
    private async void AddKnowledgeSource(object sender, RoutedEventArgs e)
    {
        var name = KnowledgeSourceNameBox.Text.Trim(); var path = KnowledgeSourcePathBox.Text.Trim();
        var role = KnowledgeSourceRoleBox.SelectedIndex == 1 ? "official" : "custom"; var kind = KnowledgeSourceKindBox.SelectedIndex;
        await ManageLibraryAsync(repository => repository.RegisterSource(role, name, kind == 0 ? path : null, kind == 1 ? path : null, kind == 2 ? path : null, false));
    }
    private async void RebuildKnowledgeLibrary(object sender, RoutedEventArgs e)
    {
        if (KnowledgeLibrariesGrid.SelectedItem is not KnowledgeLibraryRow row) return;
        if (!ManagementSelectionIsCurrent()) return;
        if (MessageBox.Show("按原登记目录重建资料索引？当前索引会先备份，原文保留。", "更新资料索引", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await ManageLibraryAsync(repository => repository.RebuildLibrary(row.Name));
    }
    private async void ArchiveKnowledgeLibrary(object sender, RoutedEventArgs e)
    {
        if (KnowledgeLibrariesGrid.SelectedItem is not KnowledgeLibraryRow row) return;
        if (!ManagementSelectionIsCurrent()) return;
        if (MessageBox.Show("移除资料库“" + row.Name + "”并放入回收站？源文件保留。", "移除资料库", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await ManageLibraryAsync(repository => repository.ArchiveLibrary(row.Name));
    }
    private async void RestoreKnowledgeLibrary(object sender, RoutedEventArgs e)
    {
        if (!ManagementSelectionIsCurrent()) return;
        if (KnowledgeTrashGrid.SelectedItem is ArchivedKnowledge row) await ManageLibraryAsync(repository => { repository.RestoreLibrary(row.Id); return row; });
    }
    private async Task ManageLibraryAsync(Func<KnowledgeRepository, object> action)
    {
        if (knowledgeManagementBusy) return;
        KnowledgeRepository repository;
        try { repository = ManagementRepository(); } catch (Exception error) { KnowledgeManagementStatus.Text = error.Message; return; }
        knowledgeManagementBusy = true; KnowledgeManagementPanel.IsEnabled = false;
        try { KnowledgeManagementStatus.Text = "正在处理本机资料…"; await Task.Run(() => action(repository)); }
        catch (Exception error) { KnowledgeManagementStatus.Text = "操作失败：" + error.Message; return; }
        finally { knowledgeManagementBusy = false; KnowledgeManagementPanel.IsEnabled = true; }
        await RefreshKnowledgeManagementAsync();
    }
    private bool ManagementSelectionIsCurrent()
    {
        try { if (string.Equals(displayedKnowledgeDirectory, ManagementRepository().DefaultDirectory, StringComparison.OrdinalIgnoreCase)) return true; }
        catch (Exception) { }
        KnowledgeManagementStatus.Text = "知识目录已变化，请刷新后重新选择。"; return false;
    }
}
