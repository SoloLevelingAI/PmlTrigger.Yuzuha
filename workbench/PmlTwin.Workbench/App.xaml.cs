using System.Windows;
using System.IO;

namespace PmlTwin.Workbench;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var index = Array.IndexOf(e.Args, "--workspace");
        var directory = index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PmlTwin", "Core30Workbench");
        MainWindow = new MainWindow(WorkbenchStore.Open(directory));
        MainWindow.Show();
    }
}
