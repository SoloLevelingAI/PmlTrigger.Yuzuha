using System.Windows.Markup;

namespace LaunchDesk.Desktop;

internal static class Ui
{
    internal static SolidColorBrush Brush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    internal static T Bind<T>(this T target, DependencyProperty property, string path,
        BindingMode mode = BindingMode.OneWay) where T : DependencyObject
    {
        BindingOperations.SetBinding(target, property,
            new Binding(path) { Mode = mode, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return target;
    }

    internal static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    internal static StackPanel Row(params UIElement[] children)
    {
        var panel = Stack(children);
        panel.Orientation = Orientation.Horizontal;
        return panel;
    }

    internal static TextBlock Text(string value, double size = 13, string color = "#182238", bool bold = false)
    {
        return new TextBlock
        {
            Text = value, FontSize = size, Foreground = Brush(color),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 7)
        };
    }

    internal static TextBlock Bound(string path, double size = 13, string color = "#182238", bool bold = false)
    {
        return Text("", size, color, bold).Bind(TextBlock.TextProperty, path);
    }

    internal static Button Button(string label, string command, string icon = "", bool primary = false)
    {
        var button =
            new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) }.Bind(
                System.Windows.Controls.Button.CommandProperty, command);
        IconAssist.SetKind(button, icon);
        if (primary) button.SetResourceReference(FrameworkElement.StyleProperty, "Primary");
        return button;
    }

    internal static FrameworkElement Field(string label, string path, bool multiline = false)
    {
        var box = new TextBox
        {
            AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height = multiline ? 76 : 38
        }.Bind(TextBox.TextProperty, path, BindingMode.TwoWay);
        return Labeled(label, box);
    }

    internal static FrameworkElement Labeled(string label, UIElement child)
    {
        var panel = Stack(Text(label, 12, "#748198"), child);
        panel.Margin = new Thickness(0, 0, 0, 14);
        return panel;
    }

    internal static FrameworkElement Combo(string label, string items, string selected, string display = "")
    {
        var combo = new ComboBox().Bind(ItemsControl.ItemsSourceProperty, items).Bind(
            System.Windows.Controls.Primitives.Selector.SelectedItemProperty, selected, BindingMode.TwoWay);
        if (display.Length > 0)
            combo.ItemTemplate =
                Template($"<TextBlock Text='{{Binding {display}}}' TextTrimming='CharacterEllipsis'/>");
        else
            combo.ItemTemplate = Template("<TextBlock Text='{Binding Converter={StaticResource EnumLabel}}' TextTrimming='CharacterEllipsis'/>");
        return Labeled(label, combo);
    }

    internal static Grid Pair(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    internal static CheckBox Check(string label, string path)
    {
        return new CheckBox { Content = label }.Bind(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            path, BindingMode.TwoWay);
    }

    internal static Border Card(UIElement body, Thickness? padding = null)
    {
        return new Border
        {
            Background = Brushes.White, BorderBrush = Brush("#E1E7F1"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = padding ?? new Thickness(22), Child = body
        };
    }

    internal static ScrollViewer Scroll(UIElement body)
    {
        return new ScrollViewer
        {
            Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 6, 0)
        };
    }

    internal static Grid Columns(UIElement left, UIElement right, double width = 310)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    internal static Grid DetailColumns(UIElement left, UIElement right, double width = 398)
    {
        var grid = Columns(left, right);
        grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = new GridLength(width);
        return grid;
    }

    internal static Grid HeaderBody(UIElement header, UIElement body, UIElement? footer = null)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(header);
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        if (footer is not null)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);
        }

        return grid;
    }

    internal static DataTemplate Template(string body)
    {
        return (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
            body + "</DataTemplate>");
    }

    internal static ListBox List(string items, string selected, string title, string? detail = null)
    {
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch }.Bind(ItemsControl.ItemsSourceProperty, items).Bind(
            System.Windows.Controls.Primitives.Selector.SelectedItemProperty, selected, BindingMode.TwoWay);
        list.SetResourceReference(ItemsControl.ItemContainerStyleProperty, "ProfileItem");
        list.ItemTemplate = Template(
            $"<StackPanel><TextBlock Text='{{Binding {title}}}' FontSize='14' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>{(detail is null ? "" : $"<TextBlock Text='{{Binding {detail}}}' Foreground='#748198' FontSize='12' Margin='0,7,0,0' TextTrimming='CharacterEllipsis'/>")}</StackPanel>");
        return list;
    }

    internal static DataGrid Table(string items, params (string Label, string Path, bool Editable)[] columns)
    {
        var grid =
            new DataGrid { IsReadOnly = !columns.Any(c => c.Editable), MinHeight = 110 }.Bind(
                ItemsControl.ItemsSourceProperty, items);
        foreach (var column in columns)
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Label,
                Binding = new Binding(column.Path)
                {
                    Mode = column.Editable ? BindingMode.TwoWay : BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                IsReadOnly = !column.Editable, Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        return grid;
    }
}
