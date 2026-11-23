using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Core30.Ui;

namespace Core30.Workbench;

internal static class Views
{
    public static T Id<T>(T element, string id) where T : FrameworkElement
    {
        AutomationProperties.SetAutomationId(element, id);
        return element;
    }

    public static T Styled<T>(T element, string style) where T : FrameworkElement
    {
        element.SetResourceReference(FrameworkElement.StyleProperty, style);
        return element;
    }

    public static Brush Brush(string key) => (Brush)Application.Current.FindResource("color." + key);

    public static TextBlock Text(string value, string style = "Text.Label") =>
        Styled(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap }, style);

    public static TextBlock Muted(string value) => Text(value, "Text.Meta");

    public static FrameworkElement Glyph(string key, double size = 18, string tone = "primary")
    {
        var icon = Styled(new ContentControl { Width = size, Height = size, Foreground = Brush(tone) }, "UiGlyph");
        icon.Content = Application.Current.FindResource("UiGlyph" + key);
        return icon;
    }

    public static StackPanel IconLabel(string glyph, string title, bool compact = false, string tone = "secondary")
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Glyph(glyph, tone: tone));
        if (!compact) row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
        return row;
    }

    public static Button ActionButton(string caption, string id, string style = "Button.Base") =>
        Id(Styled(new Button { Content = caption }, style), id);

    public static Button ActionButton(string caption, Action click, string id, string style = "Button.Base")
    {
        var button = ActionButton(caption, id, style);
        button.Click += (_, _) => click();
        return button;
    }

    public static WrapPanel Actions(params FrameworkElement[] actions)
    {
        var panel = new WrapPanel();
        foreach (var action in actions)
        {
            action.Margin = new Thickness(0, 0, 8, 4);
            panel.Children.Add(action);
        }
        return panel;
    }

    public static StackPanel Stack(params FrameworkElement[] elements)
    {
        var stack = new StackPanel();
        foreach (var element in elements) stack.Children.Add(element);
        return stack;
    }

    public static T Below<T>(T element, double gap = 12) where T : FrameworkElement
    {
        element.Margin = new Thickness(0, gap, 0, 0);
        return element;
    }

    public static Grid SideBySide(FrameworkElement left, FrameworkElement right, double ratio = 1.55,
        double breakpoint = 740, double gap = 20, bool detailsFirst = false)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(left);
        panel.Children.Add(right);
        Grid.SetColumn(right, 2);
        right.VerticalAlignment = left.VerticalAlignment = VerticalAlignment.Top;

        var stacked = false;
        panel.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < breakpoint;
            if (stacked == narrow) return;
            stacked = narrow;
            panel.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : gap);
            panel.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(right, narrow ? 0 : 2);
            Grid.SetRow(left, narrow && detailsFirst ? 1 : 0);
            Grid.SetRow(right, narrow && !detailsFirst ? 1 : 0);
            left.Margin = new Thickness(0, narrow && detailsFirst ? gap : 0, 0, 0);
            right.Margin = new Thickness(0, narrow && !detailsFirst ? gap : 0, 0, 0);
        };
        return panel;
    }

    public static Border Card(string? title, FrameworkElement content, FrameworkElement? command = null)
    {
        var card = Styled(new Border(), "Card");
        card.SetResourceReference(Border.PaddingProperty, "inset.dense-card");
        if (title is null)
        {
            card.Child = content;
            return card;
        }
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (command is not null)
        {
            DockPanel.SetDock(command, Dock.Right);
            heading.Children.Add(command);
        }
        var label = Text(title, "Text.Section");
        label.SetResourceReference(TextBlock.FontSizeProperty, "font-size.subtitle");
        label.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetHeadingLevel(label, AutomationHeadingLevel.Level2);
        heading.Children.Add(label);
        card.Child = Stack(heading, content);
        return card;
    }

    public static TextBox Input(string value = "", string id = "", string? name = null)
    {
        var box = Id(new TextBox { Text = value }, id);
        if (name is not null) AutomationProperties.SetName(box, name);
        return box;
    }

    public static Grid Prompt(TextBox input, string label)
    {
        var hint = Text(label);
        hint.Foreground = Brush("muted");
        hint.IsHitTestVisible = false;
        hint.Margin = new Thickness(13, 0, 11, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        hint.TextWrapping = TextWrapping.NoWrap;
        hint.TextTrimming = TextTrimming.CharacterEllipsis;
        void Refresh() => hint.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        input.TextChanged += (_, _) => Refresh();
        Refresh();
        var layout = new Grid();
        layout.Children.Add(input);
        layout.Children.Add(hint);
        return layout;
    }

    public static ComboBox Choice(IEnumerable options, object? value, string id, bool editable = false)
    {
        var picker = Id(new ComboBox { ItemsSource = options, IsEditable = editable }, id);
        picker.SelectedItem = value;
        if (picker.SelectedIndex < 0 && editable && value is not null) picker.Text = value.ToString();
        return picker;
    }

    public static StackPanel Field(string label, FrameworkElement field)
    {
        AutomationProperties.SetName(field, label);
        return Stack(Text(label), Below(field, 5));
    }

    public static UniformGrid Form(params FrameworkElement[] fields)
    {
        var result = new UniformGrid { Columns = 2 };
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            field.Margin = new Thickness(index % 2 == 0 ? 0 : 5, 0, index % 2 == 0 ? 5 : 0, 12);
            result.Children.Add(field);
        }
        return result;
    }

    public static ListBox Picker(string label, string id)
    {
        var list = Id(Styled(new ListBox { MaxHeight = 332 }, "UiProfileList"), id);
        AutomationProperties.SetName(list, label);
        return list;
    }

    public static ListBoxItem Row(object value, string title, string? details = null, string? glyph = null, bool trim = false)
    {
        var name = Text(title);
        name.FontWeight = FontWeights.Bold;
        var subtitle = details is null ? null : Muted(details);
        if (trim)
        {
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            if (subtitle is not null)
            {
                subtitle.TextWrapping = TextWrapping.NoWrap;
                subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
            }
        }
        var label = subtitle is null ? (FrameworkElement)name : Stack(name, Below(subtitle, 2));
        var row = new DockPanel();
        if (glyph is not null)
        {
            var mark = new Border
            {
                Width = 32, Height = 32, Background = Brush("selected"), CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 10, 0), Child = Glyph(glyph, 16)
            };
            row.Children.Add(mark);
        }
        row.Children.Add(label);
        var item = new ListBoxItem { Tag = value, Content = row };
        if (trim) item.ToolTip = details is null ? title : $"{title}\n{details}";
        AutomationProperties.SetName(item, title);
        return item;
    }

    public static DataGrid Table(IEnumerable rows, string id, params (string Label, string Path, double Weight)[] columns)
    {
        var table = Id(Styled(new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single, ItemsSource = rows, MaxHeight = 340
        }, "UiTable"), id);
        ScrollViewer.SetHorizontalScrollBarVisibility(table, ScrollBarVisibility.Disabled);
        foreach (var (label, path, weight) in columns)
        {
            table.Columns.Add(new DataGridTextColumn
            {
                Header = label, Binding = new Binding(path), Width = new DataGridLength(weight, DataGridLengthUnitType.Star)
            });
        }
        return table;
    }

    public static StackPanel Details(IEnumerable<(string Label, string Value)> entries)
    {
        var rows = new StackPanel();
        foreach (var (label, value) in entries)
        {
            var key = Muted(label);
            key.Width = 92;
            DockPanel.SetDock(key, Dock.Left);
            var row = new DockPanel();
            row.Children.Add(key);
            row.Children.Add(Text(value));
            rows.Children.Add(new Border
            {
                Child = row, Padding = new Thickness(0, 6, 0, 6),
                BorderBrush = Brush("border"), BorderThickness = new Thickness(0, 0, 0, 1)
            });
        }
        return rows;
    }

    public static Border Badge(string label, string tone = "primary")
    {
        var text = Muted(label);
        text.Foreground = Brush(tone);
        text.FontWeight = FontWeights.Bold;
        return new Border
        {
            Background = Brush(tone == "primary" ? "selected" : tone + "-bg"),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left, Child = text
        };
    }
}

internal sealed class Notice : Border
{
    private readonly TextBlock _message = Views.Text("");

    public Notice(string id)
    {
        Views.Id(this, id);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(12, 8, 12, 8);
        BorderThickness = new Thickness(1);
        Child = _message;
        Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
    }

    public void Show(string message, string tone = "primary")
    {
        _message.Text = message;
        _message.Foreground = Views.Brush(tone);
        Background = Views.Brush(tone == "primary" ? "selected" : tone + "-bg");
        BorderBrush = Views.Brush(tone == "primary" ? "hero-edge" : tone + "-border");
        Visibility = Visibility.Visible;
    }

    public void Clear() => Visibility = Visibility.Collapsed;
}

internal sealed class PageStepper : StackPanel
{
    private readonly Button _previous;
    private readonly Button _next;
    private readonly TextBlock _count = Views.Muted("");
    private readonly int _pageSize;
    private int _page;
    private int _pages;

    public PageStepper(int pageSize, string id)
    {
        _pageSize = pageSize;
        Orientation = Orientation.Horizontal;
        _count.Margin = new Thickness(6, 0, 6, 0);
        _count.VerticalAlignment = VerticalAlignment.Center;
        _previous = Views.ActionButton("上一页", () => Turn(-1), id + "Previous", "Button.Quiet");
        _next = Views.ActionButton("下一页", () => Turn(1), id + "Next", "Button.Quiet");
        Children.Add(_previous);
        Children.Add(_count);
        Children.Add(_next);
    }

    public event Action? Changed;
    public int Start => _page * _pageSize;
    public int PageSize => _pageSize;

    public void Update(int itemCount, int? revealIndex = null)
    {
        _pages = Math.Max(1, (itemCount + _pageSize - 1) / _pageSize);
        _page = Math.Clamp(revealIndex.GetValueOrDefault(Start) / _pageSize, 0, _pages - 1);
        Refresh();
    }

    private void Turn(int direction)
    {
        _page += direction;
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        Visibility = _pages > 1 ? Visibility.Visible : Visibility.Collapsed;
        _count.Text = $"{_page + 1} / {_pages}";
        _previous.IsEnabled = _page > 0;
        _next.IsEnabled = _page + 1 < _pages;
    }
}
