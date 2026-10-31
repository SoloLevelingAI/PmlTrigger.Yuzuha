using System.Windows;
namespace LaunchDesk.Next.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { var window=Bootstrap.CompositionRoot.CreateWindow(); if(window is null) { Shutdown(0); return; } window.Show(); }
        catch (Exception ex) { MessageBox.Show("LaunchDesk.Next could not load its Addins/data/settings.\n" + ex.Message, "LaunchDesk.Next", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
}
