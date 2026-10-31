using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Shell;

namespace LaunchDesk.Desktop;

public partial class MainWindow
{
    private const double FrameRadius = 8;
    private const uint ActiveFrameColor = 0x00DED4C6; // COLORREF: #C6D4DE
    private const uint InactiveFrameColor = 0x00EBE5DE; // COLORREF: #DEE5EB
    private readonly bool systemRoundedFrame = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    private void InitializeWindowFrame()
    {
        // 非零玻璃内边距使WPF保留DWM边框，避免设置窗口区域；
        // 窗口区域会禁用Windows 11圆角和阴影。
        WindowChrome.GetWindowChrome(this).GlassFrameThickness =
            systemRoundedFrame ? new Thickness(1) : new Thickness(0);
        var outline = new System.Windows.Controls.Border
        {
            Name = "WindowOutline", CornerRadius = new CornerRadius(FrameRadius), BorderThickness = new Thickness(1),
            IsHitTestVisible = false
        };
        System.Windows.Controls.Grid.SetRowSpan(outline, 2);
        System.Windows.Controls.Panel.SetZIndex(outline, 1000);
        PresentationRoot.Children.Add(outline);
        RegisterName(outline.Name, outline);
        SourceInitialized += (_, _) => UpdateWindowFrame();
        StateChanged += (_, _) =>
        {
            UpdateWindowFrame();
            Dispatcher.InvokeAsync(UpdateWindowFrame, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        Activated += (_, _) => UpdateWindowFrame();
        Deactivated += (_, _) => UpdateWindowFrame();
        LocationChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized) UpdateWindowFrame();
        };
        PresentationRoot.SizeChanged += (_, _) => UpdateWindowFrame();
        UpdateWindowFrame();
    }

    private void UpdateWindowFrame()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var maximized = WindowState == WindowState.Maximized;
        var outline = (System.Windows.Controls.Border)FindName("WindowOutline");
        var color = IsActive ? ActiveFrameColor : InactiveFrameColor;
        outline.BorderBrush = new SolidColorBrush(Color.FromRgb((byte)color, (byte)(color >> 8), (byte)(color >> 16)));
        outline.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        var margin = new Thickness(0);
        if (maximized && handle != 0 && GetWindowRect(handle, out var bounds))
        {
            var monitor = new FrameMonitorInfo { Size = Marshal.SizeOf<FrameMonitorInfo>() };
            if (GetMonitorInfoW(MonitorFromWindow(handle, 2), ref monitor))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                margin = new Thickness(Math.Max(0, monitor.Work.Left - bounds.Left) / dpi.DpiScaleX,
                    Math.Max(0, monitor.Work.Top - bounds.Top) / dpi.DpiScaleY,
                    Math.Max(0, bounds.Right - monitor.Work.Right) / dpi.DpiScaleX,
                    Math.Max(0, bounds.Bottom - monitor.Work.Bottom) / dpi.DpiScaleY);
            }
        }

        if (PresentationRoot.Margin != margin) PresentationRoot.Margin = margin;
        PresentationRoot.Clip = maximized || PresentationRoot.ActualWidth <= 0 || PresentationRoot.ActualHeight <= 0
            ? null
            : new RectangleGeometry(new Rect(0, 0, PresentationRoot.ActualWidth, PresentationRoot.ActualHeight),
                FrameRadius, FrameRadius);
        if (handle != 0 && systemRoundedFrame)
        {
            var preference = maximized ? 1u : 2u;
            DwmSetWindowAttribute(handle, 33, ref preference, sizeof(uint));
            DwmSetWindowAttribute(handle, 34, ref color, sizeof(uint));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameMonitorInfo
    {
        public int Size;
        public FrameRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out FrameRect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref FrameMonitorInfo info);
}