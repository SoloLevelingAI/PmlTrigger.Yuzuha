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
    document.Info.Description = "内置指南与纯文本预览。AVEVA 会话选择、PML 执行和知识库写入继续使用原 MCP 服务。";
    return Task.CompletedTask;
}));

var app = builder.Build();
app.MapGet("/health", DevelopmentEndpoints.Health).WithName("GetDevelopmentHealth").WithTags("开发宿主");
app.MapGet("/api/guides", DevelopmentEndpoints.Guides).WithName("GetBuiltinGuides").WithTags("内置指南");
app.MapPost("/api/pml/preview", DevelopmentEndpoints.PreviewPml).WithName("PreviewPmlCall").WithTags("PML 预览");
app.MapGet("/api/knowledge/query-plan", DevelopmentEndpoints.PlanQuery).WithName("PlanKnowledgeQuery").WithTags("检索预览");
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
