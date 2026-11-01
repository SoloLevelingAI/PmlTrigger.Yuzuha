using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>打开随安装包交付的 DocFX；仅启动本机只读站点，不接入 AVEVA。</summary>
public partial class MainWindow
{
    private Process? documentationProcess;
    /// <summary>本窗口启动且经过字节核对的只读回环文档站点。</summary>
    private string? documentationAddress;
    private bool openingDocumentation;

    private async void OpenDocumentation(object sender, System.Windows.RoutedEventArgs e) => await OpenDocumentationAt("index.html");

    /// <summary>打开经过路径约束、安装文件及HTTP字节身份核对的精确DocFX页或锚点。</summary>
    internal async Task OpenDocumentationAt(string relative)
    {
        if (openingDocumentation) return;
        openingDocumentation = true;
        try
        {
            var root = HtmlAppLauncher.FindRoot();
            var site = Path.Combine(root, "runtime", "documentation");
            var host = Path.Combine(root, "runtime", "documentation-host", "YuzuhaToolkit.DevHost.exe");
            if (!File.Exists(host) || !File.Exists(Path.Combine(site, "index.html"))) throw new FileNotFoundException("此目录没有完整文档包，请使用 Phayton 安装包中的工作台。");
            var target = DesktopDocumentationTarget.Resolve(site, relative);
            if (documentationProcess is null || documentationProcess.HasExited)
            {
                documentationProcess?.Dispose();
                // Let Windows select a free loopback port. Verify the served index
                // before opening it; a port race must never open another service.
                using var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                documentationAddress = "http://127.0.0.1:" + port + "/docs/";
                var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(host)! };
                foreach (var argument in new[] { "--urls", "http://127.0.0.1:" + port, "--docs-root", site, "--docs-only", "true" }) start.ArgumentList.Add(argument);
                start.Environment["Logging__LogLevel__Default"] = "None";
                documentationProcess = Process.Start(start) ?? throw new IOException("未能启动文档站点。");
            }
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var expected = SHA256.HashData(await File.ReadAllBytesAsync(target.File));
            var address = new Uri(new Uri(documentationAddress!), target.RelativeUrl).AbsoluteUri;
            var ready = false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (documentationProcess.HasExited) throw new IOException("文档服务已退出，请检查安装文件。");
                try
                {
                    var bytes = await client.GetByteArrayAsync(address);
                    if (!expected.AsSpan().SequenceEqual(SHA256.HashData(bytes))) throw new IOException("本地端口返回了其他版本的文档，未打开页面。");
                    ready = true; break;
                }
                catch (HttpRequestException) { await Task.Delay(200); }
            }
            if (!ready) throw new IOException("文档服务未就绪，请稍后重新打开。");
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            Say("已打开随版本交付的 Phayton DocFX；关闭工作台后，本次文档服务同时停止。");
        }
        catch (Exception error) { Say("文档打开失败：" + error.Message); StopDocumentation(); }
        finally { openingDocumentation = false; }
    }

    private void StopDocumentation()
    {
        try { if (documentationProcess is { HasExited: false }) documentationProcess.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { documentationProcess?.Dispose(); documentationProcess = null; }
    }
}
