using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YuzuhaToolkit.Workspace;

/// <summary>使用原生 GUI 启动合同，不注入标准设备，也不继承调用方句柄。</summary>
internal static class DetachedProcess
{
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct STARTUPINFO
    {
        public int cb; public IntPtr lpReserved, lpDesktop, lpTitle; public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)] private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public uint dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateProcessW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory, ref STARTUPINFO startup, out PROCESS_INFORMATION process);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
