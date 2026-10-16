using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

public interface IPmlRpcConnection : IDisposable
{
    IPmlCommandService Service { get; }
}

public interface IPmlRpcConnectionFactory
{
    IPmlRpcConnection Connect(string pipeName);
}

internal sealed class PmlRpcConnectionFactory : IPmlRpcConnectionFactory
{
    public IPmlRpcConnection Connect(string pipeName) => new Connection(pipeName);

    private sealed class Connection : IPmlRpcConnection
    {
        private readonly RpcClient _client;
        private readonly RpcHeartbeatSession _heartbeat;
        public IPmlCommandService Service { get; }

        public Connection(string pipeName)
        {
            _client = RpcClient.Connect(pipeName, connectTimeoutMilliseconds: 3000);
            try
            {
                Service = new AotPmlCommandService(_client);
                _heartbeat = _client.StartHeartbeat(new RpcHeartbeatOptions
                {
                    InstanceId = "pml-command-mcp-net10-" + Environment.ProcessId,
                    Interval = TimeSpan.FromSeconds(2)
                });
            }
            catch { _client.Dispose(); throw; }
        }

        public void Dispose()
        {
            try { _heartbeat.Dispose(); }
            finally { _client.Dispose(); }
        }
    }
}
