using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Core30.Core;

public static partial class WindowsDetachedProcess
{
    private const nuint JobListAttribute = 0x0002000D;
    private const uint DesktopObjectAccess = 0x0087;
    private const uint KillOnJobClose = 0x2000;
    private const int ExtendedLimitInformation = 9;

    /// <summary>Runs a background SDK, including its child processes, away from the user's desktop.</summary>
    public static (int ExitCode, string Output, string Error) RunIsolatedWorker(ProcessStartInfo start, TimeSpan timeout,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var name = $"Core30Reader-{Guid.NewGuid():N}";
        var threadId = GetCurrentThreadId();
        var originalDesktop = GetThreadDesktop(threadId);
        RequireWin32(originalDesktop != 0);
        using var desktop = CreateDesktop(name, 0, 0, 0, DesktopObjectAccess, 0);
        RequireWin32(!desktop.IsInvalid);
        if (GetThreadDesktop(threadId) != originalDesktop)
            RequireWin32(SetThreadDesktop(originalDesktop));

        using var job = CreateJobObject(0, null);
        RequireWin32(!job.IsInvalid);
        var limits = new JobLimits { Basic = new BasicJobLimits { Flags = KillOnJobClose } };
        RequireWin32(SetInformationJobObject(job, ExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<JobLimits>()));
        return Capture(start, timeout, name, job, cancellation);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicJobLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public BasicJobLimits Basic;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }

    private sealed class SafeDesktop : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeDesktop() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => CloseDesktop(handle);
    }

    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeDesktop CreateDesktop(string name, nint device, nint mode, uint flags, uint access, nint security);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(nint desktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetThreadDesktop(uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadDesktop(nint desktop);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeWaitHandle CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeWaitHandle job, int informationClass, ref JobLimits limits, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeWaitHandle job, uint code);
}
