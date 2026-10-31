using System.Diagnostics;
using System.Text.Json;
using YuzuhaToolkit.Manager;

/// <summary>从安装包或源码目录打开启动工作台，不要求手工编辑配置文件。</summary>
internal static class LaunchWorkbenchCommand
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken token)
    {
        try
        {
            string? assets = null;
            var port = 0;
            var openBrowser = true;
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--assets" when index + 1 < args.Length: assets = args[++index]; break;
                    case "--port" when index + 1 < args.Length: port = int.Parse(args[++index]); break;
                    case "--no-browser": openBrowser = false; break;
                    default: throw new ArgumentException("用法：workbench [--assets <目录>] [--port <端口>] [--no-browser]");
                }
            }
            using var server = new LaunchWorkbenchServer(ManagerOperationCatalog.Create(), assets ?? FindAssets(), port);
            server.Start();
            Console.WriteLine(JsonSerializer.Serialize(new { state = "listening", url = server.Address.AbsoluteUri, feature = "launch.workflow", evidence = "not-login-verification" }));
            if (openBrowser) Process.Start(new ProcessStartInfo(server.Address.AbsoluteUri) { UseShellExecute = true });
            await server.RunAsync(token);
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or IOException or System.Net.HttpListenerException or FormatException or OverflowException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("启动工作台未打开：" + error.Message);
            return 2;
        }
    }

    private static string FindAssets()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "workbench");
        if (File.Exists(Path.Combine(packaged, "index.html"))) return packaged;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var source = Path.Combine(directory.FullName, "docs", "design", "launch-workflow");
            if (File.Exists(Path.Combine(source, "index.html"))) return source;
        }
        throw new DirectoryNotFoundException("缺少启动页面资产，请使用完整发行包或提供 --assets。");
    }
}
