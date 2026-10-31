using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YuzuhaToolkit.Manager;

/// <summary>入口直接子进程的只读候选事实；不表示登录或 Host 身份核验。</summary>
public sealed record LaunchChildEvidence(int ProcessId, int ParentProcessId, long ProcessStartUtcTicks,
    string Executable, bool HasWindow, int SessionId);

internal static class LaunchChildObservation
{
    internal static LaunchChildEvidence[] Snapshot(int parentId)
    {
        if (!OperatingSystem.IsWindows()) return [];
        var handle = CreateToolhelp32Snapshot(2, 0);
        if (handle == new IntPtr(-1)) return [];
        var result = new List<LaunchChildEvidence>();
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (!Process32FirstW(handle, ref entry)) return [];
            var budget = 4096;
            do
            {
                if (entry.ParentId != parentId) continue;
                try
                {
                    using var process = Process.GetProcessById((int)entry.ProcessId);
                    var path = process.MainModule?.FileName;
                    if (path is not null) result.Add(new(process.Id, parentId, process.StartTime.ToUniversalTime().Ticks,
                        path, process.MainWindowHandle != IntPtr.Zero, process.SessionId));
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            } while (--budget > 0 && Process32NextW(handle, ref entry));
            return result.ToArray();
        }
        finally { CloseHandle(handle); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        internal uint Size, Usage, ProcessId;
        internal UIntPtr DefaultHeap;
        internal uint ModuleId, Threads, ParentId;
        internal int Priority;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
