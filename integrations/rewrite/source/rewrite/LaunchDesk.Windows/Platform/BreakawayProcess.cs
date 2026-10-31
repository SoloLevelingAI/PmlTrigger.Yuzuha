using System.ComponentModel;

namespace LaunchDesk.Windows;

// 编排器可拥有桌面Job，但不得拥有用户目标会话。
internal static class BreakawayProcess
{
    internal static Process? Start(ProcessStartInfo start)
    {
        if (!IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out var inJob))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!inJob) return Process.Start(start);
        var command = new StringBuilder(Quote(start.FileName) + " " + (start.ArgumentList.Count > 0
            ? string.Join(" ", start.ArgumentList.Select(Quote))
            : start.Arguments));
        var environment = string.Join('\0',
            start.Environment.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Select(e => e.Key + "=" + e.Value)) + "\0\0";
        var block = Marshal.StringToHGlobalUni(environment);
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        if (start.CreateNoWindow)
        {
            startup.Flags = 1;
            startup.ShowWindow = 0;
        }

        try
        {
            const uint breakaway = 0x01000000, unicode = 0x400, noWindow = 0x08000000;
            if (!CreateProcessW(start.FileName, command, IntPtr.Zero, IntPtr.Zero, false,
                    breakaway | unicode | (start.CreateNoWindow ? noWindow : 0), block, start.WorkingDirectory,
                    ref startup, out var information))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "The development host does not permit an independent target session. Use the ordinary desktop entry.");
            try
            {
                return Process.GetProcessById(information.ProcessId);
            }
            finally
            {
                CloseHandle(information.Process);
                CloseHandle(information.Thread);
            }
        }
        finally
        {
            for (var index = 0; index < environment.Length * 2; index++) Marshal.WriteByte(block, index, 0);
            Marshal.FreeHGlobal(block);
            command.Clear();
        }
    }

    private static string Quote(string value)
    {
        var output = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            if (character == '"') output.Append('\\', slashes * 2 + 1).Append('"');
            else output.Append('\\', slashes).Append(character);
            slashes = 0;
        }

        return output.Append('\\', slashes * 2).Append('"').ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size;
        internal string? Reserved, Desktop, Title;
        internal uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        internal short ShowWindow, Reserved2;
        internal IntPtr ReservedPointer, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process, Thread;
        internal int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, IntPtr job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInformation information);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}