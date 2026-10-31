using System.Runtime.InteropServices;

namespace LaunchDesk.Rewrite.Tests;

internal static partial class Program
{
    private static async Task<int> JobChild(string root)
    {
        if (IntPtr.Size != 8) return 2;
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return 3;
        var information = Marshal.AllocHGlobal(144);
        try
        {
            Marshal.Copy(new byte[144], 0, information, 144);
            Marshal.WriteInt32(information, 16, 0x2000 | 0x800); // KILL_ON_JOB_CLOSE | BREAKAWAY_OK
            if (!SetInformationJobObject(job, 9, information, 144) ||
                !AssignProcessToJobObject(job, GetCurrentProcess())) return 4;
            var gateway = new WindowsProcessGateway();
            var environment = Ok(gateway.CaptureEnvironment());
            var plan = new LaunchPlan(Guid.NewGuid(), new Revision(0), Environment.ProcessPath!, root,
                EntryKind.Executable,
                [new LaunchArgument("--survive-child"), new LaunchArgument(Path.Combine(root, "survived.txt"))],
                environment.Values, null, null, "fixture");
            await using var child = Ok(await gateway.StartAsync(plan, null, default));
            File.WriteAllText(Path.Combine(root, "started.txt"), child.RootId.ToString());
            CloseHandle(job); // 仅终止此临时辅助进程；脱离的子进程须存活。
            job = IntPtr.Zero;
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(information);
            if (job != IntPtr.Zero) CloseHandle(job);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr information,
        uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}