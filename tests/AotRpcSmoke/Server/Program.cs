using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PlantHost.Rpc;

[RpcService("yuzuha.pml-command.v1")]
public interface IService
{
    [RpcOperation("get-host-identity")] Task<Identity> GetIdentity(IdentityRequest request);
    [RpcOperation("run-pml-command")] Task<Response> Run(Request request);
}
public sealed class IdentityRequest { }
public sealed class Identity
{
    public int ProcessId { get; set; }
    public string Model { get; set; }
    public string PipeName { get; set; }
}
public sealed class Request
{
    public string PmlCommand { get; set; }
    public bool ReturnList { get; set; }
    public string GlobalVar { get; set; }
    public bool DeleteGlobalVar { get; set; }
}
public sealed class Response
{
    public bool Success { get; set; }
    public string Code { get; set; }
    public string PmlCommand { get; set; }
    public string[] ResultList { get; set; }
    public DateTime ServerTimeUtc { get; set; }
}
public sealed class Service : IService
{
    private readonly string pipe;
    public Service(string value) { pipe = value; }
    public Task<Identity> GetIdentity(IdentityRequest request) {
        return Task.FromResult(new Identity { ProcessId = Process.GetCurrentProcess().Id, Model = "Design", PipeName = pipe });
    }
    // Synthetic roundtrip only: never executes PML or connects to AVEVA.
    public Task<Response> Run(Request request) {
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
