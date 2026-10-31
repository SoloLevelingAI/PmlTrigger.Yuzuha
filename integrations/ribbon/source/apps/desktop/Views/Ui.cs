using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using Microsoft.Win32;
namespace RibbonStudio
{
    public static class Ui
    {
        public static Brush Blue = new SolidColorBrush(Color.FromRgb(37, 78, 122)), Ink = new SolidColorBrush(Color.FromRgb(38, 50, 72)), Muted = new SolidColorBrush(Color.FromRgb(104, 118, 139)), Line = new SolidColorBrush(Color.FromRgb(228, 233, 241));
        public static TextBlock Text(string text, double size = 12, Brush brush = null)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = brush ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
        }
        public static Button Button(string text, Func<Task> action, bool primary = false)
        {
            var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4), MinHeight = 32 };
            if (primary)
                button.Style = (Style)Application.Current.FindResource("PrimaryButton");
            button.Click += async (s, e) => { button.IsEnabled = false; try { await action(); } catch (Exception error) { Error(error); } finally { button.IsEnabled = true; } };
            return button;
        }
        public static Button Sync(string text, Action action)
        {
            return Button(text, () => { action(); return Task.FromResult(0); });
        }
        public static Border Card(UIElement child)
        {
            var card = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(6), Child = child };
            card.SizeChanged += (s,e) => card.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, e.NewSize.Width), Math.Max(0, e.NewSize.Height)), 12, 12);
            return card;
        }
        public static Border TableCard(DataGrid grid)
        {
            grid.BorderThickness = new Thickness(0);
            grid.SizeChanged += (s,e) => grid.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, e.NewSize.Width), Math.Max(0, e.NewSize.Height)), 11, 11);
            return new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = grid };
        }
        public static TextBox Input(string text = "", bool multiline = false)
        {
            var box = new TextBox { Text = text ?? "", Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 10), AcceptsReturn = multiline, VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 75 : 32, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "剪切", Command = ApplicationCommands.Cut, CommandTarget = box, InputGestureText = "Ctrl+X" });
            menu.Items.Add(new MenuItem { Header = "复制", Command = ApplicationCommands.Copy, CommandTarget = box, InputGestureText = "Ctrl+C" });
            menu.Items.Add(new MenuItem { Header = "粘贴", Command = ApplicationCommands.Paste, CommandTarget = box, InputGestureText = "Ctrl+V" });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "全选", Command = ApplicationCommands.SelectAll, CommandTarget = box, InputGestureText = "Ctrl+A" });
            box.ContextMenu = menu;
            return box;
        }
        public static ComboBox Combo()
        {
            return new ComboBox { MinWidth = 150, MinHeight = 32, Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(5) };
        }
        public static DataGrid Grid(params string[] columns)
        {
            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, RowHeight = 38, Background = Brushes.White, BorderBrush = Line };
            foreach (var column in columns)
            {
                var parts = column.Split('|');
                grid.Columns.Add(new DataGridTextColumn { Header = parts[0], Binding = new System.Windows.Data.Binding(parts[1]), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            }
            return grid;
        }
        public static Window Dialog(string title, Window owner, double width = 950, double height = 700)
        {
            return new Window { Title = title, Owner = owner, Width = width, Height = height, MinWidth = 650, MinHeight = 450, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(245, 247, 251)), FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12 };
        }
        public static Window Active()
        {
            return Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        }
        public static bool Confirm(string message)
        {
            return Message(message, "确认操作", true);
        }
        public static void Error(Exception error)
        {
            Message(error.Message, "操作未完成", false);
        }
        public static bool Message(string text, string title, bool cancel = false)
        {
            var window = Dialog(title, Active(), 560, 310);
            window.ResizeMode = ResizeMode.NoResize;
            window.MinWidth = 420;
            window.MinHeight = 250;
            window.SizeToContent = SizeToContent.Height;
            var root = new DockPanel { Margin = new Thickness(25) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            if (cancel)
            {
                var no = Sync("取消", () => window.DialogResult = false);
                no.IsCancel = true;
                footer.Children.Add(no);
            }
            var yes = Button(cancel ? "确认" : "知道了", () => { window.DialogResult = true; return Task.FromResult(0); }, true);
            yes.IsDefault = true;
            footer.Children.Add(yes);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            root.Children.Add(Text(text, 13));
            window.Content = root;
            Chrome.Install(window);
            return window.ShowDialog() == true;
        }
        public static string[] OpenMany(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter, Multiselect = true };
            return dialog.ShowDialog() == true ? dialog.FileNames : new string[0];
        }
        public static string Open(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        public static string Save(string filter, string filename)
        {
            var dialog = new SaveFileDialog { Filter = filter, FileName = filename };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        public static string Prompt(string title, string label, string value, Window owner)
        {
            var window = Dialog(title, owner, 620, 310);
            window.MinHeight = 260;
            window.MinWidth = 500;
            window.ResizeMode = ResizeMode.NoResize;
            var stack = new StackPanel { Margin = new Thickness(25) };
            stack.Children.Add(Text(label));
            var box = Input(value);
            stack.Children.Add(box);
            string result = null;
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = Sync("取消", () => window.DialogResult = false);
            cancel.IsCancel = true;
            actions.Children.Add(cancel);
            var ok = Button("确定", () => { result = box.Text; window.DialogResult = true; return Task.FromResult(0); }, true);
            ok.IsDefault = true;
            actions.Children.Add(ok);
            stack.Children.Add(actions);
            window.Content = stack;
            Chrome.Install(window);
            window.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            window.ShowDialog();
            return result;
        }
        static readonly System.Collections.Generic.Dictionary<string, ImageSource> imageCache = new System.Collections.Generic.Dictionary<string, ImageSource>();
        public static ImageSource Image(ImageAsset asset)
        {
            if (asset == null || String.IsNullOrEmpty(asset.base64))
                return null;
            try
            {
                ImageSource cached;
                if (imageCache.TryGetValue(asset.base64, out cached))
                    return cached;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(Convert.FromBase64String(asset.base64));
                image.EndInit();
                image.Freeze();
                if (imageCache.Count >= 256)
                    imageCache.Clear();
                imageCache[asset.base64] = image;
                return image;
            }
            catch { return null; }
        }
        public static FrameworkElement Icon(string key, int size)
        {
            if (key == "undo" || key == "redo" || key == "clear")
            {
                var data = key == "clear" ? "M 5 5 L 19 19 M 19 5 L 5 19" : key == "undo" ? "M 4 8 L 10 2 M 4 8 L 10 14 M 4 8 H 13 C 20 8 22 14 17 19" : "M 20 8 L 14 2 M 20 8 L 14 14 M 20 8 H 11 C 4 8 2 14 7 19";
                return new Viewbox { Width = size, Height = size, Child = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Stroke = new SolidColorBrush(Color.FromRgb(74, 106, 153)), StrokeThickness = 1.8, Width = 24, Height = 24, Stretch = Stretch.Uniform, Margin = new Thickness(2) } };
            }
            try
            {
                var source = (FrameworkElement)Application.Current.FindResource("Icon_" + key);
                return new Viewbox { Width = size, Height = size, Child = (FrameworkElement)XamlReader.Parse(XamlWriter.Save(source)) };
            }
            catch { return new Viewbox { Width = size, Height = size, Child = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 2 2 L 22 2 L 22 22 L 2 22 Z M 6 7 L 18 7 M 6 12 L 18 12 M 6 17 L 14 17"), Stroke = Blue, StrokeThickness = 1.4, Width = 24, Height = 24 } }; }
        }
        public static TextBox Code(string text)
        {
            var box = Input(text, true);
            box.IsReadOnly = true;
            box.FontFamily = new FontFamily("Consolas");
            box.FontSize = 11;
            box.Height = 220;
            box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            return box;
        }
        public static ScrollViewer Scroll(UIElement child)
        {
            return new ScrollViewer { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }
        public static void Capture(Window window, string path)
        {
            window.UpdateLayout();
            var pixels = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            pixels.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pixels));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var stream = File.Create(path))
                encoder.Save(stream);
        }
        public static MenuItem Menu(string text, Func<Task> action, bool enabled = true, string gesture = null)
        {
            var item = new MenuItem { Header = text, IsEnabled = enabled, InputGestureText = gesture ?? "" };
            item.Click += async (s, e) => { e.Handled = true; try { await action(); } catch (Exception error) { Error(error); } };
            return item;
        }
        public static T Ancestor<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                var match = node as T;
                if (match != null)
                    return match;
                node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }
            return null;
        }
        public static void InitTheme()
        {
            foreach (var filename in new[] { "OfficeRibbon.xaml", "StudioTheme.xaml" })
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", filename)) });
        }
    }
}
