using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>启动 Feature 的本机 HTML 适配器；业务仍由管理器操作目录处理。</summary>
/// <remarks>仅监听 IPv4 回环；同源令牌防止外部网页调用。该令牌不是多用户身份认证。</remarks>
public sealed class LaunchWorkbenchServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly OperationExecutor executor;
    private readonly string assets;
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly SemaphoreSlim slots = new(16);
    private readonly HashSet<string> operations = new(StringComparer.Ordinal)
    {
        "environment.discover", "environment.resolve", "launch.workspace.read", "launch.profile.list",
        "launch.profile.read", "launch.profile.preview", "launch.profile.save", "launch.plan", "launch.execute", "launch.observe"
    };
    private static readonly IReadOnlyDictionary<string, string> StaticFiles = new Dictionary<string, string>
    {
        ["index.html"] = "text/html; charset=utf-8", ["tokens.css"] = "text/css; charset=utf-8",
        ["launch-workflow.css"] = "text/css; charset=utf-8", ["launch-workflow.js"] = "text/javascript; charset=utf-8"
    };
    private static readonly string[] SourceFiles =
    [
        "src/YuzuhaToolkit.Manager.Core/LaunchDiscovery.cs",
        "src/YuzuhaToolkit.Manager.Core/LaunchDiscoveryOperations.cs",
        "src/YuzuhaToolkit.Manager.Core/LaunchOperationCatalog.cs",
        "src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs",
        "src/YuzuhaToolkit.Manager.Core/LaunchWorkflowOperations.cs",
        "openspec/changes/integrate-pmltwin-product-platform/contracts.md"
    ];

    /// <summary>实际监听地址；不包含会话令牌或登录信息。</summary>
    public Uri Address { get; }

    /// <summary>创建本机适配器；资产目录由宿主固定，不接受 HTTP 路径指定。</summary>
    /// <param name="executor">与 CLI 相同的业务执行器。</param>
    /// <param name="assetDirectory">含 index.html 的本机目录。</param>
    /// <param name="port">0 表示选择空闲回环端口。</param>
    public LaunchWorkbenchServer(OperationExecutor executor, string assetDirectory, int port = 0)
    {
        this.executor = executor;
        assets = Path.GetFullPath(assetDirectory);
        if (!File.Exists(Path.Combine(assets, "index.html"))) throw new FileNotFoundException("启动页面不存在。", assets);
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (port == 0)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        }
        Address = new Uri($"http://127.0.0.1:{port}/");
        listener.Prefixes.Add(Address.AbsoluteUri);
    }

    /// <summary>启动监听；失败时不回退到公网地址。</summary>
    public void Start() => listener.Start();

    /// <summary>处理请求直到宿主取消；不记录请求体，不因页面断开自动重试操作。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = new List<Task>();
        using var stop = cancellationToken.Register(() => listener.Stop());
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                if (!slots.Wait(0)) { context.Response.StatusCode = 503; context.Response.Close(); continue; }
                pending.RemoveAll(task => task.IsCompleted);
                pending.Add(HandleAsync(context, cancellationToken));
            }
        }
        catch (Exception error) when (cancellationToken.IsCancellationRequested && error is HttpListenerException or ObjectDisposedException) { }
        finally { await Task.WhenAll(pending).ConfigureAwait(false); }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var response = context.Response;
        using var readBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readBudget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            var request = context.Request;
            if (request.Url?.Authority != Address.Authority || request.RemoteEndPoint is not { } remote || !IPAddress.IsLoopback(remote.Address))
            { response.StatusCode = 403; return; }
            var path = request.Url.AbsolutePath;
            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                if (request.HttpMethod != "GET") { response.StatusCode = 405; return; }
                if (path == "/guide" || path.StartsWith("/source/", StringComparison.Ordinal))
                {
                    var relative = path == "/guide" ? "docs/reference/launch-workflow.md" : path[8..];
                    if (path != "/guide" && !SourceFiles.Contains(relative, StringComparer.Ordinal)) { response.StatusCode = 404; return; }
                    var local = ResolveSource(relative);
                    if (local is null) { response.StatusCode = 404; return; }
                    response.ContentType = "text/html; charset=utf-8";
                    var source = WebUtility.HtmlEncode(await File.ReadAllTextAsync(local, cancellationToken));
                    await WriteAsync(response, "<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><link rel=\"stylesheet\" href=\"/tokens.css\"><link rel=\"stylesheet\" href=\"/launch-workflow.css\"><title>源码与使用说明 · PMLTWIN</title><main><a href=\"/\">← 返回工作台</a><h1>" + WebUtility.HtmlEncode(relative) + "</h1><pre>" + source + "</pre></main></html>", cancellationToken);
                    return;
                }
                var file = path == "/" ? "index.html" : path.TrimStart('/');
                if (!StaticFiles.TryGetValue(file, out var type)) { response.StatusCode = 404; return; }
                var body = await File.ReadAllTextAsync(Path.Combine(assets, file), cancellationToken);
                if (file == "index.html")
                {
                    var meta = $"<meta name=\"pmltwin-token\" content=\"{token}\">";
                    body = body.Contains("<meta name=\"pmltwin-token\" content=\"\">", StringComparison.Ordinal)
                        ? body.Replace("<meta name=\"pmltwin-token\" content=\"\">", meta, StringComparison.Ordinal)
                        : body.Replace("</head>", meta + "</head>", StringComparison.OrdinalIgnoreCase);
                }
                response.ContentType = type;
                await WriteAsync(response, body, cancellationToken); return;
            }
            var origin = request.Headers["Origin"];
            if (request.Headers["X-PmlTwin-Token"] != token ||
                origin is not null && origin != Address.GetLeftPart(UriPartial.Authority))
            { response.StatusCode = 403; return; }
            response.ContentType = "application/json; charset=utf-8";
            if (path == "/api/operations" && request.HttpMethod == "GET")
            {
                await WriteAsync(response, JsonSerializer.Serialize(executor.Registry.Descriptors.Where(d => operations.Contains(d.Id)).ToArray(),
                    OperationJsonContext.Default.OperationDescriptorArray), cancellationToken); return;
            }
            const string prefix = "/api/operations/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || request.HttpMethod != "POST")
            { response.StatusCode = 404; return; }
            var id = path[prefix.Length..];
            if (!operations.Contains(id)) { response.StatusCode = 404; return; }
            if (request.ContentType?.Split(';')[0].Trim() != "application/json") { response.StatusCode = 415; return; }
            const int limit = 256 * 1024;
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await request.InputStream.ReadAsync(buffer, readBudget.Token)) != 0)
            {
                if (bytes.Length + count > limit) { response.StatusCode = 413; return; }
                bytes.Write(buffer, 0, count);
            }
            using var json = JsonDocument.Parse(bytes.ToArray());
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root) || root.EnumerateObject().Any(p => p.Name is not ("input" or "allowWrite" or "allowLaunch")) ||
                !root.TryGetProperty("input", out var input))
            { response.StatusCode = 400; return; }
            var call = new OperationContext(AllowWrite: root.TryGetProperty("allowWrite", out var write) && write.GetBoolean(),
                AllowLaunch: root.TryGetProperty("allowLaunch", out var launch) && launch.GetBoolean());
            var result = await executor.InvokeAsync(id, input, call, cancellationToken).ConfigureAwait(false);
            await WriteAsync(response, JsonSerializer.Serialize(result, OperationJsonContext.Default.OperationResult), cancellationToken);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        { response.StatusCode = 400; }
        catch (Exception error) when (error is IOException or HttpListenerException or OperationCanceledException or ObjectDisposedException)
        {
            // 请求断开不能撤销已受理动作；不要返回空的成功响应。
            try { response.StatusCode = error is OperationCanceledException ? 408 : 500; }
            catch (ObjectDisposedException) { }
        }
        finally { response.Close(); slots.Release(); }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) if (HasDuplicateProperties(item)) return true;
        return false;
    }

    private static async Task WriteAsync(HttpListenerResponse response, string text, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private string? ResolveSource(string relative)
    {
        var packaged = Path.Combine(assets, "source", relative);
        if (File.Exists(packaged)) return packaged;
        for (var directory = new DirectoryInfo(assets); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "YuzuhaToolkit.Development.slnx"))) continue;
            var file = Path.Combine(directory.FullName, relative);
            return File.Exists(file) ? file : null;
        }
        return null;
    }

    /// <summary>释放本机监听器；操作恢复由持久回执服务负责。</summary>
    public void Dispose() => listener.Close();
}
