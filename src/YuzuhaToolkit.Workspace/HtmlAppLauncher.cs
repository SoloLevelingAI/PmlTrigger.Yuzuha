using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>桌面和 CLI 共用的本地 HTML 入口；仅启动独立宿主和打开浏览器，不执行 PML。</summary>
public static class HtmlAppLauncher
{
    /// <summary>从明确覆盖、安装布局或开发布局找到工具根目录。</summary>
    public static string FindRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return Path.GetFullPath(explicitRoot);
        if (Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT") is { Length: > 0 } configured) return Path.GetFullPath(configured);
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".setup-payload.json")) ||
                File.Exists(Path.Combine(directory.FullName, "YuzuhaToolkit.Development.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("未找到 Yuzuha 安装/开发根目录，请设置 YUZUHA_TOOLKIT_ROOT。");
    }

    /// <summary>复用健康宿主或启动独立 WebApps，等待就绪后按需打开本机浏览器。</summary>
    public static async Task<string> OpenAsync(string? root = null, int port = 5288,
        bool openBrowser = true, CancellationToken token = default)
    {
        if (port is < 1024 or > 65535) throw new ArgumentException("本地端口范围为 1024–65535。");
        var address = "http://127.0.0.1:" + port;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        async Task<bool> Ready()
        {
            try
            {
                using var response = await client.GetAsync(address + "/api/health", token);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("本地端口被其他服务占用。");
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (!body.RootElement.TryGetProperty("service", out var service) || service.GetString() != "YuzuhaToolkit.WebApps")
                    throw new InvalidOperationException("本地端口不是 Yuzuha HTML 管理器。");
                if (body.RootElement.TryGetProperty("dataDirectory", out var dataDirectory)
                    ? !string.Equals(Path.GetFullPath(dataDirectory.GetString()!), UserDataPaths.AppsDirectory, StringComparison.OrdinalIgnoreCase)
                    : !string.IsNullOrEmpty(new WorkspaceConfigurationStore().Read().Value.UserDataRoot))
                    throw new IOException("正在运行的 HTML 服务使用旧数据目录。请关闭该服务后重新打开管理器，已有服务不会被强制结束。");
                return true;
            }
            catch (HttpRequestException) { return false; }
            catch (TaskCanceledException) when (!token.IsCancellationRequested) { throw new IOException("本地端口响应超时；请检查已有服务，不会重复启动。"); }
            catch (JsonException error) { throw new IOException("本地端口返回了无效健康信息。", error); }
        }
        if (!await Ready())
        {
            var home = FindRoot(root);
            var candidates = new[] { Path.Combine(home, "runtime", "webapps", "YuzuhaToolkit.WebApps.exe"),
                Path.Combine(home, "src", "YuzuhaToolkit.WebApps", "bin", "Release", "net10.0", "YuzuhaToolkit.WebApps.exe") };
            var executable = candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("此版本尚未包含 HTML 管理器，请先构建或更新发布包。");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
            start.Environment["Logging__LogLevel__Default"] = "None";
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--pml-root"); start.ArgumentList.Add(Path.Combine(home, "PMLLIB"));
            start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(UserDataPaths.AppsDirectory);
            using var process = Process.Start(start) ?? throw new IOException("未启动 HTML 管理器。");
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!await Ready())
            {
                if (process.HasExited) throw new IOException("HTML 宿主已退出，请检查本机 .NET/ASP.NET Core 10 运行时。");
                if (DateTime.UtcNow >= until) throw new IOException("HTML 宿主未在等待时间内就绪；不会重复启动。");
                await Task.Delay(200, token);
            }
        }
        if (openBrowser) Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        return address;
    }
}
