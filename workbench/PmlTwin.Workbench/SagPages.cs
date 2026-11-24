using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Core30.Knowledge;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private const string HtmlImport = "HTML 教程";
    private const string TextImport = "Markdown / TXT";
    private const string PmlImport = "未加密 PML";
    private bool _sagInitialized;
    private bool _sagBusy;
    private bool _sagError;
    private string _sagMessage = "";
    private string _sagExe = "";
    private string _sagVersion = "";
    private (long Size, DateTime Modified) _sagCliStamp;
    private string _sagFormat = HtmlImport;
    private string _sagQuery = "";
    private SagSource? _sagSource;
    private SagSearchHit? _sagHit;
    private SagSearchResult? _sagResult;
    private readonly Dictionary<SagSource, SagIndexState> _sagIndexes = [];

    private FrameworkElement SagPage() => SideBySide(SagSources(), SagSearch(), .8, 720, 14);

    private FrameworkElement SagSources()
    {
        var configure = AsyncBtn("选择 CLI…", "ChooseSagCli", ChooseSagCli);
        var refresh = AsyncBtn("刷新索引状态", "RefreshSagIndexes", RefreshSag, "Button.Quiet");
        var cli = Muted(_sagVersion.Length == 0 ? "本机 SAG CLI" : $"已连接 · {_sagVersion}");
        cli.ToolTip = _sagExe.Length == 0 ? null : _sagExe;
        configure.IsEnabled = refresh.IsEnabled = !_sagBusy && !_pmlScanning;

        var sources = Picker("已导入的目录", "SagSources");
        sources.MaxHeight = 186;
        foreach (var source in _store.Sag.Sources)
        {
            var index = _sagIndexes.GetValueOrDefault(source);
            var row = Row(source, SagSourceLabel(source), $"{source.Format} · {IndexLabel(index)}", "Book", trim: true);
            row.ToolTip = source.Directory;
            sources.Items.Add(row);
            if (source == _sagSource) sources.SelectedItem = row;
        }
        sources.IsEnabled = !_sagBusy;
        sources.SelectionChanged += (_, _) =>
        {
            if (sources.SelectedItem is not ListBoxItem { Tag: SagSource source } || source == _sagSource) return;
            _sagSource = source;
            ClearSagResults();
            if (!_sagError) _sagMessage = "";
            Render();
        };

        var type = Choice(new[] { HtmlImport, TextImport, PmlImport }, _sagFormat, "SagImportFormat");
        type.IsEnabled = !_sagBusy;
        type.SelectionChanged += (_, _) => _sagFormat = type.SelectedItem as string ?? HtmlImport;
        var import = AsyncBtn("导入目录…", "ImportSagDirectory", ImportSag, "Button.Primary");
        import.IsEnabled = CanUseSag;
        import.VerticalAlignment = VerticalAlignment.Bottom;
        import.Margin = new Thickness(8, 0, 0, 0);
        var notice = new Notice("SagIndexNotice");
        if (_sagMessage.Length > 0) notice.Show(_sagMessage, _sagError ? "warning" : "primary");
        var browse = AsyncBtn("浏览资料…", "BrowseSagDocuments", BrowseSagDocuments);
        browse.IsEnabled = CanUseSag && _sagSource is not null;
        var reindex = AsyncBtn("重建所选目录索引", "ReindexSagSource", () => ReindexSagSource(_sagSource));
        reindex.IsEnabled = browse.IsEnabled;
        var snippet = AsyncBtn("新建切片…", "NewSagSnippet", () => EditSagSnippet());
        snippet.IsEnabled = !_sagBusy;
        var content = Stack(Leading(configure, cli, Dock.Right),
            Below<FrameworkElement>(_store.Sag.Sources.Count == 0 ? Muted("导入资料目录或新建切片。") : sources, 10),
            Below(Actions(browse, snippet), 8),
            Below(Leading(import, Field("导入类型", type), Dock.Right), 10),
            Below(Actions(refresh, reindex), 7), Below(notice, 7));
        if (_sagInitialized && _sagExe.Length == 0)
            content.Children.Insert(1, Below(Actions(Jump("安装 SAG…", "Update")), 7));
        return Card("索引目录", content);
    }

    private FrameworkElement SagSearch()
    {
        var ready = _sagSource is { } source && _sagIndexes.GetValueOrDefault(source)?.State == "ready";
        var query = Input(_sagQuery, "SagQuery");
        var submit = AsyncBtn("搜索", "SearchSag", SearchSag, "Button.Primary");
        submit.VerticalAlignment = VerticalAlignment.Bottom;
        submit.Margin = new Thickness(8, 0, 0, 0);
        var results = Picker("正文命中", "SagResults");
        results.MaxHeight = 206;
        var detail = new ContentControl();
        query.IsEnabled = CanUseSag && ready;
        submit.IsEnabled = query.IsEnabled && !string.IsNullOrWhiteSpace(_sagQuery);
        query.TextChanged += (_, _) =>
        {
            _sagQuery = query.Text;
            submit.IsEnabled = query.IsEnabled && !string.IsNullOrWhiteSpace(_sagQuery);
        };
        query.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter || !submit.IsEnabled) return;
            e.Handled = true;
            await SearchSag();
        };

        foreach (var hit in _sagResult?.Hits ?? [])
        {
            var location = hit.Line > 0 ? $"{hit.RelativePath} · 第 {hit.Line} 行" : hit.RelativePath;
            var row = Row(hit, hit.Title, location, trim: true);
            row.MouseDoubleClick += (_, e) =>
            {
                e.Handled = true;
                OpenSagOriginal(hit);
            };
            results.Items.Add(row);
            if (hit == _sagHit) results.SelectedItem = row;
        }
        if (results.SelectedItem is null && results.Items.Count != 0) results.SelectedIndex = 0;

        void ShowHit()
        {
            _sagHit = (results.SelectedItem as ListBoxItem)?.Tag as SagSearchHit;
            detail.Content = _sagHit is { } hit
                ? Stack(Views.Text(hit.Snippet), Below(Actions(
                    Btn(hit.Source.Kind == SagSourceKind.Snippets ? "编辑切片" : "打开原文", "OpenSagOriginal", () => OpenSagOriginal(hit), "Button.Soft"),
                    hit.Source.Kind == SagSourceKind.Snippets ? new Border() :
                        AsyncBtn("另存为切片…", "CopySagSnippet", () => EditSagSnippet(reference: hit))), 8))
                : null;
        }
        results.SelectionChanged += (_, _) => ShowHit();
        ShowHit();
        var summary = _sagResult is null ? "" : _sagResult.Hits.Count == 0 ? "没有匹配的正文。" :
            $"显示 {_sagResult.Hits.Count} 条正文命中" + (_sagResult.Truncated ? " · 可用更具体的关键词缩小结果" : "");
        var scope = Muted(_sagSource is null ? "先导入资料目录。" : $"当前目录 · {SagSourceLabel(_sagSource)} ({_sagSource.Format})");
        scope.ToolTip = _sagSource?.Directory;
        return Card("正文搜索", Stack(scope, Below(Leading(submit, Field("关键词", query), Dock.Right), 8),
            Below(Muted(summary), summary.Length == 0 ? 0 : 8),
            Below<FrameworkElement>(results.Items.Count == 0 ? new Border() : results, 6), Below(detail, 10)));
    }

    private bool CanUseSag => !_sagBusy && !_pmlScanning && _sagVersion.Length > 0;

    private string SagSourceLabel(SagSource source)
    {
        if (_store.Sag.Sources.Count(item => item.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase)) <= 1)
            return source.Name;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source.Directory));
        return parent is null ? source.Directory : $"{Path.GetFileName(parent)} / {source.Name}";
    }

    private SagKnowledgeLibrary SagLibrary(string path) => new(_store.DirectoryPath, path);

    private Task RefreshSag() => SagWork("正在核对 SAG 索引…", async () =>
    {
        _sagInitialized = true;
        var executable = SagKnowledgeLibrary.FindExecutable(_store.Sag.CliPath);
        if (executable is null)
        {
            _sagExe = _sagVersion = "";
            _sagError = _store.SagLoadWarning.Length > 0;
            _sagMessage = _sagError ? _store.SagLoadWarning : "选择已有的 SAG CLI，或前往组件更新安装。";
            return;
        }
        var library = SagLibrary(executable);
        var stamp = SagCliStamp(executable);
        if (!string.Equals(executable, _sagExe, StringComparison.OrdinalIgnoreCase) || _sagVersion.Length == 0 || stamp != _sagCliStamp)
        {
            _sagExe = executable;
            _sagVersion = "";
            _sagVersion = await Task.Run(library.ReadVersion);
            _sagCliStamp = stamp;
        }
        await ReadSagIndexes(library);
        _sagMessage = "";
    });

    private async Task ChooseSagCli()
    {
        var dialog = new OpenFileDialog { Title = "选择现有 SAG CLI", Filter = "SAG CLI|sag.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        await SagWork("正在核对 SAG CLI…", async () =>
        {
            var library = SagLibrary(dialog.FileName);
            var stamp = SagCliStamp(dialog.FileName);
            var version = await Task.Run(library.ReadVersion);
            _store.SelectSagCli(dialog.FileName);
            _sagExe = dialog.FileName;
            _sagVersion = version;
            _sagCliStamp = stamp;
            await ReadSagIndexes(library);
            _sagMessage = "";
            if (_store.PmlScan.Stamp.Length > 0) _pmlScanQueued = true;
        });
    }

    private async Task ReadSagIndexes(SagKnowledgeLibrary library)
    {
        var sources = _store.Sag.Sources.ToArray();
        _sagIndexes.Clear();
        if (sources.Length == 0) return;
        var rows = await Task.Run(() => library.List(sources));
        foreach (var row in rows) _sagIndexes[row.Source] = row;
        _sagSource ??= sources.FirstOrDefault();
    }

    private static (long Size, DateTime Modified) SagCliStamp(string path)
    {
        var file = new FileInfo(path);
        return (file.Length, file.LastWriteTimeUtc);
    }

    private async Task ImportSag()
    {
        var kind = _sagFormat switch { PmlImport => SagSourceKind.Pml, TextImport => SagSourceKind.Text, _ => SagSourceKind.Html };
        var folder = new OpenFolderDialog { Title = $"选择{_sagFormat}目录" };
        if (folder.ShowDialog(this) != true) return;
        await SagWork("正在读取原文件并建立 SAG 索引…", async () =>
        {
            var library = SagLibrary(_sagExe);
            var receipt = await Task.Run(() => library.Import(folder.FolderName, kind));
            var row = receipt.Index;
            _sagIndexes[row.Source] = row;
            _store.SaveSagSource(row.Source);
            if (kind == SagSourceKind.Pml) RememberPmlFolder(row.Source.Directory);
            _sagSource = row.Source;
            ClearSagResults();
            _sagError = row.State is "failed" or "error";
            _sagMessage = IndexLabel(row) + (receipt.Skipped > 0 ? $" · 跳过 {receipt.Skipped} 个无正文、加密或不可读文件" : "");
            if (_sagError && row.Error.Length > 0) _sagMessage += " · " + row.Error;
        });
    }

    private Task SearchSag() => SagWork("正在搜索目录正文…", async () =>
    {
        if (_sagSource is not { } source) return;
        var query = _sagQuery;
        ClearSagResults();
        var library = SagLibrary(_sagExe);
        _sagResult = await Task.Run(() => library.Search(source, query));
        _sagMessage = "";
    });

    private void ClearSagResults()
    {
        _sagResult = null;
        _sagHit = null;
    }

    private async Task SagWork(string message, Func<Task> work)
    {
        if (_sagBusy) return;
        _sagBusy = true;
        _sagError = false;
        _sagMessage = message;
        if (_page.Id == "Sag") Render();
        try { await work(); }
        catch (Exception error) when (WorkbenchError(error))
        {
            _sagError = true;
            _sagMessage = error.Message;
        }
        finally
        {
            _sagBusy = false;
            if (_page.Id == "Sag") Render();
            RunQueuedPmlScan();
        }
    }

    private void OpenSagOriginal(SagSearchHit hit)
    {
        try
        {
            if (hit.Source.Kind == SagSourceKind.Snippets)
            {
                _ = EditSagSnippet(existing: hit);
                return;
            }
            var path = SagKnowledgeLibrary.OriginalPath(hit);
            if (hit.Source.Kind == SagSourceKind.Html)
            {
                OpenSagHtml(hit, path);
                return;
            }
            var content = hit.Source.Kind == SagSourceKind.Pml
                ? Code(SagKnowledgeLibrary.ReadPmlOriginal(hit), "SagPmlOriginal", 353)
                : Code(SagKnowledgeLibrary.ReadTextOriginal(hit), "SagTextOriginal", 353);
            content.TextWrapping = TextWrapping.NoWrap;
            content.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            content.Loaded += (_, _) =>
            {
                var index = hit.Line - 1;
                if (index < 0 || index >= content.LineCount) return;
                content.Select(content.GetCharacterIndexFromLineIndex(index), content.GetLineLength(index));
                content.ScrollToLine(index);
            };
            var location = hit.Line > 0 ? $"{path}\n命中第 {hit.Line} 行" : path;
            Modal(hit.Title, Stack(Muted(location), Below(content, 8)), "完成", () => true, content, width: 830, cancel: false);
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private static string IndexLabel(SagIndexState? index) => index?.State switch
    {
        "ready" => $"已就绪 · {index.Files:N0} 个文件",
        "failed" or "error" => "索引失败",
        "missing" => "请重新导入",
        null => "待核对",
        _ => "正在索引"
    };
}
