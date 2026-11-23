using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Core30.Ui;

internal static class InteractionMotion
{
    private static readonly CubicEase Out = new() { EasingMode = EasingMode.EaseOut };

    internal static DoubleAnimation Animation(double from, double to, int milliseconds) => new(
        from, to, TimeSpan.FromMilliseconds(milliseconds))
    {
        EasingFunction = Out,
        FillBehavior = FillBehavior.Stop
    };

    internal static void To(IAnimatable motion, DependencyProperty property, double target, int milliseconds)
    {
        var element = (DependencyObject)motion;
        var current = (double)element.GetValue(property);
        motion.BeginAnimation(property, null);
        element.SetValue(property, target);
        if (milliseconds > 0 && Math.Abs(current - target) > 0.001)
            motion.BeginAnimation(property, Animation(current, target, milliseconds), HandoffBehavior.SnapshotAndReplace);
    }
}

internal sealed class FeedbackBorder : Border
{
    public static readonly DependencyProperty PressedProperty =
        DependencyProperty.Register(nameof(Pressed), typeof(bool), typeof(FeedbackBorder),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty HotProperty =
        DependencyProperty.Register(nameof(Hot), typeof(bool), typeof(FeedbackBorder),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty TintBrushProperty =
        DependencyProperty.Register(nameof(TintBrush), typeof(Brush), typeof(FeedbackBorder),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty TintProperty =
        DependencyProperty.Register("Tint", typeof(double), typeof(FeedbackBorder),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _offset = new();

    public FeedbackBorder()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new TransformGroup { Children = { _scale, _offset } };
        Loaded += (_, _) => Refresh(0);
        IsEnabledChanged += (_, _) => Refresh(0);
    }

    public bool Pressed
    {
        get => (bool)GetValue(PressedProperty);
        set => SetValue(PressedProperty, value);
    }

    public bool Hot
    {
        get => (bool)GetValue(HotProperty);
        set => SetValue(HotProperty, value);
    }

    public Brush? TintBrush
    {
        get => (Brush?)GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    private static void OnStateChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        var surface = (FeedbackBorder)element;
        if (surface.IsLoaded) surface.Refresh(surface.Pressed ? 55 : 125);
    }

    private void Refresh(int milliseconds)
    {
        var down = IsEnabled && Pressed;
        var animate = SystemParameters.ClientAreaAnimation;
        var duration = animate ? milliseconds : 0;
        var scale = down && animate ? 0.965 : 1;
        var tint = down ? 0.17 : IsEnabled && Hot ? 0.075 : 0;
        InteractionMotion.To(_scale, ScaleTransform.ScaleXProperty, scale, duration);
        InteractionMotion.To(_scale, ScaleTransform.ScaleYProperty, scale, duration);
        InteractionMotion.To(_offset, TranslateTransform.YProperty, down && animate ? 1.25 : 0, duration);
        InteractionMotion.To(this, TintProperty, tint, down ? 0 : duration);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var tint = (double)GetValue(TintProperty);
        if (tint <= 0 || TintBrush is null || RenderSize.IsEmpty) return;
        var radius = CornerRadius.TopLeft;
        drawingContext.PushOpacity(tint);
        drawingContext.DrawRoundedRectangle(TintBrush, null, new Rect(RenderSize), radius, radius);
        drawingContext.Pop();
    }
}

/// <summary>A short entrance transition for changing content in a stationary layout.</summary>
public sealed class TransitionPresenter : ContentControl
{
    private readonly TranslateTransform _offset = new();

    public TransitionPresenter()
    {
        Focusable = false;
        RenderTransform = _offset;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        BeginAnimation(OpacityProperty, null);
        _offset.BeginAnimation(TranslateTransform.YProperty, null);
        if (!IsLoaded || oldContent is null || newContent is null || !SystemParameters.ClientAreaAnimation) return;
        var opacity = Opacity;
        BeginAnimation(OpacityProperty, InteractionMotion.Animation(opacity * 0.75, opacity, 155));
        _offset.BeginAnimation(TranslateTransform.YProperty, InteractionMotion.Animation(5, 0, 155));
    }
}
