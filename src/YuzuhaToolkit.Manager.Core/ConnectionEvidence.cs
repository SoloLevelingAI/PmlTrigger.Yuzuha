using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>目标进程、模块、产品 profile 与部署内容的联合身份。</summary>
public sealed record ManagerConnectionTarget(int ProcessId, long StartTimeUtcTicks, string Model, string? Profile, string DeploymentFingerprint);
/// <summary>一盏证据灯，状态为 green/yellow/red/grey，并保留原因。</summary>
public sealed record ConnectionLamp(string State, string Code, string Message);
/// <summary>只代表本次测试时刻的证据，有明确过期时间，不代表后续命令授权。</summary>
public sealed record ManagerConnectionEvidence(ManagerConnectionTarget Target, string RequestId,
    DateTimeOffset ObservedAtUtc, DateTimeOffset ExpiresAtUtc, ConnectionLamp Overall,
    ConnectionLamp Identity, ConnectionLamp Pipe, ConnectionLamp Protocol, ConnectionLamp Deployment);
/// <summary>管理器测试证据及原通信操作结果；保留业务错误和 unknown 标记。</summary>
public sealed record ManagerConnectionTestResult(ManagerConnectionEvidence Evidence, OperationResult Communication);

/// <summary>GUI/CLI 共用的灯态推导；窗口发现和创建入口不能生成绿色 Host 灯。</summary>
public static class ConnectionEvidence
{
    /// <summary>仅从关联通信回执生成灯态，逐项核对完整身份和协议。</summary>
    public static ManagerConnectionEvidence FromResponse(CommunicationResponse response, OperationContext context,
        string? profile, string deploymentFingerprint, DateTimeOffset? observedAt = null)
    {
        var now = observedAt ?? DateTimeOffset.UtcNow;
        var target = new ManagerConnectionTarget(context.ProcessId ?? 0, context.ExpectedProcessStartTimeUtcTicks ?? 0,
            context.ExpectedModel ?? "", profile, deploymentFingerprint);
        var identity = Grey("UNVERIFIED", "尚无完整 Host 身份证据。");
        var pipe = Grey("UNVERIFIED", "尚未核验目标专用管道。");
        var protocol = Grey("UNVERIFIED", "尚未核验 Host 协议。");
        var deployment = deploymentFingerprint == "unbound" ? Yellow("DEPLOYMENT_UNBOUND", "未绑定运行目录，不能判断部署变化。") : Green("DEPLOYMENT_BOUND", "本次测试已绑定运行文件内容指纹。");
        var result = response.Result;
        if (!result.Success)
        {
            var unknown = result.Error?.MayHaveExecuted == true || result.Error?.Code is "CLI_TIMEOUT" or "CANCELLED" or "CLI_TRANSPORT_FAILED";
            var failure = new ConnectionLamp(unknown ? "yellow" : "red", result.Error?.Code ?? "TEST_FAILED", result.Error?.Message ?? "通信测试未通过。");
            return new(target, response.RequestId, now, now.AddSeconds(60), failure, identity, pipe, protocol, deployment);
        }
        try
        {
            if (response.ProtocolVersion != CommunicationProtocol.Version || result.SchemaVersion != 1 || result.OperationId is not ("aveva.session.status" or "aveva.session.select"))
                throw new InvalidDataException("连接证据的通信契约不匹配。");
            var data = result.Data ?? throw new InvalidDataException("通信回执没有状态数据。");
            var host = Field(data, "Host");
            var pipeName = Text(data, "PipeName"); var hostPipe = Text(host, "PipeName");
            var verified = Boolean(data, "SessionSelected") && Boolean(data, "Connected") && Boolean(data, "TargetVerified") &&
                target.ProcessId > 0 && target.StartTimeUtcTicks > 0 && !string.IsNullOrWhiteSpace(target.Model) &&
                Number(data, "ExpectedProcessId") == target.ProcessId && Number(data, "ExpectedProcessStartTimeUtcTicks") == target.StartTimeUtcTicks &&
                Number(host, "ProcessId") == target.ProcessId && Number(host, "ProcessStartTimeUtcTicks") == target.StartTimeUtcTicks &&
                target.Model.Equals(Text(data, "ExpectedModel"), StringComparison.OrdinalIgnoreCase) && target.Model.Equals(Text(host, "Model"), StringComparison.OrdinalIgnoreCase);
            identity = verified ? Green("TARGET_VERIFIED", "PID、启动时间及模块均匹配。") : Red("TARGET_MISMATCH", "目标身份、启动时间或模块不匹配。");
            pipe = !string.IsNullOrWhiteSpace(pipeName) && pipeName.Equals(hostPipe, StringComparison.Ordinal)
                ? Green("PIPE_VERIFIED", "目标专用管道与 Host 一致。") : Red("PIPE_MISMATCH", "缺少匹配的目标专用管道。");
            var expectedFramework = Framework(profile);
            var frameworkMatches = expectedFramework is null || expectedFramework.Equals(Text(host, "HostFramework"), StringComparison.OrdinalIgnoreCase);
            var protocols = Number(host, "FormQueryVersion") == 1 && Number(host, "BoundedArrayReadVersion") == 1;
            protocol = !frameworkMatches ? Red("FRAMEWORK_MISMATCH", "产品 profile 与 Host 框架不匹配。") :
                !protocols ? Yellow("PROTOCOL_INSUFFICIENT", "Host 尚不支持要求的查询协议 v1。") :
                profile is null ? Yellow("PROFILE_UNBOUND", "Host 协议已核验，但未指定产品 profile。") : Green("PROTOCOL_VERIFIED", "产品框架与查询协议 v1 均匹配。");
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or FormatException or OverflowException)
        { identity = Red("INVALID_STATUS", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message); }
        var lamps = new[] { identity, pipe, protocol, deployment };
        var overall = lamps.FirstOrDefault(l => l.State == "red") ?? lamps.FirstOrDefault(l => l.State == "yellow") ??
            (lamps.All(l => l.State == "green") ? Green("CONNECTED_VERIFIED", "当前目标身份、管道、协议和部署绑定均通过。") : Grey("UNVERIFIED", "尚有未核验项目。"));
        return new(target, response.RequestId, now, now.AddSeconds(60), overall, identity, pipe, protocol, deployment);
    }

    /// <summary>发现仅提示选择，单个候选或观察到管道均不能代替 Host 测试。</summary>
    public static ConnectionLamp DiscoveryLamp(int count, string? error = null) => error is not null ? Red("DISCOVERY_FAILED", error) :
        count == 0 ? Grey("NO_SESSIONS", "没有发现可选工程会话。") : count > 1 ? Yellow("TARGET_SELECTION_REQUIRED", "发现多个会话，请明确选择目标后测试。") :
        Grey("HOST_NOT_TESTED", "已发现一个会话，尚未核验 Host。");

    /// <summary>目标、部署或时间变化立即清除旧成功证据；调用者提供当前发现身份。</summary>
    public static ManagerConnectionEvidence Revalidate(ManagerConnectionEvidence evidence, int? processId,
        long? startTimeUtcTicks, string? currentModel, string deploymentFingerprint, DateTimeOffset? at = null)
    {
        ConnectionLamp? reason = processId is null ? Red("PROCESS_EXITED", "目标进程已退出或不可访问。") :
            processId != evidence.Target.ProcessId || startTimeUtcTicks != evidence.Target.StartTimeUtcTicks ? Red("PROCESS_CHANGED", "PID 或启动身份已变化，请重新测试。") :
            !string.Equals(currentModel, evidence.Target.Model, StringComparison.OrdinalIgnoreCase) ? Red("MODULE_CHANGED", "目标模块已变化，请重新测试。") :
            deploymentFingerprint != evidence.Target.DeploymentFingerprint ? Grey("DEPLOYMENT_CHANGED", "部署内容已变化，旧灯态已清除。") :
            (at ?? DateTimeOffset.UtcNow) >= evidence.ExpiresAtUtc ? Grey("EVIDENCE_EXPIRED", "连接证据已过期，请重新测试。") : null;
        return reason is null ? evidence : evidence with { Overall = reason, Identity = reason, Pipe = reason, Protocol = reason, Deployment = reason };
    }

    /// <summary>对选择的运行 Host、模块初始化 PML、加载索引及可选环境文件冻结指纹；必需文件缺失即失败，不执行文件。</summary>
    public static string Fingerprint(string? runtimeRoot, string? profile, string? environmentFile = null)
    {
        if (runtimeRoot is null) return "unbound";
        var framework = Framework(profile) ?? throw new ArgumentException("绑定运行目录时必须选择产品 profile。");
        if (!Path.IsPathFullyQualified(runtimeRoot)) throw new ArgumentException("运行目录需要绝对路径。");
        var host = "runtime/profiles/" + profile + "/" + framework + "/YuzuhaToolkit.PmlHost.Net" + (framework == "net35" ? "35" : "48") + ".dll";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var files = new List<string>
        {
            PathRules.Inside(runtimeRoot, host),
            PathRules.Inside(runtimeRoot, "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc"),
            PathRules.Inside(runtimeRoot, "PMLLIB/Addins/YuzuhaRpc.pmlobj"),
            PathRules.Inside(runtimeRoot, "PMLLIB/Addins/YuzuhaAddin.pmlobj"),
            PathRules.Inside(runtimeRoot, "PMLLIB/pml.index")
        };
        if (environmentFile is not null) { if (!Path.IsPathFullyQualified(environmentFile)) throw new ArgumentException("环境文件需要绝对路径。"); files.Add(Path.GetFullPath(environmentFile)); }
        foreach (var file in files)
        {
            PathRules.NoLinks(file); using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 256L * 1024 * 1024) throw new IOException("连接证据文件超过预算。");
            hash.AppendData(Encoding.UTF8.GetBytes(file.ToUpperInvariant() + "\0")); hash.AppendData(SHA256.HashData(input));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string? Framework(string? profile) => profile switch { "AM" or "PDMS" => "net35", "E3D2.1" or "E3D3.1.6" => "net48", null => null, _ => throw new ArgumentException("不支持的产品 profile。") };
    private static JsonElement Field(JsonElement value, string key) => value.TryGetProperty(key, out var field) ? field : value.TryGetProperty(char.ToLowerInvariant(key[0]) + key[1..], out field) ? field : throw new InvalidDataException("状态缺少字段：" + key);
    private static string Text(JsonElement value, string key) => Field(value, key).GetString() ?? "";
    private static bool Boolean(JsonElement value, string key) => Field(value, key).GetBoolean();
    private static long Number(JsonElement value, string key) => Field(value, key).GetInt64();
    private static ConnectionLamp Green(string code, string text) => new("green", code, text);
    private static ConnectionLamp Yellow(string code, string text) => new("yellow", code, text);
    private static ConnectionLamp Red(string code, string text) => new("red", code, text);
    private static ConnectionLamp Grey(string code, string text) => new("grey", code, text);
}
