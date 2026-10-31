using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Storage;
using LaunchDesk.Next.Platform.Credentials;
using LaunchDesk.Next.Platform.Diagnostics;
using LaunchDesk.Next.Platform.Files;
using LaunchDesk.Next.Platform.Networking;
using LaunchDesk.Next.Platform.Services;
using LaunchDesk.Next.Platform.Processes;
namespace LaunchDesk.Next.Bootstrap;

// Both distributions use this one composition. No VM, developer switch or UI dependency.
public sealed class WorkspaceHost : IDisposable
{
    public const string FeatureId="login.launch";
    public LaunchWorkspace Workspace { get; }
    public NLogRecordLog Log { get; }
    public WorkspaceHost(string root)
    {
        Addins.AddinCatalog.LoadDefault().Require(FeatureId);
        Directory.CreateDirectory(root);
        Log=new NLogRecordLog(Path.Combine(root,"logs","records.jsonl"));
        var profiles=new SavedProfiles(new JsonDocumentStore(Path.Combine(root,"profiles.json")));
        var files=new WindowsFileSystem();var processes=new WindowsProcessHost();
        var launcher=new LoginLaunchCoordinator(new CheckSavedProfile(profiles),
            new CheckAvevaLicence(new TcpProbe(),new WindowsServiceProbe()),
            new ResolveLoginSecret(new WindowsCredentialStore()),new StartMonitor(processes,files),
            new StartMarineBatch(processes),new StartDirect(processes),Log);
        var settings=Path.Combine(root,"discovery.settings.json");
        if(!File.Exists(settings))File.WriteAllText(settings,JsonSerializer.Serialize(FindInstalledAveva.DefaultOptions,new JsonSerializerOptions{WriteIndented=true}));
        var options=JsonSerializer.Deserialize<DiscoveryOptions>(File.ReadAllText(settings))??throw new InvalidDataException("Discovery settings are empty.");
        Workspace=new LaunchWorkspace(new LaunchModel(),profiles,launcher,new FindInstalledAveva(files),options,Log,
            new JsonDocumentStore(Path.Combine(root,"draft.json")),new JsonDocumentDirectory(Path.Combine(root,"snapshots")));
    }
    public void Dispose()=>Log.Dispose();
}
