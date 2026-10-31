using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>预检的决定与原始证据；allowed 只表示满足配置规则。</summary>
public sealed record LaunchPrecheckResult(bool Allowed, string Code, string Message, string CheckoutState, JsonElement? Evidence = null);

/// <summary>基础管理器不引用许可工作包，只有 required 策略才调用独立插件。</summary>
public static class LaunchLicensePreflight
{
    /// <summary>端口/服务、签出与进程启动分层；不会签出许可或改变服务。</summary>
    public static async Task<LaunchPrecheckResult> CheckAsync(LaunchProfile profile, ManagerPluginRegistry? registry = null, CancellationToken token = default)
    {
        var policy = profile.LicensePrecheck;
        if (policy is null || policy.Mode == "off") return new(true, "PRECHECK_OFF", "未要求许可预检。", "unverified");
        if (policy.Mode != "required") return new(false, "INVALID_LICENSE_POLICY", "未知许可策略，未创建启动入口。", "unverified");
        if (policy.ExtensionData?.Count > 0) return new(false, "UNSUPPORTED_LICENSE_POLICY", "策略含本版本不支持的规则，未创建启动入口；原配置保留。", "unverified");
        // 当前独立包不实现签出；不得把端口/服务证据升级为 checkout。
        if (policy.RequireCheckout) return new(false, "LICENSE_CHECKOUT_UNVERIFIED", "当前只读插件不能证明实际签出，未满足 requireCheckout。", "unverified");
        var properties = new Dictionary<string, JsonElement>
        {
            ["host"] = JsonSerializer.SerializeToElement(policy.Host, PreflightJsonContext.Default.String),
            ["rmsPort"] = JsonSerializer.SerializeToElement(policy.RmsPort, PreflightJsonContext.Default.Int32),
            ["timeoutMilliseconds"] = JsonSerializer.SerializeToElement(policy.TimeoutMilliseconds, PreflightJsonContext.Default.Int32)
        };
        if (policy.RulesPort is { } rules) properties["rulesPort"] = JsonSerializer.SerializeToElement(rules, PreflightJsonContext.Default.Int32);
        if (policy.ServiceName is { } service) properties["serviceName"] = JsonSerializer.SerializeToElement(service, PreflightJsonContext.Default.String);
        var response = await (registry ?? new()).InvokeAsync("license", "license.diagnose",
            JsonSerializer.SerializeToElement(properties, PreflightJsonContext.Default.DictionaryStringJsonElement), new(), token).ConfigureAwait(false);
        if (!response.Success) return new(false, response.Error?.Code ?? "LICENSE_PRECHECK_FAILED", response.Error?.Message ?? "许可插件未返回成功诊断。", "unverified", response.Data);
        var evidence = response.Data;
        var passed = evidence is { ValueKind: JsonValueKind.Object } data && Port(data, "rms", policy.Host, policy.RmsPort) &&
            (policy.RulesPort is not { } port || Port(data, "rules", policy.Host, port)) &&
            (policy.ServiceName is null || data.TryGetProperty("service", out var state) && state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("name", out var name) && name.GetString() == policy.ServiceName && state.TryGetProperty("state", out var value) && value.GetString() == "running");
        return new(passed, passed ? "LICENSE_REACHABILITY_PASSED" : "LICENSE_PRECHECK_REJECTED",
            passed ? "已满足所选端口/服务规则；实际签出仍未验证。" : "所选端口/服务规则未通过，未创建启动入口。", "unverified", evidence);
    }
    private static bool Port(JsonElement data, string key, string host, int port) => data.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("host", out var target) && target.GetString() == host && item.TryGetProperty("port", out var number) && number.TryGetInt32(out var actual) && actual == port &&
        item.TryGetProperty("state", out var state) && state.GetString() == "reachable";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(LaunchPrecheckResult))]
internal partial class PreflightJsonContext : JsonSerializerContext;
