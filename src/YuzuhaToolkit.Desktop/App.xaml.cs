using System.Windows;

namespace YuzuhaToolkit.Desktop;

/// <summary>不依赖 Python 的 Windows 启动台入口。</summary>
public partial class App : Application
{
    /// <summary>创建本机工作台；--smoke-output 仅用于隔离数据下的界面截图验证。</summary>
    /// <param name="e">启动参数。</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var window = new MainWindow(); MainWindow = window; window.Show();
            var option = Array.IndexOf(e.Args, "--smoke-output");
            if (option >= 0)
            {
                await window.RunSmokeAsync(e.Args[option + 1]);
                Shutdown(0);
            }
        }
        catch (Exception error) { MessageBox.Show(error.Message, "Yuzuha 启动失败"); Shutdown(1); }
    }
}
