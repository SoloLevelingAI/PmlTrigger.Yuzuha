using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost
{
    internal static class PmlCommandRpcHost
    {
        internal static readonly int ProcessId =
            Process.GetCurrentProcess().Id;
        internal static readonly long ProcessStartTimeUtcTicks =
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
        internal static readonly string PipeName = ResolvePipeName();

        private static readonly object Gate = new object();
        private static PmlCommandMethod _target;
        private static RpcServer _server;
        private static int _mainThreadId;

        internal static int MainThreadId
        {
            get { return _mainThreadId; }
        }

        internal static bool IsRunning
        {
            get
            {
                lock (Gate)
                    return _server != null;
            }
        }

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
