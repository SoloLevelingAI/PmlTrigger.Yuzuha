using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Plugins.License;

/// <summary>只报告 TCP 连接证据，不发送许可签出请求。</summary>
public sealed record LicensePortEvidence(string Host, int Port, string State, string Message);
/// <summary>Windows 服务只读状态；不存在与读取失败分别报告。</summary>
public sealed record LicenseServiceEvidence(string Name, string State, string Message);
/// <summary>端口、服务与未验证签出分层，端口可达不等于许可已可用。</summary>
public sealed record LicenseDiagnosis(LicensePortEvidence? Rms, LicensePortEvidence? Rules,
    LicenseServiceEvidence? Service, string CheckoutState, DateTimeOffset ObservedAtUtc);

/// <summary>独立可选包的数据元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LicensePortEvidence))]
[JsonSerializable(typeof(LicenseServiceEvidence))]
[JsonSerializable(typeof(LicenseDiagnosis))]
public partial class LicenseJsonContext : JsonSerializerContext;

/// <summary>许可只读诊断，不含 AVEVA/RPC 或 VM 依赖，不自动启动服务。</summary>
public static class LicensePlugin
{
    /// <summary>编译进插件的版本与操作，用于父进程握手。</summary>
    public static PluginCapabilities Capabilities => new("license", "1.0.0", 1, Registrations().Select(r => r.Descriptor).ToArray());

    /// <summary>可替换的探针只供隔离验证；默认进行 TCP connect 和只读 SCM 查询。</summary>
    public static OperationRegistration[] Registrations(Func<string, int, int, CancellationToken, Task<LicensePortEvidence>>? tcp = null,
        Func<string, LicenseServiceEvidence>? service = null)
    {
        const string host = "\"host\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":255,\"pattern\":\"^[^\\\\s\\\\x00-\\\\x1f]+$\"}";
        const string port = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":65535}";
        const string name = "\"serviceName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":128,\"pattern\":\"^[A-Za-z0-9 _.()-]+$\"}";
        const string timeout = "\"timeoutMilliseconds\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":10000}";
        return [Entry("license.tcp.test", host + ",\"port\":" + port + "," + timeout, "\"host\",\"port\""),
            Entry("license.service.test", name, "\"serviceName\""),
            Entry("license.diagnose", host + ",\"rmsPort\":" + port + ",\"rulesPort\":" + port + "," + name + "," + timeout, "\"host\",\"rmsPort\"")];

        OperationRegistration Entry(string id, string properties, string required)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Read-only reachability/service evidence. Checkout remains unverified; this operation never grants a license.", schema.RootElement.Clone()),
                new DelegateOperationHandler(async (operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    var milliseconds = input.TryGetProperty("timeoutMilliseconds", out var wait) ? wait.GetInt32() : 1500;
                    if (operation == "license.service.test") return OperationResult.Ok(operation,
                        JsonSerializer.SerializeToElement((service ?? Service)(input.GetProperty("serviceName").GetString()!), LicenseJsonContext.Default.LicenseServiceEvidence));
                    var target = input.GetProperty("host").GetString()!;
                    if (operation == "license.tcp.test") return OperationResult.Ok(operation,
                        JsonSerializer.SerializeToElement(await (tcp ?? Connect)(target, input.GetProperty("port").GetInt32(), milliseconds, token).ConfigureAwait(false), LicenseJsonContext.Default.LicensePortEvidence));
                    var rms = await (tcp ?? Connect)(target, input.GetProperty("rmsPort").GetInt32(), milliseconds, token).ConfigureAwait(false);
                    var rules = input.TryGetProperty("rulesPort", out var rulesPort) ? await (tcp ?? Connect)(target, rulesPort.GetInt32(), milliseconds, token).ConfigureAwait(false) : null;
                    var state = input.TryGetProperty("serviceName", out var serviceName) ? (service ?? Service)(serviceName.GetString()!) : null;
                    return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(new LicenseDiagnosis(rms, rules, state, "unverified", DateTimeOffset.UtcNow), LicenseJsonContext.Default.LicenseDiagnosis));
                }));
        }
    }

    /// <summary>TCP connect 后立即关闭，不交换应用数据；超时与不可达分开。</summary>
    public static async Task<LicensePortEvidence> Connect(string host, int port, int milliseconds, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(milliseconds);
        using var client = new TcpClient();
        try { await client.ConnectAsync(host, port, deadline.Token).ConfigureAwait(false); return new(host, port, "reachable", "TCP 连接可达，未验证实际许可签出。"); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(host, port, "timeout", "TCP 连接超时，未验证许可签出。"); }
        catch (SocketException error) { return new(host, port, "unreachable", "TCP 不可达：" + error.SocketErrorCode); }
    }

    /// <summary>仅查询已有本机服务，不执行启动、停止或配置动作。</summary>
    public static LicenseServiceEvidence Service(string name) => OperatingSystem.IsWindows() ? WindowsService.Query(name) : new(name, "unsupported", "本平台不支持 Windows 服务查询。");

    /// <summary>通过服务控制管理器的只读句柄查询本机服务，并释放所有原生句柄。</summary>
    [SupportedOSPlatform("windows")]
    private static class WindowsService
    {
        /// <summary>读取指定服务的当前状态；区分未安装、不可读取和过渡状态，不启动或停止服务。</summary>
        public static LicenseServiceEvidence Query(string name)
        {
            var manager = OpenSCManager(null, null, 1);
            if (manager == IntPtr.Zero) return Error(name, "读取服务管理器失败");
            try
            {
                var service = OpenService(manager, name, 4);
                if (service == IntPtr.Zero) return Error(name, "读取服务失败");
                try
                {
                    if (!QueryServiceStatus(service, out var status)) return Error(name, "查询服务状态失败");
                    var state = status.CurrentState switch { 1 => "stopped", 2 => "start-pending", 3 => "stop-pending", 4 => "running", 7 => "paused", _ => "transitional" };
                    return new(name, state, "只读服务状态，不代表许可签出已验证。");
                }
                finally { CloseServiceHandle(service); }
            }
            finally { CloseServiceHandle(manager); }
        }
        private static LicenseServiceEvidence Error(string name, string message)
        {
            var error = Marshal.GetLastWin32Error(); return new(name, error == 1060 ? "missing" : "unavailable", message + "，Win32=" + error);
        }
        /// <summary>保持 Win32 SERVICE_STATUS 的字段顺序，接收 QueryServiceStatus 的状态快照。</summary>
        [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus {
            /// <summary>服务类型、当前状态、接受的控制码、系统及服务退出码、过渡检查点和预计等待毫秒数。</summary>
            public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode, CheckPoint, WaitHint;
        }
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr OpenSCManager(string? machine, string? database, uint access);
        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr OpenService(IntPtr manager, string name, uint access);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
        [DllImport("advapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseServiceHandle(IntPtr service);
    }
}
