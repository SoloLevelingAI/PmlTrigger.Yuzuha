using System;
using IdentityRequest = YuzuhaToolkit.PmlHost.HostIdentityRequest;
using Identity = YuzuhaToolkit.PmlHost.HostIdentityResponse;
using Request = YuzuhaToolkit.PmlHost.RunPmlCommandRequest;
using Response = YuzuhaToolkit.PmlHost.RunPmlCommandResponse;
using System.Diagnostics;
using System.Threading.Tasks;
using PlantHost.Rpc;

[RpcService("yuzuha.pml-command.v1")]
public interface IService
{
    [RpcOperation("get-host-identity")] Task<Identity> GetIdentity(IdentityRequest request);
    [RpcOperation("run-pml-command")] Task<Response> Run(Request request);
}
public sealed class Service : IService
{
    private readonly string pipe;
    private readonly YuzuhaToolkit.PmlHost.PmlCommandRpcService production = new YuzuhaToolkit.PmlHost.PmlCommandRpcService();
    public Service(string value) { pipe=value; YuzuhaToolkit.PmlHost.PmlCommandRpcHost.PipeName=value; }
    public Task<Identity> GetIdentity(IdentityRequest request) { return production.GetHostIdentityAsync(request, System.Threading.CancellationToken.None); }
    public Task<Response> Run(Request request)
    {
        YuzuhaToolkit.PmlHost.PmlCommandRpcHost.Target.FailBegin=request.PmlCommand=="fail-markdb";
        YuzuhaToolkit.PmlHost.PmlCommandRpcHost.Target.FailEnd=request.PmlCommand=="fail-endmarkdb";
        return production.RunPmlCommandAsync(request,System.Threading.CancellationToken.None);
    }
}
internal static class Program
{
    public static void Main(string[] args) {
        try { Run(args); }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().FullName); Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
    }
    private static void Run(string[] args) {
        var builder = RpcServer.Create(args[0]);
        builder.ForService<IService>().AddImplementation(new Service(args[0]));
        using (var server = builder.Build()) {
            server.StartAsync().GetAwaiter().GetResult(); Console.WriteLine("READY"); Console.ReadLine();
        }
    }
}
