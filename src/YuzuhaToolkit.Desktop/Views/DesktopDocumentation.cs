using System.Diagnostics;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>文档入口的交互状态和浏览器显示；本地服务交给后端管理。</summary>
public partial class MainWindow
{
    private readonly LocalDocumentationHost documentationHost = new();
    private bool openingDocumentation;

    private async void OpenDocumentation(object sender, System.Windows.RoutedEventArgs e)
    {
        if (openingDocumentation) return;
        openingDocumentation = true;
        try
        {
            var address = await documentationHost.GetAddressAsync();
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            Say("已打开随版本交付的 Phayton DocFX；关闭工作台后，本次文档服务同时停止。");
        }
        catch (Exception error) { Say("文档打开失败：" + error.Message); StopDocumentation(); }
        finally { openingDocumentation = false; }
    }

    private void StopDocumentation() => documentationHost.Dispose();
}
