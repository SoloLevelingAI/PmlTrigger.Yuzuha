using System.Runtime.InteropServices;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Platform.Services;

public sealed class WindowsServiceProbe : IWindowsServiceProbe
{
    public bool IsRunning(string serviceName)
    {
        var manager = OpenSCManager(null, null, 1);
        if (manager == IntPtr.Zero) return false;
        try
        {
            var service = OpenService(manager, serviceName, 4);
            if (service == IntPtr.Zero) return false;
            try { return QueryServiceStatus(service, out var status) && status.State == 4; }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus { public uint Type, State, Controls, Exit, SpecificExit, Checkpoint, WaitHint; }
    [DllImport("advapi32", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr manager, string name, uint access);
    [DllImport("advapi32")]
    private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
    [DllImport("advapi32")]
    private static extern void CloseServiceHandle(IntPtr handle);
}
