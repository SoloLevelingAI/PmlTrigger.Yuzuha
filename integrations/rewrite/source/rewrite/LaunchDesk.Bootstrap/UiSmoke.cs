using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaunchDesk.Bootstrap;

internal static class UiSmoke
{
    internal static async Task RunAsync(string destination)
    {
        var output = Path.GetFullPath(destination);
        Directory.CreateDirectory(output);
        if (!new ThemeResourceCatalog().Resolve(ThemePreference.Light).IsSuccess)
            throw new InvalidOperationException("Theme resource resolution failed.");
        var bindingFailures = new StringWriter();
        var trace = new TextWriterTraceListener(bindingFailures);
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        await using var root = new DesktopCompositionRoot(
            new StartupOptions(DataDirectory: Path.Combine(output, "state-" + Guid.NewGuid().ToString("N"))),
            AppContext.BaseDirectory, registryDirectory: Path.Combine(output, "registry"));
        await root.InitializeAsync(Application.Current.Dispatcher, false);
        var app1 = new ApplicationDefinition(Guid.NewGuid(), "AVEVA E3D 3.1", "E3D 3.1", EntryKind.Monitor,
            @"C:\Program Files (x86)\AVEVA\Everything3D3.1\mon.exe",
            @"C:\Program Files (x86)\AVEVA\Everything3D3.1\launch.init",
            @"C:\Program Files (x86)\AVEVA\Everything3D3.1", "E3D");
        var app2 = app1 with
        {
            Id = Guid.NewGuid(), Name = "AVEVA Marine 12.1", Version = "AM 12.1", Kind = EntryKind.MarineBatch,
            ExecutablePath = @"C:\AVEVA\Marine\marine.bat"
        };
        var license = new LicenseDefinition(Guid.NewGuid(), "E3D · 虚拟机许可",
            new ResourceDescriptor(Guid.NewGuid(), new Revision(1), root.Location.MachineId, "192.168.173.10", 4545,
                true, 4546, null, @"D:\VirtualMachines\E3D\E3D.vmx", true, new ProbePolicy()));
        var local = license with
        {
            Id = Guid.NewGuid(), Name = "AM · 本机许可",
            Resource = license.Resource with { Host = "localhost", RequireRules = false, VmPath = null }
        };
        var tool = new ToolDefinition(Guid.NewGuid(), "PML AI", "自定义工具", @"D:\PML\AI",
            new PackageSource(PackageKind.Folder, @"D:\Packages\PmlAi"), @"D:\Packages",
            [new ToolVariable(Guid.NewGuid(), "PMLLIB", "{Target}", VariableScope.Process)]);
        foreach (var definition in new[]
                 {
                     new DefinitionDraft(DefinitionKind.Application, app1), new(DefinitionKind.Application, app2),
                     new(DefinitionKind.License, License: license), new(DefinitionKind.License, License: local),
                     new(DefinitionKind.Tool, Tool: tool)
                 })
        {
            var current = (await root.Catalog.ReadAsync(default)).Value;
            var saved = await root.Catalog.SaveDefinitionAsync(CommitId.New(), definition, current.Revision, default);
            if (!saved.IsSuccess) throw new InvalidOperationException(saved.Error!.SafeMessage);
        }

        foreach (var (name, application, resource, module) in new[]
                 {
                     ("MAR · Design · E3D 3.1", app1, license, "design"),
                     ("MAR · Draw · E3D 3.1", app1, license, "draw"),
                     ("MAR · Outfitting · AM 12.1", app2, local, "Outfitting")
                 })
        {
            var current = (await root.Catalog.ReadAsync(default)).Value;
            using var secret = SecretUpdate.Keep();
            var saved = await root.Catalog.SaveProfileAsync(CommitId.New(),
                new ProfileDraft(null, name, application.Id, application.Version, "mar", "SYSTEM", "/ALL_NO_MDS",
                    module, resource.Id, [tool.Id]), secret, current.Revision, default);
            if (!saved.IsSuccess) throw new InvalidOperationException(saved.Error!.SafeMessage);
        }

        root.Shell.Workspace.Update((await root.Catalog.ReadAsync(default)).Value);
        root.Shell.ProfileEditor.Load(root.Shell.Workspace.Snapshot.Profiles[0]);
        root.Shell.Applications.Selected = root.Shell.Workspace.Snapshot.Applications[0];
        root.Shell.Licenses.Selected = root.Shell.Workspace.Resources[0];
        root.Shell.Tools.Selected = root.Shell.Workspace.Snapshot.Tools[0];
        root.Shell.Workspace.Message = "演示配置 · 未连接真实许可或执行应用。";
        root.Shell.Workspace.UpdateRun(new RunSnapshot(OperationId.New(), OperationKind.Launch, 3,
            RunStage.WindowObserved, "WindowObserved", "已观察到应用窗口，项目登录仍待确认。", DateTimeOffset.UtcNow, false, []));
        var window = new MainWindow(root.Shell);
        Application.Current.MainWindow = window;
        window.Show();
        var captures = new List<object>();
        foreach (var (width, height) in new[] { (1440, 1000), (1180, 800), (1120, 740) })
        {
            window.Width = width;
            window.Height = height;
            foreach (var page in Enum.GetValues<PageId>())
            {
                root.Shell.Page = page;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                window.UpdateLayout();
                AssertFixedAction(window, page);
                foreach (var dpi in new[] { 96, 144, 192 })
                {
                    var scale = dpi / 96.0;
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * scale),
                        (int)Math.Ceiling(window.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var file = $"{width}x{height}-{page}{(dpi == 96 ? "" : "-" + dpi + "dpi")}.png";
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, file));
                    encoder.Save(stream);
                    captures.Add(new { width, height, dpi, page = page.ToString(), file });
                }
            }
        }

        var password = root.Shell.ProfileEditor.Password;
        password.AttachInput("ui-test-only".AsSpan());
        password.RevealWhileEditing();
        password.HideDisplay();
        if (!password.HasInput || password.IsRevealed || password.DisplayText.Length != 0)
            throw new InvalidOperationException("Lost focus discarded or revealed input.");
        using (var secret = password.DetachInput())
        {
            if (!secret.Borrow().SequenceEqual("ui-test-only".AsSpan()))
                throw new InvalidOperationException("Secret handoff failed.");
        }

        trace.Flush();
        File.WriteAllText(Path.Combine(output, "bindings.log"), bindingFailures.ToString());
        File.WriteAllText(Path.Combine(output, "report.json"),
            JsonSerializer.Serialize(
                new { captures, passwordFocusAndHandoff = "passed", bindingErrors = bindingFailures.ToString() },
                new JsonSerializerOptions { WriteIndented = true }));
        PresentationTraceSources.DataBindingSource.Listeners.Remove(trace);
        window.Close();
        if (bindingFailures.ToString().Contains("Error:", StringComparison.Ordinal))
            throw new InvalidOperationException("WPF binding errors; see bindings.log.");
    }

    private static void AssertFixedAction(MainWindow window, PageId page)
    {
        var label = page switch
        {
            PageId.Dashboard => "启动选中配置", PageId.Profiles => "保存配置", PageId.Applications => "保存应用",
            PageId.Licenses => "保存连接", PageId.Tools => "添加工具", PageId.ToolSettings => "保存工具", PageId.History => "预览恢复",
            PageId.Deployment => "确认执行", PageId.Sharing => "确认导入配置", _ => "确认迁移"
        };
        var action = Descendants(window).OfType<System.Windows.Controls.Button>().First(button => button.IsVisible &&
            (button.Content?.ToString() == label || button.Content is System.Windows.Controls.StackPanel panel && panel.Children
                .OfType<System.Windows.Controls.TextBlock>().Any(text => text.Text == label)));
        var origin = action.TransformToAncestor(window).Transform(new Point());
        if (origin.X < 0 || origin.Y < 0 || origin.X + action.ActualWidth > window.ActualWidth + 1 ||
            origin.Y + action.ActualHeight > window.ActualHeight + 1)
            throw new InvalidOperationException($"Fixed action is outside the window: {page}");
        for (var parent=VisualTreeHelper.GetParent(action);parent is not null;parent=VisualTreeHelper.GetParent(parent))
            if(parent is System.Windows.Controls.ScrollViewer) throw new InvalidOperationException($"Primary action scrolls with content: {page}");
        if(page==PageId.Profiles && Descendants(window).OfType<System.Windows.Controls.ScrollViewer>().First(v=>v.Name=="ProfileBody").ScrollableHeight>1)
            throw new InvalidOperationException("The basic login form requires vertical scrolling.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
