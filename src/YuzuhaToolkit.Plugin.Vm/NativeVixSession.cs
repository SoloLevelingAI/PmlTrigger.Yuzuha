using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace YuzuhaToolkit.Plugins.Vm;

/// <summary>从 RemoteProjectLab 提取的隔离 VIX 适配；只在显式执行阶段加载厂商 DLL。</summary>
public sealed class NativeVixSession : IVmwareSession
{
    private const string Dll = "YuzuhaVmVix";
    private static IntPtr library;
    private readonly CancellationToken token;
    private readonly int timeout;
    private readonly List<int> handles = [];
    private int host, vm; private bool disposed;
    /// <inheritdoc />
    public bool HasDispatched { get; private set; }

    /// <summary>只由任务执行器调用，发现/规划与 --describe 不引用此工厂。</summary>
    public static Task<IVmwareSession> OpenAsync(VmTaskPlan plan, CancellationToken token)
        => Task.FromResult<IVmwareSession>(new NativeVixSession(plan, token));

    private NativeVixSession(VmTaskPlan plan, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("VIX 适配需要 Windows。");
        token = cancellation; timeout = plan.Request.TimeoutSeconds; token.ThrowIfCancellationRequested();
        if (VmPlanning.Hash(plan.Request.VixLibraryPath) != plan.VixHash) throw new IOException("VIX 库与计划不同。");
        if (!SetDllDirectory(Path.GetDirectoryName(plan.Request.VixLibraryPath)!)) throw new IOException("无法设置本工作进程的 VIX 依赖目录。");
        var loaded = NativeLibrary.Load(plan.Request.VixLibraryPath);
        if (Interlocked.CompareExchange(ref library, loaded, IntPtr.Zero) != IntPtr.Zero) { NativeLibrary.Free(loaded); throw new InvalidOperationException("VIX 需要独立单次工作进程。"); }
        NativeLibrary.SetDllImportResolver(typeof(NativeVixSession).Assembly, Resolve);
        try
        {
            host = Handle(Wait(VixHost_Connect(-1, 3, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, 0, 0, IntPtr.Zero, IntPtr.Zero)));
            var properties = 0;
            using var path = new Utf8Buffer(Encoding.UTF8.GetBytes(plan.Request.VmPath));
            if (plan.Request.CredentialFile is not null)
            {
                var secret = VmCredentials.Read(plan.Request.CredentialFile, plan.CredentialTargetKey);
                try
                {
                    using var password = new Utf8Buffer(secret);
                    Check(VixPropertyList_AllocPropertyList(host, out properties, 7001, password.Pointer, 0)); handles.Add(properties);
                    vm = Handle(Wait(VixHost_OpenVM(host, path.Pointer, 0, properties, IntPtr.Zero, IntPtr.Zero)));
                }
                finally { CryptographicOperations.ZeroMemory(secret); }
            }
            else vm = Handle(Wait(VixHost_OpenVM(host, path.Pointer, 0, 0, IntPtr.Zero, IntPtr.Zero)));
            if (!Path.GetFullPath(Text(vm, 103)).Equals(plan.Request.VmPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("VIX 返回路径不匹配选定 VM。");
        }
        catch { Dispose(); throw; }
    }
    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path) => name == Dll ? library : IntPtr.Zero;

    /// <inheritdoc />
    public Task<VmStepEvidence> ExecuteAsync(VmTaskStep step, int index, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested(); cancellation.ThrowIfCancellationRequested();
        switch (step.Action)
        {
            case "probe": break;
            case "start":
                if ((Integer(vm, 129) & 8) == 0) { HasDispatched = true; Wait(VixVM_PowerOn(vm, 0, 0, IntPtr.Zero, IntPtr.Zero)); }
                break;
            case "stop":
                HasDispatched = true; Wait(VixVM_PowerOff(vm, 4, IntPtr.Zero, IntPtr.Zero)); break;
            case "createSnapshot":
                using (var name = new Utf8Buffer(Encoding.UTF8.GetBytes(step.Snapshot!)))
                using (var description = new Utf8Buffer(Encoding.UTF8.GetBytes("Yuzuha explicitly planned test checkpoint")))
                { HasDispatched = true; Wait(VixVM_CreateSnapshot(vm, name.Pointer, description.Pointer, 0, 0, IntPtr.Zero, IntPtr.Zero)); }
                break;
            case "revertSnapshot":
                using (var name = new Utf8Buffer(Encoding.UTF8.GetBytes(step.Snapshot!)))
                {
                    Check(VixVM_GetNamedSnapshot(vm, name.Pointer, out var snapshot)); handles.Add(snapshot);
                    HasDispatched = true; Wait(VixVM_RevertToSnapshot(vm, snapshot, 0x80, 0, IntPtr.Zero, IntPtr.Zero));
                }
                break;
            default: throw new ArgumentException("VIX 动作未支持。");
        }
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult(new VmStepEvidence(index, step.Action, "observed", Integer(vm, 129), Integer(vm, 152)));
    }
    private int Wait(int job)
    {
        handles.Add(job); var flag = Marshal.AllocHGlobal(4); var watch = Stopwatch.StartNew();
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested(); Marshal.WriteInt32(flag, 0); Check(VixJob_CheckCompletion(job, flag));
                if (Marshal.ReadByte(flag) != 0) { Check(VixJob_GetError(job)); return job; }
                if (watch.Elapsed.TotalSeconds >= timeout) throw new TimeoutException("VIX 任务超时，状态未知；不自动重复副作用。");
                Thread.Sleep(100);
            }
        }
        finally { Marshal.FreeHGlobal(flag); }
    }
    private int Handle(int job) { var handle = Integer(job, 3010); handles.Add(handle); return handle; }
    private static int Integer(int handle, int property) { Check(PropInt(handle, property, out var value, 0)); return value; }
    private static string Text(int handle, int property)
    {
        Check(PropPointer(handle, property, out var pointer, 0));
        try
        {
            if (pointer == IntPtr.Zero) return "";
            var length = 0; while (length < 32768 && Marshal.ReadByte(pointer, length) != 0) length++;
            if (length == 32768) throw new InvalidDataException("VIX 文本超过预算。");
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return Encoding.UTF8.GetString(bytes);
        }
        finally { if (pointer != IntPtr.Zero) Vix_FreeBuffer(pointer); }
    }
    private static void Check(ulong error) { if (error != 0) throw new IOException("VIX 错误代码=" + (error & 65535)); }
    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var handle in handles.Where(h => h != 0 && h != host).Reverse()) Vix_ReleaseHandle(handle);
        if (host != 0) VixHost_Disconnect(host); handles.Clear(); host = vm = 0;
        // The isolated process owns the loaded library; keep it mapped until process exit for native callbacks.
    }
    private sealed class Utf8Buffer : IDisposable
    {
        public IntPtr Pointer { get; private set; } private readonly int size;
        public Utf8Buffer(byte[] bytes) { size = bytes.Length + 1; Pointer = Marshal.AllocHGlobal(size); Marshal.Copy(bytes, 0, Pointer, bytes.Length); Marshal.WriteByte(Pointer, bytes.Length, 0); }
        public void Dispose() { if (Pointer == IntPtr.Zero) return; for (var index = 0; index < size; index++) Marshal.WriteByte(Pointer, index, 0); Marshal.FreeHGlobal(Pointer); Pointer = IntPtr.Zero; }
    }
    // Declarations checked against VMware's maintained vix.h; see manager-plugins.md for the source link.
    [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetDllDirectory(string directory);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixHost_Connect(int version, int provider, IntPtr name, int port, IntPtr user, IntPtr password, int options, int properties, IntPtr callback, IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void VixHost_Disconnect(int host);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixHost_OpenVM(int host, IntPtr path, int options, int properties, IntPtr callback, IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern ulong VixPropertyList_AllocPropertyList(int host, out int properties, int firstProperty, IntPtr password, int end);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern ulong VixJob_CheckCompletion(int job, IntPtr done);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern ulong VixJob_GetError(int job);
    [DllImport(Dll, EntryPoint = "Vix_GetProperties", CallingConvention = CallingConvention.Cdecl)] private static extern ulong PropInt(int handle, int property, out int value, int end);
    [DllImport(Dll, EntryPoint = "Vix_GetProperties", CallingConvention = CallingConvention.Cdecl)] private static extern ulong PropPointer(int handle, int property, out IntPtr value, int end);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void Vix_ReleaseHandle(int handle);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void Vix_FreeBuffer(IntPtr value);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixVM_PowerOn(int vm, int options, int properties, IntPtr callback, IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixVM_PowerOff(int vm, int options, IntPtr callback, IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixVM_CreateSnapshot(int vm, IntPtr name, IntPtr description, int options, int properties, IntPtr callback, IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern ulong VixVM_GetNamedSnapshot(int vm, IntPtr name, out int snapshot);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int VixVM_RevertToSnapshot(int vm, int snapshot, int options, int properties, IntPtr callback, IntPtr data);
}
