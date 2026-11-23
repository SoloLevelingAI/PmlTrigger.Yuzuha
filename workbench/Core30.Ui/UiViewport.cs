using System.Windows;

namespace Core30.Ui;

/// <summary>Responsive layout tokens use DIPs. Physical pixels are converted before selecting a layout.</summary>
public readonly record struct UiViewport(double SidebarWidth, double ToolbarHeight)
{
    public Thickness PageInset => SidebarWidth switch
    {
        >= 224 => new Thickness(36, 20, 36, 20),
        >= 184 => new Thickness(28, 16, 28, 18),
        _ => new Thickness(20, 14, 20, 16)
    };

    public static UiViewport AtWidth(double logicalWidth) => logicalWidth switch
    {
        >= 1320 => new(224, 50),
        >= 1024 => new(184, 46),
        _ => new(64, 44)
    };
}
