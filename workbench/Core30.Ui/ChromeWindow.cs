using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Core30.Ui;

public class ChromeWindow : Window
{
    private static readonly DependencyPropertyKey FrameInsetPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FrameInset), typeof(Thickness), typeof(ChromeWindow),
            new PropertyMetadata(default(Thickness)));

    public static readonly DependencyProperty FrameInsetProperty = FrameInsetPropertyKey.DependencyProperty;

    public Thickness FrameInset => (Thickness)GetValue(FrameInsetProperty);

    public ChromeWindow()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this),
            (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this), CanResize));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this), CanResize));

        StateChanged += (_, _) => UpdateFrameInset();
        LocationChanged += (_, _) => UpdateFrameInset();
        SizeChanged += (_, _) => UpdateFrameInset();
        DpiChanged += (_, _) => UpdateFrameInset();
    }

    private void CanResize(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    private void UpdateFrameInset()
    {
        var inset = default(Thickness);
        if (WindowState == WindowState.Maximized)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero && ReadWorkArea(handle, out var work) && GetWindowRect(handle, out var bounds))
            {
                // The maximized HWND can extend past the work area by its invisible resize borders.
                var dpi = VisualTreeHelper.GetDpi(this);
                inset = new Thickness(
                    Math.Max(0, work.Left - bounds.Left) / dpi.DpiScaleX,
                    Math.Max(0, work.Top - bounds.Top) / dpi.DpiScaleY,
                    Math.Max(0, bounds.Right - work.Right) / dpi.DpiScaleX,
                    Math.Max(0, bounds.Bottom - work.Bottom) / dpi.DpiScaleY);
            }
        }
        if (FrameInset != inset) SetValue(FrameInsetPropertyKey, inset);
    }

    internal static Size MonitorWorkSize(Window owner)
    {
        var handle = new WindowInteropHelper(owner).Handle;
        if (handle == IntPtr.Zero || !ReadWorkArea(handle, out var work)) return SystemParameters.WorkArea.Size;
        var dpi = VisualTreeHelper.GetDpi(owner);
        return new Size((work.Right - work.Left) / dpi.DpiScaleX, (work.Bottom - work.Top) / dpi.DpiScaleY);
    }

    private static bool ReadWorkArea(IntPtr window, out NativeRect work)
    {
        const uint monitorDefaultToNearest = 2;
        var monitor = MonitorFromWindow(window, monitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var found = monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
        work = info.Work;
        return found;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
