using System;
using System.Threading;
using System.Windows.Forms;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost.Net48;

internal static class PmlCommandRpcHost
{
    internal const string PipeName = "yuzuha.pml.command.v1";

    private static readonly object Gate = new();
    private static PmlCommandMethod _target;
    private static RpcServer _server;

    internal static int MainThreadId { get; private set; }

    internal static bool IsRunning
    {
        get
        {
            lock (Gate)
            {
                return _server != null;
            }
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
                if (Thread.CurrentThread.ManagedThreadId != MainThreadId)
                    throw new InvalidOperationException(
                        "The RPC host is attached to another thread.");

                _target = target;
                return;
            }

            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            _target = target;
            EnsureHostSynchronizationContext();

            var builder = RpcServer
                .Create(PipeName)
                .UseCurrentSynchronizationContext()
                .Configure(
                    delegate(RpcServerOptions options)
                    {
                        options.HeartbeatTimeout = TimeSpan.FromSeconds(15);
                        options.SweepInterval = TimeSpan.FromSeconds(2);
                    });

            builder
                .ForService<IPmlCommandService>()
                .AddImplementation(new PmlCommandRpcService());

            _server = builder.Build();
            _server.StartAsync().GetAwaiter().GetResult();

            AppDomain.CurrentDomain.ProcessExit += delegate { Stop(); };
            AppDomain.CurrentDomain.DomainUnload += delegate { Stop(); };
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
        if (Thread.CurrentThread.ManagedThreadId != MainThreadId)
            throw new InvalidOperationException(
                "The PML command was not dispatched to the AVEVA main thread.");
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

        if (server == null)
            return;

        server.StopAsync().GetAwaiter().GetResult();
        server.Dispose();
    }
}
