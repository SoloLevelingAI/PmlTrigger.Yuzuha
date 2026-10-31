using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
namespace RibbonStudio
{
    public static class Chrome
    {
        static readonly Dictionary<Window, UIElement> bodies = new Dictionary<Window, UIElement>();
        static readonly Dictionary<Window, DockPanel> mainHeaders = new Dictionary<Window, DockPanel>();
        public static void Install(Window window, DockPanel existingHeader = null)
        {
            if (bodies.ContainsKey(window))
                return;
            var body = window.Content as UIElement;
            if (body == null)
                return;
            bodies[window] = body;
            window.WindowStyle = WindowStyle.None;
            window.AllowsTransparency = false;
            window.ResizeMode = window.ResizeMode == ResizeMode.NoResize ? ResizeMode.NoResize : ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 38, ResizeBorderThickness = window.ResizeMode == ResizeMode.NoResize ? new Thickness(0) : new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(10), UseAeroCaptionButtons = false });
            var header = existingHeader ?? new DockPanel { Height = 38, Background = Ui.Blue, LastChildFill = true };
            var captions = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(captions, Dock.Right);
            header.Children.Insert(0, captions);
            WindowChrome.SetIsHitTestVisibleInChrome(captions, true);
            if (window.Owner == null)
            {
                captions.Children.Add(Caption("最小化", "M 0 5 L 10 5", () => window.WindowState = WindowState.Minimized));
                if (window.ResizeMode != ResizeMode.NoResize)
                    captions.Children.Add(Caption("最大化 / 还原", "M 0 0 L 10 0 L 10 10 L 0 10 Z", () => ToggleMax(window)));
            }
            var close = Caption("关闭", "M 0 0 L 10 10 M 10 0 L 0 10", () => window.Close());
            close.Tag = "Close";
            captions.Children.Add(close);
            if (existingHeader == null)
            {
                var label = Ui.Text(window.Title, 12, Brushes.White);
                label.Margin = new Thickness(15, 0, 0, 0);
                label.VerticalAlignment = VerticalAlignment.Center;
                header.Children.Add(label);
                window.Content = null;
                var frame = new DockPanel();
                DockPanel.SetDock(header, Dock.Top);
                frame.Children.Add(header);
                frame.Children.Add(body);
                window.Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(188, 203, 223)), BorderThickness = new Thickness(1), Child = frame };
            }
            else
            {
                mainHeaders[window] = existingHeader;
                window.Content = null;
                window.Content = new Border { BorderBrush = Ui.Blue, BorderThickness = new Thickness(1), Child = body };
            }
            var system = new ContextMenu();
            var restore = Ui.Menu("还原", () => { window.WindowState = WindowState.Normal; return Task.FromResult(0); });
            var maximize = Ui.Menu("最大化", () => { window.WindowState = WindowState.Maximized; return Task.FromResult(0); });
            system.Items.Add(restore);
            system.Items.Add(Ui.Menu("最小化", () => { window.WindowState = WindowState.Minimized; return Task.FromResult(0); }));
            system.Items.Add(maximize);
            system.Items.Add(new Separator());
            system.Items.Add(Ui.Menu("关闭", () => { window.Close(); return Task.FromResult(0); }, true, "Alt+F4"));
            header.ContextMenu = system;
            system.Opened += (s, e) => { restore.IsEnabled = window.WindowState != WindowState.Normal; maximize.IsEnabled = window.ResizeMode != ResizeMode.NoResize && window.WindowState != WindowState.Maximized; };
            window.Closed += (s, e) => { bodies.Remove(window); mainHeaders.Remove(window); };
        }
        public static void Busy(Window window, bool busy)
        {
            UIElement body;
            if (bodies.TryGetValue(window, out body))
            {
                DockPanel header;
                if (mainHeaders.TryGetValue(window, out header) && body is Panel)
                {
                    foreach (UIElement child in ((Panel)body).Children)
                        if (child != header)
                            child.IsEnabled = !busy;
                }
                else
                    body.IsEnabled = !busy;
            }
            else if (window.Content is UIElement)
                ((UIElement)window.Content).IsEnabled = !busy;
        }
        public static void ToggleMax(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        static Button Caption(string tooltip, string data, Action action)
        {
            var button = Ui.Sync("", action);
            button.Style = (Style)Application.Current.FindResource("CaptionButton");
            button.Margin = new Thickness(0);
            button.Padding = new Thickness(0);
            button.Width = 44;
            button.Height = 38;
            button.MinHeight = 0;
            button.ToolTip = tooltip;
            button.Content = new Path { Data = Geometry.Parse(data), Stroke = new SolidColorBrush(Color.FromRgb(222, 233, 247)), StrokeThickness = 1.1, Width = 10, Height = 10, Stretch = Stretch.Uniform };
            return button;
        }
    }
}
