using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Knowledge;
using Core30.PmlTwin;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private bool _pmlScanning;
    private bool _pmlScanQueued;
    private string _pmlScanActivity = "";

    private FrameworkElement PmlScanNotice(string id)
    {
        var scan = _store.PmlScan;
        var label = _pmlScanning ? _pmlScanActivity : scan.Status;
        if (label.Length == 0) return Muted("设置 EVARS 后会扫描 PML 目录，也可手动刷新。");
        var notice = new Core30.Workbench.Notice(id);
        notice.Show(label, !_pmlScanning && scan.Warning ? "warning" : "primary");
        if (scan.Notes.Length == 0 || _pmlScanning) return notice;
        return Stack(notice, Below(Btn("查看扫描详情", id + "Details", () =>
            Modal("PML 目录扫描详情", Code(string.Join("\n", scan.Notes), "PmlScanNotes", 230), "完成", () => true,
                width: 780, cancel: false), "Button.Quiet"), 4));
    }

    private Button RefreshPmlButton(string id)
    {
        var button = AsyncBtn("刷新 PML 目录", id, () => ScanPmlFolders(manual: true));
        button.IsEnabled = !_pmlScanning && !_sagBusy;
        return button;
    }

    private async Task AddPmlFolder()
    {
        var dialog = new OpenFolderDialog { Title = "选择要扫描的 PML 目录" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var directory = Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(dialog.FolderName));
            RememberPmlFolder(directory);
            await ScanPmlFolders();
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private async Task ScanPmlFolders(bool manual = false)
    {
        if (_pmlScanning || _sagBusy)
        {
            _pmlScanQueued = true;
            return;
        }
        if (Install is not { } installation || ReadEvarsConfiguration() is not { FilePath.Length: > 0 } config)
        {
            if (manual) Tell("先选择 AVEVA 安装和 EVARS，再刷新 PML 目录。");
            return;
        }
        var extra = _store.Pml.SuppliedFolders.ToArray();
        var stamp = "";
        try
        {
            var exe = SagKnowledgeLibrary.FindExecutable(_store.Sag.CliPath) ?? "";
            var fingerprint = FileContent.Sha256(FileContent.ReadBounded(config.FilePath, 1024 * 1024));
            var identity = new[] { installation.Directory, config.FilePath, fingerprint, exe }.Concat(extra.Order(StringComparer.OrdinalIgnoreCase));
            stamp = FileContent.Sha256(Encoding.UTF8.GetBytes(string.Join("\n", identity)));
            if (!manual && stamp == _store.PmlScan.Stamp) return;

            _pmlScanning = true;
            _pmlScanActivity = "正在扫描 EVARS 的 PML 目录…";
            Render();
            var folders = await Task.Run(() => PmlTwinFeatures.ReadLibraryFolders(config.FilePath, installation.Product, installation.Directory, extra));
            var scan = await Task.Run(() => PmlTwinFeatures.ScanLibraryFunctions(folders));
            var notes = scan.Warnings.ToList();
            string[] summary = [$"已登记 {scan.Functions.Length:N0} 个函数", $"跳过加密 {scan.Encrypted:N0} 个、不可解析 {scan.Skipped:N0} 个"];
            _pmlScanActivity = summary[0] + " · 正在核对 SAG 索引…";
            if (_page.Id is "Ev" or "Functions" or "Sag") Render();
            await IndexScannedLibraries(exe, folders, manual, notes);
            var warning = notes.Count > 0;
            _store.SavePmlScan(new PmlScanState
            {
                Stamp = stamp, Status = string.Join(" · ", summary) + (warning ? $" · {notes.Count} 项待核对" : ""),
                Warning = warning, Functions = scan.Functions, Notes = notes.ToArray()
            });
            if (warning) Tell("PML 扫描有待核对的目录，可查看详情后手动刷新。");
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            var status = "PML 目录扫描失败，可手动刷新。";
            try
            {
                _store.SavePmlScan(new PmlScanState { Stamp = stamp, Status = status, Warning = true, Notes = [error.Message] });
            }
            catch (Exception saveError) when (WorkbenchError(saveError)) { status += " " + saveError.Message; }
            Tell(status + " " + error.Message);
        }
        finally
        {
            if (_pmlScanning)
            {
                _pmlScanning = false;
                Render();
            }
            RunQueuedPmlScan();
        }
    }

    private async Task IndexScannedLibraries(string exe, PmlLibraryFolder[] folders, bool manual, List<string> notes)
    {
        if (exe.Length == 0)
        {
            notes.Add("SAG CLI 尚未配置。函数目录已登记，正文索引可在选择 sag.exe 后刷新。");
            return;
        }
        var library = SagLibrary(exe);
        try
        {
            _sagVersion = await Task.Run(library.ReadVersion);
            _sagExe = exe;
            await ReadSagIndexes(library);
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            notes.Add("SAG CLI · " + error.Message);
            return;
        }
        foreach (var folder in folders.Where(folder => folder.State == "available"))
        {
            var source = new SagSource(folder.Path, SagSourceKind.Pml);
            if (!manual && _sagIndexes.GetValueOrDefault(source)?.State == "ready") continue;
            try
            {
                var result = await Task.Run(() => library.Import(folder.Path, SagSourceKind.Pml));
                _store.SaveSagSource(result.Index.Source);
                _sagIndexes[result.Index.Source] = result.Index;
                _sagSource ??= result.Index.Source;
                if (result.Index.State != "ready")
                    notes.Add($"{folder.Path} · {IndexLabel(result.Index)} {result.Index.Error}".TrimEnd());
            }
            catch (Exception error) when (WorkbenchError(error)) { notes.Add(folder.Path + " · " + error.Message); }
        }
    }

    private void RunQueuedPmlScan()
    {
        if (!_pmlScanQueued || _pmlScanning || _sagBusy) return;
        _pmlScanQueued = false;
        _ = ScanPmlFolders();
    }

    private void RememberPmlFolder(string directory) => _store.UpdatePmlPreferences(settings =>
    {
        if (!settings.SuppliedFolders.Contains(directory, StringComparer.OrdinalIgnoreCase)) settings.SuppliedFolders.Add(directory);
    });
}
