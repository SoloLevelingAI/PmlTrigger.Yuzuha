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
    public Service(string value) { pipe = value; }
    public Task<Identity> GetIdentity(IdentityRequest request) {
        return Task.FromResult(new Identity { ProcessId = Process.GetCurrentProcess().Id, Model = "Design", PipeName = pipe, BoundedArrayReadVersion = 1, FormQueryVersion = 1 });
    }
    // Synthetic roundtrip only: never executes PML or connects to AVEVA.
    public Task<Response> Run(Request request) {
        if (request.FormQuery != null) {
            YuzuhaToolkit.Pml.PmlFormQuery.ValidateEnvelope(request.ReadArray, request.ReturnList, request.PmlCommand, request.GlobalVar, request.DeleteGlobalVar);
            return Task.FromResult(new Response { Success = true, Code = "FORM_QUERY_SMOKE", FormQueryResult = YuzuhaToolkit.Pml.PmlFormQuery.Read(request.FormQuery, new NativeFormFixture()) });
        }
        if (request.ReadArray) return Task.FromResult(new Response { Success = true, Code = "BOUNDED_SMOKE",
            ArrayWindow = YuzuhaToolkit.Pml.PmlArrayWindow.Read(5, request.StartIndex, request.MaxItems, request.MaxCharacters, i => "row" + i) });
        return Task.FromResult(new Response { Success = request.ReturnList, Code = "SMOKE", PmlCommand = request.PmlCommand,
            ResultList = new[] { request.GlobalVar, request.DeleteGlobalVar.ToString() }, ServerTimeUtc = DateTime.UtcNow });
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
