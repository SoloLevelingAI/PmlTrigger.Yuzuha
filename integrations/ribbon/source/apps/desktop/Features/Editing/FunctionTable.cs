using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        sealed class FunctionRow : System.ComponentModel.INotifyPropertyChanged
        {
            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            public void Refresh() { var changed = PropertyChanged; if (changed != null) changed(this, new System.ComponentModel.PropertyChangedEventArgs(null)); }
            public MainWindow Main; public Selection Scope;
            object Key { get { return (object)Scope.Item ?? Scope.Button; } }
            public bool Marked { get { return Main.activeDocument.TableMarked.Contains(Key); } set { if (value) Main.activeDocument.TableMarked.Add(Key); else Main.activeDocument.TableMarked.Remove(Key); } }
            public string Caption { get { return Scope.Button.caption; } set { Set("caption", value); } }
            public string Group { get { return Scope.Group == null ? "未放置" : Scope.Tab.caption + " / " + Scope.Group.caption; } }
            public string Size { get { return Scope.Item == null ? "未放置" : Scope.Item.size; } set { if (Scope.Item != null && new[] { "large", "small" }.Contains((value ?? "").ToLowerInvariant())) Main.ApplyFunctionBatch(new[] { this }, new Dictionary<string, string>(), null, value.ToLowerInvariant(), null, false); } }
            public string Macro { get { return Scope.Button.macro; } set { Set("macro", value); } }
            public string TooltipTitle { get { return Scope.Button.tooltipTitle; } set { Set("tooltipTitle", value); } }
            public string TooltipText { get { return Scope.Button.tooltipText; } set { Set("tooltipText", value); } }
            public ImageSource Icon { get { return Ui.Image(Scope.Button.image); } }
            public string Tooltip { get { return (Scope.Button.tooltipTitle ?? "") + "\n" + (Scope.Button.tooltipText ?? ""); } }
            void Set(string field, string value) { Main.ApplyFunctionBatch(new[] { this }, new Dictionary<string, string> { { field, value } }, null, null, null, false); }
        }
        FrameworkElement documentCommands; Grid designLayout; DockPanel expandedFunctionHost; DataGrid compactFunctions, expandedFunctions; bool tableRendering, tableRefreshPending;
        List<FunctionRow> functionRows = new List<FunctionRow>();
        void BuildFunctionTables(Panel preview)
        {
            compactFunctions = FunctionGrid(false); expandedFunctions = FunctionGrid(true);
            compactFunctionHost = new DockPanel();
            var actions = new DockPanel { LastChildFill = true, Margin = new Thickness(6, 0, 6, 2) };
            var bulk = Ui.Button("批量设置 ▾", EditFunctionBatch); DockPanel.SetDock(bulk, Dock.Right); actions.Children.Add(bulk);
            var expand = Ui.Sync("详细列 ↗", () => SetFunctionExpanded(activeDocument == null || !activeDocument.TableExpanded)); DockPanel.SetDock(expand, Dock.Right); actions.Children.Add(expand);
            var searchLabel = Ui.Text("搜索", 10, Ui.Muted); searchLabel.VerticalAlignment = VerticalAlignment.Center; searchLabel.Margin = new Thickness(4,0,6,0); DockPanel.SetDock(searchLabel,Dock.Left); actions.Children.Add(searchLabel);
            localFunctionSearch = Ui.Input(); localFunctionSearch.ToolTip = "查找当前 UIC 的名称、Group、Macro 与 Tooltip";
            localFunctionSearch.Margin = new Thickness(4); localFunctionSearch.TextChanged += (s,e) => { if (!tableRendering && activeDocument != null) { activeDocument.FunctionQuery = localFunctionSearch.Text; FilterFunctions(); } }; actions.Children.Add(localFunctionSearch);
            comparisonActions = actions;
            comparisonContent = new DockPanel();
            var tables = new Grid(); compactFunctionHost.Children.Add(Ui.TableCard(compactFunctions)); tables.Children.Add(compactFunctionHost);
            expandedFunctionHost = new DockPanel { Visibility = Visibility.Collapsed }; expandedFunctionHost.Children.Add(Ui.TableCard(expandedFunctions)); tables.Children.Add(expandedFunctionHost); comparisonContent.Children.Add(tables);
            BuildBottomDock(preview);
        }
        void FilterFunctions()
        {
            string query = activeDocument == null ? "" : (activeDocument.FunctionQuery ?? "").Trim();
            foreach (var grid in new[] { compactFunctions, expandedFunctions }) {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource); if (view == null) continue;
                view.Filter = item => { var row = item as FunctionRow; return row != null && (query.Length == 0 || String.Join(" ", new[] { row.Caption, row.Group, row.Macro, row.Tooltip }).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0); };
            }
        }
        DataGrid FunctionGrid(bool full)
        {
            var grid = Ui.Grid(); grid.IsReadOnly = false; grid.RowHeight = full ? 36 : 38; grid.EnableRowVirtualization = true; grid.EnableColumnVirtualization = true;
            var rowStyle = new Style(typeof(DataGridRow), (Style)Application.Current.FindResource(typeof(DataGridRow))); rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("Tooltip"))); grid.RowStyle = rowStyle;
            var check = new FrameworkElementFactory(typeof(CheckBox)); check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("Marked") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            grid.Columns.Add(new DataGridTemplateColumn { Header = "选", CellTemplate = new DataTemplate { VisualTree = check }, Width = 34 });
            foreach (var column in new[] { "按钮|Caption|124", "Group|Group|135", "显示|Size|65", "图标|Icon|54", "Macro|Macro|250", "Tooltip 标题|TooltipTitle|135", "Tooltip 正文|TooltipText|230" })
            {
                var parts = column.Split('|');
                if (parts[1] == "Icon")
                {
                    var image = new FrameworkElementFactory(typeof(Image)); image.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Icon")); image.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("Tooltip")); image.SetValue(FrameworkElement.WidthProperty, 24.0); image.SetValue(FrameworkElement.HeightProperty, 24.0); image.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    grid.Columns.Add(new DataGridTemplateColumn { Header = "图标", CellTemplate = new DataTemplate { VisualTree = image }, Width = 54, IsReadOnly = true });
                }
                else { var textStyle = new Style(typeof(TextBlock)); textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis)); textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center)); grid.Columns.Add(new DataGridTextColumn { Header = parts[0], ElementStyle = textStyle, Binding = new System.Windows.Data.Binding(parts[1]) { Mode = parts[1] == "Group" ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnExceptions = true }, IsReadOnly = parts[1] == "Group", Width = Double.Parse(parts[2]) }); }
            }
            if (!full)
            {
                grid.Columns[0].Width = 38; grid.Columns[1] = CompactTextColumn("按钮 / Group", "Caption", "Group", 140);
                grid.Columns[2].Visibility = Visibility.Collapsed; grid.Columns[3].Width = 48; grid.Columns[4].Width = 42; grid.Columns[5].Width = 190;
                grid.Columns[6].Visibility = Visibility.Collapsed; grid.Columns[7] = CompactTextColumn("Tooltip", "TooltipTitle", "TooltipText", 145);
            }
            grid.SelectionChanged += (s, e) => { if (tableRendering || !IsDocumentView) return; var row = grid.SelectedItem as FunctionRow; if (row == null) return; Selected = row.Scope; activePreviewTab = Selected.Tab ?? activePreviewTab; RenderTree(); RenderInspector(); RenderPreview(); };
            var menu = new ContextMenu(); var edit = new MenuItem { Header = "批量设置已勾选项…" }; edit.Click += async (s, e) => await EditFunctionBatch(); menu.Items.Add(edit);
            var select = new MenuItem { Header = "勾选筛选结果" }; select.Click += (s, e) => { foreach (var row in grid.Items.OfType<FunctionRow>()) row.Marked = true; grid.Items.Refresh(); }; menu.Items.Add(select);
            var clear = new MenuItem { Header = "取消全部勾选" }; clear.Click += (s, e) => { activeDocument.TableMarked.Clear(); grid.Items.Refresh(); }; menu.Items.Add(clear); grid.ContextMenu = menu;
            return grid;
        }
        DataGridTemplateColumn CompactTextColumn(string header, string first, string second, double width)
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            foreach (var property in new[] { first, second }) { var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(property)); text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis); text.SetValue(TextBlock.FontSizeProperty,property == first ? 11.0 : 9.0); if (property != first) text.SetValue(TextBlock.ForegroundProperty,Ui.Muted); panel.AppendChild(text); }
            return new DataGridTemplateColumn { Header=header, Width=width, IsReadOnly=true, CellTemplate=new DataTemplate { VisualTree=panel } };
        }
        void SetFunctionExpanded(bool full)
        {
            if (activeDocument == null) return; activeDocument.TableExpanded = full;
            RenderFunctionTable(); ApplyCommandView(); SelectComparisonDock();
            SetDockHeight(full ? 390 : 294);
        }
        void RenderFunctionTable()
        {
            if (compactFunctions == null || activeDocument == null || Project == null) return;
            tableRendering = true;
            try
            {
                var index = Project.buttons.GroupBy(b => b.id).ToDictionary(g => g.Key, g => g.First()); functionRows.Clear();
                foreach (var tab in Project.tabs) foreach (var group in tab.groups) foreach (var item in group.items) { RibbonButton button; if (index.TryGetValue(item.buttonId, out button)) functionRows.Add(new FunctionRow { Main = this, Scope = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button } }); }
                var used = new HashSet<RibbonButton>(functionRows.Select(r => r.Scope.Button));
                functionRows.AddRange(Project.buttons.Where(b => !used.Contains(b)).Select(b => new FunctionRow { Main = this, Scope = new Selection { Kind = "button", Button = b } }));
                foreach (var grid in new[] { compactFunctions, expandedFunctions }) { grid.ItemsSource = null; grid.ItemsSource = functionRows.ToList(); grid.SelectedItem = functionRows.FirstOrDefault(r => Selected.Item != null ? r.Scope.Item == Selected.Item : r.Scope.Button == Selected.Button); }
                localFunctionSearch.Text = activeDocument.FunctionQuery ?? ""; FilterFunctions();
            }
            finally { tableRendering = false; ApplyCommandView(); }
        }
        void SyncFunctionSelection()
        {
            if (tableRendering || compactFunctions == null) return;
            tableRendering = true;
            try { var row = functionRows.FirstOrDefault(r => Selected.Item != null ? r.Scope.Item == Selected.Item : r.Scope.Button == Selected.Button); compactFunctions.SelectedItem = row; expandedFunctions.SelectedItem = row; }
            finally { tableRendering = false; }
        }
        void ApplyFunctionBatch(IEnumerable<FunctionRow> input, Dictionary<string, string> fields, RibbonGroup destination, string size, ImageAsset image, bool replaceImage)
        {
            var rows = input.ToList(); if (rows.Count == 0 || !IsDocumentView) return;
            if (fields.Any(f => (f.Key == "caption" || f.Key == "macro") && String.IsNullOrWhiteSpace(f.Value))) throw new ArgumentException("按钮名称和 Macro 不能为空");
            RunEdit("表格修改 " + rows.Count + " 项", () => {
                foreach (var button in rows.Select(r => r.Scope.Button).Distinct())
                {
                    foreach (var field in fields) switch (field.Key) { case "caption": SetEdit(button.caption, field.Value, v => button.caption = v); break; case "macro": SetEdit(button.macro, field.Value, v => button.macro = v); break; case "tooltipTitle": SetEdit(button.tooltipTitle, field.Value, v => button.tooltipTitle = v); break; case "tooltipText": SetEdit(button.tooltipText, field.Value, v => button.tooltipText = v); break; }
                    if (replaceImage) SetEdit(button.image, image, v => button.image = v);
                }
                foreach (var row in rows)
                {
                    var scope = row.Scope; if (scope.Item != null && size != null) SetEdit(scope.Item.size, size, v => scope.Item.size = v);
                    if (destination != null && scope.Group != destination) { if (scope.Item != null) { RemoveEdit(scope.Group.items, scope.Item); InsertEdit(destination.items, destination.items.Count, scope.Item); } else InsertEdit(destination.items, destination.items.Count, new RibbonItem { buttonId = scope.Button.id, size = size ?? "small" }); }
                }
                if (destination != null && rows.Any(r => r.Scope.Button == Selected.Button)) { var item = destination.items.FirstOrDefault(i => i.buttonId == Selected.Button.id); Selected = new Selection { Kind = "button", Tab = Project.tabs.First(t => t.groups.Contains(destination)), Group = destination, Item = item, Button = Selected.Button }; activePreviewTab = Selected.Tab; }
            });
            if (!tableRefreshPending) { tableRefreshPending = true; var document = activeDocument; Dispatcher.BeginInvoke(new Action(() => { tableRefreshPending = false; if (activeDocument == document && IsDocumentView) RenderAll(); })); }
        }
        async Task EditFunctionBatch()
        {
            var rows = functionRows.Where(r => r.Marked).ToList();
            if (rows.Count == 0) { var row = (activeDocument.TableExpanded ? expandedFunctions : compactFunctions).SelectedItem as FunctionRow; if (row != null) rows.Add(row); }
            if (rows.Count == 0) return;
            var dialog = Ui.Dialog("批量修改 · " + rows.Count + " 项", this, 690, 640); dialog.MinHeight = 520; var panel = new StackPanel { Margin = new Thickness(20) }; var body = new DockPanel(); dialog.Content = body;
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) }; DockPanel.SetDock(footer, Dock.Bottom); body.Children.Add(footer); body.Children.Add(Ui.Scroll(panel));
            panel.Children.Add(Ui.Text("勾选需要修改的字段；未勾选的字段保持不变。", 12, Ui.Muted));
            var values = new Dictionary<string, TextBox>(); var checks = new Dictionary<string, CheckBox>();
            foreach (var field in new[] { "caption|按钮名称", "macro|Macro", "tooltipTitle|Tooltip 标题", "tooltipText|Tooltip 正文" }) { var a = field.Split('|'); var check = new CheckBox { Content = a[1], Margin = new Thickness(0, 5, 0, 2) }; var box = Ui.Input("", a[0] == "macro" || a[0] == "tooltipText"); box.IsEnabled = false; check.Checked += (s,e) => box.IsEnabled = true; check.Unchecked += (s,e) => box.IsEnabled = false; checks[a[0]] = check; values[a[0]] = box; panel.Children.Add(check); panel.Children.Add(box); }
            var groupCheck = new CheckBox { Content = "移动到 Group" }; panel.Children.Add(groupCheck); var groups = Project.tabs.SelectMany(t => t.groups.Select(g => Tuple.Create(t, g))).ToList(); var group = Ui.Combo(); group.ItemsSource = groups.Select(p => p.Item1.caption + " / " + p.Item2.caption).ToList(); group.SelectedIndex = 0; panel.Children.Add(group);
            var sizeCheck = new CheckBox { Content = "显示大小" }; panel.Children.Add(sizeCheck); var sizes = Ui.Combo(); sizes.ItemsSource = new[] { "large", "small" }; sizes.SelectedIndex = 1; panel.Children.Add(sizes);
            ImageAsset replacement = null; bool changeImage = false; var imageNote = Ui.Text("图标不修改；悬停图标显示 Tooltip", 11, Ui.Muted); panel.Children.Add(imageNote);
            panel.Children.Add(Ui.Button("为所选项替换图标…", async () => { string path = Ui.Open("图片|*.png;*.jpg;*.jpeg"); if (path == null) return; replacement = (await Api.Post<PreparedImage>("images/prepare", new { image = new ImageAsset { mediaType = System.IO.Path.GetExtension(path).ToLower() == ".png" ? "image/png" : "image/jpeg", base64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(path)) } })).image; changeImage = true; imageNote.Text = "将替换为：" + System.IO.Path.GetFileName(path); }));
            panel.Children.Add(Ui.Sync("移除所选图标", () => { replacement = null; changeImage = true; imageNote.Text = "将移除所选项图标"; }));
            footer.Children.Add(Ui.Sync("取消", () => dialog.DialogResult = false)); footer.Children.Add(Ui.Sync("应用（一步撤销）", () => { var fields = values.Where(p => checks[p.Key].IsChecked == true).ToDictionary(p => p.Key, p => p.Value.Text); ApplyFunctionBatch(rows, fields, groupCheck.IsChecked == true && group.SelectedIndex >= 0 ? groups[group.SelectedIndex].Item2 : null, sizeCheck.IsChecked == true ? Convert.ToString(sizes.SelectedItem) : null, replacement, changeImage); dialog.DialogResult = true; }));
            Chrome.Install(dialog); dialog.ShowDialog(); await Task.FromResult(0);
        }
    }
}
