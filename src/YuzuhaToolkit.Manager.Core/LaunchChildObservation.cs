using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YuzuhaToolkit.Manager;

/// <summary>入口直接子进程的只读候选事实；不表示登录或 Host 身份核验。</summary>
public sealed record LaunchChildEvidence(int ProcessId, int ParentProcessId, long ProcessStartUtcTicks,
    string Executable, bool HasWindow, int SessionId);

/// <summary>使用 Toolhelp 快照读取指定入口的直接子进程；忽略已退出或不可访问的候选。</summary>
internal static class LaunchChildObservation
{
    /// <summary>在 Windows 上最多遍历 4096 个快照条目，返回可读取路径、启动时间和窗口状态的直接子进程。</summary>
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

    /// <summary>Process32FirstW/NextW 使用的 PROCESSENTRY32W 顺序布局。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        /// <summary>原生结构字节数、已废弃使用计数与被枚举进程标识。</summary>
        internal uint Size, Usage, ProcessId;
        /// <summary>PROCESSENTRY32W 的默认堆保留字段；只为保持原生布局。</summary>
        internal UIntPtr DefaultHeap;
        /// <summary>原生模块保留字段、线程数与父进程标识；父进程用于筛选直接子进程。</summary>
        internal uint ModuleId, Threads, ParentId;
        /// <summary>枚举进程的基本线程优先级。</summary>
        internal int Priority;
        /// <summary>PROCESSENTRY32W 的保留标志字段。</summary>
        internal uint Flags;
        /// <summary>原生快照携带的固定长度可执行文件名；证据路径另从 MainModule 读取。</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
