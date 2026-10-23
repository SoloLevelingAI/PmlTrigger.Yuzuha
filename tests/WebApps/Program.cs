using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.WebApps;

var count = 0;
static JsonElement J(object? v) => JsonSerializer.SerializeToElement(v);
static void Assert(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
static void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
async Task Test(string name, Func<Task> body) { await body(); count++; Console.WriteLine("PASS " + name); }
var path = Path.Combine(Path.GetTempPath(), "yuzuha-web-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(path);
File.WriteAllText(Path.Combine(path,"Data.pmlobj"), "define object Data\nendobject\n");
var binding = new ActionBinding("data.read","Read","object","Data","Read","array",
    [new("ref","Ref","dbref",J("=1/2")),new("limit","Limit","integer",J(20),1,100)]);
var seed = Path.Combine(path,"seed.json");
var catalog = new AppCatalog(1,1,["Data"],[new("data","Data","Data","","table",true,[binding])]);
File.WriteAllText(seed,JsonSerializer.Serialize(catalog,CatalogStore.Json));
var store = new CatalogStore(Path.Combine(path,"state"),seed);
var ipc = new FakeIpc();
var manager = new ManagerService(store,ipc,Path.Combine(path,"evidence"),path);
InvokeRequest Request() => new(Guid.NewGuid().ToString("N"),store.Read().Revision,"data","data.read",[]);
await Test("parameters reject multiline, macro expansion and malformed DBREF",()=>{
    foreach(var value in new[]{"=1/2\nDELETE","=1/2$!code","/name","=1/2;Q"})
        Reject(()=>ActionCompiler.Literal(binding.Parameters[0],J(value)));
    Reject(()=>ActionCompiler.Literal(binding.Parameters[1],J(2.5)));
    Reject(()=>ActionCompiler.Literal(binding.Parameters[1],J(101)));
    Reject(()=>ActionCompiler.Literal(binding.Parameters[1],J("20")));
    return Task.CompletedTask;
});
await Test("scalar encoder escapes quotes and preserves typed values",()=>{
    Assert(ActionCompiler.Literal(new("p","Text","string",J("")),J("O'Brien 中文"))=="'O''Brien 中文'");
    Assert(ActionCompiler.Literal(new("p","Bool","bool",J(false)),J(true))=="TRUE");
    return Task.CompletedTask;
});
await Test("compiler restricts target identifiers and unknown parameters",()=>{
    Reject(()=>ActionCompiler.Compile(binding with{Target="Data() DELETE"},new Dictionary<string,JsonElement>(),path,Guid.NewGuid().ToString("N")));
    Reject(()=>ActionCompiler.Compile(binding,new Dictionary<string,JsonElement>{{"extra",J(1)}},path,Guid.NewGuid().ToString("N")));
    Reject(()=>ActionCompiler.Compile(binding,null!,path,Guid.NewGuid().ToString("N")));
    var compiled=ActionCompiler.Compile(binding,new Dictionary<string,JsonElement>(),path,Guid.NewGuid().ToString("N"));
    Assert(compiled.Macro.Contains("!webObject.Read('=1/2',20)")&&compiled.ResultGlobal.Length<=32);
    return Task.CompletedTask;
});
await Test("stale catalog revision cannot overwrite current bindings",()=>{
    store.Save(catalog);
    Reject(()=>store.Save(catalog));
    Reject(()=>CatalogStore.Validate(catalog with{Categories=["Other"]}));
    return Task.CompletedTask;
});
await Test("execution without a selected session sends nothing",async()=>{
    try { await manager.InvokeAsync(Request(),default); } catch(InvalidOperationException) { Assert(ipc.Calls==0); return; }
    throw new Exception("Missing session accepted");
});
await manager.SelectAsync(42,default);
await Test("execute then read uses existing IPC and preserves raw items",async()=>{
    var record=await manager.InvokeAsync(Request(),default);
    Assert(record.Status=="Succeeded"&&record.Steps.Count==2&&record.Result!.Data!.Value.GetProperty("Count").GetInt32()==1);
    Assert(ipc.Commands[0].StartsWith("$M '")&&ipc.Commands[1].StartsWith("!!YzWeb"));
});
await Test("duplicate request is never dispatched a second time",async()=>{
    var request=Request();await manager.InvokeAsync(request,default);var before=ipc.Calls;
    await manager.InvokeAsync(request,default);Assert(ipc.Calls==before);
    try{await manager.InvokeAsync(request with{Parameters=new(){{"limit",J(4)}}},default);}catch(InvalidOperationException){Assert(ipc.Calls==before);return;}
    throw new Exception("Reused request accepted");
});
await Test("unknown execution stops before array read and is not retried",async()=>{
    ipc.FailExecute=true;var before=ipc.Calls;var request=Request();
    var record=await manager.InvokeAsync(request,default);Assert(record.Status=="Unknown"&&ipc.Calls==before+1);
    await manager.InvokeAsync(request,default);Assert(ipc.Calls==before+1);ipc.FailExecute=false;
});
await Test("unparsed rows do not produce a successful run",async()=>{
    ipc.Unparsed=true;var record=await manager.InvokeAsync(Request(),default);Assert(record.Status=="Failed");ipc.Unparsed=false;
});
await Test("disabled app and changed revision block before dispatch",async()=>{
    var old=Request();var c=store.Read();store.Save(c with{Apps=[c.Apps[0] with{Enabled=false}]});var before=ipc.Calls;
    foreach(var request in new[]{old,Request()})try{await manager.InvokeAsync(request,default);throw new Exception("Disabled/stale accepted");}catch(Exception e)when(e is InvalidOperationException or ArgumentException){}
    Assert(ipc.Calls==before);
});
Console.WriteLine($"Passed {count} WebApps checks. Evidence: {path}");

sealed class FakeIpc : IWebIpc
{
    public int Calls;public bool FailExecute;public bool Unparsed;public List<string> Commands=[];
    public AvevaSessionListResponse Discover()=>new();
    public Task<SessionPin> SelectAsync(int pid,CancellationToken token)=>Task.FromResult(new SessionPin(pid,123,"TEST"));
    public Task<OperationResult> InvokeAsync(string operation,JsonElement input,SessionPin pin,CancellationToken token)
    {
        Calls++;Commands.Add(input.GetProperty("pmlCommand").GetString()!);
        if(operation==YuzuhaOperationIds.Execute&&FailExecute)return Task.FromResult(OperationResult.Fail(operation,7,"TIMEOUT","Unknown",mayHaveExecuted:true));
        var data=operation==YuzuhaOperationIds.Execute?JsonSerializer.SerializeToElement(new{Success=true}):JsonSerializer.SerializeToElement(new{Success=true,Count=1,UnparsedCount=Unparsed?1:0,Items=new[]{new{Depth=0,Path="Result",Type="STRING",Value="safe",Empty=false}}});
        return Task.FromResult(OperationResult.Ok(operation,data));
    }
}
