using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Bootstrap;
using LaunchDesk.Next.Bootstrap.Addins;

// A file/CLI editor for the same backend used by the WPF product. No separate startup implementation.
try
{
    var addins=AddinCatalog.LoadDefault();
    if(args.Length==1 && args[0]=="features") { Console.WriteLine(JsonSerializer.Serialize(addins.EnabledIds));return 0; }
    if(args.Length==3 && args[0]=="addin-check" && args[1]=="--id")
    { if(!addins.IsEnabled(args[2]))return 78;return 0; }
    if(args.Length>0 && args[0].StartsWith("model-",StringComparison.Ordinal))addins.Require(WorkspaceHost.FeatureId);

    if(args.Length==3 && args[0]=="model-init" && args[1]=="--file")
    {
        using var file=new FileStream(args[2],FileMode.CreateNew);
        JsonSerializer.Serialize(file,new LaunchModel(),new JsonSerializerOptions{WriteIndented=true});return 0;
    }
    if(args.Length>=3 && args[1]=="--file" && (args[0]=="model-check" || args[0]=="model-execute"))
    {
        var model=JsonSerializer.Deserialize<LaunchModel>(File.ReadAllText(args[2]))??throw new ArgumentException("Model missing.");
        model.EnsureNoSecretFields();
        if(args[0]=="model-check")
        {
            var error=LaunchValidation.Validate(model.ToInput().CreateSnapshot(Guid.Empty));
            Console.WriteLine(JsonSerializer.Serialize(new{valid=error is null,error}));return error is null?0:2;
        }
        if(args.Length!=5 || args[3]!="--data")throw new ArgumentException("model-execute --file <model.json> --data <isolated-data-directory>");
        using var host=new WorkspaceHost(Path.GetFullPath(args[4]));
        await host.Workspace.InitializeAsync();host.Workspace.Model.Apply(model);
        var result=await host.Workspace.ExecuteAsync();Console.WriteLine(JsonSerializer.Serialize(result));
        return result.Status==LaunchStatus.Created?0:2;
    }
    if(args.Length==5 && args[0]=="package" && args[1]=="--repo" && args[3]=="--output")
    {
        Pack(Path.GetFullPath(args[2]),Path.GetFullPath(args[4]));return 0;
    }
    Console.WriteLine("features | package --repo <root> --output <new-directory>");
    if(addins.IsEnabled(WorkspaceHost.FeatureId))Console.WriteLine("model-init|model-check --file <model.json> | model-execute --file <model.json> --data <directory>");
    if(addins.IsEnabled("development.remote-project-lab"))Console.WriteLine("VM Feature: LabControl with Addin, local activation, model and target fingerprint.");
    return 2;
}
catch(AddinDisabledException e){Console.Error.WriteLine(e.Message);return 78;}
catch(Exception e){Console.Error.WriteLine(e.Message);return 2;}

static void Run(string exe,IEnumerable<string> args,string directory)
{
    var start=new ProcessStartInfo(exe){WorkingDirectory=directory,UseShellExecute=false};
    foreach(var value in args)start.ArgumentList.Add(value);
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";
    using var process=Process.Start(start)!;process.WaitForExit();
    if(process.ExitCode!=0)throw new InvalidOperationException(Path.GetFileName(exe)+" failed: "+process.ExitCode);
}
static void Pack(string root,string output)
{
    if(!File.Exists(Path.Combine(root,"LaunchDesk.Next.slnx")))throw new ArgumentException("Repository root required.");
    if(Directory.Exists(output))throw new ArgumentException("Use a new output directory; existing files will not be overwritten.");
    Directory.CreateDirectory(output);
    var outer=Directory.GetParent(root)!.FullName; // Use the installed SDK; do not rewrite the repository SDK pin.
    var product=Path.Combine(output,"product");var development=Path.Combine(output,"development");
    Run("dotnet",new[]{"publish",Path.Combine(root,"src/LaunchDesk.Next.Desktop/LaunchDesk.Next.Desktop.csproj"),"--no-restore","--disable-build-servers","-c","Release","-o",product},outer);
    Run("dotnet",new[]{"publish",Path.Combine(root,"dev/RemoteProjectLab/Tool/LaunchDesk.Next.Development.csproj"),"--no-restore","--disable-build-servers","-c","Release","-o",development},outer);
    string scripts=Path.Combine(root,"dev/RemoteProjectLab");Directory.CreateDirectory(Path.Combine(scripts,".local"));
    string csc=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Microsoft.NET/Framework64/v4.0.30319/csc.exe");
    Run(csc,new[]{"@build.rsp"},scripts);
    Run(csc,new[]{"/nologo","/target:exe","/platform:x64","/r:System.Web.Extensions.dll","/r:System.Security.dll","/out:.local/LabVix.exe","DevelopmentSwitch.cs","AddinGate.cs","LabSettings.cs",@"Host\LabVix.cs"},scripts);
    foreach(var file in new[]{"LabControl.exe","LabVix.exe"})File.Copy(Path.Combine(scripts,".local",file),Path.Combine(development,file));
    File.Copy(Path.Combine(scripts,"lab.example.json"),Path.Combine(development,"lab.example.json"));
    File.Copy(Path.Combine(scripts,"README.md"),Path.Combine(development,"README.md"));

    var shared=new[]{"LaunchDesk.Next.Domain.dll","LaunchDesk.Next.Application.dll","LaunchDesk.Next.Platform.dll","LaunchDesk.Next.Bootstrap.dll"};
    var hashes=shared.ToDictionary(name=>name,name=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(product,name)))));
    foreach(var name in shared)
        if(hashes[name]!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(development,name)))))throw new InvalidOperationException("Shared backend diverged: "+name);
    if(Directory.EnumerateFiles(product,"*",SearchOption.AllDirectories).Any(path=>Path.GetFileName(path).Contains("LabVix",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path).Contains("Development",StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Product contains developer executable.");
    var zipHashes=new Dictionary<string,string>();
    foreach(var name in new[]{"product","development"})
    {
        var zip=Path.Combine(output,"LaunchDesk.Next."+name+".zip");ZipFile.CreateFromDirectory(Path.Combine(output,name),zip);
        zipHashes[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));
    }
    File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{sharedBackendSha256=hashes,archives=zipHashes,frameworkDependent=true,productAddins=AddinCatalog.Load(Path.Combine(product,"Addins")).EnabledIds,developmentAddins=AddinCatalog.Load(Path.Combine(development,"Addins")).EnabledIds,vmActionsExecuted=0},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine("Created two packages with byte-identical shared backend assemblies: "+output);
}
