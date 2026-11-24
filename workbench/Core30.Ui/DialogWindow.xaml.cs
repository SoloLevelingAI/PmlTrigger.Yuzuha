using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Core30.Ui;

public partial class DialogWindow : ChromeWindow
{
    private Control? _initialFocus;

    public DialogWindow(Window owner, string title, string? description = null, Geometry? icon = null,
        double width = 520, bool destructive = false)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        var work = MonitorWorkSize(owner);
        Width = Math.Min(width, Math.Max(1, work.Width - 32));
        MaxHeight = Math.Clamp(work.Height - 40, 1, 760);
        Description.Text = description;
        Intro.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
        HeadingIcon.Data = icon;
        if (icon is null)
        {
            HeadingBadge.Visibility = Visibility.Collapsed;
            HeadingCopy.Margin = default;
        }
        if (destructive)
        {
            HeadingBadge.SetResourceReference(Border.BackgroundProperty, "color.danger-bg");
            HeadingBadge.SetResourceReference(Border.BorderBrushProperty, "color.danger-border");
            HeadingIcon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "color.danger");
        }
        Loaded += (_, _) =>
        {
            if (_initialFocus is not { } control) return;
            Keyboard.Focus(control);
            if (control is TextBox text) text.SelectAll();
        };
    }

    public Panel Body => DialogBody;

    public void SetActions(string primaryText, Func<bool> accept, bool showCancel = true, bool destructive = false)
    {
        DialogActions.Children.Clear();
        _initialFocus = null;
        if (showCancel)
        {
            var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, _) => DialogResult = false;
            DialogActions.Children.Add(cancel);
            _initialFocus = cancel;
        }
        var primary = new Button { Content = primaryText, IsDefault = true, MinWidth = 96 };
        primary.SetResourceReference(StyleProperty, destructive ? "Button.Danger" : "Button.Primary");
        primary.Click += (_, _) => { if (accept()) DialogResult = true; };
        DialogActions.Children.Add(primary);
        _initialFocus ??= primary;
    }

    public void FocusOnOpen(Control control) => _initialFocus = control;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
