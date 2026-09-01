using PlantHost.Rpc;

[RpcService("yuzuha.pml-command.v1")]
public interface IIdentityService
{
    [RpcOperation("get-host-identity")]
    Task<HostIdentityResponse> GetHostIdentityAsync(
        HostIdentityRequest request,
        CancellationToken cancellationToken);
}

public sealed class HostIdentityRequest
{
}

public sealed class HostIdentityResponse
{
    public int ProcessId { get; set; }
    public long ProcessStartTimeUtcTicks { get; set; }
    public string? Model { get; set; }
    public string? PipeName { get; set; }
    public string? HostFramework { get; set; }
    public string? HostVersion { get; set; }
    public DateTime ServerTimeUtc { get; set; }
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var pipeName = args.Length == 0
            ? "yuzuha.pml.command.v1.compat-smoke"
            : args[0];
        using var client = RpcClient.Connect(
            pipeName,
            connectTimeoutMilliseconds: 3000);
        var service = client.CreateProxy<IIdentityService>();
        var response = await service.GetHostIdentityAsync(
            new HostIdentityRequest(),
            CancellationToken.None);

        var passed = response.ProcessId > 0 &&
                     response.ProcessStartTimeUtcTicks > 0 &&
                     response.Model == "Design" &&
                     response.PipeName == pipeName &&
                     response.HostFramework == "net35";
        Console.WriteLine(
            $"PID={response.ProcessId};StartTicks={response.ProcessStartTimeUtcTicks};" +
            $"Model={response.Model};" +
            $"Pipe={response.PipeName};Framework={response.HostFramework}");
        return passed ? 0 : 1;
    }
}
