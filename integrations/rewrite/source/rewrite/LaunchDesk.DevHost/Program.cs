using System.Diagnostics;
using LaunchDesk.DevHost;
using LaunchDesk.OperationContracts;
using LaunchDesk.Windows;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

// 可选开发宿主独立于桌面发行。
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5275");
var options = new StartupOptions(builder.Configuration["config"], builder.Configuration["data-dir"]);
var location = new WorkspaceLocationResolver().Resolve(options, AppContext.BaseDirectory);
if (!location.IsSuccess)
{
    Console.Error.WriteLine(location.Error!.SafeMessage);
    return;
}

builder.Services.AddSingleton(new DiagnosticState(location.Value.MachineId));
builder.Services.AddSingleton(new JsonRunJournal(location.Value));
builder.Services.AddHostedService<OperationJournalObserver>();
// 名称须为字面量，供.NET 10 XML注释生成器拦截注册。
builder.Services.AddOpenApi("v1", openApi => openApi.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "LaunchDesk 开发诊断 API";
    document.Info.Version = "v1";
    document.Info.Description = "本机只读诊断接口。读取已经记录的操作事实，不启动 AVEVA、不执行部署、不读取本机秘密。";
    return Task.CompletedTask;
}));
var app = builder.Build();
app.MapGet("/health", DiagnosticEndpoints.ReadHealth).WithName("GetHostHealth").WithTags("宿主状态");
app.MapGet("/api/operations", DiagnosticEndpoints.ReadOperations).WithName("ListOperationFacts").WithTags("操作事实");
app.MapOpenApi();
app.MapScalarApiReference(scalar => scalar
    .WithTitle("LaunchDesk · Scalar API 文档")
    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
    .DisableDefaultFonts()
    .DisableAgent());

if (builder.Configuration["docs-root"] is { Length: > 0 } docsRoot)
{
    var docs = new PhysicalFileProvider(Path.GetFullPath(docsRoot));
    app.Lifetime.ApplicationStopped.Register(docs.Dispose);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = docs,
        RequestPath = "/docs",
        OnPrepareResponse = context =>
        {
            // 页面与关系索引须重验证；按内容寻址的模块树可安全缓存，
            // 避免旧主题与新生成HTML混用。
            context.Context.Response.Headers.CacheControl =
                context.Context.Request.Path.Value?.StartsWith("/docs/assets-", StringComparison.Ordinal) == true
                    ? "public, max-age=31536000, immutable" : "no-cache";
        }
    });
    app.MapGet("/docs", () => Results.Redirect("/docs/index.html")).ExcludeFromDescription();
}
app.MapGet("/", () => Results.Content("""
<!doctype html><html lang="zh"><meta charset="utf-8"><title>LaunchDesk DevHost</title><style>body{font:16px system-ui;background:#f5f7fb;color:#182238;max-width:1100px;margin:48px auto}article{background:white;border:1px solid #e1e7f1;border-radius:14px;padding:24px;margin-top:20px}pre{white-space:pre-wrap}a,span{color:#4e5ddb}nav{display:flex;gap:24px}</style><h1>LaunchDesk <span>开发诊断</span></h1><nav><a href="/scalar/v1">Scalar · HTTP API</a><a href="/openapi/v1.json">OpenAPI JSON</a><a href="/docs/index.html">DocFX · C# API</a></nav><p>本机编排与事实观察 · 生产桌面端可独立运行</p><article><h2>操作事实</h2><pre id="runs">正在读取…</pre></article><script>async function refresh(){try{const data=await(await fetch('/api/operations')).json();document.querySelector('#runs').textContent=data.length?JSON.stringify(data,null,2):'暂无操作；此面板读取同一工作空间的操作日志。'}catch{document.querySelector('#runs').textContent='诊断连接中断'}}refresh();setInterval(refresh,2000)</script></html>
""", "text/html; charset=utf-8")).ExcludeFromDescription();
if (builder.Configuration["desktop"] is { Length: > 0 } executable)
{
    var start = new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
    start.ArgumentList.Add("--data-dir");
    start.ArgumentList.Add(location.Value.Directory);
    using var desktop = Process.Start(start);
    Console.WriteLine(desktop is null ? "Desktop was not created." : $"Desktop PID {desktop.Id}; stopping this host will not terminate it.");
}
await app.RunAsync();
