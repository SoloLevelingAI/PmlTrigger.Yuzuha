using System;
using IdentityRequest = YuzuhaToolkit.PmlHost.HostIdentityRequest;
using Identity = YuzuhaToolkit.PmlHost.HostIdentityResponse;
using Request = YuzuhaToolkit.PmlHost.RunPmlCommandRequest;
using Response = YuzuhaToolkit.PmlHost.RunPmlCommandResponse;
using System.Diagnostics;

using PlantHost.Rpc;

[RpcService("yuzuha.pml-command.v1")]
public interface IService
{
    [RpcOperation("get-host-identity")] Identity GetIdentity(IdentityRequest request);
    [RpcOperation("run-pml-command")] Response Run(Request request);
}
public sealed class Service : IService
{
    private readonly string pipe;
    private readonly YuzuhaToolkit.PmlHost.PmlCommandRpcService production = new YuzuhaToolkit.PmlHost.PmlCommandRpcService();
    public Service(string value) { pipe=value; YuzuhaToolkit.PmlHost.PmlCommandRpcHost.PipeName=value; }
    public Identity GetIdentity(IdentityRequest request) { return production.GetHostIdentity(request); }
    public Response Run(Request request)
    {
        YuzuhaToolkit.PmlHost.PmlCommandRpcHost.Target.FailBegin=request.PmlCommand=="fail-markdb";
        YuzuhaToolkit.PmlHost.PmlCommandRpcHost.Target.FailEnd=request.PmlCommand=="fail-endmarkdb";
        return production.RunPmlCommand(request);
    }
}
internal static class Program
{
    public static void Main(string[] args) {
        try { Run(args); }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().FullName); Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
    }
    private static void Run(string[] args) {
        var builder = RpcServer.ForPipe(args[0]);
        builder.AddService<IService>(new Service(args[0]));
        using (var server = builder.Build()) {
            server.Start(); Console.WriteLine("READY"); Console.ReadLine();
        }
    }
}
