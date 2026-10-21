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
    public Service(string value) { pipe = value; }
    public Identity GetIdentity(IdentityRequest request) {
        return new Identity { ProcessId = Process.GetCurrentProcess().Id, Model = "Design", PipeName = pipe, BoundedArrayReadVersion = 1 };
    }
    // Synthetic roundtrip only: never executes PML or connects to AVEVA.
    public Response Run(Request request) {
        if (request.ReadArray) return new Response { Success = true, Code = "BOUNDED_SMOKE",
            ArrayWindow = YuzuhaToolkit.Pml.PmlArrayWindow.Read(5, request.StartIndex, request.MaxItems, request.MaxCharacters, i => "row" + i) };
        return new Response { Success = request.ReturnList, Code = "SMOKE", PmlCommand = request.PmlCommand,
            ResultList = new[] { request.GlobalVar, request.DeleteGlobalVar.ToString() }, ServerTimeUtc = DateTime.UtcNow };
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
