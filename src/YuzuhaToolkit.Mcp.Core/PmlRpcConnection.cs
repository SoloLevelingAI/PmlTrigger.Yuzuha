using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

/// <summary>持有一个目标会话的 RPC 服务及其连接生命周期。</summary>
public interface IPmlRpcConnection : IDisposable
{
    /// <summary>当前连接上的类型化 PML 命令服务。</summary>
    IPmlCommandService Service { get; }
}

/// <summary>为明确给定的专用管道建立独立 RPC 连接。</summary>
public interface IPmlRpcConnectionFactory
{
    /// <summary>连接指定 PID 专用管道并开始心跳，失败时释放连接资源。</summary>
    IPmlRpcConnection Connect(string pipeName);
}

/// <summary>生产 Named Pipe 连接工厂，为每个连接配置独立心跳。</summary>
internal sealed class PmlRpcConnectionFactory : IPmlRpcConnectionFactory
{
    /// <summary>连接指定 PID 专用管道并开始心跳，失败时释放连接资源。</summary>
    public IPmlRpcConnection Connect(string pipeName) => new Connection(pipeName);

    /// <summary>持有生产 RPC 客户端、心跳和类型化服务的连接。</summary>
    private sealed class Connection : IPmlRpcConnection
    {
        private readonly RpcClient _client;
        private readonly RpcHeartbeatSession _heartbeat;
        /// <summary>当前连接上的类型化 PML 命令服务。</summary>
        public IPmlCommandService Service { get; }

        /// <summary>持有生产 RPC 客户端、心跳和类型化服务的连接。</summary>
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

        /// <summary>停止心跳并释放当前 RPC 连接，不发送用户 PML。</summary>
        public void Dispose()
        {
            try { _heartbeat.Dispose(); }
            finally { _client.Dispose(); }
        }
    }
}
