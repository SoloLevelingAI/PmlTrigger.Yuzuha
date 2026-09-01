using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using PlantHost.Rpc;

namespace YuzuhaToolkit.RpcCompatibilitySmoke.Server
{
    [RpcService("yuzuha.pml-command.v1")]
    public interface IIdentityService
    {
        [RpcOperation("get-host-identity")]
        HostIdentityResponse GetHostIdentity(HostIdentityRequest request);
    }

    public sealed class HostIdentityRequest
    {
    }

    public sealed class HostIdentityResponse
    {
        public int ProcessId { get; set; }
        public long ProcessStartTimeUtcTicks { get; set; }
        public string Model { get; set; }
        public string PipeName { get; set; }
        public string HostFramework { get; set; }
        public string HostVersion { get; set; }
        public DateTime ServerTimeUtc { get; set; }
    }

    internal sealed class IdentityService : IIdentityService
    {
        private readonly string _pipeName;
        private readonly int _processId;
        private readonly long _processStartTimeUtcTicks;

        public IdentityService(
            string pipeName,
            int processId,
            long processStartTimeUtcTicks)
        {
            _pipeName = pipeName;
            _processId = processId;
            _processStartTimeUtcTicks = processStartTimeUtcTicks;
        }

        public HostIdentityResponse GetHostIdentity(HostIdentityRequest request)
        {
            return new HostIdentityResponse
            {
                ProcessId = _processId,
                ProcessStartTimeUtcTicks = _processStartTimeUtcTicks,
                Model = "Design",
                PipeName = _pipeName,
                HostFramework = "net35",
                HostVersion = "smoke",
                ServerTimeUtc = DateTime.UtcNow
            };
        }
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            Process process = Process.GetCurrentProcess();
            int processId = process.Id;
            long startTicks = process.StartTime.ToUniversalTime().Ticks;
            string pipeName = args.Length == 0
                ? "yuzuha.pml.command.v1.pid-" + processId
                : args[0];
            using (RpcServer server = RpcServer
                .ForPipe(pipeName)
                .AddService<IIdentityService>(new IdentityService(
                    pipeName,
                    processId,
                    startTicks))
                .Build())
            {
                server.Start();
                using (Form form = new Form())
                {
                    form.Text =
                        "CompatibilitySmoke | ALL | Model - AVEVA Everything3D";
                    form.Width = 360;
                    form.Height = 120;
                    form.ShowInTaskbar = false;
                    Thread shutdownThread = new Thread(
                        delegate()
                        {
                            Console.ReadLine();
                            if (form.IsHandleCreated)
                                form.BeginInvoke(new MethodInvoker(form.Close));
                        });
                    shutdownThread.IsBackground = true;
                    shutdownThread.Start();
                    Console.WriteLine(
                        "READY PID=" + processId + ";PIPE=" + pipeName);
                    Application.Run(form);
                }
            }
        }
    }
}
