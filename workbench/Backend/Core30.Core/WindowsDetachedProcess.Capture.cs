using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Core30.Core;

public static partial class WindowsDetachedProcess
{
    private const int UseStandardHandles = 0x00000100;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const int HandleListAttribute = 0x00020002;
    private const uint WaitTimedOut = 0x00000102;

    /// <summary>Captures the foreground command without passing the caller's pipes to its descendants.</summary>
    public static (int ExitCode, string Output, string Error) RunCaptured(ProcessStartInfo start, TimeSpan timeout) =>
        Capture(start, timeout, null, null, default);

    private static (int ExitCode, string Output, string Error) Capture(ProcessStartInfo start, TimeSpan timeout,
        string? desktop, SafeWaitHandle? job, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var input = OpenSpool();
        using var output = OpenSpool();
        using var error = OpenSpool();
        using var process = StartCaptured(start, input.SafeFileHandle, output.SafeFileHandle, error.SafeFileHandle, desktop, job);
        using var stop = job is not null && cancellation.CanBeCanceled
            ? cancellation.Register(() => { TerminateJobObject(job, 1); }) : default;
        var exit = AwaitExit(process, timeout);
        cancellation.ThrowIfCancellationRequested();
        return (exit, ReadSpool(output), ReadSpool(error));
    }

    private static FileStream OpenSpool() => new(Path.Combine(Path.GetTempPath(), $"core30-result-{Guid.NewGuid():N}.tmp"),
        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete | FileShare.Inheritable,
        bufferSize: 4096, FileOptions.DeleteOnClose);

    private static string ReadSpool(FileStream spool)
    {
        if (spool.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("外部程序输出超过 4 MiB。");
        spool.Position = 0;
        using var reader = new StreamReader(spool, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd().Trim();
    }

    private static SafeProcessHandle StartCaptured(ProcessStartInfo start,
        SafeFileHandle input, SafeFileHandle output, SafeFileHandle error, string? desktop, SafeWaitHandle? job)
    {
        var handles = new[] { input.DangerousGetHandle(), output.DangerousGetHandle(), error.DangerousGetHandle() };
        var size = handles.Length * IntPtr.Size;
        var values = Marshal.AllocHGlobal(size + (job is null ? 0 : IntPtr.Size));
        try
        {
            Marshal.Copy(handles, 0, values, handles.Length);
            if (job is not null) Marshal.WriteIntPtr(values, size, job.DangerousGetHandle());
            return StartWithHandleList(start, handles, values, size, desktop, job is not null);
        }
        finally { Marshal.FreeHGlobal(values); }
    }

    private static SafeProcessHandle StartWithHandleList(ProcessStartInfo start, nint[] handles, nint values, int size,
        string? desktop, bool isolated)
    {
        var count = isolated ? 2 : 1;
        nuint bytes = 0;
        InitializeProcThreadAttributeList(0, count, 0, ref bytes);
        var attributes = Marshal.AllocHGlobal((nint)bytes);
        try
        {
            RequireWin32(InitializeProcThreadAttributeList(attributes, count, 0, ref bytes));
            try
            {
                RequireWin32(UpdateProcThreadAttribute(attributes, 0, HandleListAttribute, values, (nuint)size, 0, 0));
                if (isolated)
                    RequireWin32(UpdateProcThreadAttribute(attributes, 0, JobListAttribute, values + size, (nuint)IntPtr.Size, 0, 0));
                return CreateCapturedProcess(start, handles, attributes, desktop);
            }
            finally { DeleteProcThreadAttributeList(attributes); }
        }
        finally { Marshal.FreeHGlobal(attributes); }
    }

    private static SafeProcessHandle CreateCapturedProcess(ProcessStartInfo start, nint[] handles, nint attributes, string? desktop)
    {
        var desktopName = desktop is null ? 0 : Marshal.StringToHGlobalUni(desktop);
        var startup = new StartupInfoEx
        {
            Startup = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfoEx>(), Flags = UseStandardHandles, Desktop = desktopName,
                StandardInput = handles[0], StandardOutput = handles[1], StandardError = handles[2]
            },
            Attributes = attributes
        };
        var flags = CreateUnicodeEnvironment | CreateNoWindow | ExtendedStartupInfoPresent;
        try
        {
            RequireWin32(CreateProcessWithHandles(start.FileName, BuildCommandLine(start), 0, 0, true,
                flags, BuildEnvironment(start), start.WorkingDirectory, ref startup, out var child));
            using var thread = new SafeWaitHandle(child.Thread, ownsHandle: true);
            return new SafeProcessHandle(child.Process, ownsHandle: true);
        }
        finally { Marshal.FreeHGlobal(desktopName); }
    }

    private static int AwaitExit(SafeProcessHandle process, TimeSpan timeout)
    {
        var result = WaitForSingleObject(process, (uint)timeout.TotalMilliseconds);
        if (result == WaitTimedOut)
        {
            TerminateProcess(process, 1);
            throw new TimeoutException("外部程序操作超时。");
        }
        RequireWin32(result == 0);
        RequireWin32(GetExitCodeProcess(process, out var code));
        return code;
    }

    private static void RequireWin32(bool succeeded)
    {
        if (!succeeded)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo Startup;
        public nint Attributes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute,
        nint value, nuint size, nint previousValue, nint returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint list);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithHandles(string executable, StringBuilder commandLine, nint processAttributes,
        nint threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags,
        string environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle process, out int code);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint code);
}
