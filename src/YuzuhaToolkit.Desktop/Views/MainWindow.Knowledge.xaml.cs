using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;
using System.Windows.Input;

namespace YuzuhaToolkit.Desktop;

/// <summary>主窗口的知识检索接线，直接调用现有本机仓库。</summary>
public partial class MainWindow
{
    private DesktopKnowledgeBrowser? knowledgeBrowser;
    private readonly List<KnowledgeHistoryRow> knowledgeHistory = [];
    private void InitializeKnowledge()
    {
        KnowledgeDirectoryBox.Text = YuzuhaToolkit.Workspace.UserDataPaths.KnowledgeDirectory();
        KnowledgeDirectoryBox.Text = Path.GetFullPath(KnowledgeDirectoryBox.Text);
        UserDirectoryBox.Text = YuzuhaToolkit.Workspace.UserDataPaths.Root;
        UserDirectoryRecommendation.Text = "推荐：" + YuzuhaToolkit.Workspace.UserDataPaths.RecommendedRoot;
        _ = RefreshKnowledgeManagementAsync();
    }
    private async void SearchKnowledge(object sender, RoutedEventArgs e) => await SearchKnowledgeAsync();
    private async void KnowledgeQueryKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await SearchKnowledgeAsync(); } }
    private async Task SearchKnowledgeAsync()
    {
        if (!KnowledgeSearchButton.IsEnabled) return;
        KnowledgeSearchButton.IsEnabled = false;
        try
        {
            knowledgeBrowser = null; KnowledgeResultsGrid.ItemsSource = null;
            KnowledgeDetailTitle.Text = "正在查询…"; KnowledgeDetailBody.Clear(); KnowledgeDetailLocation.Text = ""; KnowledgeDetailVerification.Text = "";
            var query = KnowledgeQueryBox.Text.Trim(); var directory = Path.GetFullPath(KnowledgeDirectoryBox.Text);
            var supplemental = KnowledgeSupplementalBox.IsChecked == true;
            var browser = new DesktopKnowledgeBrowser(directory); var clock = Stopwatch.StartNew();
            KnowledgeStatusText.Text = "正在读取本机资料…";
            var rows = await Task.Run(() => browser.Search(query, supplemental));
            knowledgeBrowser = browser; KnowledgeResultsGrid.ItemsSource = rows;
            KnowledgeStatusText.Text = $"本次返回 {rows.Length} 项；搜索不创建或重建数据库。";
            KnowledgeSourcesText.Text = "内置指南始终可用；" + (Directory.Exists(directory) ? $"此目录有 {Directory.EnumerateFiles(directory, "*.sqlite3").Count()} 个本机数据库。" : "当前目录没有数据库，可选择已有资料目录。") + " 下方可管理个人笔记、资料源与回收站。";
            knowledgeHistory.Insert(0, new(DateTime.Now.ToString("HH:mm:ss"), query, rows.Length, directory, clock.ElapsedMilliseconds));
            if (knowledgeHistory.Count > 20) knowledgeHistory.RemoveAt(20);
            KnowledgeHistoryGrid.ItemsSource = knowledgeHistory.ToArray();
            KnowledgeDetailTitle.Text = "选择一个结果查看完整正文"; KnowledgeDetailLocation.Text = ""; KnowledgeDetailBody.Clear();
            if (rows.Length > 0) KnowledgeResultsGrid.SelectedIndex = 0;
        }
        catch (Exception error) { KnowledgeStatusText.Text = "检索失败：" + error.Message; }
        finally { KnowledgeSearchButton.IsEnabled = true; }
    }
    private void KnowledgeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (KnowledgeResultsGrid.SelectedItem is not KnowledgeBrowseRow row || knowledgeBrowser is null) return;
        try
        {
            var detail = knowledgeBrowser.Read(row);
            KnowledgeDetailTitle.Text = row.Title;
            KnowledgeDetailLocation.Text = row.Role + " · " + (row.Database.Length == 0 ? "随程序嵌入" : row.Database + " · chunk " + row.ChunkId) + "\n" + detail.SourcePath + (detail.StartLine is null ? "" : ":" + detail.StartLine);
            KnowledgeDetailBody.Text = detail.Content; KnowledgeDetailVerification.Text = detail.Verification;
        }
        catch (Exception error) { KnowledgeDetailTitle.Text = row.Title; KnowledgeDetailLocation.Text = row.Database + " · chunk " + row.ChunkId; KnowledgeDetailBody.Text = "正文读取失败：" + error.Message; KnowledgeDetailVerification.Text = "失败，不作为可用证据。"; }
    }
    private void CopyKnowledgeLocation(object sender, RoutedEventArgs e) => Try(() => { Clipboard.SetText(KnowledgeDetailLocation.Text); Say("已复制当前资料的来源定位。"); });
    private void BrowseKnowledgeDirectory(object sender, RoutedEventArgs e) { var directory = Folder(); if (directory is not null) KnowledgeDirectoryBox.Text = directory; }
    private void SaveKnowledgeDirectory(object sender, RoutedEventArgs e) => Try(() =>
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YUZUHA_KNOWLEDGE_DIR"))) throw new IOException("当前有 YUZUHA_KNOWLEDGE_DIR 覆盖，请先调整该环境变量。");
        var target = Path.GetFullPath(KnowledgeDirectoryBox.Text);
        PathRules.NoLinks(target);
        if (!string.Equals(target, UserDataPaths.KnowledgeDirectory(), StringComparison.OrdinalIgnoreCase) && UserDataPaths.Contains(UserDataPaths.InstallRoot, target))
            throw new IOException("新的知识目录请选择安装目录之外的位置。");
        var previous = _settings.KnowledgeDirectory; _settings.KnowledgeDirectory = target;
        try { SaveWorkspaceSnapshot("知识库目录设置"); } catch { _settings.KnowledgeDirectory = previous; throw; }
        Say("已保存共用知识库目录；新启动的 CLI 使用同一设置，正在运行的知识服务需重启。资料未自动搬移。");
        _ = RefreshKnowledgeManagementAsync();
    });
    /// <summary>本窗口最近检索的有界显示记录，不冒充持久审计历史。</summary>
    /// <param name="Time">本机显示时间。</param>
    /// <param name="Query">查询词。</param>
    /// <param name="Count">返回项数。</param>
    /// <param name="Directory">本次选择的知识目录。</param>
    /// <param name="Milliseconds">实际检索耗时。</param>
    private sealed record KnowledgeHistoryRow(string Time, string Query, int Count, string Directory, long Milliseconds);
}
