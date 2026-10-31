using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
namespace RibbonStudio
{
    public static class ThemeVerification
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception("Theme verification: " + message);
        }
        static System.Collections.Generic.IEnumerable<MenuItem> MenuItems(ItemCollection items)
        {
            foreach (var item in items.OfType<MenuItem>())
            {
                yield return item;
                foreach (var child in MenuItems(item.Items))
                    yield return child;
            }
        }
        static Color Surface(Button button)
        {
            return ((SolidColorBrush)((Border)button.Template.FindName("Surface", button)).Background).Color;
        }
        static void Capture(FrameworkElement element, string path)
        {
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path))
                encoder.Save(stream);
        }
        public static async Task Run(MainWindow main, string resultPath)
        {
            Check(main.WindowStyle == WindowStyle.None, "borderless main");
            Check(WindowChrome.GetWindowChrome(main) != null && WindowChrome.GetWindowChrome(main).ResizeBorderThickness.Left >= 6, "native resize chrome");
            var dialog = Ui.Dialog("Office Ribbon · 控件状态检查", main, 1020, 650);
            var root = new StackPanel { Margin = new Thickness(24) };
            root.Children.Add(Ui.Text("Office Ribbon 控件与状态", 22));
            root.Children.Add(Ui.Text("同一主题覆盖正常、悬停、按下、选中、禁用、焦点和右键菜单。", 12, Ui.Muted));
            var buttons = new WrapPanel { Margin = new Thickness(0, 14, 0, 14) };
            root.Children.Add(buttons);
            var normal = Ui.Sync("普通按钮", () => { });
            var hover = Ui.Sync("悬停", () => { });
            var pressed = Ui.Sync("按下", () => { });
            var selected = Ui.Sync("选中", () => { });
            selected.Tag = "Selected";
            var disabled = Ui.Sync("禁用", () => { });
            disabled.IsEnabled = false;
            var primary = Ui.Button("主操作", () => Task.FromResult(0), true);
            var primaryHover = Ui.Button("主操作悬停", () => Task.FromResult(0), true);
            foreach (var button in new[] { normal, hover, pressed, selected, disabled, primary, primaryHover })
            {
                button.Width = 112;
                buttons.Children.Add(button);
            }
            var form = new Grid();
            form.ColumnDefinitions.Add(new ColumnDefinition());
            form.ColumnDefinitions.Add(new ColumnDefinition());
            var a = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var b = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(b, 1);
            form.Children.Add(a);
            form.Children.Add(b);
            root.Children.Add(form);
            a.Children.Add(Ui.Text("输入框", 12, Ui.Muted));
            a.Children.Add(Ui.Input("正常输入 · 中文 / Macro"));
            var focused = Ui.Input("键盘焦点");
            a.Children.Add(focused);
            var readOnly = Ui.Input("只读内容可复制");
            readOnly.IsReadOnly = true;
            a.Children.Add(readOnly);
            var unavailable = Ui.Input("禁用输入");
            unavailable.IsEnabled = false;
            a.Children.Add(unavailable);
            b.Children.Add(Ui.Text("下拉与选择", 12, Ui.Muted));
            var combo = Ui.Combo();
            foreach (var text in new[] { "Large · 32 × 32", "Small · 16 × 16", "保持图标比例" })
                combo.Items.Add(text);
            combo.SelectedIndex = 0;
            b.Children.Add(combo);
            var checks = new WrapPanel { Margin = new Thickness(0, 10, 0, 16) };
            checks.Children.Add(new CheckBox { Content = "未选", Margin = new Thickness(6) });
            checks.Children.Add(new CheckBox { Content = "已选", IsChecked = true, Margin = new Thickness(6) });
            checks.Children.Add(new CheckBox { Content = "部分", IsThreeState = true, IsChecked = null, Margin = new Thickness(6) });
            checks.Children.Add(new CheckBox { Content = "禁用", IsChecked = true, IsEnabled = false, Margin = new Thickness(6) });
            b.Children.Add(checks);
            var sizes = new WrapPanel();
            foreach (var size in new[] { 32, 16 })
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal };
                item.Children.Add(Ui.Icon("paste", size));
                item.Children.Add(Ui.Text(size + "px", 11));
                sizes.Children.Add(new ToggleButton { Content = item, IsChecked = size == 32, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(4) });
            }
            b.Children.Add(sizes);
            root.Children.Add(Ui.Text("原生右键菜单", 14));
            root.Children.Add(Ui.Text("重命名 / 独立副本 / 大小图标 / 移动到 Group / 复制标识与 Macro / 排序 / 删除。只读或无适用目标的操作禁用。", 11, Ui.Muted));
            dialog.Content = Ui.Scroll(root);
            Chrome.Install(dialog);
            dialog.Show();
            dialog.UpdateLayout();
            Check(dialog.WindowStyle == WindowStyle.None, "borderless dialog");
            await Task.Delay(60);
            Check(VisualStateManager.GoToState(hover, "MouseOver", false), "hover state available");
            Check(VisualStateManager.GoToState(pressed, "Pressed", false), "pressed state available");
            Check(VisualStateManager.GoToState(primaryHover, "MouseOver", false), "primary hover state available");
            dialog.UpdateLayout();
            await Task.Delay(40);
            Check(Surface(hover) == Color.FromRgb(233, 241, 254), "hover brush actual=" + Surface(hover));
            Check(Surface(pressed) == Color.FromRgb(205, 222, 250), "pressed brush");
            Check(Surface(selected) == Color.FromRgb(224, 234, 254), "selected brush");
            Check(Surface(primaryHover) == Color.FromRgb(38, 74, 202), "primary hover brush");
            Check(((Border)disabled.Template.FindName("Surface", disabled)).Opacity < 0.5, "disabled state");
            focused.Focus();
            Ui.Capture(dialog, Path.ChangeExtension(resultPath, "theme.png"));
            combo.IsDropDownOpen = true;
            await Task.Delay(80);
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            Check(popup != null && popup.IsOpen && combo.SelectedIndex == 0, "custom dropdown opens and retains selection");
            Capture((FrameworkElement)popup.Child, Path.ChangeExtension(resultPath, "dropdown.png"));
            combo.IsDropDownOpen = false;
            var selection = main.Selected;
            Check(selection.Button != null && selection.Item != null, "selected editable button");
            var menu = main.SelectionMenu(selection);
            Check(MenuItems(menu.Items).Any(item => Convert.ToString(item.Header) == "移动到 Group"), "group submenu");
            Check(MenuItems(menu.Items).First(item => Convert.ToString(item.Header).StartsWith("Large")).IsChecked == (selection.Item.size == "large"), "checked size command");
            menu.PlacementTarget = normal;
            menu.IsOpen = true;
            await Task.Delay(60);
            menu.ApplyTemplate();
            menu.UpdateLayout();
            Capture(menu, Path.ChangeExtension(resultPath, "context-menu.png"));
            menu.IsOpen = false;
            int count = main.Project.buttons.Count;
            var originalId = selection.Button.id;
            Check(menu.Items.OfType<MenuItem>().Count() <= 7, "bounded top-level menu");
            var copy = menu.Items.OfType<MenuItem>().First(item => Convert.ToString(item.Header) == "复制");
            copy.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var paste = main.SelectionMenu(selection).Items.OfType<MenuItem>().First(item => Convert.ToString(item.Header) == "粘贴");
            paste.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(main.Project.buttons.Count == count + 1, "duplicate menu command");
            Check(main.Selected.Button.id != originalId && main.Selected.Button.sourceId == null && main.Selected.Button.commandKey == "", "independent clone identity");
            await main.VerifyLayoutWorkflow();
            await main.VerifyEditHistory();
            await main.SetItemSize("small");
            Check(main.Selected.Item.size == "small", "context size action");
            main.Load(await main.Api.Post<Project>("projects/normalize", main.Project));
            await main.SaveDraft("edit");
            dialog.Close();
        }
    }
}
