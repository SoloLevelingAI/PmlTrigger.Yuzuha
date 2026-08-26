using Microsoft.Extensions.Hosting;
using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

public sealed class YuzuhaRpcBridge : IHostedService, IDisposable
{
    public const string PipeName = "yuzuha.pml.command.v1";

    private readonly RpcClient _client;
    private readonly RpcHeartbeatOptions _heartbeatOptions;
    private readonly IPmlCommandService _service;
    private bool _disposed;
    private RpcHeartbeatSession? _heartbeat;

    public YuzuhaRpcBridge()
    {
        _client = RpcClient.Connect(
            PipeName,
            connectTimeoutMilliseconds: 3000);
        _service = _client.CreateProxy<IPmlCommandService>();
        _heartbeatOptions = new RpcHeartbeatOptions
        {
            InstanceId =
                "pml-command-mcp-net10-" +
                Environment.ProcessId,
            Interval = TimeSpan.FromSeconds(2)
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _heartbeat?.Dispose();
        _client.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _heartbeat = _client.StartHeartbeat(
            _heartbeatOptions);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public Task<RunPmlCommandResponse> RunPmlCommandAsync(
        string pmlCommand,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _service.RunPmlCommandAsync(
            new RunPmlCommandRequest
            {
                PmlCommand = pmlCommand
            },
            cancellationToken);
    }

    public Task<RunPmlCommandResponse> RunPmlCommandListAsync(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _service.RunPmlCommandAsync(
            new RunPmlCommandRequest
            {
                PmlCommand = pmlCommand,
                ReturnList = true,
                GlobalVar = globalVar,
                DeleteGlobalVar = deleteGlobalVar
            },
            cancellationToken);
    }
}
