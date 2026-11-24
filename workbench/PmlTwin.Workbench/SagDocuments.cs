using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Knowledge;
using Core30.Workbench;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private async Task BrowseSagDocuments()
    {
        if (_sagSource is not { } source) return;
        try
        {
            var library = SagLibrary(_sagExe);
            var documents = await Task.Run(() => library.Browse(source));
            var filter = Input("", "SagDocumentFilter");
            var list = Table(Array.Empty<SagSearchHit>(), "SagDocumentList", ("名称", "Title", 1),
                ("路径", "RelativePath", 2.25));
            list.Height = 345;
            var count = Muted("");
            var notes = source.Kind == SagSourceKind.Snippets;
            void Filter()
            {
                var text = filter.Text.Trim();
                var rows = documents.Where(hit => hit.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    hit.RelativePath.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
                list.ItemsSource = rows;
                list.SelectedItem = rows.FirstOrDefault();
                count.Text = $"{rows.Length:N0} / {documents.Count:N0} 份资料";
            }
            filter.TextChanged += (_, _) => Filter();
            Filter();
            var open = Modal(SagSourceLabel(source), Stack(Prompt(filter, "按文件名或路径筛选"), Below(count, 7), Below(list, 8)),
                notes ? "编辑切片" : "打开原文", () => list.SelectedItem is SagSearchHit, filter, width: 830);
            if (open && list.SelectedItem is SagSearchHit selected) OpenSagOriginal(selected);
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private async Task EditSagSnippet(SagSearchHit? existing = null, SagSearchHit? reference = null)
    {
        try
        {
            var directory = Path.Combine(_store.DirectoryPath, "knowledge-snippets");
            var source = new SagSource(directory, SagSourceKind.Snippets);
            var store = new FeatureContext(Directory.CreateDirectory(directory).FullName);
            var file = store.FilePath(existing?.RelativePath ?? Guid.NewGuid().ToString("N") + ".md");
            string initial = "";
            string heading = reference?.Title ?? "";
            if (existing is not null)
            {
                if (!LocalSource.SamePath(existing.Source.Directory, directory))
                    throw new InvalidDataException("此条目不在工作台切片目录。");
                using var read = new StringReader(Encoding.UTF8.GetString(FileContent.ReadBounded(file, 512 * 1024)));
                heading = (read.ReadLine() ?? "").TrimStart('#', ' ');
                initial = read.ReadToEnd().TrimStart('\r', '\n');
            }
            if (reference is not null)
                initial = reference.Snippet + "\n\n来源：" + SagKnowledgeLibrary.OriginalPath(reference) +
                    (reference.Line > 0 ? $"（第 {reference.Line} 行）" : "");
            var title = Input(heading, "SagSnippetTitle");
            title.MaxLength = 200;
            var text = Input(initial, "SagSnippetText");
            text.MaxLength = 60000;
            text.AcceptsReturn = true;
            text.TextWrapping = TextWrapping.Wrap;
            text.Height = 258;
            text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            var notice = new Notice("SagSnippetNotice");
            if (!Modal(existing is null ? "新建切片" : "编辑切片",
                    Stack(Field("标题", title), Below(Field("正文", text), 8), Below(notice, 8)),
                    "保存切片", () =>
                    {
                        var name = title.Text.Trim();
                        if (name.Length == 0 || name.Any(char.IsControl) || string.IsNullOrWhiteSpace(text.Text))
                        {
                            notice.Show("填写单行标题和正文。", "warning");
                            return false;
                        }
                        try
                        {
                            var pending = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            try
                            {
                                File.WriteAllText(pending, "# " + name + "\n\n" + text.Text.Trim() + "\n", new UTF8Encoding(false));
                                File.Move(pending, file, overwrite: true);
                            }
                            finally { File.Delete(pending); }
                            _store.SaveSagSource(source);
                            return true;
                        }
                        catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "danger"); return false; }
                    }, title, 745))
                return;
            _sagSource = source;
            ClearSagResults();
            if (CanUseSag) await ReindexSagSource(source, savedSnippet: true);
            else { _sagMessage = "切片已保存。连接 SAG CLI 后可为它建立索引。"; Render(); }
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private Task ReindexSagSource(SagSource? source, bool savedSnippet = false) =>
        SagWork("正在重建 SAG 索引…", async () =>
        {
            if (source is null) return;
            var library = SagLibrary(_sagExe);
            try
            {
                var result = await Task.Run(() => library.Import(source.Directory, source.Kind));
                _sagIndexes[source] = result.Index;
                ClearSagResults();
                _sagError = result.Index.State is "failed" or "error";
                _sagMessage = (savedSnippet ? "切片已保存 · " : "") + IndexLabel(result.Index);
                if (_sagError && result.Index.Error.Length > 0) _sagMessage += " · " + result.Index.Error;
            }
            catch (Exception error) when (savedSnippet && WorkbenchError(error))
            {
                _sagError = true;
                _sagMessage = "切片已保存，索引未完成 · " + error.Message;
            }
        });
}
