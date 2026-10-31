using System.Windows;

namespace YuzuhaToolkit.Desktop;

/// <summary>Isolated smoke rendering uses an explicit client canvas; native monitor limits are recorded separately.</summary>
public partial class MainWindow
{
    private Size? _smokeViewport;
    private readonly List<object> _smokeCaptures = [];

    /// <summary>Lay out the real workspace at the requested DIP size without relying on HWND resize acceptance.</summary>
    /// <param name="width">Required client canvas width in device-independent pixels.</param>
    /// <param name="height">Required client canvas height in device-independent pixels.</param>
    private void SetSmokeViewport(double width, double height)
    {
        _smokeViewport = new(width, height);
        WindowState = WindowState.Normal;
        WindowSurface.FixedViewport = true;
        WindowSurface.HorizontalAlignment = HorizontalAlignment.Left;
        WindowSurface.VerticalAlignment = VerticalAlignment.Top;
        WindowSurface.Margin = new Thickness(0);
        WindowSurface.Width = width;
        WindowSurface.Height = height;
        WindowSurface.Measure(_smokeViewport.Value);
        WindowSurface.Arrange(new Rect(_smokeViewport.Value));
        WindowSurface.UpdateLayout();
    }
}
