using System.IO;
using System.Windows;
using Core30.Core;
using Core30.Generated;
using Core30.Knowledge;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private sealed record SagComponentPlan(string Home, string State, string Version, string Digest, string Source, string Cli);

    private SagComponentPlan? _sagComponentPlan;
    private string? _sagComponentHome;
    private string _sagComponentStatus = "";
    private bool _sagComponentError;
    private bool _sagComponentBusy;

    private string SagWorkspaceIndex => Path.Combine(_store.DirectoryPath, ".core30", "sag-knowledge", "data");
    private static string DefaultSagComponentHome => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVEVA-Toolbox", "SAG-Wiki");

    private FrameworkElement SagComponentCard()
    {
        var configured = _store.Sag.CliPath;
        string? existing;
        try { existing = SagKnowledgeLibrary.FindExecutable(configured); }
        catch (Exception error) when (WorkbenchError(error)) { existing = null; }
        var plan = _sagComponentPlan;
        var install = AsyncBtn(plan is null ? "检查并预览安装…" : plan.State == "Current" ? "使用此组件" : "确认安装",
            "ReviewSagComponent", _sagComponentPlan is null ? ReviewSagComponent : ConfirmSagComponent,
            existing is null || plan is not null ? "Button.Primary" : "Button.Base");
        var choose = AsyncBtn("选择已有 CLI…", "ChooseExistingSagCli", ChooseSagCli);
        var change = Btn("更改安装位置…", "ChangeSagComponentLocation", ChangeSagComponentLocation);
        install.IsEnabled = change.IsEnabled = !_sagComponentBusy;
        choose.IsEnabled = !_sagComponentBusy && !_sagBusy && !_pmlScanning;
        var notice = new Notice("SagComponentStatus");
        if (_sagComponentStatus.Length > 0) notice.Show(_sagComponentStatus, _sagComponentError ? "warning" : "primary");
        var content = Stack(
            Muted(existing is null ? "安装或选择本机已有的 SAG CLI，随后进入知识库检查当前工作区索引。"
                : "已找到 SAG CLI，可直接进入正文检索。"),
            Below(Details([("安装位置", _sagComponentHome ?? InitialSagComponentHome())]), 8),
            Below(Muted("检查会获取并核验发行包。确认安装时才执行安装程序。"), 5));
        if (plan is not null)
        {
            var fields = plan.State == "Current" ? new[] { ("可用版本", plan.Version), ("SAG CLI", plan.Cli) }
                : new[] { ("发行来源", plan.Source), ("版本", plan.Version), ("SHA-256", plan.Digest) };
            content.Children.Add(Below(Details([.. fields, ("当前工作区索引", SagWorkspaceIndex)]), 8));
        }
        var controls = existing is null ? Actions(install, choose) : Actions(install, choose, Jump("打开正文检索", "Sag"));
        if (plan is not null) controls.Children.Add(Btn("取消", "CancelSagReview", () =>
        {
            _sagComponentPlan = null;
            _sagComponentStatus = "已取消本次安装确认。";
            Render();
        }, "Button.Quiet"));
        content.Children.Add(Below(controls, 8));
        content.Children.Add(Below(Actions(change), 5));
        content.Children.Add(Below(notice, 4));
        return Card("SAG 正文检索组件", content);
    }

    private string InitialSagComponentHome()
    {
        var configured = _store.Sag.CliPath;
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) &&
                Path.GetDirectoryName(configured) is { } versionDirectory)
        {
            var runtime = Directory.GetParent(versionDirectory);
            if (runtime is { Name: "runtime", Parent: { } home } && File.Exists(Path.Combine(home.FullName, "active-version.txt")))
                return home.FullName;
        }
        return DefaultSagComponentHome;
    }

    private void ChangeSagComponentLocation()
    {
        var folder = new OpenFolderDialog { Title = "选择安装位置。将在所选目录下使用 SAG-Wiki 子目录。" };
        if (folder.ShowDialog(this) != true) return;
        _sagComponentHome = string.Equals(new DirectoryInfo(folder.FolderName).Name, "SAG-Wiki", StringComparison.OrdinalIgnoreCase)
            ? folder.FolderName : Path.Combine(folder.FolderName, "SAG-Wiki");
        _sagComponentPlan = null;
        _sagComponentStatus = "";
        Render();
    }

    private async Task ReviewSagComponent()
    {
        var home = _sagComponentHome ?? InitialSagComponentHome();
        await SagComponentWork("正在核对发行版本并验证安装包。", async () =>
        {
            var request = LocalSource.SamePath(home, DefaultSagComponentHome)
                ? SagWikiRequests.ReviewRelease() : SagWikiRequests.ReviewReleaseAt(home);
            var result = await Task.Run(() => Run(request));
            var status = Value(result, "state");
            if (status is not ("Verified" or "Current"))
                throw new InvalidDataException("SAG 组件未返回可用安装预览。");
            var plan = new SagComponentPlan(home, status, Value(result, "version"), Value(result, "sha256"),
                Value(result, "source"), Value(result, "cli"));
            _sagComponentPlan = plan;
            _sagComponentStatus = status == "Current" ? "目标目录已安装可用的 SAG 版本。"
                : "安装包已通过核验，请确认版本和安装位置。";
        });
    }

    private async Task ConfirmSagComponent()
    {
        if (_sagComponentPlan is not { } plan) return;
        var existingBinding = _store.Sag.CliPath;
        var current = plan.State == "Current";
        _sagComponentPlan = null;
        await SagComponentWork(current ? "正在核对已有 SAG CLI。" : "正在安装已确认的 SAG 版本。", async () =>
        {
            var executable = plan.Cli;
            if (!current)
            {
                var request = LocalSource.SamePath(plan.Home, DefaultSagComponentHome)
                    ? SagWikiRequests.InstallReviewedRelease(plan.Version, plan.Digest)
                    : SagWikiRequests.InstallReviewedReleaseAt(plan.Home, plan.Version, plan.Digest);
                var result = await Task.Run(() => Run(request));
                if (Value(result, "state") is not ("Installed" or "Current") || Value(result, "version") != plan.Version)
                    throw new InvalidDataException("SAG 的安装版本已变更，请重新获取安装预览。");
                executable = Value(result, "cli");
                _sagComponentStatus = "程序安装已结束，正在核对 SAG CLI 和工作区绑定。";
                if (_page.Id == "Update") Render();
            }

            var library = SagLibrary(executable);
            var version = await Task.Run(library.ReadVersion);
            if (!string.Equals(version, $"sag {plan.Version}", StringComparison.Ordinal))
                throw new InvalidDataException($"SAG CLI 返回版本 {version}，请复查目标安装。");
            if (_sagBusy || _pmlScanning || !string.Equals(existingBinding, _store.Sag.CliPath, StringComparison.OrdinalIgnoreCase))
            {
                _sagComponentStatus = $"{version} 已就绪。当前工作区使用原有绑定，可稍后在此页选择 {executable}。";
                return;
            }
            _store.SelectSagCli(executable);
            _sagInitialized = false;
            _sagVersion = "";
            _sagIndexes.Clear();
            ClearSagResults();
            _sagComponentStatus = $"已核对并绑定 {version}。可进入正文检索核对工作区索引。";
        });
        if (_page.Id == "Sag" && !_sagInitialized) await RefreshSag();
    }

    private async Task SagComponentWork(string message, Func<Task> action)
    {
        if (_sagComponentBusy) return;
        _sagComponentBusy = true;
        _sagComponentStatus = message;
        _sagComponentError = false;
        if (_page.Id == "Update") Render();
        try { await action(); }
        catch (Exception error) when (WorkbenchError(error))
        {
            _sagComponentError = true;
            _sagComponentStatus = $"SAG 操作未完成。当前工作区保留原有绑定。{error.Message}";
        }
        finally
        {
            _sagComponentBusy = false;
            if (_page.Id == "Update") Render();
        }
    }
}
