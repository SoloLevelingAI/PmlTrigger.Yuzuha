using System.IO;
using System.Text.Json;
using System.Windows;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;
using Microsoft.Extensions.Logging.Abstractions;

namespace YuzuhaToolkit.Desktop;

/// <summary>桌面的本地 HTML 入口和可预览 PML 开发流程。</summary>
public partial class MainWindow
{
    private DevelopmentPreview? developmentPreview;

    private async void OpenHtmlManager(object sender, RoutedEventArgs e)
    {
        OpenHtmlButton.IsEnabled = false;
        var id = _journal.Begin("open_html_manager", "{}");
        try
        {
            var url = await HtmlAppLauncher.OpenAsync(); HtmlAddressBox.Text = url;
            HtmlStateText.Text = "独立 HTML 管理器已就绪；AVEVA 调用仍在网页中明确选择会话。";
            _journal.Complete(id, "succeeded", url); Say("已打开本地 HTML 管理器。");
        }
        catch (Exception error) { _journal.Complete(id, "failed", error.Message); Say(error.Message); HtmlStateText.Text = error.Message; }
        finally { OpenHtmlButton.IsEnabled = true; }
    }

    private void BrowseDevelopmentManifest(object sender, RoutedEventArgs e)
    {
        var path = FilePath("验证清单|*.json"); if (path is not null) DevelopmentManifestBox.Text = path;
    }
    private void DevelopmentManifestChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { developmentPreview = null; if (RunDevelopmentButton is not null) RunDevelopmentButton.IsEnabled = false; }

    private async void PreviewDevelopment(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_copying) return;
            var source = SourceBox.Text.Trim(); var root = RegisteredBox.Text.Trim(); var plugin = PluginBox.Text.Trim();
            var manifest = DevelopmentManifestBox.Text.Trim();
            var preview = await Task.Run(() => PmlDevelopmentWorkflow.Preview(PluginDeployment.Preview(source, root, plugin), manifest));
            if (source != SourceBox.Text.Trim() || root != RegisteredBox.Text.Trim() || plugin != PluginBox.Text.Trim() || manifest != DevelopmentManifestBox.Text.Trim())
                throw new IOException("输入已变化，请重新预览。");
            developmentPreview = preview; _plan = preview.Plan; CopyGrid.ItemsSource = preview.Plan.Files;
            ApplyCopyButton.IsEnabled = true; RunDevelopmentButton.IsEnabled = true;
            DevelopmentSummaryText.Text = $"{preview.Plan.Files.Length} 个文件；Object: {string.Join(", ",preview.Manifest.Objects)}；Form: {string.Join(", ",preview.Manifest.Forms)}；预期 {preview.Manifest.Expected.Count} 项。\n清单 SHA256：{preview.ManifestHash}\n目标：{preview.Plan.Target}";
            Say("已生成复制和验证预览；尚未复制或发送 PML。");
        }
        catch (Exception error) { Say(error.Message); }
    }

    private async void RunDevelopment(object sender, RoutedEventArgs e)
    {
        if (_copying || developmentPreview is null) return;
        var preview = developmentPreview;
        try
        {
            var target = SessionView.DevelopmentTarget();
            _copying = true; SessionView.IsEnabled = false; RunDevelopmentButton.IsEnabled = false; ApplyCopyButton.IsEnabled = false;
            var evidence = Path.Combine(UserDataPaths.StateDirectory,"Development",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"-"+Guid.NewGuid().ToString("N"));
            var id = _journal.Begin("pml_development",JsonSerializer.Serialize(new { preview.Plan.Source, preview.Plan.Target, preview.ManifestHash }),target.ProcessId);
            try
            {
                using var host = new PmlDevelopmentHost(target);
                var result = await Task.Run(()=>PmlDevelopmentWorkflow.RunAsync(preview,target,evidence,host));
                DevelopmentStepsGrid.ItemsSource = result.Steps;
                DevelopmentResultsGrid.ItemsSource = result.Result?.Items;
                DevelopmentEvidenceBox.Text = Path.Combine(evidence,"run.json");
                var status = result.Status == "Passed" ? "succeeded" : "failed";
                _journal.Complete(id,status,result.Error ?? "复制、REHASH、重载和结果核对通过。");
                DevelopmentSummaryText.Text = result.Status + (result.Error is null ? "" : "："+result.Error) + "\n证据："+evidence;
                Say(result.Status == "Passed" ? "开发链路验证通过。" : "流程失败或结果未知；请核查证据，不自动重发。");
            }
            catch (Exception error) { _journal.Complete(id,"failed",error.Message); throw; }
        }
        catch (Exception error) { Say(error.Message); }
        finally { _copying=false; SessionView.IsEnabled=true; developmentPreview=null; }
    }

    private Dictionary<string,string> ReadStartupEnvironment()
    {
        var environment = new Dictionary<string,string>(_selected?.Environment ?? new(),StringComparer.OrdinalIgnoreCase);
        if (StartupPolicyBox.SelectedIndex < 0) throw new ArgumentException("请明确选择有效启动策略。");
        if (StartupPolicyBox.SelectedIndex == 0) { environment.Remove(StartupPolicy.Variable); environment.Remove(StartupPolicy.OverrideVariable); }
        else
        {
            environment[StartupPolicy.Variable] = StartupPolicyBox.SelectedIndex == 1 ? "rehash-all" : "none";
            environment[StartupPolicy.OverrideVariable] = environment[StartupPolicy.Variable];
        }
        return environment;
    }
    private void FillStartupPolicy(LaunchProfile profile)
    {
        var pair = profile.Environment.FirstOrDefault(p=>p.Key.Equals(StartupPolicy.Variable,StringComparison.OrdinalIgnoreCase));
        if (pair.Key is null) { StartupPolicyBox.SelectedIndex = 0; return; }
        try { StartupPolicyBox.SelectedIndex = StartupPolicy.Normalize(pair.Value) == "rehash-all" ? 1 : 2; }
        catch (ArgumentException) { StartupPolicyBox.SelectedIndex = -1; Say("此配置的启动策略无效，请明确选择后保存。"); }
    }
}

