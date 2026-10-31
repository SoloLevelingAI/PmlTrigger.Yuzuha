using System.Diagnostics;
using System.Runtime.InteropServices;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Domain.Processes;
namespace LaunchDesk.Next.Platform.Processes;

public sealed class WindowsProcessHost : IProcessHost
{
    public IProcessHandle Start(ProcessCommand command, ISecretHandle? secret)
    {
        if (!File.Exists(command.ExecutablePath) || !Directory.Exists(command.WorkingDirectory))
            throw new ArgumentException("Executable or working directory does not exist.");
        var args = command.Arguments.Select(argument => command.SecretPlaceholder is null ? argument
            : argument.Replace(command.SecretPlaceholder, secret?.Reveal() ?? "", StringComparison.Ordinal)).ToArray();
        var start = new ProcessStartInfo(command.ExecutablePath) { UseShellExecute = false, WorkingDirectory = command.WorkingDirectory };
        if (command.Invocation == ProcessInvocation.WindowsBatch)
        {
            if (!args.All(CommandLineRules.IsLiteralBatchToken) || command.ExecutablePath.Any(c => c is '%' or '"' or '\r' or '\n')) throw new ArgumentException("Batch command cannot be handed off literally.");
            start.FileName = System.Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            start.Arguments = "/d /v:off /s /c \"\"" + command.ExecutablePath + "\" " + string.Join(" ", args) + "\"";
        }
        else foreach (var argument in args) start.ArgumentList.Add(argument);
        foreach (var name in start.Environment.Keys.Where(key =>
            (command.RemoveEnvironmentKeyFragments ?? []).Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase))).ToArray())
            start.Environment.Remove(name);
        foreach (var pair in command.Environment) start.Environment[pair.Key] = pair.Value;
        var process = Process.Start(start) ?? throw new ProcessStartUncertainException();
        Array.Clear(args);
        return new Handle(process, command.Observation == ProcessObservation.DescendantsMayDetach);
    }
    private sealed class Handle(Process process, bool descendantHandoff) : IProcessHandle
    {
        private readonly HashSet<int> known = [process.Id];
        private bool sawDescendant;
        private Process[] Active()
        {
            if (!descendantHandoff) return process.HasExited ? [] : [process];
            var snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot != new IntPtr(-1)) try
                {
                    var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
                    var parents = new Dictionary<int, int>();
                    if (Process32First(snapshot, ref entry)) do { parents[(int)entry.Id] = (int)entry.ParentId; } while (Process32Next(snapshot, ref entry));
                    bool changed; do
                    {
                        changed = false; foreach (var item in parents) if (known.Contains(item.Value) && known.Add(item.Key))
                        {
                            sawDescendant = true;
                            changed = true;
                        }
                    } while (changed);
                }
                finally { CloseHandle(snapshot); }
            var active = new List<Process>(); foreach (var id in known) try
                {
                    var item = Process.GetProcessById(id);
                    if (!item.HasExited) active.Add(item); else item.Dispose();
                }
                catch (ArgumentException) { }
            return active.ToArray();
        }
        public int Id => process.Id;
        // A parent may exit before a detached child can be observed. Do not claim the whole tree exited.
        public bool HasExited
        {
            get
            {
                var active = Active();
                try { return active.Length == 0 && (!descendantHandoff || sawDescendant); }
                finally { foreach (var item in active) if (item != process) item.Dispose(); }
            }
        }
        public bool HasWindow
        {
            get
            {
                var active = Active();
                try { return active.Any(item => item.MainWindowHandle != IntPtr.Zero); }
                finally { foreach (var item in active) if (item != process) item.Dispose(); }
            }
        }
        public void Dispose() => process.Dispose();
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry { public uint Size, Usage, Id; public UIntPtr Heap; public uint Module, Threads, ParentId; public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe; }
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32")] private static extern bool CloseHandle(IntPtr handle);
}
