using System.Net;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>Ribbon HTTP v1 薄适配；编辑、历史与部署仍由原服务唯一处理，不复制其存储规则。</summary>
public sealed class RibbonHttpAdapter
{
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly TimeSpan timeout;
    private const int MaximumBytes = 20 * 1024 * 1024;

    /// <summary>绑定明确本机端点；生产客户端禁用重定向与代理，不接受任意远程服务。</summary>
    public RibbonHttpAdapter(Uri endpoint, HttpClient client, TimeSpan? timeout = null)
    {
        if (endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" || endpoint.AbsolutePath != "/" ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
            throw new ArgumentException("Ribbon 端点必须为 http://127.0.0.1:<port>/。");
        this.endpoint = endpoint;
        this.client = client;
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    /// <summary>创建本机客户端；不启动 Ribbon、不创建工作区或触发导入。</summary>
    public static RibbonHttpAdapter Local() => new(new Uri(Environment.GetEnvironmentVariable("YUZUHA_RIBBON_ENDPOINT") ?? "http://127.0.0.1:4318/"),
        SharedClient);
    private static readonly HttpClient SharedClient = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
    { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>固定 HTTP v1 路由表；原服务仍负责输入限制、绑定、保护与冲突判断。</summary>
    public static readonly IReadOnlyDictionary<string, bool> Routes = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["projects/validate"] = false, ["projects/normalize"] = false, ["uic/export"] = false,
        ["uic/import"] = true, ["workspace/save"] = true, ["workspace/context"] = false,
        ["workspace/open"] = false, ["workspace/restore"] = true, ["workspace/original"] = false,
        ["workspace/versions"] = false, ["deployment/modules"] = false, ["deployment/file-preview"] = false,
        ["deployment/file-apply"] = true, ["deployment/registration-preview"] = false,
        ["deployment/registration-apply"] = true, ["management/list"] = false,
        ["management/adopt"] = true, ["management/toggle"] = true
    }.AsReadOnly();

    /// <summary>单次请求保留 HTTP 状态及原响应；写请求失去确定回执返回 unknown，永不重发。</summary>
    public async Task<OperationResult> InvokeAsync(string operation, string route, JsonElement input, CancellationToken token)
    {
        if (!Routes.TryGetValue(route, out var writes)) throw new ArgumentException("未注册 Ribbon 路由。");
        var body = Encoding.UTF8.GetBytes(input.GetRawText());
        if (body.Length > MaximumBytes) return OperationResult.Fail(operation, 2, "RIBBON_INPUT_LIMIT", "Ribbon 输入超过 20 MiB。");
        token.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        token = deadline.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/v1/" + route));
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidDataException("Ribbon 响应超过 20 MiB。");
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + count > MaximumBytes) throw new InvalidDataException("Ribbon 响应超过 20 MiB。");
                buffer.Write(chunk, 0, count);
            }
            // Construct JSON explicitly for Native AOT; preserve binary/XML exports without decoding loss.
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject(); writer.WriteNumber("httpStatus", (int)response.StatusCode);
                writer.WriteString("mediaType", response.Content.Headers.ContentType?.MediaType);
                if (response.Content.Headers.ContentType?.MediaType == "application/json")
                {
                    JsonDocument? json = null;
                    try { json = JsonDocument.Parse(buffer.ToArray()); }
                    catch (JsonException) when (!response.IsSuccessStatusCode)
                    {
                        writer.WriteBoolean("bodyParseError", true);
                        writer.WriteBase64String("bytesBase64", buffer.ToArray());
                    }
                    using (json)
                        if (json is not null) { writer.WritePropertyName("response"); json.RootElement.WriteTo(writer); }
                }
                else writer.WriteBase64String("bytesBase64", buffer.ToArray());
                writer.WriteEndObject();
            }
            using var data = JsonDocument.Parse(output.ToArray());
            return response.IsSuccessStatusCode ? OperationResult.Ok(operation, data.RootElement.Clone())
                : OperationResult.Fail(operation, response.StatusCode == HttpStatusCode.Forbidden ? 5 : 7,
                    "RIBBON_HTTP_" + (int)response.StatusCode, "Ribbon 拒绝请求；原状态与诊断见 data。", data.RootElement.Clone());
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
        {
            return OperationResult.Fail(operation, 7, writes ? "RIBBON_RESULT_UNKNOWN" : "RIBBON_UNAVAILABLE",
                writes ? "写请求未取得可确认回执；请在原工作区核对，不要自动重放。" : "Ribbon 服务未返回可用结果：" + error.GetType().Name,
                mayHaveExecuted: writes);
        }
    }

    /// <summary>注册管理 CLI 兼容入口，写操作需要独立 --allow-write；不推断宏执行授权。</summary>
    public static IEnumerable<OperationRegistration> Registrations(Func<RibbonHttpAdapter>? factory = null)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":true}");
        foreach (var route in Routes)
        {
            var path = route.Key;
            yield return new(new("ribbon." + path.Replace('/', '.'), 1, "Ribbon HTTP v1 兼容适配；原后端校验、历史与部署语义保持。",
                schema.RootElement.Clone(), RequiresWrite: route.Value),
                new DelegateOperationHandler((id, input, _, token) => (factory ?? Local)().InvokeAsync(id, path, input, token)));
        }
    }
}
