namespace LaunchDesk.Windows;

/// <summary>跟踪启动器及后代，不读取命令行或凭据。</summary>
internal sealed class ProcessFamilyObserver : IDisposable
{
    private readonly Process root;
    private readonly object gate = new();
    private bool disposed;
    private readonly Dictionary<int, Process> processes = [];
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public int RootId { get; }

    public ProcessFamilyObserver(Process root)
    {
        this.root = root;
        RootId = root.Id;
        processes.Add(root.Id, root);
    }

    public ProcessFamilyObservation Observe()
    {
        lock (gate)
        {
            if (disposed) throw new InvalidOperationException("启动状态监测已结束。");
            return ObserveCore();
        }
    }

    private ProcessFamilyObservation ObserveCore()
    {
        if (OperatingSystem.IsWindows())
        {
            var parents = SnapshotParents();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var entry in parents)
                {
                    if (processes.ContainsKey(entry.Key) ||
                        !processes.TryGetValue(entry.Value, out var parent)) continue;
                    // 后续进程复用PID不得扩入本次启动进程族。
                    if (parent.HasExited && parents.ContainsKey(entry.Value)) continue;
                    try
                    {
                        var child = Process.GetProcessById(entry.Key);
                        if (child.StartTime.ToUniversalTime() < StartedAt.UtcDateTime.AddSeconds(-1))
                        {
                            child.Dispose();
                            continue;
                        }

                        processes.Add(entry.Key, child);
                        changed = true;
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                }
            }
        }

        int alive = 0, windows = 0;
        foreach (var process in processes.Values)
            try
            {
                process.Refresh();
                if (process.HasExited) continue;
                alive++;
                var name = process.ProcessName;
                // mon/cmd仅是入口，不确认请求模块已打开。
                if (name.Equals("mon", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("cmd", StringComparison.OrdinalIgnoreCase)) continue;
                if (process.MainWindowHandle != 0 && process.Responding) windows++;
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

        var exited = root.HasExited;
        int? code = exited ? root.ExitCode : null;
        return new ProcessFamilyObservation(alive, windows, exited, code, DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var process in processes.Values) process.Dispose();
            processes.Clear();
        }
    }

    private static Dictionary<int, int> SnapshotParents()
    {
        var result = new Dictionary<int, int>();
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) return result;
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), ExeFile = "" };
        if (Process32FirstW(snapshot, ref entry))
            do
            {
                result[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            } while (Process32NextW(snapshot, ref entry));

        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public nuint DefaultHeap;
        public uint ModuleId, Threads, ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);
}