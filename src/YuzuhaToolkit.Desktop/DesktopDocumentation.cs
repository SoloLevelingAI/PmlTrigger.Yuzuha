using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>区分默认使用指南和显式选择的开发参考；只启动本机只读站点。</summary>
public partial class MainWindow
{
    private Process? documentationProcess;
    /// <summary>本窗口启动且经过字节核对的只读回环文档站点。</summary>
    private string? documentationAddress;
    /// <summary>当前自有服务的用户站与开发参考身份，防止复用上一个集合。</summary>
    private string? documentationSiteKey;
    /// <summary>用户本窗口明确选中的解压目录；不写入工作区设置。</summary>
    private string? developerReferenceRoot;
    private bool openingDocumentation;

    private async void OpenDocumentation(object sender, System.Windows.RoutedEventArgs e) => await OpenDocumentationAt("index.html");

    /// <summary>按需选择配套开发参考目录，再打开该集合首页。</summary>
    private async void OpenDeveloperDocumentation(object sender, System.Windows.RoutedEventArgs e) =>
        await OpenDocumentationAt("index.html", DocumentationCollection.Developer);

    /// <summary>打开经过路径约束、安装文件及HTTP字节身份核对的精确DocFX页或锚点。</summary>
    internal async Task OpenDocumentationAt(string relative, DocumentationCollection collection = DocumentationCollection.User)
    {
        if (openingDocumentation) return;
        openingDocumentation = true;
        var verifyingHost = false;
        try
        {
            var root = HtmlAppLauncher.FindRoot();
            var site = Path.Combine(root, "runtime", "documentation");
            var host = Path.Combine(root, "runtime", "documentation-host", "YuzuhaToolkit.DevHost.exe");
            if (!File.Exists(host) || !File.Exists(Path.Combine(site, "documentation-profile.json")))
                throw new FileNotFoundException("此目录没有配套使用指南，请核对 Phayton 安装包。");
            var userGuide = await Task.Run(() => DocumentationPackage.ValidateUserGuide(site));
            if (_closed) return;
            if (collection == DocumentationCollection.Developer && developerReferenceRoot is null)
            {
                Say("开发参考为独立包，请先解压，再选择包根目录；不会后台下载或修改设置。");
                var dialog = new OpenFolderDialog { Title = "选择已解压的 Phayton 开发参考包根目录" };
                if (dialog.ShowDialog(this) != true) { Say("未选择开发参考；使用指南仍可打开。");return; }
                developerReferenceRoot = dialog.FolderName;
            }
            ValidatedDocumentationReference? reference = null;
            if (developerReferenceRoot is { } selected)
            {
                Say("正在核对开发参考版本和文件，请稍候；可以继续阅读工作区。");
                try { reference = await Task.Run(() => DocumentationPackage.ValidateReference(site, selected)); }
                catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or JsonException)
                {
                    developerReferenceRoot = null;
                    if (collection == DocumentationCollection.Developer) throw;
                    Say("开发参考不可用，仅打开使用指南：" + error.Message);
                }
            }
            if (_closed) return;
            if (collection == DocumentationCollection.Developer && reference is null)
                throw new IOException("未选择可用的开发参考包，请重新选择配套目录。");
            var targetSite = collection == DocumentationCollection.Developer ? reference!.SiteRoot : userGuide.Root;
            var publishedFiles = collection == DocumentationCollection.Developer ? reference!.Files : userGuide.Files;
            var target = DesktopDocumentationTarget.Resolve(targetSite, relative, publishedFiles.Keys);
            var targetFile = publishedFiles[Path.GetRelativePath(targetSite, target.File).Replace('\\', '/')];
            await Task.Run(() => { using var stream = DocumentationPackage.OpenRead(targetSite, targetFile); });
            if (_closed) return;
            var siteKey = userGuide.Root + "|" + userGuide.SourceCommit + "|" + reference?.PackageRoot + "|" + reference?.ManifestSha256;
            if (documentationProcess is null || documentationProcess.HasExited || documentationSiteKey != siteKey)
            {
                StopDocumentation();
                // Let Windows select a free loopback port. Verify the served index
                // before opening it; a port race must never open another service.
                using var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                documentationAddress = "http://127.0.0.1:" + port + "/";
                var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(host)! };
                foreach (var argument in new[] { "--urls", "http://127.0.0.1:" + port, "--docs-root", site, "--docs-only", "true" }) start.ArgumentList.Add(argument);
                if (reference is not null) { start.ArgumentList.Add("--reference-root");start.ArgumentList.Add(reference.PackageRoot); }
                start.Environment["Logging__LogLevel__Default"] = "None";
                documentationProcess = Process.Start(start) ?? throw new IOException("未能启动文档站点。");
                documentationSiteKey = siteKey;
            }
            verifyingHost = true;
            var ownedProcess = documentationProcess ?? throw new IOException("文档服务未启动。");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var expected = Convert.FromHexString(targetFile.Sha256);
            var prefix = collection == DocumentationCollection.Developer ? "reference/" : "docs/";
            var address = new Uri(new Uri(documentationAddress!), prefix + target.RelativeUrl).AbsoluteUri;
            var ready = false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (_closed) { StopDocumentation();return; }
                if (ownedProcess.HasExited) throw new IOException("文档服务已退出，请检查安装文件。");
                try
                {
                    var bytes = await client.GetByteArrayAsync(address);
                    if (!expected.AsSpan().SequenceEqual(SHA256.HashData(bytes))) throw new IOException("本地端口返回了其他版本的文档，未打开页面。");
                    ready = true; break;
                }
                catch (HttpRequestException) { await Task.Delay(200); }
            }
            if (!ready) throw new IOException("文档服务未就绪，请稍后重新打开。");
            if (_closed) { StopDocumentation();return; }
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            Say(collection == DocumentationCollection.Developer
                ? "已打开配套开发参考；目录选择仅本窗口有效，关闭工作台后本次服务停止。"
                : "已打开使用指南；完整开发参考可从旁边的入口按需选择。");
        }
        catch (Exception error) { Say("文档打开失败：" + error.Message);if (verifyingHost) StopDocumentation(); }
        finally { openingDocumentation = false; }
    }

    private void StopDocumentation()
    {
        try { if (documentationProcess is { HasExited: false }) documentationProcess.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { documentationProcess?.Dispose(); documentationProcess = null;documentationAddress = null;documentationSiteKey = null; }
    }
}
