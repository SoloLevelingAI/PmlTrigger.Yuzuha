using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using YuzuhaToolkit.DevHost;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5276");
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Yuzuha 开发 API";
    document.Info.Version = "v1";
    document.Servers = [new() { Url = "/" }];
    document.Info.Description = "内置指南与纯文本预览。AVEVA 会话核验、PML 执行和知识库写入通过原生 CLI 调用；独立 MCP 服务已禁用。";
    return Task.CompletedTask;
}));

var app = builder.Build();
// The installed documentation opens in a read-only mode. Existing development
// routes below keep their original contracts when this option is absent.
if (builder.Configuration["docs-only"] == "true")
{
    var site = Path.GetFullPath(builder.Configuration["docs-root"] ?? throw new ArgumentException("docs-root is required."));
    if (!File.Exists(Path.Combine(site, "index.html"))) throw new DirectoryNotFoundException("Documentation index is missing.");
    app.Use(async (context, next) =>
    {
        if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip) ||
            context.Request.Host.Host != "127.0.0.1" || context.Request.Method is not ("GET" or "HEAD"))
        { context.Response.StatusCode = 403; return; }
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        await next();
    });
    var docs = new PhysicalFileProvider(site);
    app.Lifetime.ApplicationStopped.Register(docs.Dispose);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = docs, RequestPath = "/docs",
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache" });
    app.MapGet("/", () => Results.Redirect("/docs/index.html"));
    app.MapGet("/docs", () => Results.Redirect("/docs/index.html"));
    await app.RunAsync();
    return;
}
app.MapGet("/health", DevelopmentEndpoints.Health).WithName("GetDevelopmentHealth").WithTags("开发宿主");
app.MapGet("/api/guides", DevelopmentEndpoints.Guides).WithName("GetBuiltinGuides").WithTags("内置指南");
app.MapPost("/api/pml/preview", DevelopmentEndpoints.PreviewPml).WithName("PreviewPmlCall").WithTags("PML 预览");
app.MapGet("/api/knowledge/query-plan", DevelopmentEndpoints.PlanQuery).WithName("PlanKnowledgeQuery").WithTags("检索预览");
app.MapGet("/api/manager/contracts", DevelopmentEndpoints.ManagerContracts).WithName("GetManagerContracts").WithTags("管理契约（只读）");
app.MapGet("/api/manager/operations", DevelopmentEndpoints.ManagerOperations).WithName("GetManagerOperations").WithTags("管理契约（只读）");
app.MapGet("/api/manager/operations/{id}", DevelopmentEndpoints.ManagerOperation).WithName("GetManagerOperation").WithTags("管理契约（只读）");
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Yuzuha · HTTP API").DisableDefaultFonts().DisableAgent());

if (builder.Configuration["docs-root"] is { Length: > 0 } docsRoot)
{
    var provider = new PhysicalFileProvider(Path.GetFullPath(docsRoot));
    app.Lifetime.ApplicationStopped.Register(provider.Dispose);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = provider,
        RequestPath = "/docs",
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
    });
    app.MapGet("/docs", () => Results.Redirect("/docs/index.html")).ExcludeFromDescription();
}
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
await app.RunAsync();

/// <summary>可选开发宿主入口；供进程内 HTTP 集成测试定位程序集。</summary>
public partial class Program;
