// WPF prototype of the redesigned Ribbon Studio desktop UI.
// Code-only (no XAML) so it builds with the .NET Framework csc:
//   csc /target:winexe /out:WpfPrototype.exe /r:WPF/PresentationFramework.dll ... Prototype.cs
// Run:  WpfPrototype.exe                 (interactive)
//       WpfPrototype.exe --shot DIR      (renders every view to PNG, then exits)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RibbonStudioPrototype
{
    internal static class Tok
    {
        public static Brush Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            var s = hex.TrimStart('#');
            if (s.Length == 8) return Frozen(Color.FromArgb(B(s, 0), B(s, 2), B(s, 4), B(s, 6)));
            if (s.Length == 6) return Frozen(Color.FromArgb(255, B(s, 0), B(s, 2), B(s, 4)));
            return Frozen(Colors.Black);
        }
        private static byte B(string s, int i) { return byte.Parse(s.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        public static readonly Brush Nav = Hex("#254E7A"), White = Hex("#FFFFFF"), Page = Hex("#F5F7FB");
        public static readonly Brush Ink = Hex("#1B2430"), Body = Hex("#263248"), Sub = Hex("#3C4A5E");
        public static readonly Brush Muted = Hex("#5A6678"), Faint = Hex("#8A94A6"), TabInk = Hex("#4A5568");
        public static readonly Brush Accent = Hex("#2F5FD0"), Soft = Hex("#EDF3FE"), Chip = Hex("#EEF3FE");
        public static readonly Brush Line = Hex("#DFE4EC"), Hair = Hex("#F1F4F9"), CardLine = Hex("#E3E8F0");
        public static readonly Brush Field = Hex("#FBFCFE"), FieldLine = Hex("#DDE3EC"), Head = Hex("#F7F9FC");
        public static readonly Brush PillGray = Hex("#EEF1F6"), PillBlue = Hex("#E8F0FE");
        public static readonly Brush PillGreen = Hex("#E9F4EF"), PillAmber = Hex("#FBF1E4"), Check = Hex("#C3CCDA");
        public static readonly Brush Ok = Hex("#3C8A68"), Warn = Hex("#C07A2E"), TabBar = Hex("#EEF1F6");

        public const string Ui = "Microsoft YaHei UI", Mono = "Consolas";
    }

    internal static class Ico
    {
        private static readonly Dictionary<string, string> P32 = new Dictionary<string, string>
        {
            { "open", "M2 9 V5 A1.5 1.5 0 0 1 3.5 3.5 H11 L14 6.5 H27 A2 2 0 0 1 29 8.5 V9 M2 9 H30 A2 2 0 0 1 30 11 L27.5 24 A2 2 0 0 1 25.5 25.5 H6 A2 2 0 0 1 4 24 L2 11 Z" },
            { "newUic", "M7 3 H20 L26 9 V29 H7 Z M20 3 V9 H26 M16 15 V24 M12 19.5 H20" },
            { "save", "M5 4 H23 L28 9 V28 H5 Z M9 4 V12 H21 V4 M9 18 H23 V28 H9 Z" },
            { "deploy", "M16 25 V7 M9 14 L16 7 L23 14 M5 22 V27 H27 V22" },
            { "check", "M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3 M19 21 L28 30 M7.5 12.5 L11 16 L17 9" }
        };
        private static readonly Dictionary<string, string> P16 = new Dictionary<string, string>
        {
            { "exportUic", "M2 3 H9 V14 H2 Z M8 8 H15 M12 5 L15 8 L12 11" },
            { "exportJson", "M7 2 L4.5 2 L4.5 6.5 L2.5 8 L4.5 9.5 L4.5 14 L7 14 M9 2 L11.5 2 L11.5 6.5 L13.5 8 L11.5 9.5 L11.5 14 L9 14" },
            { "addPage", "M2 2 H9 L13 6 V14 H2 Z M9 2 V6 H13 M7 8 V12 M5 10 H9" },
            { "addGroup", "M2 4 H10 M2 8 H10 M2 12 H7 M13 6 V13 M9.5 9.5 H16" },
            { "addButton", "M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z M12 10 V15 M9.5 12.5 H14.5" },
            { "search", "M6 2.5 A3.8 3.8 0 1 0 6 10.1 A3.8 3.8 0 1 0 6 2.5 M9 9.2 L14 14.2" },
            { "undo", "M7 3 L2 7.5 L7 12 M2 7.5 H9 A4.2 4.2 0 0 1 9 16 H5.5" },
            { "redo", "M9 3 L14 7.5 L9 12 M14 7.5 H7 A4.2 4.2 0 0 0 7 16 H10.5" },
            { "caret", "M0.5 1 L4 4.5 L7.5 1" },
            { "plus", "M5.5 1 V10 M1 5.5 H10" },
            { "close", "M1 1 L9 9 M9 1 L1 9" },
            { "more", "M1 4 H11 M1 8 H11" },
            { "chevDown", "M1 1 L5 5 L9 1" },
            { "chevRight", "M1 1 L5 5 L1 9" },
            { "back", "M10 2 L3 8 L10 14" },
            { "box", "M8 2 L15 5.5 V11.5 L8 15 L1 11.5 V5.5 Z" },
            { "copy", "M5 1.5 H12 V10 H5 Z M2 4.5 V14 H9" },
            { "stack", "M8 1.5 L14.5 5 L8 8.5 L1.5 5 Z M1.5 7.5 L8 11 L14.5 7.5" },
            { "out", "M3 4 V12 H10 M7 8 H14 M11 5.5 L13.5 8 L11 10.5" },
            { "beam", "M2 2 H14 M2 14 H14 M8 2 V14" },
            { "ring", "M4 8 A4 4 0 1 0 12 8 A4 4 0 1 0 4 8" },
            { "grid", "M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z M9 9 H14 V14 H9 Z" },
            { "cube", "M8 1.5 L14.5 5 V11 L8 14.5 L1.5 11 V5 Z M1.5 5 L8 8.5 L14.5 5 M8 8.5 V14.5" },
            { "find", "M6 2.5 A3.6 3.6 0 1 0 6 9.7 A3.6 3.6 0 1 0 6 2.5 M9 9 L14 14" },
            { "layers", "M2 4 H9 V11 H2 Z M6 7 H14 V15 H6 Z" },
            { "ruler", "M1 11 L11 1 L15 5 L5 15 Z M4 8 L6 10 M7 5 L9 7" },
            { "sun", "M5 5 A3 3 0 1 0 11 5 A3 3 0 1 0 5 5 M8 0.5 V2 M8 12.5 V14 M0.5 8 H2 M12.5 8 H14" }
        };

        public static System.Windows.Shapes.Path Make(string key, double size, Brush stroke = null, double? weight = null)
        {
            var bigger = size >= 28;
            var table = bigger ? P32 : P16;
            string data;
            if (!table.TryGetValue(key, out data) && !P16.TryGetValue(key, out data)) data = P16["box"];
            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = stroke ?? Tok.Accent,
                StrokeThickness = weight ?? (bigger ? 1.8 : 1.35),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = null,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            return p;
        }

        public static FrameworkElement InBox(string key, double box, double icon)
        {
            var g = new Grid { Width = box, Height = box };
            g.Children.Add(Make(key, icon));
            return g;
        }
    }

    internal sealed class Cmd
    {
        public string Name, Macro, TipTitle, TipText, Group, Icon;
        public bool Large;
    }

    internal sealed class RowState
    {
        public Cmd Cmd;
        public bool Selected;
        public readonly List<Action> Refresh = new List<Action>();
    }

    public partial class Prototype : Window
    {
        private readonly List<RowState> rows = new List<RowState>();
        private readonly ContentControl host = new ContentControl();
        private readonly StackPanel ribbonGroups = new StackPanel { Orientation = Orientation.Horizontal };
        private string view = "table";
        private string query = "";
        private string groupFilter = "全部 Group";
        private Grid titlebar;
        private readonly Dictionary<string, Border> tabVisuals = new Dictionary<string, Border>();
        private TextBlock selectionLabel;
        private FrameworkElement traits;

        public Prototype()
        {
            Title = "Ribbon Studio · WPF Prototype";
            Width = 1280; Height = 840;
            MinWidth = 760; MinHeight = 620;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Tok.Page;
            FontFamily = new FontFamily(Tok.Ui);
            FontSize = 12;
            AllowsTransparency = false;

            Seed();
            Content = BuildShell();
            SizeChanged += (s, e) => { if (traits != null) traits.Visibility = ActualWidth < 1000 ? Visibility.Collapsed : Visibility.Visible; };
            Loaded += (s, e) => { if (ActualWidth < 1000 && traits != null) traits.Visibility = Visibility.Collapsed; };
            ShowView("table");
        }

        private void Seed()
        {
            var data = new[]
            {
                new object[]{"元件库工具","矢勤工具",false,"box","show !!SeqendCatalogManager","元件库","打开元件管理工具"},
                new object[]{"复制移动","矢勤工具",false,"copy","show !!CopyMove","复制移动","复制或移动选中元素"},
                new object[]{"批处理工具","矢勤工具",false,"stack","show !!BatchTools","批处理","运行批处理操作"},
                new object[]{"元素导出","矢勤工具",false,"out","show !!ExportElement","元素导出","导出当前元素"},
                new object[]{"支架建模工具","矢勤工具",false,"beam","show !!Support","支架建模","打开支架建模工具"},
                new object[]{"Spco工具","矢勤工具",false,"ring","show !!Spco","Spco工具","打开 Spco 工具"},
                new object[]{"脚手架工具箱","开发工具",true,"grid","show !!Scaffold","脚手架","打开脚手架工具箱"},
                new object[]{"MaWei工具箱","开发工具",false,"cube","show !!MaWei","MaWei工具箱","打开 MaWei 工具箱"},
                new object[]{"查询工具","个人常用",false,"find","show !!Query","查询","打开查询工具"},
                new object[]{"Spec工具","个人常用",false,"layers","show !!Spec","Spec","打开等级工具"},
                new object[]{"测量","个人常用",false,"ruler","show !!Measure","测量","测量元素"},
                new object[]{"小太阳","个人常用",true,"sun","show !!Sun","小太阳","打开常用工具"}
            };
            for (int i = 0; i < data.Length; i++)
            {
                var d = data[i];
                var r = new RowState
                {
                    Selected = i < 2,
                    Cmd = new Cmd
                    {
                        Name = (string)d[0], Group = (string)d[1], Large = (bool)d[2], Icon = (string)d[3],
                        Macro = (string)d[4], TipTitle = (string)d[5], TipText = (string)d[6]
                    }
                };
                rows.Add(r);
            }
        }

        // ---------------- shell ----------------
        private FrameworkElement BuildShell()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(136) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            titlebar = BuildTitlebar();
            root.Children.Add(titlebar);
            var ribbon = BuildRibbon();
            Grid.SetRow(ribbon, 1);
            root.Children.Add(ribbon);
            var tabs = BuildTabs();
            Grid.SetRow(tabs, 2);
            root.Children.Add(tabs);
            var crumb = BuildBreadcrumb();
            Grid.SetRow(crumb, 3);
            root.Children.Add(crumb);
            Grid.SetRow(host, 4);
            root.Children.Add(host);
            var status = BuildStatus();
            Grid.SetRow(status, 5);
            root.Children.Add(status);
            return root;
        }

        private Grid BuildTitlebar()
        {
            var g = new Grid { Background = Tok.Nav };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var mark = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), Background = Tok.Hex("#A8C4E8"), Margin = new Thickness(0, 0, 8, 0) };
            left.Children.Add(mark);
            left.Children.Add(new TextBlock { Text = "Ribbon Studio / UIC Designer", Foreground = Tok.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(left);
            g.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            right.Children.Add(WindowButton("—", (s, e) => WindowState = WindowState.Minimized));
            right.Children.Add(WindowButton("▢", (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
            right.Children.Add(WindowButton("✕", (s, e) => Close()));
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
            return g;
        }

        private Border WindowButton(string glyph, RoutedEventHandler click)
        {
            var t = new TextBlock { Text = glyph, Foreground = Tok.Hex("#C9D8EA"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Width = 38, Height = 34, Background = Brushes.Transparent, Child = t, Cursor = Cursors.Hand };
            b.MouseEnter += (s, e) => b.Background = Tok.Hex("#2F5F92");
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonUp += click;
            return b;
        }

        private FrameworkElement BuildRibbon()
        {
            var wrap = new Grid { Background = Tok.White };
            wrap.RowDefinitions.Add(new RowDefinition());
            wrap.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });

            var bar = new Grid();
            bar.Children.Add(ribbonGroups);
            ribbonGroups.Margin = new Thickness(12, 0, 0, 0);
            ribbonGroups.VerticalAlignment = VerticalAlignment.Top;

            ribbonGroups.Children.Add(Group("文件",
                new[] { Large("加载文件", "open"), Large("新建 UIC", "newUic"), Large("保存快照", "save") },
                new[] { Small("导出 UIC", "exportUic"), Small("导出 JSON", "exportJson") }));
            ribbonGroups.Children.Add(Group("设计", new FrameworkElement[0],
                new[] { Small("添加菜单页", "addPage"), Small("添加 Group", "addGroup"), Small("添加按钮", "addButton") }));
            var checkGroup = Group("检查", new[] { Large("检查项目", "check") }, new FrameworkElement[0]);
            ribbonGroups.Children.Add(checkGroup);
            ribbonGroups.Children.Add(Group("撤销 / 重做", new FrameworkElement[0], new[] { Small("撤销", "undo"), Small("重做", "redo") }));
            var toolsGroup = Group("工具", new[] { Large("部署", "deploy") }, new FrameworkElement[0]);
            ribbonGroups.Children.Add(toolsGroup);
            ribbonGroups.Children.Add(MoreGroup());

            wrap.Children.Add(bar);
            var line = new Border { Height = 1, Background = Tok.Line, VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetRow(line, 1);
            wrap.Children.Add(line);
            return wrap;
        }

        private FrameworkElement Group(string caption, FrameworkElement[] large, FrameworkElement[] small)
        {
            var outer = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            outer.ColumnDefinitions.Add(new ColumnDefinition());
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13) });

            var inner = new Grid { Height = 136 };
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(106) });
            inner.RowDefinitions.Add(new RowDefinition());

            // content block, vertically centred in the 106px band
            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            if (large.Length > 0)
            {
                var lg = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var b in large) lg.Children.Add(b);
                content.Children.Add(lg);
            }
            if (small.Length > 0)
            {
                if (large.Length > 0) content.Children.Add(new Border { Width = 10 });
                var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                foreach (var b in small) st.Children.Add(b);
                content.Children.Add(st);
            }
            inner.Children.Add(content);

            var cap = new TextBlock
            {
                Text = caption, FontSize = 11, Foreground = Tok.Muted, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(cap, 1);
            inner.Children.Add(cap);

            outer.Children.Add(inner);
            var sep = new Border { Width = 1, Background = Tok.Line, Margin = new Thickness(6, 12, 6, 24) };
            Grid.SetColumn(sep, 1);
            outer.Children.Add(sep);
            return outer;
        }

        private FrameworkElement MoreGroup()
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            p.Children.Add(Ico.Make("more", 16, Tok.Muted, 1.3));
            p.Children.Add(new TextBlock { Text = "更多", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Tok.Body });
            return new Grid { Width = 76, Height = 106, Children = { p } };
        }

        private Border Large(string label, string icon)
        {
            var stack = new StackPanel { Width = 76, Height = 76, VerticalAlignment = VerticalAlignment.Center };
            var ic = Ico.Make(icon, 32);
            ic.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(ic);
            stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Tok.Body, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            return Hover(new Border { Child = stack, Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(2) });
        }

        private Border Small(string label, string icon)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Height = 28, VerticalAlignment = VerticalAlignment.Center };
            var ic = Ico.Make(icon, 16);
            ic.Margin = new Thickness(8, 0, 0, 0);
            p.Children.Add(ic);
            p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Tok.Body });
            return Hover(new Border { Child = p, Background = Brushes.Transparent, CornerRadius = new CornerRadius(4) });
        }

        private static Border Hover(Border b)
        {
            b.Cursor = Cursors.Hand;
            b.MouseEnter += (s, e) => b.Background = Tok.Hex("#F2F6FD");
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) => b.Background = Tok.Hex("#E4EDFB");
            return b;
        }

        private FrameworkElement BuildTabs()
        {
            var g = new Grid { Background = Tok.TabBar };
            var strip = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 0, 0) };
            strip.Children.Add(DocTab("YtoolsAM.uic", false, "table"));
            strip.Children.Add(DocTab("Equipment.uic", true, null));
            strip.Children.Add(DocTab("Piping.uic", false, null));
            strip.Children.Add(DocTab("DraftTools.uic", false, null));
            strip.Children.Add(DocTab("Personal.uic", false, null));
            strip.Children.Add(new Border { Width = 1, Background = Tok.Hex("#D9DEE8"), Height = 18, Margin = new Thickness(10, 0, 10, 11) });
            strip.Children.Add(PageTab("部署", "deploy"));
            strip.Children.Add(PageTab("加载登记", "registry"));
            strip.Children.Add(PageTab("工作区", "workspace"));
            var plus = new Border
            {
                Width = 26, Height = 26, Margin = new Thickness(8, 0, 4, 6), CornerRadius = new CornerRadius(6),
                BorderBrush = Tok.Hex("#E6EAF2"), BorderThickness = new Thickness(1), Background = Tok.White,
                Child = Ico.Make("plus", 16, Tok.Muted, 1.4), VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.Hand
            };
            strip.Children.Add(plus);
            g.Children.Add(strip);
            return g;
        }

        private FrameworkElement DocTab(string label, bool dirty, string target)
        {
            var inner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 10, 10, 0) };
            if (dirty)
            {
                var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Tok.Accent, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                inner.Children.Add(dot);
            }
            inner.Children.Add(new TextBlock { Text = label, Foreground = Tok.TabInk, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            inner.Children.Add(new TextBlock { Text = "✕", FontSize = 9, Foreground = Tok.Hex("#B6C0CE"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var b = new Border { Height = 36, Child = inner, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            if (target != null)
            {
                tabVisuals[target] = b;
                b.MouseLeftButtonUp += (s, e) => ShowView(target);
            }
            return b;
        }

        private FrameworkElement PageTab(string label, string target)
        {
            var txt = new TextBlock { Text = label, FontSize = 12, Foreground = Tok.TabInk, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border
            {
                Width = label.Length > 3 ? 82 : 58, Height = 26, Margin = new Thickness(0, 0, 8, 7), CornerRadius = new CornerRadius(6),
                BorderBrush = Tok.Hex("#E6EAF2"), BorderThickness = new Thickness(1), Background = Tok.White, Child = txt,
                VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.Hand
            };
            tabVisuals[target] = b;
            b.MouseLeftButtonUp += (s, e) => ShowView(target);
            return b;
        }

        private FrameworkElement BuildBreadcrumb()
        {
            var g = new Grid { Background = Tok.White };
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var back = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(6), BorderBrush = Tok.CardLine, BorderThickness = new Thickness(1),
                Background = Tok.White, Child = Ico.Make("back", 16, Tok.Muted, 1.5), Cursor = Cursors.Hand
            };
            p.Children.Add(back);
            p.Children.Add(new TextBlock { Text = "返回 Ribbon", Foreground = Tok.Accent, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(new TextBlock { Text = "/", Foreground = Tok.Hex("#C9D2E0"), Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(new TextBlock { Text = "YtoolsAM.uic", Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(new TextBlock { Text = "/", Foreground = Tok.Hex("#C9D2E0"), Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(new TextBlock { Text = "功能表格", Foreground = Tok.Body, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(p);

            traits = new Border
            {
                Background = Tok.Hex("#F3F6FB"), CornerRadius = new CornerRadius(6), Height = 24, Padding = new Thickness(12, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock { Text = "同一文件 · 共用选择与撤销栈", FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center }
            };
            g.Children.Add(traits);
            var line = new Border { Height = 1, Background = Tok.Hex("#EDF0F6"), VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetRow(line, 1);
            g.Children.Add(line);
            return g;
        }

        private FrameworkElement BuildStatus()
        {
            var g = new Grid { Background = Tok.White };
            g.Children.Add(new Border { Height = 1, Background = Tok.CardLine, VerticalAlignment = VerticalAlignment.Top });
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            left.Children.Add(new TextBlock { Text = "草稿已记录 · YtoolsAM.uic", FontSize = 11, Foreground = Tok.Muted });
            left.Children.Add(new Border { Width = 1, Height = 10, Background = Tok.Hex("#DDE3EC"), Margin = new Thickness(12, 0, 12, 0) });
            left.Children.Add(new TextBlock { Text = "独立文件状态与增量撤销", FontSize = 11, Foreground = Tok.Faint });
            g.Children.Add(left);
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 0) };
            right.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Tok.Ok, Margin = new Thickness(0, 0, 8, 0) });
            right.Children.Add(new TextBlock { Text = "本地服务已连接", FontSize = 11, Foreground = Tok.Muted });
            g.Children.Add(right);
            return g;
        }

        private void ShowView(string name)
        {
            view = name;
            foreach (var kv in tabVisuals)
            {
                bool active = kv.Key == name;
                kv.Value.Background = active ? Tok.White : Tok.Hex("#EEF1F6");
                kv.Value.BorderThickness = active ? new Thickness(0) : new Thickness(1);
                var tb = (kv.Value.Child as TextBlock);
                if (tb != null) tb.Foreground = active ? Tok.Accent : Tok.TabInk;
            }
            switch (name)
            {
                case "workspace": host.Content = WorkspaceView(); break;
                case "deploy": host.Content = DeployView(); break;
                case "registry": host.Content = RegistryView(); break;
                default: host.Content = TableView(); break;
            }
        }

        // ---------------- shared bits ----------------
        private static Border Card(FrameworkElement child, Thickness margin)
        {
            return new Border
            {
                Background = Tok.White, CornerRadius = new CornerRadius(8), BorderBrush = Tok.CardLine,
                BorderThickness = new Thickness(1), Child = child, Margin = margin
            };
        }

        private static Border Chip(string text, Brush bg, Brush fg, double width = double.NaN)
        {
            return new Border
            {
                Background = bg, CornerRadius = new CornerRadius(6), Height = 30, Padding = new Thickness(10, 0, 10, 0),
                Width = width, VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 12, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
        }

        private static Border Pill(string text, bool blue)
        {
            return new Border
            {
                Background = blue ? Tok.PillBlue : Tok.PillGray, CornerRadius = new CornerRadius(10), Height = 20, Width = 56,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = text, FontSize = 11, Foreground = blue ? Tok.Accent : Tok.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
        }

        private static Border ActionButton(string text, bool primary, Action click, double width = double.NaN)
        {
            var b = new Border
            {
                Height = 30, Width = width, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 0, 12, 0),
                Background = primary ? Tok.Accent : Tok.White,
                BorderBrush = primary ? Tok.Accent : Tok.CardLine, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = text, FontSize = 12, Foreground = primary ? Tok.White : Tok.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
                Cursor = Cursors.Hand
            };
            b.MouseEnter += (s, e) => b.Opacity = 0.9;
            b.MouseLeave += (s, e) => b.Opacity = 1;
            b.MouseLeftButtonUp += (s, e) => click();
            return b;
        }

        private static TextBox Input(string text, bool mono = false)
        {
            return new TextBox
            {
                Text = text, Height = 30, FontSize = 12, Padding = new Thickness(8, 5, 8, 0),
                Background = Tok.Field, BorderBrush = Tok.FieldLine, BorderThickness = new Thickness(1),
                Foreground = mono ? Tok.Sub : Tok.Body,
                FontFamily = new FontFamily(mono ? Tok.Mono : Tok.Ui),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock Label(string text, double size = 11, Brush fg = null)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = fg ?? Tok.Muted, Margin = new Thickness(0, 0, 0, 6) };
        }

        private static FrameworkElement CardHeader(string title, string subtitle)
        {
            var p = new StackPanel();
            p.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            if (subtitle != null) p.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 4, 0, 0) });
            return p;
        }

        // ---------------- table view ----------------
        private FrameworkElement TableView()
        {
            var page = new Grid { Background = Tok.Page };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());

            var head = new StackPanel { Margin = new Thickness(18, 14, 18, 8) };
            head.Children.Add(new TextBlock { Text = "按钮与属性对照", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            head.Children.Add(new TextBlock { Text = "直接编辑单元格；勾选多行后批量修改 Group、图标、Macro 或 Tooltip。", FontSize = 13, Foreground = Tok.Muted, Margin = new Thickness(0, 6, 0, 0) });
            page.Children.Add(head);

            var body = new Grid { Margin = new Thickness(18, 0, 18, 14) };
            var card = Card(null, new Thickness(0));
            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition());
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // toolbar
            var toolbar = new Grid { Margin = new Thickness(16, 16, 16, 0), Height = 30 };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition());
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var search = Input("");
            var searchHost = new Grid();
            searchHost.Children.Add(search);
            var glass = Ico.Make("search", 16, Tok.Faint, 1.4);
            glass.HorizontalAlignment = HorizontalAlignment.Left;
            glass.Margin = new Thickness(10, 0, 0, 0);
            glass.VerticalAlignment = VerticalAlignment.Center;
            searchHost.Children.Add(glass);
            search.Padding = new Thickness(30, 5, 8, 0);
            search.TextChanged += (s, e) => { query = search.Text.Trim(); RebuildRows(); };
            toolbar.Children.Add(searchHost);
            var filter = Chip("全部 Group ▾", Tok.White, Tok.Body, 124);
            filter.BorderBrush = Tok.FieldLine;
            filter.BorderThickness = new Thickness(1);
            filter.Cursor = Cursors.Hand;
            filter.MouseLeftButtonUp += (s, e) => CycleFilter();
            Grid.SetColumn(filter, 2);
            toolbar.Children.Add(filter);
            selectionLabel = new TextBlock { Text = "", FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(selectionLabel, 4);
            toolbar.Children.Add(selectionLabel);
            var clear = new TextBlock { Text = "清除", FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), Cursor = Cursors.Hand };
            clear.MouseLeftButtonUp += (s, e) => { foreach (var r in rows) r.Selected = false; RebuildRows(); };
            Grid.SetColumn(clear, 5);
            toolbar.Children.Add(clear);
            var batch = ActionButton("批量设置 ▾", true, () => OpenBatch(), 96);
            Grid.SetColumn(batch, 6);
            toolbar.Children.Add(batch);
            cardGrid.Children.Add(toolbar);

            // table
            var table = new Grid { Margin = new Thickness(16, 14, 16, 0) };
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            table.RowDefinitions.Add(new RowDefinition());
            var header = TableHeader();
            table.Children.Add(header);
            var list = new StackPanel();
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
            Grid.SetRow(scroll, 1);
            table.Children.Add(scroll);
            var rowsHost = list;
            Grid.SetRow(table, 1);
            cardGrid.Children.Add(table);

            var footer = new Border { BorderBrush = Tok.Hair, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 12, 0, 0) };
            footer.Child = new TextBlock { Text = "12 个按钮 · 表格与 Ribbon 双向同步；一次批量修改回为一步撤销。", FontSize = 11, Foreground = Tok.Faint };
            Grid.SetRow(footer, 2);
            cardGrid.Children.Add(footer);

            card.Child = cardGrid;
            body.Children.Add(card);
            Grid.SetRow(body, 1);
            page.Children.Add(body);

            rowsHost = rowsHost;
            this.tableHost = rowsHost;
            RebuildRows();
            return page;
        }

        private StackPanel tableHost;
        private readonly Grid headerGrid = new Grid();

        private FrameworkElement TableHeader()
        {
            var g = new Grid { Background = Tok.Head };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(166) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var all = new CheckBox { IsThreeState = false, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 14, Height = 14, IsChecked = false };
            all.Click += (s, e) =>
            {
                bool target = all.IsChecked == true;
                foreach (var r in rows) r.Selected = target;
                RebuildRows();
            };
            Grid.SetColumn(all, 0);
            g.Children.Add(all);
            string[] titles = { "按钮名称", "Group", "显示", "图标", "Macro", "Tooltip 标题", "Tooltip 正文" };
            for (int i = 0; i < titles.Length; i++)
            {
                var t = new TextBlock { Text = titles[i], FontSize = 11, Foreground = Tok.Hex("#6B7688"), FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 3 ? 0 : 8, 0, 8, 0) };
                if (i == 3) t.TextAlignment = TextAlignment.Center;
                Grid.SetColumn(t, i + 1);
                g.Children.Add(t);
            }
            this.headerAll = all;
            return new Border { Child = g, BorderBrush = Tok.Hex("#E6EAF2"), BorderThickness = new Thickness(0, 0, 0, 1) };
        }

        private CheckBox headerAll;

        private void CycleFilter()
        {
            var options = new[] { "全部 Group", "矢勤工具", "开发工具", "个人常用" };
            int i = Array.IndexOf(options, groupFilter);
            groupFilter = options[(i + 1) % options.Length];
            RebuildRows();
        }

        private void RebuildRows()
        {
            if (tableHost == null) return;
            tableHost.Children.Clear();
            int selected = 0, visible = 0;
            foreach (var r in rows)
            {
                if (groupFilter != "全部 Group" && r.Cmd.Group != groupFilter) continue;
                if (query.Length > 0)
                {
                    var hay = (r.Cmd.Name + " " + r.Cmd.Macro + " " + r.Cmd.TipTitle + " " + r.Cmd.TipText).ToLowerInvariant();
                    if (!hay.Contains(query.ToLowerInvariant())) continue;
                }
                visible++;
                if (r.Selected) selected++;
                tableHost.Children.Add(TableRow(r));
            }
            if (selectionLabel != null) selectionLabel.Text = "已选 " + selected + " / " + rows.Count + " 项";
            if (headerAll != null)
            {
                headerAll.IsChecked = selected == 0 ? false : (selected == rows.Count ? (bool?)true : null);
            }
        }

        private FrameworkElement TableRow(RowState r)
        {
            var g = new Grid { Height = 30, Background = r.Selected ? Tok.Soft : Tok.White };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(166) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            var box = new CheckBox { IsChecked = r.Selected, Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            box.Click += (s, e) => { r.Selected = box.IsChecked == true; RebuildRows(); };
            Grid.SetColumn(box, 0);
            g.Children.Add(box);

            var cells = new FrameworkElement[]
            {
                new TextBlock { Text = r.Cmd.Name, FontSize = 13, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) },
                new TextBlock { Text = r.Cmd.Group, FontSize = 13, Foreground = Tok.Sub, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) },
                null,
                Ico.Make(r.Cmd.Icon, 16),
                new TextBlock { Text = r.Cmd.Macro, FontSize = 12, FontFamily = new FontFamily(Tok.Mono), Foreground = Tok.Sub, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) },
                new TextBlock { Text = r.Cmd.TipTitle, FontSize = 13, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) },
                new TextBlock { Text = r.Cmd.TipText, FontSize = 13, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }
            };
            for (int i = 0; i < cells.Length; i++)
            {
                FrameworkElement el = cells[i];
                if (i == 2) el = Pill(r.Cmd.Large ? "Large" : "Small", r.Cmd.Large) is Border p ? new Grid { Children = { WithMargin(p, 8, 0, 0, 0) } } : null;
                Grid.SetColumn(el, i + 1);
                g.Children.Add(el);
            }

            var wrap = new Border { Child = g, BorderBrush = Tok.Hair, BorderThickness = new Thickness(0, 0, 0, 1) };
            g.MouseEnter += (s, e) => { if (!r.Selected) g.Background = Tok.Hex("#F7FAFF"); };
            g.MouseLeave += (s, e) => { if (!r.Selected) g.Background = Tok.White; };
            return wrap;
        }

        private static FrameworkElement WithMargin(FrameworkElement e, double l, double t, double r, double b)
        {
            e.Margin = new Thickness(l, t, r, b);
            return e;
        }

        // ---------------- batch dialog ----------------
        private void OpenBatch()
        {
            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(90, 27, 36, 48)) };
            var dlg = new Border
            {
                Width = 600, Height = 424, Background = Tok.White, CornerRadius = new CornerRadius(10),
                BorderBrush = Tok.Hex("#D8DEE9"), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var inner = new Grid { Margin = new Thickness(24) };
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition());
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock { Text = "批量设置 " + rows.Count(r => r.Selected) + " 个按钮", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Tok.Ink };
            inner.Children.Add(title);
            var close = new TextBlock { Text = "✕", FontSize = 12, Foreground = Tok.Faint, HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            close.MouseLeftButtonUp += (s, e) => host.Content = TableView();
            inner.Children.Add(close);

            var fields = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            var states = new List<CheckBox>();
            string[] names = { "Group", "显示", "图标", "Macro", "Tooltip 标题", "Tooltip 正文" };
            string[] values = { "矢勤工具", "Small", "保持不变", "show !!", "", "" };
            for (int i = 0; i < names.Length; i++)
            {
                var line = new Grid { Height = 38 };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
                line.ColumnDefinitions.Add(new ColumnDefinition());
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                var cb = new CheckBox { IsChecked = i < 2, VerticalAlignment = VerticalAlignment.Center };
                states.Add(cb);
                Grid.SetColumn(cb, 0);
                line.Children.Add(cb);
                var lb = new TextBlock { Text = names[i], FontSize = 12, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lb, 1);
                line.Children.Add(lb);
                var box = Input(values[i]);
                box.IsEnabled = i < 2;
                Grid.SetColumn(box, 2);
                line.Children.Add(box);
                var mark = new TextBlock { Text = i < 2 ? "将覆盖" : "不变", FontSize = 11, Foreground = i < 2 ? Tok.Warn : Tok.Faint, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(mark, 3);
                line.Children.Add(mark);
                fields.Children.Add(line);
            }
            Grid.SetRow(fields, 1);
            inner.Children.Add(fields);

            var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var note = new TextBlock { Text = "将写入勾选的字段；操作可一步撤销。", FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center };
            footer.Children.Add(note);
            var cancel = ActionButton("取消", false, () => host.Content = TableView(), 100);
            Grid.SetColumn(cancel, 1);
            footer.Children.Add(cancel);
            var ok = ActionButton("应用 " + rows.Count(r => r.Selected) + " 项", true, () =>
            {
                foreach (var r in rows.Where(x => x.Selected))
                {
                    if (states[0].IsChecked == true) r.Cmd.Group = "矢勤工具";
                    if (states[1].IsChecked == true) r.Cmd.Large = values[1] == "Large";
                }
                RebuildRows();
            }, 112);
            Grid.SetColumn(ok, 3);
            footer.Children.Add(ok);
            Grid.SetRow(footer, 2);
            inner.Children.Add(footer);

            dlg.Child = inner;
            overlay.Children.Add(dlg);
            host.Content = overlay;
        }

        // ---------------- workspace view ----------------
        private FrameworkElement WorkspaceView()
        {
            var page = new Grid { Background = Tok.Page, Margin = new Thickness(12) };
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            page.ColumnDefinitions.Add(new ColumnDefinition());
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(316) });

            // tree
            var treeInner = new StackPanel();
            var treeHead = new Grid { Margin = new Thickness(16, 14, 16, 10) };
            treeHead.Children.Add(new TextBlock { Text = "结构", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            treeHead.Children.Add(new TextBlock { Text = "≡", FontSize = 12, Foreground = Tok.Faint, HorizontalAlignment = HorizontalAlignment.Right });
            treeInner.Children.Add(treeHead);
            var items = new List<Tuple<string, int, bool, bool>>();
            items.Add(Tuple.Create("我的工具", 0, true, false));
            items.Add(Tuple.Create("矢勤工具", 1, true, false));
            items.Add(Tuple.Create("元件库工具", 2, false, true));
            items.Add(Tuple.Create("复制移动", 2, false, true));
            items.Add(Tuple.Create("批处理工具", 2, false, true));
            items.Add(Tuple.Create("元素导出", 2, false, true));
            items.Add(Tuple.Create("支架建模工具", 2, false, true));
            items.Add(Tuple.Create("Spco工具", 2, false, true));
            items.Add(Tuple.Create("开发工具", 1, true, false));
            items.Add(Tuple.Create("脚手架工具箱", 2, false, true));
            items.Add(Tuple.Create("MaWei工具箱", 2, false, true));
            foreach (var it in items)
            {
                var row = new Grid { Height = 28, Margin = new Thickness(8, 0, 8, 0), Background = it.Item4 ? Tok.Soft : Brushes.Transparent };
                row.CornerRadius = new CornerRadius(6);
                var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(it.Item2 * 14, 0, 0, 0) };
                if (it.Item3) p.Children.Add(Ico.Make("chevDown", 16, Tok.Faint, 1.2));
                else p.Children.Add(new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Tok.Accent, Margin = new Thickness(5, 0, 5, 0) });
                p.Children.Add(new TextBlock { Text = it.Item1, FontSize = 12, Foreground = it.Item4 ? Tok.Accent : Tok.Body, FontWeight = it.Item3 ? FontWeights.Medium : FontWeights.Normal, Margin = new Thickness(6, 0, 0, 0) });
                row.Children.Add(p);
                treeInner.Children.Add(row);
            }
            treeInner.Children.Add(new Border { Height = 1, Background = Tok.Hair, Margin = new Thickness(8, 10, 8, 10) });
            treeInner.Children.Add(new TextBlock { Text = "12 个按钮 · 3 个分组", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(16, 0, 0, 0) });
            var tree = Card(treeInner, new Thickness(0));
            page.Children.Add(tree);

            // canvas
            var canvasInner = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            canvasInner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            canvasInner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
            canvasInner.RowDefinitions.Add(new RowDefinition());
            var canvasLabel = new TextBlock { Text = "预览 · 我的工具 / 矢勤工具", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 0, 0, 10) };
            canvasInner.Children.Add(canvasLabel);

            var mini = new Border { Background = Tok.Hex("#FBFCFE"), CornerRadius = new CornerRadius(6), BorderBrush = Tok.CardLine, BorderThickness = new Thickness(1) };
            var miniGrid = new Grid();
            miniGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            miniGrid.RowDefinitions.Add(new RowDefinition());
            var miniTabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            miniTabs.Children.Add(new TextBlock { Text = "我的工具", FontSize = 11, Foreground = Tok.Accent, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            miniTabs.Children.Add(new TextBlock { Text = "开发工具", FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center });
            miniGrid.Children.Add(miniTabs);
            var miniRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 6, 0, 0) };
            foreach (var cmd in rows.Take(4))
            {
                var cell = new Border { Width = 128, Height = 48, CornerRadius = new CornerRadius(6), BorderBrush = Tok.CardLine, BorderThickness = new Thickness(1), Background = Tok.White, Margin = new Thickness(0, 0, 12, 0) };
                var cellStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                cellStack.Children.Add(Ico.Make(cmd.Cmd.Icon, 16));
                var txt = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                txt.Children.Add(new TextBlock { Text = cmd.Cmd.Name, FontSize = 11, Foreground = Tok.Body });
                txt.Children.Add(new TextBlock { Text = "Small · 16px", FontSize = 10, Foreground = Tok.Faint });
                cellStack.Children.Add(txt);
                cell.Child = cellStack;
                miniRow.Children.Add(cell);
            }
            Grid.SetRow(miniRow, 1);
            miniGrid.Children.Add(miniRow);
            mini.Child = miniGrid;
            Grid.SetRow(mini, 1);
            canvasInner.Children.Add(mini);

            var doc = new Border { Background = Tok.Hex("#FCFDFE"), BorderBrush = Tok.Hex("#EDF0F6"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 16, 0, 0) };
            var docStack = new StackPanel { Margin = new Thickness(16) };
            docStack.Children.Add(new TextBlock { Text = "AVEVA 中显示效果", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Tok.Sub });
            docStack.Children.Add(new TextBlock { Text = "Ribbon 由 Tab → Group → 按钮三级构成；此处预览最终写入 UIC 后，在 AVEVA 菜单页中的呈现顺序与分组边界。", FontSize = 11, Foreground = Tok.Faint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            docStack.Children.Add(new Border { Height = 1, Background = Tok.Hair, Margin = new Thickness(0, 14, 0, 12) });
            docStack.Children.Add(new TextBlock { Text = "当前选中：元件库工具（矢勤工具 / 我的工具）", FontSize = 11, Foreground = Tok.Muted });
            docStack.Children.Add(new TextBlock { Text = "Macro  show !!SeqendCatalogManager", FontSize = 11, Foreground = Tok.Faint, FontFamily = new FontFamily(Tok.Mono), Margin = new Thickness(0, 6, 0, 0) });
            docStack.Children.Add(new TextBlock { Text = "Tooltip  元件库 — 打开元件管理工具", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 6, 0, 0) });
            doc.Child = docStack;
            Grid.SetRow(doc, 2);
            canvasInner.Children.Add(doc);

            var canvasCard = new Border { Background = Tok.Hex("#EDF0F6"), CornerRadius = new CornerRadius(8), Child = canvasInner };
            Grid.SetColumn(canvasCard, 2);
            page.Children.Add(canvasCard);

            // inspector
            var insp = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
            insp.Children.Add(new TextBlock { Text = "属性 · 元件库工具", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            insp.Children.Add(new Border { Height = 1, Background = Tok.Hair, Margin = new Thickness(0, 12, 0, 14) });
            insp.Children.Add(Label("名称"));
            insp.Children.Add(Input("元件库工具"));
            insp.Children.Add(new Border { Height = 12 });
            insp.Children.Add(Label("显示大小"));
            var seg = new Grid { Height = 30, Background = Tok.PillGray, Margin = new Thickness(0, 0, 0, 0) };
            seg.ColumnDefinitions.Add(new ColumnDefinition());
            seg.ColumnDefinitions.Add(new ColumnDefinition());
            var largeSeg = new Border { Margin = new Thickness(3), CornerRadius = new CornerRadius(5), Background = Tok.White, BorderBrush = Tok.FieldLine, BorderThickness = new Thickness(1), Child = new TextBlock { Text = "Large", FontSize = 11, Foreground = Tok.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var smallSeg = new Border { Margin = new Thickness(3), CornerRadius = new CornerRadius(5), Background = Tok.Accent, Child = new TextBlock { Text = "Small", FontSize = 11, Foreground = Tok.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Grid.SetColumn(smallSeg, 1);
            seg.Children.Add(largeSeg);
            seg.Children.Add(smallSeg);
            insp.Children.Add(seg);
            insp.Children.Add(new Border { Height = 14 });
            insp.Children.Add(Label("Macro"));
            insp.Children.Add(Input("show !!SeqendCatalogManager", true));
            insp.Children.Add(new Border { Height = 14 });
            insp.Children.Add(Label("Tooltip 标题"));
            insp.Children.Add(Input("元件库"));
            insp.Children.Add(new Border { Height = 14 });
            insp.Children.Add(Label("Tooltip 正文"));
            var tip = new TextBox { Text = "打开元件管理工具", Height = 56, FontSize = 11, Background = Tok.Field, BorderBrush = Tok.FieldLine, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 0), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            insp.Children.Add(tip);
            insp.Children.Add(new Border { Height = 14 });
            insp.Children.Add(Label("图标"));
            var iconRow = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBox = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Tok.Field, BorderBrush = Tok.FieldLine, BorderThickness = new Thickness(1), Child = Ico.Make("open", 32) };
            iconRow.Children.Add(iconBox);
            var iconInfo = new StackPanel { Margin = new Thickness(12, 6, 0, 0) };
            iconInfo.Children.Add(new TextBlock { Text = "32 / 16 自动生成", FontSize = 11, Foreground = Tok.Muted });
            iconInfo.Children.Add(new TextBlock { Text = "点击替换或拖入图片", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 6, 0, 0) });
            iconRow.Children.Add(iconInfo);
            insp.Children.Add(iconRow);

            var actions = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            var reset = ActionButton("重置", false, () => { });
            Grid.SetColumn(reset, 0);
            actions.Children.Add(reset);
            var apply = ActionButton("应用修改", true, () => { });
            Grid.SetColumn(apply, 2);
            actions.Children.Add(apply);
            insp.Children.Add(actions);

            var inspCard = Card(insp, new Thickness(0));
            Grid.SetColumn(inspCard, 4);
            page.Children.Add(inspCard);
            return page;
        }

        // ---------------- deploy view ----------------
        private FrameworkElement DeployView()
        {
            var page = new Grid { Background = Tok.Page };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            var head = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };
            head.Children.Add(new TextBlock { Text = "部署 UIC", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            head.Children.Add(new TextBlock { Text = "核对目标文件与模块关联，确认后备份并写入；只在确认时改动实际 UIC。", FontSize = 13, Foreground = Tok.Muted, Margin = new Thickness(0, 6, 0, 0) });
            page.Children.Add(head);

            var body = new Grid { Margin = new Thickness(18, 0, 18, 14) };
            body.ColumnDefinitions.Add(new ColumnDefinition());
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(510) });

            var left = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
            left.Children.Add(CardHeader("目标文件", null));
            left.Children.Add(new Border { Height = 10 });
            var target = new Border { Background = Tok.Field, BorderBrush = Tok.FieldLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8) };
            var ts = new StackPanel();
            ts.Children.Add(new TextBlock { Text = @"C:\AVEVA\PMLLIB\Other\UIC\YtoolsAM.uic", FontSize = 12, FontFamily = new FontFamily(Tok.Mono), Foreground = Tok.Sub });
            ts.Children.Add(new TextBlock { Text = "3 个 Tab · 3 个 Group · 12 个按钮 · 上次部署 2026-10-27 15:04", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 4, 0, 0) });
            target.Child = ts;
            left.Children.Add(target);
            left.Children.Add(new Border { Height = 16 });
            left.Children.Add(CardHeader("模块关联", "同一 UIC 可被多个模块引用；已有引用只读显示。"));
            var modHead = new Grid { Height = 26, Background = Tok.Head, Margin = new Thickness(0, 12, 0, 0) };
            string[] mh = { "模块", "来源", "状态", "引用路径" };
            double[] mw = { 220, 180, 140, 0 };
            for (int i = 0; i < mh.Length; i++)
                modHead.Children.Add(new TextBlock { Text = mh[i], FontSize = 11, Foreground = Tok.Hex("#6B7688"), FontWeight = FontWeights.Medium, Margin = new Thickness(i == 0 ? 8 : mw.Take(i).Sum() + 8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(modHead);
            foreach (var m in new[] { new object[] { "mon.exe", "官方", "已关联", @"ui\YtoolsAM.xml" }, new object[] { "dabacon.exe", "官方", "已关联", @"ui\YtoolsAM.xml" }, new object[] { "user.xml", "自定义", "本次新增", @"ui\YtoolsAM.xml" } })
            {
                var rg = new Grid { Height = 40 };
                rg.Children.Add(new TextBlock { Text = (string)m[0], FontSize = 12, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                rg.Children.Add(new TextBlock { Text = (string)m[1], FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(228, 0, 0, 0) });
                var isNew = (string)m[2] == "本次新增";
                var pill = new Border { Background = isNew ? Tok.PillAmber : Tok.PillGreen, CornerRadius = new CornerRadius(10), Height = 20, Width = 72, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(408, 0, 0, 0), Child = new TextBlock { Text = (string)m[2], FontSize = 11, Foreground = isNew ? Tok.Warn : Tok.Ok, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                rg.Children.Add(pill);
                rg.Children.Add(new TextBlock { Text = (string)m[3], FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(540, 0, 0, 0) });
                left.Children.Add(new Border { Child = rg, BorderBrush = Tok.Hair, BorderThickness = new Thickness(0, 0, 0, 1) });
            }
            left.Children.Add(new TextBlock { Text = "其余 4 个模块已记录在加载登记中，可在「加载登记」页接管或移除。", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 12, 0, 0) });
            left.Children.Add(new TextBlock { Text = "写入步骤", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Tok.Ink, Margin = new Thickness(0, 18, 0, 8) });
            var steps = new[] { new[] { "1", "备份 YtoolsAM.uic 为 .bak", "保留最近 5 份" }, new[] { "2", "写入 12 个按钮与 3 个分组", "按当前编辑结果" }, new[] { "3", "更新模块引用登记", "仅新增本次选择的模块" } };
            for (int i = 0; i < steps.Length; i++)
            {
                var sg = new Grid { Height = 30 };
                var num = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Tok.Chip, Child = new TextBlock { Text = steps[i][0], FontSize = 10, Foreground = Tok.Accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, VerticalAlignment = VerticalAlignment.Center };
                sg.Children.Add(num);
                sg.Children.Add(new TextBlock { Text = steps[i][1], FontSize = 11, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28, 0, 0, 0) });
                sg.Children.Add(new TextBlock { Text = steps[i][2], FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right });
                left.Children.Add(sg);
            }
            left.Children.Add(new TextBlock { Text = "写入期间保留切换与关闭保护；失败不修改原文件。", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 12, 0, 0) });
            body.Children.Add(Card(left, new Thickness(0)));

            var right = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
            right.Children.Add(CardHeader("本次写入摘要", null));
            foreach (var s in new[] { new object[] { "新增模块引用", "1", Tok.Ok }, new object[] { "更新按钮", "12", Tok.Accent }, new object[] { "保持不变", "4", Tok.Muted } })
            {
                var rg = new Grid { Height = 44 };
                rg.Children.Add(new TextBlock { Text = (string)s[0], FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center });
                rg.Children.Add(new TextBlock { Text = (string)s[1], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)s[2], VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right });
                right.Children.Add(rg);
            }
            right.Children.Add(new Border { Height = 1, Background = Tok.Hair, Margin = new Thickness(0, 8, 0, 12) });
            right.Children.Add(new TextBlock { Text = "写入前备份原文件", FontSize = 12, Foreground = Tok.Body });
            var toggle = new Border { Width = 36, Height = 20, CornerRadius = new CornerRadius(10), Background = Tok.Accent, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            toggle.Child = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Tok.White, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0) };
            right.Children.Add(toggle);
            right.Children.Add(new TextBlock { Text = "备份写入同目录 .bak，保留最近 5 份。", FontSize = 11, Foreground = Tok.Faint, Margin = new Thickness(0, 6, 0, 0) });
            right.Children.Add(new Border { Height = 1, Background = Tok.Hair, Margin = new Thickness(0, 14, 0, 12) });
            right.Children.Add(new TextBlock { Text = "最近部署", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            foreach (var h in new[] { new[] { "2026-10-27 15:04", "mon.exe · 成功" }, new[] { "2026-10-24 09:12", "dabacon.exe · 成功" } })
            {
                var hg = new Grid { Height = 22, Margin = new Thickness(0, 6, 0, 0) };
                hg.Children.Add(new TextBlock { Text = h[0], FontSize = 11, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center });
                hg.Children.Add(new TextBlock { Text = h[1], FontSize = 11, Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right });
                right.Children.Add(hg);
            }
            var buttons = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var preview = ActionButton("仅预览差异", false, () => { });
            Grid.SetColumn(preview, 0);
            buttons.Children.Add(preview);
            var write = ActionButton("备份并写入 UIC", true, () => { });
            Grid.SetColumn(write, 2);
            buttons.Children.Add(write);
            right.Children.Add(buttons);
            var rightCard = Card(right, new Thickness(0));
            Grid.SetColumn(rightCard, 2);
            body.Children.Add(rightCard);

            Grid.SetRow(body, 1);
            page.Children.Add(body);
            return page;
        }

        // ---------------- registry view ----------------
        private FrameworkElement RegistryView()
        {
            var page = new Grid { Background = Tok.Page };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            var head = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };
            head.Children.Add(new TextBlock { Text = "加载登记", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Tok.Ink });
            head.Children.Add(new TextBlock { Text = "记录哪些模块引用了这个 UIC；接管后可在此移除或恢复加载。", FontSize = 13, Foreground = Tok.Muted, Margin = new Thickness(0, 6, 0, 0) });
            page.Children.Add(head);

            var body = new Grid { Margin = new Thickness(18, 0, 18, 14) };
            var inner = new StackPanel { Margin = new Thickness(16) };
            var filters = new StackPanel { Orientation = Orientation.Horizontal };
            string[] chips = { "全部 7", "官方 4", "自定义 2", "已接管 1" };
            for (int i = 0; i < chips.Length; i++)
            {
                var c = Chip(chips[i], i == 0 ? Tok.Soft : Tok.White, i == 0 ? Tok.Accent : Tok.Muted, chips[i].Length > 3 ? 86 : 70);
                c.BorderBrush = i == 0 ? Tok.Hex("#D6E2FA") : Tok.FieldLine;
                c.BorderThickness = new Thickness(1);
                c.Margin = new Thickness(0, 0, 8, 0);
                c.Cursor = Cursors.Hand;
                filters.Children.Add(c);
            }
            inner.Children.Add(filters);

            var header = new Grid { Height = 26, Background = Tok.Head, Margin = new Thickness(0, 14, 0, 0) };
            double[] xs = { 8, 196, 320, 430, 730, 1100 };
            string[] hs = { "模块", "来源", "权限", "引用路径", "最近加载", "操作" };
            for (int i = 0; i < hs.Length; i++)
                header.Children.Add(new TextBlock { Text = hs[i], FontSize = 11, Foreground = Tok.Hex("#6B7688"), FontWeight = FontWeights.Medium, Margin = new Thickness(xs[i], 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            inner.Children.Add(header);

            var regs = new[]
            {
                new object[]{"mon.exe","官方","只读",@"ui\YtoolsAM.xml","2026-10-27 15:04","查看"},
                new object[]{"dabacon.exe","官方","只读",@"ui\YtoolsAM.xml","2026-10-24 09:12","查看"},
                new object[]{"user.xml","自定义","已接管",@"ui\YtoolsAM.xml","2026-10-20 18:30","移除"},
                new object[]{@"ui\custom.xml","自定义","只读",@"ui\custom.xml","2026-10-11 11:02","接管"},
                new object[]{"mon.exe (备份)","官方","只读",@"ui\YtoolsAM.bak.xml","2026-09-30 08:41","查看"}
            };
            int idx = 0;
            foreach (var r in regs)
            {
                var rg = new Grid { Height = 34, Background = idx % 2 == 1 ? Tok.Hex("#FAFBFD") : Tok.White };
                rg.Children.Add(new TextBlock { Text = (string)r[0], FontSize = 12, Foreground = Tok.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(xs[0], 0, 0, 0) });
                rg.Children.Add(new TextBlock { Text = (string)r[1], FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(xs[1], 0, 0, 0) });
                bool owned = (string)r[2] == "已接管";
                var pill = new Border { Background = owned ? Tok.PillBlue : Tok.PillGray, CornerRadius = new CornerRadius(10), Height = 20, Width = 64, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(xs[2], 0, 0, 0), Child = new TextBlock { Text = (string)r[2], FontSize = 11, Foreground = owned ? Tok.Accent : Tok.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                rg.Children.Add(pill);
                rg.Children.Add(new TextBlock { Text = (string)r[3], FontSize = 11, FontFamily = new FontFamily(Tok.Mono), Foreground = Tok.Faint, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(xs[3], 0, 0, 0) });
                rg.Children.Add(new TextBlock { Text = (string)r[4], FontSize = 12, Foreground = Tok.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(xs[4], 0, 0, 0) });
                rg.Children.Add(new TextBlock { Text = (string)r[5], FontSize = 12, Foreground = Tok.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(xs[5], 0, 0, 0), Cursor = Cursors.Hand });
                inner.Children.Add(new Border { Child = rg, BorderBrush = Tok.Hair, BorderThickness = new Thickness(0, 0, 0, 1) });
                idx++;
            }
            inner.Children.Add(new Border { Height = 1, Background = Tok.Hex("#F0F3F8"), Margin = new Thickness(0, 10, 0, 8) });
            inner.Children.Add(new TextBlock { Text = "接管只改变登记归属，不影响 UIC 文件与其他模块的引用。", FontSize = 11, Foreground = Tok.Faint });
            body.Children.Add(Card(inner, new Thickness(0)));
            Grid.SetRow(body, 1);
            page.Children.Add(body);
            return page;
        }

        // ---------------- offscreen shots ----------------
        private static void Shoot(string dir)
        {
            Directory.CreateDirectory(dir);
            var app = new Application();
            foreach (var v in new[] { "table", "workspace", "deploy", "registry" })
            {
                var w = new Prototype { WindowStyle = WindowStyle.None, Width = 1280, Height = 840 };
                w.ShowView(v);
                w.Measure(new Size(1280, 840));
                w.Arrange(new Rect(0, 0, 1280, 840));
                w.UpdateLayout();
                var content = (FrameworkElement)w.Content;
                content.Measure(new Size(1280, 840));
                content.Arrange(new Rect(0, 0, 1280, 840));
                content.UpdateLayout();
                var bmp = new RenderTargetBitmap(1280, 840, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(content);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(System.IO.Path.Combine(dir, v + ".png"))) enc.Save(fs);
                Console.WriteLine("shot " + v);
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--shot") { Shoot(args[1]); return 0; }
            var app = new Application();
            app.Run(new Prototype());
            return 0;
        }
    }
}
