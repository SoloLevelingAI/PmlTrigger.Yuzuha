using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Core30.Backend;
using Core30.Core;
using Core30.Generated;
using Core30.PmlTwin;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

internal sealed record DeployedPmlResource(string Directory, string Id, string Sha256, int Count);
internal sealed record PmlResourceDeployment(DeployedPmlResource Library, DeployedPmlResource Interface, string? ProgramDirectory = null,
    string? SharedRoot = null);

public partial class MainWindow
{
    private const string PhaytonProgramVersion = "0.3.5.0";
    private const string LibraryEntry = "Bootstrap/YuzuhaResolveRuntimePath.pmlfnc";
    private const string InterfaceEntry = "des/Addins/YuzuhaAddin";
    private bool _showOtherDeployments;
    private bool _editingPmlResources;
    private sealed record PmlResourceSources(string Library, string Interface, string ProgramDirectory = "");
    private sealed record PmlFileDiff(string File, string Action);

    private FrameworkElement PmlResourceDeploymentPage()
    {
        var deployment = _store.Pml.PhaytonDeployment;
        var installed = InstalledResourceSources();
        var source = DefaultResourceSources(installed);
        var deployButton = AsyncBtn("部署并接入所有版本…", "DeployPhaytonResources", DeployPhaytonResources, "Button.Primary");
        deployButton.IsEnabled = !_scanning;
        var actions = Actions(deployButton,
            Btn("其他文件部署", "OpenOtherDeployments", () => { _showOtherDeployments = true; Render(); }));
        var deploy = Card("共用 PML 资源", Stack(
            Muted("一次部署 PMLLIB 和 PMLUI，并预览本机各 AVEVA 版本的 EVARS 接入。"),
            Below<FrameworkElement>(source is null
                ? Muted(installed.Length > 1 ? "找到多套安装，请在部署时选择来源。" : "请选择包含 PML 源码的 Phayton 安装目录。")
                : Details([("PMLLIB 来源", source.Library), ("PMLUI 来源", source.Interface)]), 12)), actions);
        var receipt = deployment is null
            ? Card("部署回执", Muted("等待部署。"))
            : Card("部署回执", Stack(Details([
                ("PMLLIB", $"{deployment.Library.Directory}\n{deployment.Library.Count} 个文件"),
                ("PMLUI", $"{deployment.Interface.Directory}\n{deployment.Interface.Count} 个文件")
            ]), Below(Muted("在 EVARS 页可按版本查看接入状态。"), 8)),
                Actions(Jump("前往 EVARS", "Ev")));
        return SideBySide(deploy, receipt);
    }

    private static PmlResourceSources[] InstalledResourceSources() => ProgramDirectories().Select(TryReadResourceSources).OfType<PmlResourceSources>().ToArray();

    private static PmlResourceSources? TryReadResourceSources(string directory)
    {
        try { return ResourceSourcesAt(directory); }
        catch (Exception error) when (WorkbenchError(error)) { return null; }
    }

    private static PmlResourceSources? ResourceSourcesAt(string directory)
    {
        var program = new FeatureContext(directory);
        var manifest = program.FilePath(".setup-payload.json");
        if (!File.Exists(manifest)) return null;
        using var payload = JsonDocument.Parse(FileContent.ReadBounded(manifest, 1024 * 1024));
        if (payload.RootElement.ValueKind != JsonValueKind.Object || !payload.RootElement.TryGetProperty("version", out var value) ||
            value.ValueKind != JsonValueKind.String || !Version.TryParse(value.GetString(), out var version) || version.Revision < 0)
            return null;
        var deployment = $"deployment/{value.GetString()}";
        if (!File.Exists(program.FilePath($"{deployment}/PMLLIB/{LibraryEntry}")) ||
            !File.Exists(program.FilePath($"{deployment}/PMLUI/{InterfaceEntry}"))) return null;
        return new(program.FilePath($"{deployment}/PMLLIB"), program.FilePath($"{deployment}/PMLUI"), program.Root);
    }

    private PmlResourceSources? DefaultResourceSources(PmlResourceSources[] installed)
    {
        if (_store.Pml.PhaytonDeployment?.ProgramDirectory is { Length: > 0 } remembered && TryReadResourceSources(remembered) is { } source)
            return source;
        if (installed.Length != 0) return installed.Length == 1 ? installed[0] : null;
        var directory = Path.Combine(AppContext.BaseDirectory, "phayton", "deployment");
        var library = Path.Combine(directory, "PMLLIB.zip");
        var ui = Path.Combine(directory, "PMLUI.zip");
        return File.Exists(library) && File.Exists(ui) ? new(library, ui) : null;
    }

    private async Task DeployPhaytonResources()
    {
        if (_editingPmlResources || _scanning) return;
        _editingPmlResources = true;
        try
        {
            var roots = ProgramDirectories();
            var sources = DefaultResourceSources(InstalledResourceSources());
            var initialProgram = sources is { ProgramDirectory.Length: > 0 } ? sources.ProgramDirectory : roots.Length == 1 ? roots[0] : "";
            var programField = Input(initialProgram, "PmlSharedProgramDirectory");
            var sharedField = Input(DefaultSharedRoot(initialProgram), "PmlSharedResourceDirectory");
            var librarySource = Input(sources?.Library ?? "", "PmlLibSource");
            var uiSourceField = Input(sources?.Interface ?? "", "PmlUiSource");
            var notice = new Notice("PmlResourceTargetNotice");
            var content = Stack(
                BrowsePath("Phayton 程序目录", programField, "PickPmlSourceProgram", () =>
                {
                    var previousDefault = DefaultSharedRoot(programField.Text);
                    if (!ChoosePmlFolder(programField, "选择 Phayton 程序目录")) return;
                    if (sharedField.Text.Length == 0 || LocalSource.SamePath(sharedField.Text, previousDefault))
                        sharedField.Text = DefaultSharedRoot(programField.Text);
                    if (TryReadResourceSources(programField.Text) is not { } selected) return;
                    librarySource.Text = selected.Library;
                    uiSourceField.Text = selected.Interface;
                }),
                Below(BrowsePath("共用资源根目录", sharedField, "PickPmlSharedResourceDirectory",
                    () => ChoosePmlFolder(sharedField, "选择共用的 PML 资源根目录")), 8),
                Below(Muted("各版本共用此根目录下的 PMLLIB 和 PMLUI。"), 8),
                Below(new Expander { Header = "部署来源", Content = Stack(
                    BrowsePath("PMLLIB 来源", librarySource, "PickPmlLibArchive", () => ChooseArchive(librarySource), "旧版 ZIP…"),
                    Below(BrowsePath("PMLUI 来源", uiSourceField, "PickPmlUiArchive", () => ChooseArchive(uiSourceField), "旧版 ZIP…"), 7)) }, 8),
                Below(notice, 8));
            string libSource = "", uiSource = "", libTarget = "", uiTarget = "", programDirectory = "", sharedRoot = "";
            if (!Modal("共享 PML 部署", content, "预览部署与接入", () =>
            {
                try
                {
                    var program = new FeatureContext(PmlResourceSwitch.LocalDirectory(programField.Text));
                    if (!File.Exists(program.FilePath("runtime/cli/PmlTwin.Phayton.Cli.exe")))
                        throw new InvalidDataException("请选择已安装的 Phayton 程序目录。");
                    programDirectory = program.Root;
                    sharedRoot = PmlResourceSwitch.LocalDirectory(sharedField.Text);
                    (libTarget, uiTarget) = PmlResourceSwitch.SharedDirectories(sharedRoot);
                    if (!LocalSource.SamePath(sharedRoot, programDirectory) &&
                        (PmlResourceSwitch.Overlaps(programDirectory, libTarget) || PmlResourceSwitch.Overlaps(programDirectory, uiTarget)))
                        throw new InvalidDataException("请核对共用资源根目录与程序目录的位置。");
                    var bundled = TryReadResourceSources(programDirectory);
                    libSource = ResolvePmlSource(librarySource.Text.Length == 0 ? bundled?.Library ?? "" : librarySource.Text);
                    uiSource = ResolvePmlSource(uiSourceField.Text.Length == 0 ? bundled?.Interface ?? "" : uiSourceField.Text);
                    if (PmlResourceSwitch.Overlaps(libSource, uiSource)) throw new InvalidDataException("请选择各自独立的 PMLLIB 和 PMLUI 来源。");
                    if (new[] { libSource, uiSource }.Any(source => PmlResourceSwitch.Overlaps(libTarget, source) || PmlResourceSwitch.Overlaps(uiTarget, source)))
                        throw new InvalidDataException("部署目录与源码目录重叠，请核对共用根目录。");
                    return true;
                }
                catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "warning"); return false; }
            }, programField, 780)) return;

            var visibleInstalls = Installations.ToArray();
            var detected = await Task.Run(() => LaunchBackend.Scan().Installations);
            var installations = visibleInstalls.Concat(detected).DistinctBy(item => item.Directory, StringComparer.OrdinalIgnoreCase).ToArray();
            if (installations.Any(item => PmlResourceSwitch.Overlaps(item.Directory, libTarget) || PmlResourceSwitch.Overlaps(item.Directory, uiTarget)))
                throw new InvalidDataException("共用资源目录与 AVEVA 程序目录重叠，请核对目标。");
            var environmentFiles = installations.Select(item => (item, EvarsFileFor(item))).ToArray();
            var prepared = await Task.Run(() =>
            {
                var batch = PmlEvarsBatch.Preview(programDirectory, libTarget, uiTarget, environmentFiles);
                var library = PreparePmlResource(libSource, libTarget, "PMLLIB", LibraryEntry);
                var ui = PreparePmlResource(uiSource, uiTarget, "PMLUI", InterfaceEntry);
                return (Deployment: new PmlResourceDeployment(library.Resource, ui.Resource, programDirectory, sharedRoot),
                    Files: library.Files.Concat(ui.Files).ToArray(), Batch: batch);
            });
            var deployment = prepared.Deployment;
            if (!ConfirmSharedDeployment(deployment, prepared.Files, prepared.Batch)) return;

            var backups = await Task.Run(() => ApplySharedDeployment(deployment, prepared.Batch));
            _store.UpdatePmlPreferences(settings => settings.PhaytonDeployment = deployment);
            var summary = $"共用资源已部署。写入 {backups.Length} 个 EVARS，{prepared.Batch.NeedsReview} 项待核对。";
            var receipt = string.Join("\n", backups.Select(item => $"{item.File}\n备份 · {item.Backup}"));
            RecordLog("共享 PML 部署", prepared.Batch.NeedsReview == 0 ? "成功" : "待核对", 0,
                $"{summary}\nPMLLIB · {libTarget}\nPMLUI · {uiTarget}\n{receipt}", "");
            Render();
            Modal("共享部署回执", Stack(Muted(summary), Below(Details([("PMLLIB", libTarget), ("PMLUI", uiTarget)]), 8),
                    Below(Code(receipt, "PmlBatchBackupReceipt", 160), 8), Below(Muted("重新启动相应版本的 AVEVA 后生效。"), 8)),
                "完成", () => true, cancel: false, width: 850);
            _ = ScanPmlFolders();
        }
        catch (UnauthorizedAccessException error) { Tell($"需要部署目录和 EVARS 的写入权限。\n{error.Message}"); }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        finally { _editingPmlResources = false; }
    }

    private string DefaultSharedRoot(string program)
    {
        var remembered = _store.Pml.PhaytonDeployment?.SharedRoot;
        if (Directory.Exists(remembered)) return remembered!;
        var conventional = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuzuhaToolkit", "PmlTrigger.Yuzuha");
        return File.Exists(Path.Combine(conventional, "PMLLIB", LibraryEntry)) ? conventional : program;
    }

    private bool ConfirmSharedDeployment(PmlResourceDeployment deployment, PmlFileDiff[] files, PmlEvarsBatch batch)
    {
        var targetTable = Table(batch.Targets, "PmlBatchEvarsPreview", ("版本", "Version", .9), ("EVARS", "File", 2.6),
            ("Host", "Host", .8), ("状态", "Status", 1.3));
        targetTable.MaxHeight = 176;
        var diff = new ContentControl();
        targetTable.SelectionChanged += (_, _) => diff.Content = targetTable.SelectedItem is PmlEvarsTarget { Edit.Changed: true } item
            ? EvarsDiff(item.Edit.Lines) : null;
        targetTable.SelectedIndex = Array.FindIndex(batch.Targets, target => target.Edit?.Changed == true);
        var fileTable = Table(files, "PmlSharedFilesPreview", ("相对路径", "File", 3), ("操作", "Action", 1));
        fileTable.MaxHeight = 150;
        return Modal("预览共享部署与 EVARS 接入", Stack(Details([
                ("共享 PMLLIB", deployment.Library.Directory), ("共享 PMLUI", deployment.Interface.Directory)
            ]), Below(Muted($"{files.Length} 个文件待复制或更新。{batch.ChangeCount} 个 EVARS 待写入，{batch.NeedsReview} 项待核对。"), 8),
            Below(fileTable, 6), Below(targetTable, 8), Below(new Expander { Header = "所选 EVARS 的行级变更", Content = diff }, 8)),
            batch.ChangeCount > 0 ? "确认部署并写入 EVARS" : "确认部署", () => true, width: 1000);
    }

    private static string ResolvePmlSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("请先选择 Phayton 安装目录或已有 ZIP。");
        path = Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(path.Trim()));
        if (!Directory.Exists(path) && !(Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path)))
            throw new InvalidDataException("请选择存在的 PML 源码目录或 ZIP 文件。");
        return path;
    }

    private (DeployedPmlResource Resource, PmlFileDiff[] Files) PreparePmlResource(string source, string directory, string kind, string required)
    {
        var plan = Run(FileDeploymentRequests.Preview(source, directory));
        var files = plan.Child("package")?.Children("file").ToArray() ?? [];
        if (!files.Any(file => Value(file, "path").Replace('\\', '/').Equals(required, StringComparison.OrdinalIgnoreCase)) ||
            files.Any(file => IsPackageBinary(Value(file, "path")) ||
                Path.GetExtension(Value(file, "path")).ToLowerInvariant() is ".bat" or ".cmd" or ".ps1"))
            throw new InvalidDataException($"来源须包含完整的 {kind} 资源。\n{source}");
        return (new(directory, Value(plan, "id"), Value(plan, "planSha256"), files.Length),
            files.Where(file => Value(file, "action") != "Unchanged").Select(file => new PmlFileDiff(
                $"{kind}/{Value(file, "path")}", Value(file, "action") == "Add" ? "新增" : "替换并备份")).ToArray());
    }

    private static bool IsPhaytonResource(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("YuzuhaResolveRuntimePath.pmlfnc", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("YuzuhaAddin", StringComparison.OrdinalIgnoreCase);
    }

    private PmlEvarsBackup[] ApplySharedDeployment(PmlResourceDeployment deployment, PmlEvarsBatch batch)
    {
        batch.RequireWritable();
        ApplyPmlResources(deployment);
        try { return batch.Apply(); }
        catch (PmlEvarsBatchException error) when (error.Restored)
        {
            var conflicts = RollbackPmlResources([deployment.Library, deployment.Interface]);
            if (conflicts.Length != 0) throw new IOException($"{error.Message}\n请核对资源回滚。\n{string.Join("\n", conflicts)}", error);
            throw;
        }
        catch (PmlEvarsBatchException error)
        { throw new IOException($"{error.Message}\n共用 PML 资源已保留，供仍指向该目录的版本使用。", error); }
    }

    private void ApplyPmlResources(PmlResourceDeployment deployment)
    {
        DeployedPmlResource[] packages = [deployment.Library, deployment.Interface];
        var started = new List<DeployedPmlResource>();
        try
        {
            foreach (var package in packages)
            {
                started.Add(package);
                var applied = Run(FileDeploymentRequests.Apply(package.Id, package.Sha256));
                if (Value(applied, "status") != "Applied") throw new InvalidDataException("部署回执未完成。");
            }
            VerifyPmlResources(deployment);
        }
        catch (Exception error) when (WorkbenchError(error))
        {
            var issues = RollbackPmlResources(started);
            if (issues.Length > 0) throw new IOException($"{error.Message}\n请核对部署回滚。\n{string.Join("\n", issues)}", error);
            throw;
        }
    }

    private string[] RollbackPmlResources(IEnumerable<DeployedPmlResource> packages)
    {
        var issues = new List<string>();
        foreach (var package in packages.Reverse())
        {
            try
            {
                var plan = Run(FileDeploymentRequests.Inspect(package.Id, package.Sha256));
                if (Value(plan, "status") is "Applying" or "Applied")
                {
                    var result = Run(FileDeploymentRequests.Rollback(package.Id, package.Sha256));
                    if (Value(result, "status") != "RolledBack") throw new IOException("部署回滚未完成。");
                }
            }
            catch (Exception error) when (WorkbenchError(error)) { issues.Add($"{package.Directory} · {error.Message}"); }
        }
        return issues.ToArray();
    }

    private void VerifyPmlResources(PmlResourceDeployment deployment)
    {
        foreach (var package in new[] { deployment.Library, deployment.Interface })
        {
            var check = Run(DeploymentVerificationRequests.Verify(package.Id, package.Sha256));
            if (Value(check, "status") != "Applied" || Value(check.Child("verification"), "status") != "Verified" ||
                !LocalSource.SamePath(package.Directory, Value(check, "target")))
                throw new InvalidDataException($"部署文件需要重新核对。\n{package.Directory}");
        }
        var bootstrap = new FeatureContext(deployment.Library.Directory).FilePath(LibraryEntry);
        var text = LocalText.Decode(FileContent.ReadBounded(bootstrap, 512 * 1024)).Content;
        if (!text.Contains("YUZUHA_PROGRAM_ROOT", StringComparison.Ordinal))
            throw new InvalidDataException("此 PMLLIB 版本缺少独立程序路径支持，请选择新版 PML 源码。");
    }

    private static FrameworkElement EvarsDiff(IEnumerable<EvarsLine> lines)
    {
        var rows = new StackPanel();
        foreach (var line in lines)
        {
            var row = new DockPanel { LastChildFill = true };
            var tone = line.Removed ? "danger" : "success";
            row.SetResourceReference(Panel.BackgroundProperty, "color." + tone + "-bg");
            var number = new TextBlock { Text = $"{line.Number,3}  {(line.Removed ? "−" : "+")}", Width = 57, Padding = new Thickness(5, 3, 5, 3) };
            var text = new TextBlock { Text = line.Text, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(5, 3, 8, 3) };
            foreach (var part in new[] { number, text })
            {
                part.SetResourceReference(TextBlock.FontFamilyProperty, "font-family.mono");
                part.SetResourceReference(TextBlock.ForegroundProperty, "color." + tone);
                row.Children.Add(part);
            }
            rows.Children.Add(row);
        }
        return Id(new ScrollViewer { MaxHeight = 315, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rows }, "PmlEvarsSwitchDiff");
    }

    private static string[] ProgramDirectories()
    {
        if (RegisteredPhaytonCli.FindInstalledWorkbenchRoot() is { } installed) return [installed];
        string[] programs;
        try { programs = RegisteredPhaytonCli.FindPrograms(); }
        catch (Exception error) when (WorkbenchError(error)) { programs = []; }
        if (programs.Length == 0 && RegisteredPhaytonCli.FindStandardUserProgram() is { } fallback) programs = [fallback];
        return programs.Select(path => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static FrameworkElement BrowsePath(string label, TextBox field, string id, Action browse, string buttonLabel = "浏览…")
    {
        var button = Btn(buttonLabel, id, browse);
        button.VerticalAlignment = VerticalAlignment.Bottom;
        button.Margin = new Thickness(8, 0, 0, 0);
        return Leading(button, Field(label, field), Dock.Right);
    }

    private bool ChoosePmlFolder(TextBox field, string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (dialog.ShowDialog(this) != true) return false;
        field.Text = dialog.FolderName;
        return true;
    }

    private void ChooseArchive(TextBox field)
    {
        var dialog = new OpenFileDialog { Title = "选择 PML ZIP 压缩包", Filter = "ZIP 压缩包|*.zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) field.Text = dialog.FileName;
    }

}
