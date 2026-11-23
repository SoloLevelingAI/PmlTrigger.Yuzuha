using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Core30.Ui;

internal sealed class ChromeFrameBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (Child is { } content)
        {
            // The frame uses a uniform radius. Clip the opaque title and body to its inner curve.
            var radius = CornerRadius.TopLeft;
            content.Clip = radius == 0
                ? null
                : new RectangleGeometry(new Rect(content.RenderSize),
                    Math.Max(0, radius - BorderThickness.Left / 2),
                    Math.Max(0, radius - BorderThickness.Top / 2));
        }
        return size;
    }
}
