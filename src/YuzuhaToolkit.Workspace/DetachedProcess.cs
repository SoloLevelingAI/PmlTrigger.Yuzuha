using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YuzuhaToolkit.Workspace;

/// <summary>使用原生 GUI 启动合同，不注入标准设备，也不继承调用方句柄。</summary>
internal static class DetachedProcess
{
    /// <summary>返回新入口进程的 PID；Windows 使用独立控制台策略及显式环境块，其他平台委托 Process.Start。</summary>
    public static int Start(ProcessStartInfo start)
    {
        if (!OperatingSystem.IsWindows()) { using var process = Process.Start(start) ?? throw new IOException("未创建启动入口进程。"); return process.Id; }
        var environment = IntPtr.Zero;
        PROCESS_INFORMATION info = default;
        try
        {
            var startup = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
            var command = new StringBuilder(Quote(start.FileName, force: true));
            if (start.ArgumentList.Count > 0)
                foreach (var argument in start.ArgumentList) command.Append(' ').Append(Quote(argument));
            else if (start.Arguments.Length > 0) command.Append(' ').Append(start.Arguments);
            var block = string.Join('\0', start.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
            environment = Marshal.StringToHGlobalUni(block);
            // 直接返回原生 PID，避免快速退出的入口在查询托管 Process 时被误报为启动失败。
            // 不关联调用方控制台，防止 Windows 为控制台入口自动补回标准管道。
            // Marine 沿用原有无控制台策略；两种标志互斥，不改变产品窗口显示状态。
            var flags = 0x00000400u | (start.CreateNoWindow ? 0x08000000u : 0x00000008u);
            if (!CreateProcess(start.FileName, command, IntPtr.Zero, IntPtr.Zero, false, flags, environment, start.WorkingDirectory, ref startup, out info)) throw new Win32Exception();
            return checked((int)info.dwProcessId);
        }
        finally
        {
            if (info.hThread != IntPtr.Zero) CloseHandle(info.hThread);
            if (info.hProcess != IntPtr.Zero) CloseHandle(info.hProcess);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
        }
    }

    private static string Quote(string value, bool force = false)
    {
        // 与 ProcessStartInfo.ArgumentList 一致：简单参数保持原样，兼容厂商非 CRT 参数解析器。
        if (!force && value.Length > 0 && !value.Any(character => char.IsWhiteSpace(character) || character == '"')) return value;
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    /// <summary>与 Win32 STARTUPINFOW 对齐；调用时只设置结构大小，不请求标准句柄重定向。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct STARTUPINFO
    {
        /// <summary>STARTUPINFOW 的字节大小，创建进程前由 Marshal.SizeOf 设置。</summary>
        public int cb;
        /// <summary>保留字符串、桌面和窗口标题指针；当前启动策略全部留空。</summary>
        public IntPtr lpReserved, lpDesktop, lpTitle;
        /// <summary>窗口位置、尺寸、控制台字符尺寸、填充属性及启用标志；当前均保持零值。</summary>
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        /// <summary>窗口显示命令和保留缓冲区长度；未设置对应标志时不改变显示策略。</summary>
        public ushort wShowWindow, cbReserved2;
        /// <summary>保留缓冲区及标准输入、输出、错误句柄；当前均为空且不继承调用方句柄。</summary>
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    /// <summary>接收 CreateProcessW 创建的进程和主线程标识；句柄在返回 PID 前的清理阶段关闭。</summary>
    [StructLayout(LayoutKind.Sequential)] private struct PROCESS_INFORMATION {
        /// <summary>新进程及其主线程的内核句柄，使用后分别调用 CloseHandle。</summary>
        public IntPtr hProcess, hThread;
        /// <summary>新进程及其主线程的系统标识；进程标识作为启动结果返回。</summary>
        public uint dwProcessId, dwThreadId;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateProcessW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory, ref STARTUPINFO startup, out PROCESS_INFORMATION process);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
