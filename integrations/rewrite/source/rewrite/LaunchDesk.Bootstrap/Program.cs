namespace LaunchDesk.Bootstrap;

internal sealed class DesktopApplication(string[] arguments) : Application
{
    private DesktopCompositionRoot? root;
    private bool closing;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            new ThemeResourceCatalog().Apply(this);
            if (arguments.Length >= 2 && arguments[0] == "--ui-smoke")
            {
                await UiSmoke.RunAsync(arguments[1]);
                Shutdown(0);
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--contract-smoke")
            {
                await ContractSmoke.RunAsync(arguments[1]);
                Shutdown(0);
                return;
            }

            var options = new StartupOptions(Value("--config"), Value("--data-dir"), arguments.Contains("--portable"));
            root = new DesktopCompositionRoot(options, AppContext.BaseDirectory);
            await root.InitializeAsync(Dispatcher);
            var window = new MainWindow(root.Shell);
            MainWindow = window;
            window.Closing += async (_, args) =>
            {
                if (closing) return;
                args.Cancel = true;
                closing = true;
                window.IsEnabled = false;
                try
                {
                    await root.DisposeAsync();
                }
                finally
                {
                    Shutdown();
                }
            };
            window.Show();
            window.Width = Math.Min(window.Width, SystemParameters.WorkArea.Width - 36);
            window.Height = Math.Min(window.Height, SystemParameters.WorkArea.Height - 36);
        }
        catch (Exception exception)
        {
            if (arguments.Length >= 2 && arguments[0] is "--ui-smoke" or "--contract-smoke")
            {
                Directory.CreateDirectory(arguments[1]);
                File.WriteAllText(Path.Combine(arguments[1], "failure.txt"), exception.ToString());
            }
            else
            {
                MessageBox.Show("工作空间未能打开：" + exception.Message + "\n可使用 --data-dir 指定独立的数据目录。", "LaunchDesk",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    private string? Value(string name)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return new DesktopApplication(args) { ShutdownMode = ShutdownMode.OnExplicitShutdown }.Run();
    }
}
