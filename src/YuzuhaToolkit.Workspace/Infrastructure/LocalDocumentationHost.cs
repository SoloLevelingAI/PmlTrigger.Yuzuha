using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;

namespace YuzuhaToolkit.Workspace;

/// <summary>管理安装包中的本地只读文档服务；就绪核对后返回地址，不依赖窗口或打开浏览器。</summary>
public sealed class LocalDocumentationHost(string? installRoot = null) : IDisposable
{
    private Process? documentationProcess;
    private string? documentationAddress;

    /// <summary>复用本实例的服务；核对首页内容，防止端口竞争打开其他服务。</summary>
    public async Task<string> GetAddressAsync()
    {
        var root = installRoot ?? HtmlAppLauncher.FindRoot();
        var site = Path.Combine(root, "runtime", "documentation");
        var host = Path.Combine(root, "runtime", "documentation-host", "YuzuhaToolkit.DevHost.exe");
        if (!File.Exists(host) || !File.Exists(Path.Combine(site, "index.html"))) throw new FileNotFoundException("此目录没有完整文档包，请使用 Phayton 安装包中的工作台。");
        if (documentationProcess is null || documentationProcess.HasExited)
        {
            documentationProcess?.Dispose();
            // Let Windows select a free loopback port. Verify the served index
            // before opening it; a port race must never open another service.
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            documentationAddress = "http://127.0.0.1:" + port + "/docs/index.html";
            var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(host)! };
            foreach (var argument in new[] { "--urls", "http://127.0.0.1:" + port, "--docs-root", site, "--docs-only", "true" }) start.ArgumentList.Add(argument);
            start.Environment["Logging__LogLevel__Default"] = "None";
            documentationProcess = Process.Start(start) ?? throw new IOException("未能启动文档站点。");
        }
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var expected = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(site, "index.html")));
        var ready = false;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (documentationProcess.HasExited) throw new IOException("文档服务已退出，请检查安装文件。");
            try
            {
                var bytes = await client.GetByteArrayAsync(documentationAddress);
                if (!expected.AsSpan().SequenceEqual(SHA256.HashData(bytes))) throw new IOException("本地端口返回了其他版本的文档，未打开页面。");
                ready = true; break;
            }
            catch (HttpRequestException) { await Task.Delay(200); }
        }
        if (!ready) throw new IOException("文档服务未就绪，请稍后重新打开。");
        return documentationAddress!;
    }

    /// <summary>只停止本实例启动的文档进程。</summary>
    public void Dispose()
    {
        try { if (documentationProcess is { HasExited: false }) documentationProcess.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { documentationProcess?.Dispose(); documentationProcess = null; }
    }
}
