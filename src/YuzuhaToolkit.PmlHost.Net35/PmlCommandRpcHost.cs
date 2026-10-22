using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost
{
    /// <summary>将 PID 专用 RPC 服务绑定到 AVEVA 主线程与当前 PMLNet 目标。</summary>
    internal static class PmlCommandRpcHost
    {
        /// <summary>当前 Host 所在 AVEVA 进程的固定 PID。</summary>
        internal static readonly int ProcessId =
            Process.GetCurrentProcess().Id;
        /// <summary>当前 Host 所在进程启动 UTC ticks，用于抵抗 PID 重用。</summary>
        internal static readonly long ProcessStartTimeUtcTicks =
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
        /// <summary>绑定该目标 PID 的专用 Named Pipe 名称。</summary>
        internal static readonly string PipeName = ResolvePipeName();

        private static readonly object Gate = new object();
        private static PmlCommandMethod _target;
        private static RpcServer _server;
        private static int _mainThreadId;

        /// <summary>绑定 PMLNet 目标时记录的 AVEVA 主线程编号。</summary>
        internal static int MainThreadId
        {
            get { return _mainThreadId; }
        }

        /// <summary>当前监听服务或心跳循环是否处于运行状态。</summary>
        internal static bool IsRunning
        {
            get
            {
                lock (Gate)
                    return _server != null;
            }
        }

        /// <summary>仅从目标 AVEVA 主线程绑定对象并启动当前 PID 的专用管道服务。</summary>
        /// <param name="target">已在 AVEVA 主线程构造的 PMLNet 命令目标。</param>
        internal static void Attach(PmlCommandMethod target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            lock (Gate)
            {
                if (_server != null)
                {
                    if (Thread.CurrentThread.ManagedThreadId !=
                        _mainThreadId)
                        throw new InvalidOperationException(
                            "The RPC host is attached to another thread.");

                    _target = target;
                    return;
                }

                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _target = target;
                EnsureHostSynchronizationContext();

                _server = RpcServer
                    .ForPipe(PipeName)
                    .UseCurrentSynchronizationContext()
                    .Configure(
                        delegate(RpcServerOptions options)
                        {
                            options.HeartbeatTimeout =
                                TimeSpan.FromSeconds(15);
                            options.SweepInterval =
                                TimeSpan.FromSeconds(2);
                        })
                    .AddService<IPmlCommandService>(
                        new PmlCommandRpcService())
                    .Build();

                _server.Start();
                AppDomain.CurrentDomain.ProcessExit +=
                    delegate { Stop(); };
                AppDomain.CurrentDomain.DomainUnload +=
                    delegate { Stop(); };
            }
        }

        /// <summary>取得已绑定的主线程 PMLNet 目标；尚未绑定时抛出异常。</summary>
        internal static PmlCommandMethod GetTarget()
        {
            lock (Gate)
            {
                if (_target == null)
                    throw new InvalidOperationException(
                        "The PML command target is not available.");

                return _target;
            }
        }

        /// <summary>拒绝在未绑定线程或非 AVEVA 主线程上处理模型操作。</summary>
        internal static void AssertMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException(
                    "The PML command was not dispatched to the AVEVA main thread.");
        }

        private static string ResolvePipeName()
        {
            string configured = Environment.GetEnvironmentVariable(
                "YUZUHA_PML_PIPE");
            return String.IsNullOrEmpty(configured) ||
                   configured.Trim().Length == 0
                ? "yuzuha.pml.command.v1.pid-" + ProcessId
                : configured.Trim();
        }

        private static void EnsureHostSynchronizationContext()
        {
            if (SynchronizationContext.Current != null)
                return;

            SynchronizationContext.SetSynchronizationContext(
                new WindowsFormsSynchronizationContext());
        }

        private static void Stop()
        {
            RpcServer server;
            lock (Gate)
            {
                server = _server;
                _server = null;
                _target = null;
            }

            if (server != null)
                server.Dispose();
        }
    }
}
