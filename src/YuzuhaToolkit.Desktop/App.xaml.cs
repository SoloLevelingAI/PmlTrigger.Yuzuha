using System.Windows;
using System.Diagnostics;

namespace YuzuhaToolkit.Desktop;

/// <summary>不依赖 Python 的 Windows 启动台入口。</summary>
public partial class App : Application
{
    /// <summary>创建本机工作台；--smoke-output 仅用于隔离数据下的界面截图验证。</summary>
    /// <param name="e">启动参数。</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var option = Array.IndexOf(e.Args, "--smoke-output");
        var launchOnly = Array.IndexOf(e.Args, "--launch-lifecycle-smoke-output");
        if (launchOnly >= 0) option = launchOnly;
        TextWriterTraceListener? bindingTrace = null;
        try
        {
            if (option >= 0)
            {
                var output = System.IO.Path.GetFullPath(e.Args[option + 1]); System.IO.Directory.CreateDirectory(output);
                System.IO.File.Delete(System.IO.Path.Combine(output, "desktop-smoke.json"));
                System.IO.File.Delete(System.IO.Path.Combine(output, "smoke-error.txt"));
                Environment.SetEnvironmentVariable("YUZUHA_STATE_DIR", System.IO.Path.Combine(output, "isolated-state", Guid.NewGuid().ToString("N")));
                bindingTrace = new(new System.IO.StreamWriter(System.IO.Path.Combine(output, "binding-errors.log"), false));
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
                PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTrace);
            }
            var syntheticLaunches = 0;
            var launchScript = option < 0 ? null : new YuzuhaToolkit.Workspace.AvevaLaunchScript(
                startProcess: (_, _) => { syntheticLaunches++; return 424242; });
            var window = new MainWindow(launchScript); MainWindow = window; window.Show();
            if (option >= 0)
            {
                if (launchOnly >= 0) await window.RunLaunchLifecycleSmokeAsync(e.Args[option + 1]);
                else await window.RunSmokeAsync(e.Args[option + 1]);
                if (launchOnly < 0 && syntheticLaunches != 1) throw new InvalidOperationException("桌面启动流程应调用一次后端进程边界。");
                bindingTrace!.Flush(); bindingTrace.Dispose(); PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingTrace);
                if (new System.IO.FileInfo(System.IO.Path.Combine(e.Args[option + 1], "binding-errors.log")).Length > 0) throw new InvalidOperationException("WPF 数据绑定产生错误，请检查 binding-errors.log。");
                Shutdown(0);
            }
        }
        catch (Exception error)
        {
            if (option >= 0) { System.IO.Directory.CreateDirectory(e.Args[option + 1]); System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[option + 1], "smoke-error.txt"), error.ToString()); }
            else MessageBox.Show(error.Message, "Phayton 启动失败");
            Shutdown(1);
        }
    }
}
