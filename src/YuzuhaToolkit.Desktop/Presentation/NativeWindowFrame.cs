using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YuzuhaToolkit.Desktop;

/// <summary>Keep the custom caption on a native DWM frame so the OS draws exterior shadows and rounded corners.</summary>
internal static class NativeWindowFrame
{
    /// <summary>Use DWM while restored and WindowChrome's work-area clipping while maximized.</summary>
    internal static void Attach(Window window)
    {
        void UpdateState()
        {
            var maximized = window.WindowState == WindowState.Maximized;
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
            if (chrome is not null) chrome.GlassFrameThickness = new Thickness(maximized ? 0 : 1);
            if (window.Content is FrameworkElement surface)
            {
                var padding = new Thickness(0);
                var handle = new WindowInteropHelper(window).Handle;
                var monitor = MonitorFromWindow(handle, 2);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (maximized && GetMonitorInfo(monitor, ref info) && GetWindowRect(handle, out var rect))
                {
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
                    padding = new(Math.Max(0, info.Work.Left - rect.Left) / dpi.DpiScaleX,
                        Math.Max(0, info.Work.Top - rect.Top) / dpi.DpiScaleY,
                        Math.Max(0, rect.Right - info.Work.Right) / dpi.DpiScaleX,
                        Math.Max(0, rect.Bottom - info.Work.Bottom) / dpi.DpiScaleY);
                }
                surface.Margin = padding;
            }
            Apply(window);
        }
        window.StateChanged += (_, _) => UpdateState();
        window.DpiChanged += (_, _) => UpdateState();
        UpdateState();
    }

    /// <summary>Apply supported frame preferences after HWND creation; Windows retains maximize, Snap and resize behavior.</summary>
    internal static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        // WindowChrome keeps a nonzero glass margin and no custom HRGN. Both are
        // important: a region-based rounded WPF window prevents native DWM rounding.
        Set(handle, 33, 2); // DWMWA_WINDOW_CORNER_PREFERENCE / DWMWCP_ROUND
        Set(handle, 20, DesktopTheme.Current == "Dark" ? 1 : 0); // immersive dark frame
        Set(handle, 34, unchecked((int)0xFFFFFFFE)); // DWMWA_BORDER_COLOR / COLOR_NONE: shadow defines the edge
    }

    private static void Set(IntPtr handle, int attribute, int value)
    {
        var result = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        if (result < 0) Trace.TraceWarning("DWM frame attribute {0} was unavailable: 0x{1:X8}", attribute, result);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    /// <summary>Native screen rectangle in physical pixels.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        /// <summary>Screen coordinates of the four edges.</summary>
        internal int Left, Top, Right, Bottom;
    }

    /// <summary>MONITORINFO layout for resolving the maximized work area.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        /// <summary>Size of this native structure in bytes.</summary>
        internal int Size;
        /// <summary>Full monitor bounds and usable work area, in physical pixels.</summary>
        internal Rect Monitor, Work;
        /// <summary>Native flags identifying the primary monitor.</summary>
        internal int Flags;
    }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
