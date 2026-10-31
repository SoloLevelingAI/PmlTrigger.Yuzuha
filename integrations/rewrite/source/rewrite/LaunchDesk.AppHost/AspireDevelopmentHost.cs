using Aspire.Hosting;

namespace LaunchDesk.DevHost.Aspire;

/// <summary>可选 Aspire 编排入口使用的桌面程序与独立数据目录。</summary>
/// <param name="DesktopExecutable">待编排的桌面 EXE 完整路径</param>
/// <param name="DataDirectory">数据目录的绝对路径</param>
/// <param name="HostArguments">仅传给开发宿主的命令行参数</param>
public sealed record DevelopmentOptions(string DesktopExecutable, string DataDirectory, string[] HostArguments);
/// <summary>提供可替换开发入口及开发资源所有权管理。</summary>
public sealed class AspireDevelopmentHost
{
    /// <summary>以真实Aspire AppHost启动普通入口所用Desktop产物，传入显式隔离数据目录及可选诊断配置。许可服务、AVEVA、VMware归外部所有；停止仅回收自有开发资源和工作台；目标会话不得加入随AppHost终止的进程组。</summary>
    /// <include file="../Comments.xml" path="doc/Options/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>正常结束返回 0；桌面 EXE 缺失时返回 2。</returns>
    public async ValueTask<int> RunAsync(DevelopmentOptions options, CancellationToken ct)
    {
        var executable = Path.GetFullPath(options.DesktopExecutable);
        var data = Path.GetFullPath(options.DataDirectory);
        if (!File.Exists(executable)) return 2;
        Directory.CreateDirectory(data);
        var smoke = options.HostArguments.Contains("--smoke");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = options.HostArguments, DisableDashboard = smoke });
        var desktopArguments = smoke ? new[] { "--ui-smoke", Path.Combine(data, "ui") } : new[] { "--data-dir", data };
        builder.AddExecutable("launchdesk", executable, Path.GetDirectoryName(executable)!, desktopArguments);
        // AVEVA、许可和VMware归外部用户所有，禁止加入本宿主。
        await using var application = builder.Build();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (smoke) timeout.CancelAfter(TimeSpan.FromSeconds(18));
        await application.RunAsync(timeout.Token);
        return 0;
    }
}
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? Value(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        var executable = Value("--desktop"); var directory = Value("--data-dir");
        if (executable is null || directory is null) { Console.Error.WriteLine("Required: --desktop <review build EXE> --data-dir <isolated development workspace>"); return 2; }
        return await new AspireDevelopmentHost().RunAsync(new(executable, directory, args), CancellationToken.None);
    }
}
