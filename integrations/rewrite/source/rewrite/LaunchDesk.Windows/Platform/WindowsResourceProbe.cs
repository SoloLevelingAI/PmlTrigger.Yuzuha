using System.Net;
using System.Net.Sockets;

namespace LaunchDesk.Windows;

/// <summary>实现冻结 TCP、服务或虚拟机入口描述的一次 Windows 观测。</summary>
/// <param name="machineId"><include file="../../Comments.xml" path="doc/MachineId/param[@name='machineId']/*"/></param>
public sealed class WindowsResourceProbe(Guid? machineId = null) : IResourceProbe
{
    /// <inheritdoc />
    public async ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct)
    {
        if (machineId.HasValue && resource.MachineId != machineId.Value)
            return Result<ProbeObservation>.Fail("MachineBindingRequired", "许可连接需要在当前电脑重新绑定。");
        var started = DateTimeOffset.UtcNow;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(resource.Policy.TimeoutMilliseconds);
        var token = timeout.Token;
        var tasks = new List<Task<ProbeEvidence>>();
        tasks.Add(resource.ServiceName is { Length: > 0 } service
            ? Task.FromResult(Service(service))
            : Tcp(resource.Host, resource.RmsPort, token));
        if (resource.RequireRules) tasks.Add(Tcp(resource.Host, resource.RulesPort, token));
        if (resource.VmPath is { Length: > 0 } vm) tasks.Add(VirtualMachine(vm, token));
        try
        {
            var evidence = await Task.WhenAll(tasks);
            return Result<ProbeObservation>.Ok(new ProbeObservation(resource, started, DateTimeOffset.UtcNow,
                evidence.ToImmutableArray()));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<ProbeObservation>.Ok(new ProbeObservation(resource, started, DateTimeOffset.UtcNow,
                [new ProbeEvidence(EvidenceKind.Tcp, resource.Host, false, "ProbeTimeout", "探测超时，等待后续复查。")]));
        }
    }

    private static async Task<ProbeEvidence> Tcp(string host, int port, CancellationToken ct)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            using var client = new TcpClient();
            await client.ConnectAsync(addresses, port, ct);
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? host + ":" + port;
            return new ProbeEvidence(EvidenceKind.Tcp, endpoint, true, "TcpReachable", "端口可达；不代表许可已经签出。");
        }
        catch (SocketException)
        {
            return new ProbeEvidence(EvidenceKind.Tcp, $"{host}:{port}", false, "TcpUnavailable", "端口本次不可达。");
        }
    }

    private static async Task<ProbeEvidence> VirtualMachine(string vmPath, CancellationToken ct)
    {
        var candidates = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            }
            .Select(root => Path.Combine(root, "VMware", "VMware Workstation", "vmrun.exe"));
        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is null)
            return new ProbeEvidence(EvidenceKind.VirtualMachine, vmPath, null, "VmStatusUnknown",
                "未找到 vmrun，虚拟机状态未知；不会据此报告丢失。");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, ArgumentList = { "-T", "ws", "list" }
            });
            if (process is null)
                return new ProbeEvidence(EvidenceKind.VirtualMachine, vmPath, null, "VmStatusUnknown", "无法读取虚拟机列表。");
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(ct);
                var error = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                var lines = (await output).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                await error;
                if (process.ExitCode != 0)
                    return new ProbeEvidence(EvidenceKind.VirtualMachine, vmPath, null, "VmStatusUnknown",
                        "虚拟机列表读取失败，端口证据单独展示。");
                var running = lines.Skip(1).Any(line =>
                    string.Equals(line.Trim(), vmPath, StringComparison.OrdinalIgnoreCase));
                return new ProbeEvidence(EvidenceKind.VirtualMachine, vmPath, running,
                    running ? "VmRunning" : "VmNotListed", running ? "指定虚拟机在运行列表中。" : "本次运行列表中没有指定虚拟机。");
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill();
                throw;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ProbeEvidence(EvidenceKind.VirtualMachine, vmPath, null, "VmStatusUnknown",
                "虚拟机状态查询不可用，保留独立端口证据。");
        }
    }

    private static ProbeEvidence Service(string name)
    {
        var manager = OpenSCManagerW(null, null, 1);
        if (manager == IntPtr.Zero)
            return new ProbeEvidence(EvidenceKind.LocalService, name, null, "ServiceAccessDenied", "无法读取服务状态。");
        try
        {
            var handle = OpenServiceW(manager, name, 4);
            if (handle == IntPtr.Zero)
                return new ProbeEvidence(EvidenceKind.LocalService, name, null, "ServiceUnavailable",
                    "服务不存在或当前账户无法读取。");
            try
            {
                if (!QueryServiceStatus(handle, out var status))
                    return new ProbeEvidence(EvidenceKind.LocalService, name, null, "ServiceQueryFailed", "服务状态暂时未知。");
                return new ProbeEvidence(EvidenceKind.LocalService, name, status.CurrentState == 4,
                    status.CurrentState == 4 ? "ServiceRunning" : "ServiceStopped",
                    status.CurrentState == 4 ? "本机服务运行中。" : "本机服务未运行。");
            }
            finally
            {
                CloseServiceHandle(handle);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint Type, CurrentState, Accepted, Win32Exit, SpecificExit, CheckPoint, WaitHint;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string name, uint access);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);

    [DllImport("advapi32.dll")]
    private static extern bool CloseServiceHandle(IntPtr service);
}