using System.IO;
using System.Diagnostics;
using LaunchDesk.Next.Bootstrap;
using LaunchDesk.Next.Desktop.Features.Launch;
namespace LaunchDesk.Next.Desktop.Bootstrap;

public static class CompositionRoot
{
    public static LaunchWindow? CreateWindow() =>
        LaunchDesk.Next.Bootstrap.Addins.AddinCatalog.LoadDefault().CreateIfEnabled(WorkspaceHost.FeatureId,CreateLoginWindow);

    private static LaunchWindow CreateLoginWindow()
    {
        var root=Environment.GetEnvironmentVariable("LAUNCHDESK_NEXT_DATA")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LaunchDesk.Next");
        var host=new WorkspaceHost(root);
        var window=new LaunchWindow(new LaunchInteraction(host.Workspace,host.Log),new LaunchViewModel(host.Workspace.Model));
        window.OpenDataRequested+=(_,_)=>Process.Start(new ProcessStartInfo(root){UseShellExecute=true});
        window.Closed+=(_,_)=>host.Dispose();return window;
    }
}
