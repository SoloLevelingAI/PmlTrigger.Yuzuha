using System.Net;
using System.Security.Cryptography;
using YuzuhaToolkit.WebApps;

namespace YuzuhaToolkit.WebApps;

/// <summary>独立本地 HTML 应用宿主，不改变现有 DevHost/MCP/Desktop 的入口。</summary>
public static class WebAppsProgram
{
/// <summary>读取本机端口和数据目录设置，启动目录、会话与绑定 API。</summary>
public static async Task Main(string[] args)
{
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args,
    ContentRootPath = AppContext.BaseDirectory, WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot") });
var port = int.Parse(builder.Configuration["port"] ?? "5288", System.Globalization.CultureInfo.InvariantCulture);
if (port is < 1024 or > 65535) throw new ArgumentException("端口范围为 1024–65535。");
var origin = "http://127.0.0.1:" + port;
builder.WebHost.UseUrls(origin);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 512 * 1024);
var directory = Path.GetFullPath(builder.Configuration["data-dir"] ?? YuzuhaToolkit.Workspace.UserDataPaths.AppsDirectory);
var root = Path.GetFullPath(builder.Configuration["pml-root"] ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuzuhaToolkit", "PmlTrigger.Yuzuha", "PMLLIB"));
if ((directory + root).Any(c => c is '$' or '\r' or '\n')) throw new ArgumentException("本地路径包含不支持的 PML 展开符。");
if (!Directory.Exists(root)) throw new DirectoryNotFoundException("PMLLIB 目录不存在：" + root);
var seed = Path.Combine(AppContext.BaseDirectory, "apps", "catalog.json");
var store = new CatalogStore(directory, seed);
var service = new ManagerService(store, new ExistingIpc(), directory, root);
var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var app = builder.Build();
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip) ||
        request.Host.Value != "127.0.0.1:" + port ||
        request.Headers.Origin.Count > 0 && request.Headers.Origin != origin ||
        request.Headers["Sec-Fetch-Site"] == "cross-site")
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (request.Method is not ("GET" or "HEAD") && request.Headers["X-Yuzuha-Token"] != nonce)
    { context.Response.StatusCode = 403; return; }
    try { await next(); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or System.Text.Json.JsonException)
    {
        context.Response.StatusCode = e is InvalidOperationException ? 409 : 400;
        await context.Response.WriteAsJsonAsync(new { error = e.Message });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => new { service = "YuzuhaToolkit.WebApps", version = 1, dataDirectory = directory });
app.MapGet("/api/bootstrap", () => new { token = nonce, catalog = store.Read(), selected = service.Selected });
app.MapGet("/api/catalog", store.Read);
app.MapPut("/api/catalog", (AppCatalog catalog) => store.Save(catalog));
app.MapGet("/api/sessions", service.Sessions);
app.MapPost("/api/session", async (SelectRequest input, CancellationToken token) => await service.SelectAsync(input.ProcessId, token));
app.MapPost("/api/invoke", async (InvokeRequest input, CancellationToken token) => await service.InvokeAsync(input, token));
app.MapGet("/api/runs", service.Runs);
app.MapGet("/api/runs/{id}", service.ReadRun);
await app.RunAsync();
}
}

/// <summary>会话选择请求，只接受发现结果中的 PID。</summary>
public sealed record SelectRequest(int ProcessId);
