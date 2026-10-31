using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public void RenderAll()
        {
            loading = true;
            try
            {
                RenderTree();
                RenderPreview();
                RenderInspector();
                RenderFunctionTable();
                RenderCommandLibrary();
                RenderCommandPicker();
                ApplyCommandView();
            }
            finally { loading = false; }
        }
        void RenderTree()
        {
            if (Project == null)
                return;
            var buttonIndex = Project.buttons.GroupBy(b => b.id).ToDictionary(g => g.Key, g => g.First());
            bool before = loading;
            loading = true;
            tree.Items.Clear();
            tree.Items.Add(Node("项目 · " + Project.@namespace, new Selection { Kind = "project" }));
            foreach (var tab in Project.tabs)
            {
                var node = Node(tab.caption, new Selection { Kind = "tab", Tab = tab });
                tree.Items.Add(node);
                foreach (var group in tab.groups)
                {
                    var groupNode = Node(group.caption, new Selection { Kind = "group", Tab = tab, Group = group });
                    node.Items.Add(groupNode);
                    foreach (var item in group.items)
                    {
                        RibbonButton button;
                        if (buttonIndex.TryGetValue(item.buttonId, out button))
                            groupNode.Items.Add(Node((item.size == "large" ? "▣  " : "▪  ") + button.caption, new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button }));
                    }
                }
            }
            var referenced = new HashSet<string>(Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Select(i => i.buttonId));
            var unused = Project.buttons.Where(b => !referenced.Contains(b.id)).ToList();
            if (unused.Count > 0)
            {
                var folder = new TreeViewItem { Header = "未放置按钮（" + unused.Count + "）", IsExpanded = true };
                tree.Items.Add(folder);
                foreach (var b in unused)
                    folder.Items.Add(Node(b.caption, new Selection { Kind = "button", Button = b }));
            }
            tree.ContextMenu = SelectionMenu(new Selection { Kind = "project" });
            loading = before;
        }
        TreeViewItem Node(string label, Selection selection)
        {
            UIElement header;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (selection.Button != null)
            {
                var image = Ui.Image(selection.Button.image);
                row.Children.Add(image == null ? (UIElement)Ui.Icon("copy", 16) : new Image { Source = image, Width = 16, Height = 16 });
                var caption = Ui.Text(selection.Button.caption, 11);
                caption.Margin = new Thickness(8, 0, 4, 0);
                row.Children.Add(caption);
                header = row;
            }
            else
                header = Ui.Text(label, 11);
            var node = new TreeViewItem { Header = header, Tag = selection, IsExpanded = true, Padding = new Thickness(3, 6, 3, 6) };
            node.ContextMenu = SelectionMenu(selection);
            WireDrag(node, selection, false, true);
            node.PreviewMouseRightButtonDown += (s, e) => { if (Ui.Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject) == node) node.IsSelected = true; };
            if (Selected.Kind == selection.Kind && Selected.Tab == selection.Tab && Selected.Group == selection.Group && Selected.Button == selection.Button && Selected.Item == selection.Item)
                node.IsSelected = true;
            return node;
        }
        void RenderPreview()
        {
            if (Project == null)
                return;
            var buttonIndex = Project.buttons.GroupBy(b => b.id).ToDictionary(g => g.Key, g => g.First());
            title.Text = Project.@namespace + "  /  " + (Project.origin == null ? "新项目" : Project.origin.fileName);
            summary.Text = Project.tabs.Count + " 个 Tab · " + Project.tabs.Sum(t => t.groups.Count) + " 个 Group · " + Project.buttons.Count + " 个按钮定义";
            ribbonPreview.Children.Clear();
            if (activePreviewTab == null || !Project.tabs.Contains(activePreviewTab))
                activePreviewTab = Selected.Tab ?? Project.tabs.FirstOrDefault();
            previewTabs.Margin = new Thickness(0,0,0,6); previewTabs.Children.Clear();
            ribbonPreview.Children.Add(previewTabs);
            var tabStrip = previewTabs;
            foreach (var t in Project.tabs)
            {
                var scope = new Selection { Kind = "tab", Tab = t };
                var tabButton = Ui.Sync(t.caption, () => { if (DateTime.UtcNow < suppressPreviewClick) return; activePreviewTab = t; Selected = scope; RenderAll(); });
                tabButton.DataContext = scope; tabButton.Margin = new Thickness(0); tabButton.MinHeight = 0; tabButton.Height = 34; tabButton.Padding = new Thickness(12,6,12,6);
                tabButton.Style = (Style)Application.Current.FindResource("RibbonTab");
                tabButton.Tag = t == activePreviewTab ? "Selected" : null;
                tabButton.ContextMenu = SelectionMenu(scope);
                WireDrag(tabButton, scope, true);
                tabStrip.Children.Add(tabButton);
            }
            foreach (var tab in Project.tabs.Where(t => t == activePreviewTab))
            {
                var groups = new WrapPanel();
                ribbonPreview.Children.Add(groups);
                foreach (var group in tab.groups)
                {
                    var groupScope = new Selection { Kind = "group", Tab = tab, Group = group };
                    var body = new DockPanel { Margin = new Thickness(4) };
                    var caption = Ui.Text(group.caption, 10, Ui.Muted);
                    caption.TextAlignment = TextAlignment.Center;
                    caption.Tag = group;
                    DockPanel.SetDock(caption, Dock.Bottom);
                    body.Children.Add(caption);
                    var items = new WrapPanel { MinHeight = 85, MaxWidth = 500 };
                    body.Children.Add(items);
                    StackPanel small = null;
                    int smallCount = 0;
                    foreach (var item in group.items)
                    {
                        RibbonButton b;
                        if (!buttonIndex.TryGetValue(item.buttonId, out b))
                            continue;
                        var selection = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = b };
                        var click = Ui.Sync("", () => { if (DateTime.UtcNow < suppressPreviewClick) return; Selected = selection; RenderTree(); RenderInspector(); RenderPreview(); });
                        click.DataContext = selection;
                        click.Style = (Style)Application.Current.FindResource("RibbonTool");
                        click.Tag = Selected.Item == item ? "Selected" : null;
                        click.ContextMenu = SelectionMenu(selection);
                        WireDrag(click, selection, item.size == "large");
                        click.ToolTip = (b.tooltipTitle ?? "") + "\n" + (b.tooltipText ?? "");
                        click.BorderThickness = new Thickness(1);
                        click.Margin = new Thickness(1);
                        click.HorizontalContentAlignment = item.size == "large" ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                        var content = new StackPanel { Orientation = item.size == "large" ? Orientation.Vertical : Orientation.Horizontal, HorizontalAlignment = item.size == "large" ? HorizontalAlignment.Center : HorizontalAlignment.Left };
                        var image = Ui.Image(b.image);
                        if (image != null)
                            content.Children.Add(new Image { Source = image, Width = item.size == "large" ? 32 : 16, Height = item.size == "large" ? 32 : 16, Margin = item.size == "large" ? new Thickness(3) : new Thickness(0) });
                        else
                            content.Children.Add(Ui.Icon("copy", item.size == "large" ? 32 : 16));
                        var buttonCaption = Ui.Text(b.caption, 11);
                        if (item.size == "small")
                        {
                            buttonCaption.Margin = new Thickness(6, 0, 0, 0);
                            buttonCaption.VerticalAlignment = VerticalAlignment.Center;
                            buttonCaption.TextAlignment = TextAlignment.Left;
                        }
                        content.Children.Add(buttonCaption);
                        click.Content = content;
                        if (item.size == "large")
                        {
                            click.MinHeight = 75;
                            items.Children.Add(click);
                            small = null;
                            smallCount = 0;
                        }
                        else
                        {
                            if (small == null || smallCount == 3)
                            {
                                small = new StackPanel();
                                items.Children.Add(small);
                                smallCount = 0;
                            }
                            click.MinHeight = 24;
                            click.Padding = new Thickness(5, 1, 5, 1);
                            small.Children.Add(click);
                            smallCount++;
                        }
                    }
                    var groupBorder = new Border { Child = body, DataContext = groupScope, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0), Margin = new Thickness(0, 6, 6, 8), MinWidth = 95, MinHeight = 95, Background = Brushes.Transparent };
                    groupBorder.ContextMenu = SelectionMenu(groupScope);
                    WireDrag(groupBorder, groupScope, true);
                    caption.MouseLeftButtonUp += (s, e) => { if (DateTime.UtcNow < suppressPreviewClick) return; Selected = groupScope; RenderTree(); RenderInspector(); };
                    groups.Children.Add(groupBorder);
                }
            }
            projectOverview.Children.Clear();
            projectOverview.Margin = new Thickness(0, 8, 0, 2);
            SyncFunctionSelection();
            foreach (var row in functionRows.Where(r => r.Scope.Button == Selected.Button)) row.Refresh();
        }
        void Field(string label, string value, Action<string> update, bool multiline = false, StackPanel target = null)
        {
            target = target ?? inspector;
            var fieldLabel = Ui.Text(label, 10, Ui.Muted);
            fieldLabel.Margin = new Thickness(0, 3, 0, 3);
            target.Children.Add(fieldLabel);
            var box = Ui.Input(value, multiline);
            box.Margin = new Thickness(0, 2, 0, 6);
            if (multiline)
                box.MinHeight = 48;
            box.Tag = label;
            System.Windows.Automation.AutomationProperties.SetName(box, label);
            box.TextChanged += (s, e) => { if (!loading) { var next = box.Text; var previous = value; RunEdit("修改 " + label, () => SetEdit(previous, next, update)); value = next; RefreshTextViews(); } };
            target.Children.Add(box);
        }
        void RenderInspector()
        {
            bool before = loading;
            loading = true;
            inspector.Children.Clear();
            if (Project == null)
            {
                loading = before;
                return;
            }
            inspector.Children.Add(Ui.Text(Selected.Kind == "button" ? "按钮设置" : Selected.Kind == "group" ? "Group 设置" : Selected.Kind == "tab" ? "菜单页设置" : "项目设置", 17));
            if (Selected.Kind == "project")
            {
                Field("Namespace", Project.@namespace, value => Project.@namespace = value);
                inspector.Children.Add(Ui.Text("同一后端保存草稿、压缩图标、生成 UIC，并管理模块登记与部署备份。", 11, Ui.Muted));
            }
            if (Selected.Kind == "tab")
            {
                var tab = Selected.Tab;
                Field("显示名称", tab.caption, value => tab.caption = value);
                Field("唯一标识", tab.id, value => tab.id = value);
            }
            if (Selected.Kind == "group")
            {
                var group = Selected.Group;
                Field("显示名称", group.caption, value => group.caption = value);
                Field("唯一标识", group.id, value => group.id = value);
            }
            if (Selected.Kind == "button")
            {
                var b = Selected.Button;

                Field("按钮名称", b.caption, value => b.caption = value);
                var idReferences = Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Where(i => i.buttonId == b.id).ToList();
                Field("唯一标识", b.id, value => { b.id = value; foreach (var reference in idReferences) reference.buttonId = value; });
                var image = Ui.Image(b.image);
                var imageRow = new WrapPanel();
                imageRow.Children.Add(Ui.Button("选择图片", Upload));
                imageRow.Children.Add(Ui.Sync("移除图标", () => { RemoveButtonImage(b); RenderTree(); RenderInspector(); RenderPreview(); }));
                inspector.Children.Add(imageRow);
                inspector.Children.Add(Ui.Text("图片由后端自动生成大 / 小图标", 10, Ui.Muted));
                if (Selected.Item != null)
                {
                    var sizes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
                    foreach (var size in new[] { "large", "small" })
                    {
                        var chosenSize = size;
                        var toggle = new System.Windows.Controls.Primitives.ToggleButton { Width = 108, Height = 58, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 7, 0), IsChecked = Selected.Item.size == size, ToolTip = size == "large" ? "32 × 32 大图标" : "16 × 16 小图标" };
                        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        int pixels = size == "large" ? 32 : 16;
                        var icon = image == null ? (FrameworkElement)Ui.Icon("copy", pixels) : new Image { Source = image, Width = pixels, Height = pixels };
                        content.Children.Add(new Grid { Height = 34, Children = { icon } });
                        var label = Ui.Text(size == "large" ? "大图标" : "小图标", 10);
                        label.Margin = new Thickness(0); label.TextAlignment = TextAlignment.Center;
                        content.Children.Add(label);
                        toggle.Content = content;
                        toggle.Click += async (s, e) => { await SetItemSize(chosenSize); };
                        sizes.Children.Add(toggle);
                    }
                    inspector.Children.Add(sizes);
                }
                inspector.Children.Add(Ui.Text("所属 Group（当前引用）", 10, Ui.Muted));
                var destinations = Ui.Combo();
                var choices = new List<Tuple<RibbonTab, RibbonGroup>>();
                foreach (var t in Project.tabs)
                    foreach (var g in t.groups)
                    {
                        choices.Add(Tuple.Create(t, g));
                        destinations.Items.Add(t.caption + " / " + g.caption);
                    }
                destinations.SelectedIndex = choices.FindIndex(pair => pair.Item2 == Selected.Group);
                destinations.SelectionChanged += (s, e) => { if (loading || destinations.SelectedIndex < 0) return; var pair = choices[destinations.SelectedIndex]; MoveButtonTo(pair.Item1, pair.Item2); };
                destinations.Margin = new Thickness(0,2,0,4); inspector.Children.Add(destinations);
                inspector.Children.Add(Ui.Text("Macro / Tooltip 在本 UIC 的 " + CommandReferenceLocations(b.id).Count + " 处引用共享", 9, Ui.Muted));
                Field("Macro", b.macro, value => b.macro = value, true);
                Field("Tooltip 标题", b.tooltipTitle, value => b.tooltipTitle = value);
                Field("Tooltip 正文", b.tooltipText, value => b.tooltipText = value, true);
                var advanced = new StackPanel();
                Field("命令 Key", b.commandKey, value => b.commandKey = value, false, advanced);
                inspector.Children.Add(new Expander { Header = "高级：命令 Key", Content = advanced });
            }
            loading = before;
        }
        public Task Add(string kind)
        {
            if (Project == null)
                return Task.FromResult(0);
            RunEdit("添加 " + kind, () => { var tab = Selected.Tab ?? Project.tabs.FirstOrDefault(); var group = Selected.Group ?? (tab == null ? null : tab.groups.FirstOrDefault()); if (kind == "tab") { tab = new RibbonTab { id = Id("Tab"), caption = "新选项卡", groups = new List<RibbonGroup>() }; InsertEdit(Project.tabs, Project.tabs.Count, tab); Selected = new Selection { Kind = "tab", Tab = tab }; } else if (kind == "group") { if (tab == null) throw new Exception("请先添加 Tab"); group = new RibbonGroup { id = Id("Group"), caption = "新分组", items = new List<RibbonItem>() }; InsertEdit(tab.groups, tab.groups.Count, group); Selected = new Selection { Kind = "group", Tab = tab, Group = group }; } else { if (group == null) throw new Exception("请先添加 Group"); var button = new RibbonButton { id = Id("Button"), caption = "新按钮", macro = "show !!MyForm", commandKey = "", tooltipTitle = "", tooltipText = "" }; var item = new RibbonItem { buttonId = button.id, size = "large" }; InsertEdit(Project.buttons, Project.buttons.Count, button); InsertEdit(group.items, group.items.Count, item); Selected = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button }; } });
            activePreviewTab = Selected.Tab;
            RenderAll();
            return Task.FromResult(0);
        }
        static string Id(string kind)
        {
            return "Custom." + kind + "." + Guid.NewGuid().ToString("N");
        }
        public Task Move(int offset)
        {
            if (Project == null)
                return Task.FromResult(0);
            RunEdit("排序", () => { if (Selected.Kind == "tab") ShiftEdit(Project.tabs, Selected.Tab, offset); else if (Selected.Kind == "group") ShiftEdit(Selected.Tab.groups, Selected.Group, offset); else if (Selected.Item != null) ShiftEdit(Selected.Group.items, Selected.Item, offset); });
            RenderAll();
            return Task.FromResult(0);
        }
        void ShiftEdit<T>(List<T> list, T item, int offset)
        {
            int index = list.IndexOf(item), next = index + offset;
            if (index >= 0 && next >= 0 && next < list.Count)
                PositionEdit(list, item, next + (offset > 0 ? 1 : 0));
        }
        async Task Delete()
        {
            if (Project == null || Selected.Kind == "project")
                return;
            if (!Ui.Confirm("移除所选布局？功能定义将保留在功能库，可以 Ctrl+Z 撤销。实际 UIC 需确认部署才会变化。"))
                return;
            DeleteSelection();
            await Task.FromResult(0);
        }
        void DeleteSelection()
        {
            RunEdit("移除布局 " + Caption(Selected), () => { if (Selected.Kind == "tab") RemoveEdit(Project.tabs, Selected.Tab); else if (Selected.Kind == "group") RemoveEdit(Selected.Tab.groups, Selected.Group); else if (Selected.Group != null && Selected.Item != null) RemoveEdit(Selected.Group.items, Selected.Item); Selected = new Selection { Kind = "project" }; });
            RenderAll();
        }


        void RefreshTextViews()
        {
            foreach (var node in TreeNodes(tree.Items))
            {
                var selection = node.Tag as Selection;
                if (selection == null)
                    continue;
                string caption = selection.Kind == "project" ? "项目 · " + Project.@namespace : Caption(selection);
                var label = node.Header as TextBlock;
                if (label == null && node.Header is StackPanel)
                    label = ((StackPanel)node.Header).Children.OfType<TextBlock>().FirstOrDefault();
                if (label != null)
                    label.Text = caption;
            }
            foreach (var button in PreviewButtons(ribbonPreview).Concat(previewTabs.Children.OfType<Button>()))
            {
                var selection = button.DataContext as Selection;
                if (selection == null)
                    continue;
                if (selection.Kind == "tab")
                    button.Content = selection.Tab.caption;
                else if (selection.Button != null)
                {
                    var panel = button.Content as StackPanel;
                    if (panel != null)
                    {
                        var label = panel.Children.OfType<TextBlock>().FirstOrDefault();
                        if (label != null)
                            label.Text = selection.Button.caption;
                    }
                    button.ToolTip = (selection.Button.tooltipTitle ?? "") + "\n" + (selection.Button.tooltipText ?? "");
                }
            }
            foreach (var label in PreviewText(ribbonPreview))
            {
                var group = label.Tag as RibbonGroup;
                if (group != null)
                    label.Text = group.caption;
            }
            title.Text = Project.@namespace + " / " + (Project.origin == null ? "新项目" : Project.origin.fileName);
            foreach (var row in functionRows) row.Refresh();
        }
        static IEnumerable<TextBlock> PreviewText(DependencyObject element)
        {
            var text = element as TextBlock;
            if (text != null)
                yield return text;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                foreach (var child in PreviewText(VisualTreeHelper.GetChild(element, i)))
                    yield return child;
        }

    }
}
