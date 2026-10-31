using System.Net.Sockets;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Platform.Networking;

public sealed class TcpProbe : ITcpProbe
{
    public async Task<bool> ConnectAsync(string host, int port, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }
}
