using System.Globalization;
using System.Windows.Documents;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace LaunchDesk.Desktop;

/// <summary>为按钮和导航附加图标资源名称。</summary>
public static class IconAssist
{
    /// <summary>图标类别的 WPF 依赖属性标识。</summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.RegisterAttached("Kind", typeof(string), typeof(IconAssist), new PropertyMetadata(""));

    /// <summary>读取依赖对象附加的图标资源名称。</summary>
    /// <param name="element">需读取或设置图标附加属性的 WPF 依赖对象。</param>
    /// <returns>当前依赖对象登记的图标资源键。</returns>
    public static string GetKind(DependencyObject element)
    {
        return (string)element.GetValue(KindProperty);
    }

    /// <summary>设置依赖对象附加的图标资源名称。</summary>
    /// <param name="element">需读取或设置图标附加属性的 WPF 依赖对象。</param>
    /// <include file="../../Comments.xml" path="doc/Value/*"/>
    public static void SetKind(DependencyObject element, string value)
    {
        element.SetValue(KindProperty, value);
    }
}

/// <summary>从几何资源绘制可继承前景色的界面图标。</summary>
public sealed class AppIcon : FrameworkElement
{
    /// <summary>图标类别的 WPF 依赖属性标识。</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string),
        typeof(AppIcon), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>图标前景色的 WPF 依赖属性标识。</summary>
    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(typeof(AppIcon),
            new FrameworkPropertyMetadata(Brushes.Black,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>定义、入口、来源或操作的有限类别。</summary>
    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>几何图标继承的前景色。</summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>创建默认 18 像素、可继承前景色的几何图标。</summary>
    public AppIcon()
    {
        Width = 18;
        Height = 18;
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>按当前图标键绘制几何资源，沿用控件前景色。</summary>
    /// <param name="context">由 WPF 提供、用于本次图标绘制的上下文。</param>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (string.IsNullOrWhiteSpace(Kind) || TryFindResource("Icon." + Kind) is not Geometry geometry) return;
        var bounds = geometry.Bounds;
        if (bounds.IsEmpty) return;
        var scale = Math.Min(Math.Max(1, ActualWidth - 3) / Math.Max(1, bounds.Width),
            Math.Max(1, ActualHeight - 3) / Math.Max(1, bounds.Height));
        var transform = new Matrix(scale, 0, 0, scale, (ActualWidth - bounds.Width * scale) / 2 - bounds.X * scale,
            (ActualHeight - bounds.Height * scale) / 2 - bounds.Y * scale);
        context.PushTransform(new MatrixTransform(transform));
        context.DrawGeometry(null,
            new Pen(Foreground, 1.5 / scale)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round },
            geometry);
        context.Pop();
    }
}