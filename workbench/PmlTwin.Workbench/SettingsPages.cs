using System.IO;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private string _feedbackTitle = "";
    private string _feedbackSteps = "";
    private string _feedbackExpected = "";
    private static string WorkbenchVersion => typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";

    private FrameworkElement SettingsPage()
    {
        var target = Input(Data.DefaultLibrary, "DefaultSagLibrary");
        var notice = new Notice("SettingsNotice");
        return SideBySide(Card("PML 执行设置", Stack(
                Details([("函数已授权", $"{Data.Functions.Count(fn => fn.AllowInvoke && fn.SourceCurrent)} 项")]),
                Below(Muted("支持只读模式的会话会拦截 PML 执行，仍可进行结构化窗体查询。"), 7),
                Below(Actions(Jump("管理 PML 函数登记", "Functions")), 7)), ReadOnlyButton()),
            Card("SAG 资料部署", Stack(Field("默认资料库", target), Below(notice, 6),
                Below(Actions(Jump("打开资料部署", "Docs"), Primary("保存设置", "SaveSettings", () =>
                {
                    var name = target.Text.Trim();
                    if (name.Length == 0) { notice.Show("请输入默认资料库名称。", "warning"); return; }
                    Data.DefaultLibrary = _docTarget = name;
                    target.Text = name;
                    notice.Clear();
                    Save("已保存 PmlTwin 设置。");
                })), 9))), 1.2);
    }

    private FrameworkElement AboutPage()
    {
        var author = Input(Data.Author, "AboutAuthor");
        return SideBySide(Card("PmlTwin", Details([("产品", "PmlTwin · Core30"), ("版本", WorkbenchVersion), ("工作区", _store.DirectoryPath)])),
            Card("作者与维护", Stack(Field("作者／维护者", author),
                Below(Primary("保存作者信息", "SaveAuthor", () =>
                {
                    Data.Author = author.Text.Trim();
                    Save("已保存作者信息。");
                }), 9))));
    }

    private FrameworkElement UpdatesPage()
    {
        var bundled = File.Exists(SetupPath);
        var setup = PackageSetupButton(bundled ? "打开程序安装器…" : "选择安装包…", "OpenPackageSetup");
        var note = bundled ? "安装器更新程序与随包 PML 源码。请在文件部署页选择何时部署。" : "选择已下载的 Phayton 安装包来更新程序和随包源码。";
        if (RegisteredPhaytonCli.FindInstalledWorkbenchRoot() is not null) note += "打开向导后，工作台会关闭以便更新。";
        return Card("Phayton 程序安装包", Stack(
            Muted(note),
            Below(Actions(setup, Jump("打开资源部署", "Folders")), 8)));
    }

    private FrameworkElement FeedbackPage()
    {
        var title = Input(_feedbackTitle, "FeedbackTitle");
        var steps = Input(_feedbackSteps, "FeedbackSteps");
        steps.AcceptsReturn = true;
        steps.TextWrapping = TextWrapping.Wrap;
        steps.Height = 96;
        var expected = Input(_feedbackExpected, "FeedbackExpected");
        expected.AcceptsReturn = true;
        expected.TextWrapping = TextWrapping.Wrap;
        expected.Height = 62;
        title.TextChanged += (_, _) => _feedbackTitle = title.Text;
        steps.TextChanged += (_, _) => _feedbackSteps = steps.Text;
        expected.TextChanged += (_, _) => _feedbackExpected = expected.Text;
        var notice = new Notice("FeedbackNotice");
        void Output(bool copy)
        {
            if (title.Text.Trim().Length == 0 || steps.Text.Trim().Length == 0)
            {
                notice.Show("请填写问题标题和复现步骤。", "warning");
                return;
            }
            notice.Clear();
            var text = $"# {title.Text.Trim()}\n\n产品：PmlTwin {WorkbenchVersion}\n\n## 复现步骤和实际结果\n{steps.Text.Trim()}\n\n## 预期结果\n{expected.Text.Trim()}\n";
            if (copy) { CopyText(text); return; }
            var dialog = new SaveFileDialog { Title = "导出 Bug 反馈", Filter = "Markdown|*.md", FileName = "PmlTwin-Bug-反馈.md" };
            if (dialog.ShowDialog(this) != true) return;
            try { File.WriteAllText(dialog.FileName, text); Tell("反馈文件已保存。"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { notice.Show(error.Message, "warning"); }
        }
        return SideBySide(Card("问题描述", Stack(Field("问题标题", title), Below(Field("复现步骤和实际结果", steps), 8),
            Below(Field("预期结果", expected), 8), Below(notice, 5), Below(Actions(
                Btn("下载反馈", "SaveFeedback", () => Output(false)), Primary("复制反馈", "CopyFeedback", () => Output(true))), 8))),
            Card("项目反馈入口", Stack(Muted("可把已核对的反馈粘贴到项目问题页。"), Below(
                Btn("打开 GitHub 问题页", "OpenIssuePage", () => OpenExternal("https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/issues/new")), 8))), 1.4);
    }
}
