using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private static readonly Dictionary<string, string> DocumentFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "HTML", [".htm"] = "HTML", [".pdf"] = "PDF", [".md"] = "Markdown", [".txt"] = "TXT"
    };
    private string _docQuery = "";
    private string _docFormat = "全部格式";
    private string _docId = "";
    private string? _docTarget;

    private FrameworkElement DocumentsPage()
    {
        var search = Input(_docQuery, "DocumentSearch");
        var format = Choice(new[] { "全部格式", "HTML", "PDF", "Markdown", "TXT" }, _docFormat, "DocumentFormat");
        format.Width = 115;
        format.Margin = new Thickness(8, 0, 0, 0);
        AutomationName(format, "按格式筛选");
        var grid = Table(Array.Empty<DocumentEntry>(), "SagDocuments", ("资料", "Name", 2.55), ("格式", "Format", .85), ("状态", "Status", .85));
        grid.MaxHeight = 275;
        var empty = Muted("可添加 HTML、PDF、Markdown 或 TXT。");
        var detail = new ContentControl();
        void ShowSelection()
        {
            if (grid.SelectedItem is not DocumentEntry item)
            {
                detail.Content = Card("检查与部署", Muted("从资料列表选择一项。"));
                return;
            }
            _docId = item.Id;
            var target = Input(_docTarget ?? Data.DefaultLibrary, "DocumentTarget");
            target.TextChanged += (_, _) => _docTarget = target.Text;
            var entries = new List<(string, string)>
            {
                ("文件", item.Name), ("来源", item.Source), ("大小", item.Size < 1024 ? $"{item.Size} B" : $"{item.Size / 1024.0:F1} KB")
            };
            if (item.DeployedAt.HasValue)
            {
                entries.Add(("已部署到", item.Target));
                entries.Add(("部署日期", item.DeployedAt.Value.ToLocalTime().ToString("yyyy-MM-dd")));
            }
            var commands = new List<FrameworkElement>();
            if (item.Format == "PDF" && item.Inspection == "检查通过")
                commands.Add(Btn("打开 PDF", "OpenPdf", () => OpenExternal(_store.DocumentPath(item))));
            commands.Add(Btn("下载原件", "ExportDocument", () => ExportDocument(item)));
            commands.Add(Btn("检查资料", "InspectDocument", () => VerifyDocument(item, true)));
            commands.Add(Primary("部署资料…", "DeployDocument", () => DeployDocument(item, target.Text)));
            var content = Stack(Details(entries));
            if (item.Preview.Length > 0)
            {
                if (item.Inspection == "检查失败")
                    content.Children.Add(Below(Badge(item.Preview, "warning"), 8));
                else
                    content.Children.Add(Below(Btn("查看资料摘要", "DocumentPreview",
                        () => Modal(item.Name, Code(item.Preview, "DocumentText", 248), "完成", () => true, cancel: false)), 6));
            }
            var browse = Btn("选择目录…", "BrowseSagFolder", () =>
            {
                var dialog = new OpenFolderDialog { Title = "选择 SAG 资料目录" };
                if (dialog.ShowDialog(this) == true) target.Text = dialog.FolderName;
            });
            content.Children.Add(Below(Field("目标资料库", Leading(browse, target, Dock.Right)), 8));
            content.Children.Add(Below(Actions(commands.ToArray()), 9));
            detail.Content = Card("检查与部署", content,
                Badge(item.Status, item.DeployedAt.HasValue ? "success" : item.Inspection == "检查失败" ? "danger" : "primary"));
        }
        void Reload()
        {
            var rows = Data.Documents.Where(item =>
                (_docFormat == "全部格式" || item.Format == _docFormat) &&
                $"{item.Name} {item.Target} {item.Source}".Contains(_docQuery.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            var selected = rows.FirstOrDefault(item => item.Id == _docId);
            grid.ItemsSource = rows;
            grid.SelectedItem = selected ?? rows.FirstOrDefault();
            empty.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            empty.Text = Data.Documents.Count > 0 ? "没有符合条件的资料。" : "可添加 HTML、PDF、Markdown 或 TXT。";
            ShowSelection();
        }
        grid.SelectionChanged += (_, _) => ShowSelection();
        search.TextChanged += (_, _) => { _docQuery = search.Text; Reload(); };
        format.SelectionChanged += (_, _) => { _docFormat = (string)format.SelectedItem; Reload(); };
        Reload();
        return SideBySide(Card("资料清单", Stack(Leading(format, Prompt(search, "搜索文件名或资料库"), Dock.Right), Below(grid, 8), Below(empty, 7)),
            Primary("添加资料…", "AddDocuments", AddDocuments)), detail, 1.2, 695);
    }

    private void AddDocuments()
    {
        var dialog = new OpenFileDialog
        {
            Title = "添加 SAG 资料", Filter = "可部署资料|*.html;*.htm;*.pdf;*.md;*.txt|HTML|*.html;*.htm|PDF|*.pdf",
            Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var count = 0;
        var failed = new List<string>();
        foreach (var path in dialog.FileNames)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (!DocumentFormats.TryGetValue(extension, out var format)) { failed.Add(Path.GetFileName(path)); continue; }
            try
            {
                var id = Guid.NewGuid().ToString("N");
                var stored = _store.StoreDocument(path, id, extension);
                var entry = new DocumentEntry(id, Path.GetFileName(path), stored, format, "本地资料", new FileInfo(path).Length);
                Data.Documents.Insert(0, entry);
                _docId = entry.Id;
                count++;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed.Add(Path.GetFileName(path)); }
        }
        _docQuery = "";
        _docFormat = "全部格式";
        Render();
        Save($"已添加 {count} 份资料。" + (failed.Count > 0 ? " 读取失败：" + string.Join("、", failed) : ""));
    }

    private DocumentEntry VerifyDocument(DocumentEntry item, bool report)
    {
        var result = item;
        try
        {
            using var reader = new StreamReader(_store.DocumentPath(item), detectEncodingFromByteOrderMarks: true);
            var characters = new char[item.Format == "PDF" ? 1024 : 65536];
            var n = reader.ReadBlock(characters, 0, characters.Length);
            var content = new string(characters, 0, n);
            var issue = content.Length == 0 ? "文件为空" :
                item.Format == "PDF" && !Regex.IsMatch(content, @"%PDF-\d\.\d", RegexOptions.CultureInvariant, ParseTimeout) ?
                    "未识别到 PDF 文件标识" : "";
            if (issue.Length > 0) result = item with { Inspection = "检查失败", Preview = issue };
            else
            {
                var preview = item.Format switch { "PDF" => "", "HTML" => PlainHtml(content), _ => content.Trim() };
                result = item with { Inspection = "检查通过", Preview = preview.Length <= 1300 ? preview : preview[..1300] };
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            result = item with { Inspection = "检查失败", Preview = "原件无法读取。" };
        }
        ReplaceDocument(result);
        if (report)
        {
            Render();
            Save(result.Inspection == "检查通过" ? "资料检查通过。" : result.Preview);
        }
        return result;
    }

    private static string PlainHtml(string content)
    {
        var visible = Regex.Replace(content, @"<(head|script|style)\b[^>]*>[\s\S]*?</\1>", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ParseTimeout);
        var paragraphs = Regex.Replace(visible, @"</(?:div|p|li|h[1-6])>", "\n", RegexOptions.IgnoreCase, ParseTimeout);
        return System.Net.WebUtility.HtmlDecode(Regex.Replace(paragraphs, "<[^>]*>", "", RegexOptions.None, ParseTimeout)).Trim();
    }

    private void ReplaceDocument(DocumentEntry value)
    {
        var index = Data.Documents.FindIndex(item => item.Id == value.Id);
        if (index >= 0) Data.Documents[index] = value;
    }

    private void DeployDocument(DocumentEntry item, string target)
    {
        target = target.Trim();
        if (target.Length == 0) { Tell("请输入资料库名称，或者选择 SAG 目录。"); return; }
        var verified = VerifyDocument(item, false);
        if (verified.Inspection != "检查通过")
        {
            Render();
            Save(verified.Preview);
            return;
        }
        try
        {
            var directory = Path.IsPathFullyQualified(target) ? new FeatureContext(target).Root :
                new FeatureContext(_store.DirectoryPath).FilePath(Path.Combine("SAG", target));
            var name = Path.GetFileName(item.Name);
            FeatureContext.ValidateRelativePath(name);
            var destination = Path.Combine(directory, name);
            if (Directory.Exists(directory)) destination = new FeatureContext(directory).FilePath(name);
            var line = PreviewCopy(name, _store.DocumentPath(verified), destination, overwrite: true);
            var replace = line.Change == "替换";
            if (!Confirm(replace ? "替换资料库的同名文件" : "保存到资料库", $"{item.Name}\n{destination}", replace ? "替换" : "保存")) return;
            if (line.Change != "内容相同") CopyReviewed(line);
            var deployed = DateTimeOffset.Now;
            foreach (var same in Data.Documents.Where(entry => entry.Id != item.Id && entry.DeployedAt.HasValue &&
                string.Equals(entry.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Target, directory, StringComparison.OrdinalIgnoreCase)).ToArray())
                ReplaceDocument(same with { DeployedAt = null, Target = "" });
            ReplaceDocument(verified with { Target = directory, DeployedAt = deployed });
            RecordLog("SAG 资料部署", "成功", 0, destination, "");
            Render();
            Save("资料已写入目标目录。");
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private void ExportDocument(DocumentEntry item)
    {
        var save = new SaveFileDialog { Title = "保存资料原件", FileName = item.Name, Filter = "原始资料|*" + Path.GetExtension(item.Name) };
        if (save.ShowDialog(this) != true) return;
        try { File.Copy(_store.DocumentPath(item), save.FileName, overwrite: true); Tell("资料原件已保存。"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Tell("无法保存原件：" + error.Message); }
    }

    private void OpenExternal(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        { Tell("无法打开：" + error.Message); }
    }

}
