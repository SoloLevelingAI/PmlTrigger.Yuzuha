using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Core30.Core;

public static partial class WindowsDetachedProcess
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;

    [Obsolete("请使用 StartWithIdentity(start)。只需要进程 ID 时读取返回值的 Pid。")]
    public static int Start(ProcessStartInfo start) => StartWithIdentity(start).Pid;

    // Vendor processes may outlive Core. Their lifetime must not hold the CLI receipt pipes open.
    public static (int Pid, long StartTicks) StartWithIdentity(ProcessStartInfo start)
    {
        var commandLine = BuildCommandLine(start);
        var environment = BuildEnvironment(start);
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var flags = CreateUnicodeEnvironment | (start.CreateNoWindow ? CreateNoWindow : 0);
        var directory = string.IsNullOrEmpty(start.WorkingDirectory) ? null : start.WorkingDirectory;
        var child = StartWithoutHandles(start.FileName, commandLine, environment, directory, flags, ref startup);
        using var process = new SafeProcessHandle(child.Process, ownsHandle: true);
        using var thread = new SafeWaitHandle(child.Thread, ownsHandle: true);
        var ticks = GetProcessTimes(process, out var created, out _, out _, out _) && created > 0
            ? DateTime.FromFileTimeUtc(created).Ticks : 0;
        return (child.ProcessId, ticks);
    }

    private static ProcessInformation StartWithoutHandles(string executable, StringBuilder commandLine,
        string environment, string? directory, uint flags, ref StartupInfo startup)
    {
        if (!CreateProcess(executable, commandLine, IntPtr.Zero, IntPtr.Zero, inheritHandles: false,
            flags, environment, directory, ref startup, out var child))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return child;
    }

    private static StringBuilder BuildCommandLine(ProcessStartInfo start)
    {
        var commandLine = new StringBuilder().Append('"').Append(start.FileName).Append('"');
        if (start.ArgumentList.Count == 0)
            return commandLine.Append(' ').Append(start.Arguments);
        foreach (var argument in start.ArgumentList)
            AppendArgument(commandLine.Append(' '), argument);
        return commandLine;
    }

    private static void AppendArgument(StringBuilder commandLine, string argument)
    {
        if (argument.Length != 0 && !argument.Contains('"') && !argument.Any(char.IsWhiteSpace))
        {
            commandLine.Append(argument);
            return;
        }

        commandLine.Append('"');
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            commandLine.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
            slashes = 0;
        }
        commandLine.Append('\\', slashes * 2).Append('"');
    }

    private static string BuildEnvironment(ProcessStartInfo start) =>
        string.Join('\0', start.Environment.Where(item => item.Value is not null)
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}={item.Value}")) + "\0\0";

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, ReservedBytes;
        public IntPtr ReservedData, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string executable, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags,
        string environment, string? directory, ref StartupInfo startup, out ProcessInformation information);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited,
        out long kernelTime, out long userTime);
}
