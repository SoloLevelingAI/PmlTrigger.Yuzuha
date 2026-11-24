using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Generated;
using Core30.PmlTwin;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private sealed record DeploymentLine(string File, string Source, string Target, string Change, string Digest, string Previous);
    private string _folderQuery = "";
    private string _folderId = "";
    private string _folderReceipt = "";
    private SeaGendDeployment? _seaGend;
    private bool _checkingSeaGend;

    private FrameworkElement FoldersPage()
    {
        if (!_showOtherDeployments) return PmlResourceDeploymentPage();
        var query = Input(_folderQuery, "FolderProfileSearch");
        var table = Table(Array.Empty<FolderProfile>(), "FolderProfiles", ("配置", "Name", 1.2),
            ("来源", "Source", 1.9), ("二级范围", "Scope", 1));
        table.MaxHeight = 240;
        var details = new ContentControl();
        var edit = Btn("编辑范围", "EditFolderProfile", () => { if (table.SelectedItem is FolderProfile profile) EditFolder(profile); });
        var preview = AsyncBtn("预览部署", "PreviewFolder", async () =>
        {
            if (table.SelectedItem is not FolderProfile profile) return;
            try
            {
                var lines = await Task.Run(() => PreviewFolder(profile));
                if (table.SelectedItem is FolderProfile selected && selected == profile)
                    details.Content = DeploymentPreview(lines, profile.Target);
            }
            catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        }, "Button.Primary");

        void ShowSelection()
        {
            edit.IsEnabled = preview.IsEnabled = table.SelectedItem is FolderProfile;
            if (table.SelectedItem is not FolderProfile profile)
            {
                details.Content = Card("部署预览与回执", Muted(_folderReceipt.Length > 0 ? _folderReceipt : "可部署 PMLLIB / PMLUI 文件，或保存目录配置。Host DLL 由整包更新。"));
                return;
            }
            _folderId = profile.Id;
            details.Content = Card("部署预览与回执", Stack(Details([("已选配置", profile.Name), ("目标目录", profile.Target), ("部署范围", profile.Scope)]),
                Below(Muted(_folderReceipt), _folderReceipt.Length > 0 ? 8 : 0)));
        }

        void Filter()
        {
            var rows = Data.Folders.Where(profile => $"{profile.Name} {profile.Source} {profile.Target} {profile.Scope}"
                .Contains(_folderQuery.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
            var previous = rows.FirstOrDefault(item => item.Id == _folderId);
            table.ItemsSource = rows;
            table.SelectedItem = previous ?? rows.FirstOrDefault();
            ShowSelection();
        }

        table.SelectionChanged += (_, _) => ShowSelection();
        query.TextChanged += (_, _) => { _folderQuery = query.Text; Filter(); };
        Filter();
        return Stack(Actions(Btn("返回 PML 资源", "OpenPhaytonDeployments", () => { _showOtherDeployments = false; Render(); })),
            Below(SeaGendCard(), 8), Below(SideBySide(Card("目录部署配置", Stack(Prompt(query, "搜索配置、目录或范围"), Below(table, 8),
            Below(Actions(edit, preview), 8)), Actions(Btn("新增配置", "AddFolderProfile", () => EditFolder()),
                AsyncBtn("部署 ZIP / 7Z…", "DeployPmlArchive", DeployPmlArchive),
                AsyncBtn("部署单个文件…", "DeployOneFile", () => DeploySingle(details), "Button.Primary"))),
            details, 1.24, 690), 12));
    }

    private async Task DeployPmlArchive()
    {
        var package = new OpenFileDialog { Title = "选择要部署的通用压缩包", Filter = "ZIP / 7Z|*.zip;*.7z", CheckFileExists = true };
        if (package.ShowDialog(this) != true) return;
        var folder = new OpenFolderDialog { Title = "选择独立的本机部署目录" };
        if (folder.ShowDialog(this) != true) return;
        try
        {
            var plan = await Task.Run(() => Run(FileDeploymentRequests.Preview(package.FileName, folder.FolderName)));
            var items = plan.Child("package")?.Children("file").ToArray() ?? [];
            if (items.Length == 0) throw new InvalidDataException("压缩包没有可部署的文件。");
            if (items.Any(item => IsPhaytonResource(Value(item, "path"))))
                throw new InvalidDataException("请从「PML 资源」部署 Phayton 的 PMLLIB 和 PMLUI 压缩包。");
            if (items.Any(item => IsPackageBinary(Value(item, "path"))))
                throw new InvalidDataException("压缩包含 DLL 或 EXE。请通过整包更新这些文件。");
            var lines = items.Select(item => $"{Value(item, "action")}  {Value(item, "path")}");
            if (!Modal("预览部署 PML 压缩包", Stack(Muted(folder.FolderName),
                    Below(Code(string.Join("\n", lines), "ArchiveDeploymentItems", 268), 8)),
                "确认部署", () => true, width: 785)) return;
            var applied = await Task.Run(() => Run(FileDeploymentRequests.Apply(Value(plan, "id"), Value(plan, "planSha256"))));
            if (Value(applied, "status") != "Applied") throw new InvalidDataException("后端没有返回部署完成回执。");
            _folderReceipt = $"已部署 {items.Length} 个文件 · {folder.FolderName}";
            Render();
            Tell(_folderReceipt);
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private FrameworkElement SeaGendCard()
    {
        var refresh = AsyncBtn("重新检测", "RefreshSeaGend", CheckSeaGend);
        refresh.IsEnabled = !_checkingSeaGend;
        var result = _seaGend;
        FrameworkElement body = result is null
            ? Muted(_checkingSeaGend ? "正在检测下载包和 AVEVA 目录…" : "等待检测下载包和 AVEVA 目录。")
            : Details([
                ("下载包", $"{(result.Archive.Length > 0 ? result.Archive : result.Downloads)}\n{result.ArchiveStatus}"),
                ("本机目录", $"{result.Installed}\n{result.InstalledStatus}"),
                ("文件名和大小", result.Comparison)
            ]);
        var actions = result is null ? Actions(refresh) : Actions(refresh,
            Btn("打开下载目录", "OpenSeaGendDownloads", () => BrowseDeploymentDirectory(result.Downloads)),
            Btn("打开 SeaGend 目录", "OpenSeaGendFolder", () => BrowseDeploymentDirectory(result.Installed)));
        var card = Card("一级部署 · SeaGend", body, actions);
        if (result is null && !_checkingSeaGend) card.Loaded += async (_, _) => await CheckSeaGend();
        return card;
    }

    private async Task CheckSeaGend()
    {
        if (_checkingSeaGend) return;
        var installation = Install;
        var evars = ReadEvarsConfiguration().FilePath;
        _checkingSeaGend = true;
        try { _seaGend = await Task.Run(() => SeaGendDeployment.Detect(evars, installation?.Product, installation?.Directory)); }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); return; }
        finally { _checkingSeaGend = false; }
        if (_page.Id == "Folders") Render();
    }

    private void BrowseDeploymentDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            using var process = Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }
    private async Task DeploySingle(ContentControl details)
    {
        var source = new OpenFileDialog { Title = "选择要部署的文件", CheckFileExists = true };
        if (source.ShowDialog(this) != true) return;
        if (IsPackageBinary(source.FileName))
        {
            Tell("Host DLL 和应用程序请通过「PmlTwin 设置 → 更新」整包更新。");
            return;
        }
        var folder = new OpenFolderDialog { Title = "选择部署目标目录" };
        if (folder.ShowDialog(this) != true) return;
        try
        {
            var target = new FeatureContext(folder.FolderName).FilePath(Path.GetFileName(source.FileName));
            var overwrite = File.Exists(target) && Confirm("目标已有同名文件", target, "预览替换");
            var line = await Task.Run(() => PreviewCopy(Path.GetFileName(source.FileName), source.FileName, target, overwrite));
            details.Content = DeploymentPreview([line], folder.FolderName);
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private static DeploymentLine[] PreviewFolder(FolderProfile profile)
    {
        var source = new FeatureContext(profile.Source);
        var target = new FeatureContext(profile.Target);
        var relative = Path.GetRelativePath(source.Root, target.Root);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(@"..\", StringComparison.Ordinal)))
            throw new InvalidDataException("目标目录须位于来源目录之外。");
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, RecurseSubdirectories = true };
        return profile.Groups.SelectMany(group => group.Length == 0
                ? Directory.EnumerateFiles(source.Root)
                : Directory.EnumerateFiles(source.FilePath(group), "*", options))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var name = Path.GetRelativePath(source.Root, path);
                var sourceFile = source.FilePath(name);
                var targetFile = target.FilePath(name);
                return IsPackageBinary(name)
                    ? new DeploymentLine(name, sourceFile, targetFile, "通过整包更新", "", "")
                    : PreviewCopy(name, sourceFile, targetFile, profile.Overwrite);
            }).ToArray();
    }

    private static bool IsPackageBinary(string file) =>
        Path.GetExtension(file).ToLowerInvariant() is ".dll" or ".exe";

    private static DeploymentLine PreviewCopy(string name, string source, string target, bool overwrite)
    {
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("来源与目标是同一个文件。");
        var content = FileDigest(source);
        var existing = File.Exists(target) ? FileDigest(target) : "";
        var action = existing.Length == 0 ? "新增" : existing == content ? "内容相同" : overwrite ? "替换" : "跳过同名";
        return new(name, source, target, action, content, existing);
    }

    private FrameworkElement DeploymentPreview(DeploymentLine[] lines, string target)
    {
        var grid = Table(lines, "FolderDeployPreview", ("相对路径", "File", 2.6), ("变更", "Change", .8));
        grid.MaxHeight = 208;
        var changes = lines.Count(item => item.Change is "新增" or "替换");
        var deploy = AsyncBtn("确认部署…", "DeployFolder", async () =>
        {
            if (!Confirm("确认复制文件", $"{target}\n复制 {changes} 项，跳过 {lines.Length - changes} 项。", "复制")) return;
            var receipt = await Task.Run(() => ApplyDeployment(lines, target));
            _folderReceipt = $"{receipt.Count(item => item.EndsWith(" · 已复制", StringComparison.Ordinal))} 项已复制，{lines.Length - changes} 项跳过。";
            var failed = receipt.Any(item => !item.EndsWith(" · 已复制", StringComparison.Ordinal));
            RecordLog("文件部署", failed ? "待核对" : "成功", failed ? 1 : 0, string.Join("\n", receipt), "");
            Render();
            Modal("文件部署回执", Stack(Muted(_folderReceipt), Below(Code(string.Join("\n", receipt), "FileCopyReceipt", 190), 6)),
                "完成", () => true, cancel: false, width: 675);
        }, "Button.Primary");
        deploy.IsEnabled = changes > 0;
        return Card("部署预览与回执", Stack(Details([("目标", target), ("待复制", $"{changes} 项")]),
            Below(grid, 7), Below(deploy, 7)));
    }

    private static string[] ApplyDeployment(DeploymentLine[] lines, string directory)
    {
        var results = new List<string>();
        foreach (var line in lines.Where(item => item.Change is "新增" or "替换"))
        {
            try
            {
                if (IsPackageBinary(line.File)) throw new InvalidDataException("程序文件由整包更新。");
                var target = new FeatureContext(directory).FilePath(line.File);
                CopyReviewed(line with { Target = target });
                results.Add($"{line.File} · 已复制");
            }
            catch (Exception error) when (WorkbenchError(error))
            { results.Add($"{line.File} · {error.Message}"); }
        }
        return results.ToArray();
    }

    private static string FileDigest(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static void CopyReviewed(DeploymentLine line)
    {
        var folder = Path.GetDirectoryName(line.Target)!;
        Directory.CreateDirectory(folder);
        var pending = Path.Combine(folder, ".pmltwin-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.Copy(line.Source, pending, overwrite: false);
            if (FileDigest(pending) != line.Digest || (File.Exists(line.Target) ? FileDigest(line.Target) : "") != line.Previous)
                throw new InvalidDataException("来源或目标自预览后已变化，请重新预览。");
            File.Move(pending, line.Target, overwrite: line.Previous.Length > 0);
            if (FileDigest(line.Target) != line.Digest) throw new InvalidDataException("复制结果的摘要不一致。");
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }

    private void EditFolder(FolderProfile? profile = null)
    {
        var name = Input(profile?.Name ?? "", "FolderProfileName");
        var source = Input(profile?.Source ?? "", "FolderSource");
        var target = Input(profile?.Target ?? "", "FolderDestination");
        source.IsReadOnly = target.IsReadOnly = true;
        var overwrite = Check("预览并确认后替换同名文件", "FolderOverwrite", profile?.Overwrite == true);
        var notice = new Notice("FolderProfileNotice");
        var choices = new WrapPanel();

        void ListGroups(string directory, string[] selected, bool all = false)
        {
            choices.Children.Clear();
            try
            {
                var root = new FeatureContext(directory);
                var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint };
                var groups = Directory.EnumerateDirectories(root.Root, "*", options).Select(Path.GetFileName).OfType<string>().Order()
                    .Prepend("");
                foreach (var group in groups)
                {
                    var check = Check(group.Length == 0 ? "根目录文件" : group, group.Length == 0 ? "Group_Root" : "Group_" + group,
                        all || selected.Contains(group, StringComparer.OrdinalIgnoreCase));
                    check.Tag = group;
                    check.Margin = new Thickness(0, 0, 12, 4);
                    choices.Children.Add(check);
                }
            }
            catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "warning"); }
        }
        if (profile is not null) ListGroups(profile.Source, profile.Groups);
        var browse = Btn("选择来源…", "BrowseFolderSource", () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择来源文件夹" };
            if (dialog.ShowDialog(this) != true) return;
            source.Text = dialog.FolderName;
            ListGroups(dialog.FolderName, [], all: true);
        });
        var choose = Btn("选择目标…", "BrowseFolderDestination", () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择部署目标文件夹" };
            if (dialog.ShowDialog(this) == true) target.Text = dialog.FolderName;
        });
        if (!Modal(profile is null ? "新增一级部署配置" : "编辑一级部署配置",
            Stack(Field("配置名称", name), Below(Field("来源文件夹", Leading(browse, source, Dock.Right)), 8),
                Below(Field("目标文件夹", Leading(choose, target, Dock.Right)), 8),
                Below(Views.Text("二级范围"), 9), Below(choices, 5), overwrite, Below(notice, 6)), "保存配置", () =>
        {
            var scope = choices.Children.OfType<CheckBox>().Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToArray();
            var label = name.Text.Trim();
            if (label.Length == 0 || !Directory.Exists(source.Text) || !Directory.Exists(target.Text) || scope.Length == 0)
            {
                notice.Show("请填写名称、选择来源和目标目录，并勾选部署范围。", "warning");
                return false;
            }
            if (Data.Folders.Any(item => item.Id != profile?.Id && item.Name.Equals(label, StringComparison.OrdinalIgnoreCase)))
            {
                notice.Show("已存在同名配置。", "warning");
                return false;
            }
            var record = new FolderProfile(profile?.Id ?? Guid.NewGuid().ToString("N"), label,
                source.Text, target.Text, scope, overwrite.IsChecked == true);
            if (profile is null) Data.Folders.Insert(0, record);
            else Data.Folders[Data.Folders.IndexOf(profile)] = record;
            _folderId = record.Id;
            return true;
        }, name, 625)) return;
        Render();
        Save("已保存部署配置和范围。");
    }
}
