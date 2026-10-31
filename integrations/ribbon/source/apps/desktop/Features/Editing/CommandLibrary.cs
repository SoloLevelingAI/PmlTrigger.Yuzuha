using System;
using System.IO;
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
        sealed class CommandLibraryState
        {
            public string PrimaryId, Query = "";
            public bool Unplaced;
            public HashSet<string> Marked = new HashSet<string>();
            public Dictionary<string, Dictionary<string,string>> Drafts = new Dictionary<string, Dictionary<string,string>>();
            public double ScrollOffset;
        }
        sealed class CommandLibraryRow
        {
            public RibbonButton Button;
            public CommandLibraryState State;
            public int References { get; set; }
            public string Caption { get { return Button.caption; } }
            public string Macro { get { return Button.macro; } }
            public string Tooltip { get { return String.IsNullOrEmpty(Button.tooltipTitle) ? Button.tooltipText : Button.tooltipTitle + " · " + Button.tooltipText; } }
            public string Placement { get { return References == 0 ? "未放置" : "已放置"; } }
            public ImageSource Icon { get { return Ui.Image(Button.image); } }
            public bool Marked { get { return State.Marked.Contains(Button.id); } set { if (value) State.Marked.Add(Button.id); else State.Marked.Remove(Button.id); } }
        }

        readonly Dictionary<Project, CommandLibraryState> commandLibraryStates = new Dictionary<Project, CommandLibraryState>();
        DataGrid commandLibraryGrid;
        StackPanel commandLibraryDetails;
        TextBox commandLibrarySearch;
        CheckBox commandLibraryUnplaced;
        TextBlock commandLibrarySummary;
        Grid commandLibraryLayout;
        Expander commandLibraryDrawer;
        Border commandLibraryDetailCard;
        bool commandLibraryRendering;

        CommandLibraryState LibraryState()
        {
            if (Project == null) return null;
            CommandLibraryState state;
            if (!commandLibraryStates.TryGetValue(Project, out state))
            {
                state = new CommandLibraryState();
                commandLibraryStates[Project] = state;
            }
            return state;
        }

        FrameworkElement BuildCommandLibraryView()
        {
            var root = new DockPanel { Margin = new Thickness(18, 12, 18, 10) };
            var heading = new StackPanel();
            heading.Children.Add(Ui.Text("功能库", 20));
            heading.Children.Add(Ui.Text("维护功能定义，再将功能放入 Ribbon；同一功能的所有引用共用 Macro、图标和提示。", 12, Ui.Muted));
            var tools = new WrapPanel { Margin = new Thickness(-4, 6, 0, 6) };
            tools.Children.Add(Ui.Button("新建功能", () => Guard(() => { AddLibraryCommand(); return Task.FromResult(0); }), true));
            tools.Children.Add(Ui.Button("批量修改", () => Guard(EditLibraryBatch)));
            tools.Children.Add(Ui.Button("导入 UIC", () => Guard(Import)));
            tools.Children.Add(Ui.Button("放入 Ribbon…", () => Guard(() => ChooseCommandPlacement(CommandLibrarySelection().Select(b => b.id)))));
            heading.Children.Add(tools);
            var filters = new WrapPanel();
            commandLibrarySearch = Ui.Input(); commandLibrarySearch.Width = 245; commandLibrarySearch.ToolTip = "按功能名称、Macro 或 Tooltip 筛选";
            commandLibrarySearch.TextChanged += (s, e) => { if (commandLibraryRendering || Project == null) return; LibraryState().Query = commandLibrarySearch.Text; RenderCommandLibrary(); };
            filters.Children.Add(commandLibrarySearch);
            commandLibraryUnplaced = new CheckBox { Content = "仅未放置", Margin = new Thickness(16, 12, 12, 0) };
            commandLibraryUnplaced.Checked += (s,e) => SetLibraryUnplaced(true);
            commandLibraryUnplaced.Unchecked += (s,e) => SetLibraryUnplaced(false);
            filters.Children.Add(commandLibraryUnplaced);
            filters.Children.Add(Ui.Sync("勾选当前结果", () => { if (Project == null) return; foreach (var row in commandLibraryGrid.Items.OfType<CommandLibraryRow>()) row.Marked = true; commandLibraryGrid.Items.Refresh(); }));
            filters.Children.Add(Ui.Sync("清除勾选", () => { if (Project == null) return; LibraryState().Marked.Clear(); commandLibraryGrid.Items.Refresh(); }));
            heading.Children.Add(filters);
            commandLibrarySummary = Ui.Text("", 12, Ui.Muted); heading.Children.Add(commandLibrarySummary);
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            commandLibraryLayout = new Grid();
            commandLibraryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            commandLibraryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            commandLibraryLayout.RowDefinitions.Add(new RowDefinition());
            commandLibraryLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            commandLibraryGrid = Ui.Grid("功能名称|Caption", "Macro|Macro", "Tooltip|Tooltip", "使用处|References", "状态|Placement");
            commandLibraryGrid.Name = "CommandLibraryGrid"; commandLibraryGrid.FontSize = 13;
            commandLibraryGrid.MinColumnWidth = 45; commandLibraryGrid.RowHeight = 42;
            var check = new FrameworkElementFactory(typeof(CheckBox)); check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("Marked") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            commandLibraryGrid.Columns.Insert(0, new DataGridTemplateColumn { Header = "勾选", CellTemplate = new DataTemplate { VisualTree = check }, Width = 48 });
            commandLibraryGrid.Columns[1].Width = new DataGridLength(1.05, DataGridLengthUnitType.Star);
            commandLibraryGrid.Columns[2].Width = new DataGridLength(1.5, DataGridLengthUnitType.Star);
            commandLibraryGrid.Columns[3].Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            commandLibraryGrid.Columns[4].Width = 58; commandLibraryGrid.Columns[5].Width = 65;
            commandLibraryGrid.SelectionChanged += (s,e) => {
                if (commandLibraryRendering || Project == null) return;
                var row = commandLibraryGrid.SelectedItem as CommandLibraryRow;
                if (row == null) return;
                LibraryState().PrimaryId = row.Button.id;
                Selected = new Selection { Kind = "button", Button = row.Button };
                RenderLibraryDetails(row.Button);
            };
            commandLibraryGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s,e) => { if (!commandLibraryRendering && Project != null && e.VerticalChange != 0) LibraryState().ScrollOffset = e.VerticalOffset; }));
            commandLibraryLayout.Children.Add(Ui.TableCard(commandLibraryGrid));
            commandLibraryDetails = new StackPanel { Margin = new Thickness(14, 4, 14, 8) };
            commandLibraryDetailCard = new Border { Background = Brushes.White, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(16, 0, 0, 0), Child = Ui.Scroll(commandLibraryDetails) };
            Grid.SetColumn(commandLibraryDetailCard, 1); commandLibraryLayout.Children.Add(commandLibraryDetailCard);
            commandLibraryDrawer = new Expander { Header = "功能详情（展开编辑）", Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            Grid.SetRow(commandLibraryDrawer, 1); Grid.SetColumnSpan(commandLibraryDrawer, 2); commandLibraryLayout.Children.Add(commandLibraryDrawer);
            root.Children.Add(commandLibraryLayout);
            root.SizeChanged += (s,e) => AdaptCommandLibrary(e.NewSize.Width);
            return root;
        }

        void AdaptCommandLibrary(double width)
        {
            bool narrow = width < 960;
            bool inDrawer = commandLibraryDrawer.Content != null;
            if (narrow == inDrawer) return;
            if (narrow)
            {
                var content = commandLibraryDetailCard.Child; commandLibraryDetailCard.Child = null;
                commandLibraryDrawer.Content = content; commandLibraryDrawer.MaxHeight = 260;
                commandLibraryDetailCard.Visibility = Visibility.Collapsed; commandLibraryDrawer.Visibility = Visibility.Visible;
                commandLibraryLayout.ColumnDefinitions[1].Width = new GridLength(0);
            }
            else
            {
                var content = commandLibraryDrawer.Content as UIElement; commandLibraryDrawer.Content = null;
                commandLibraryDetailCard.Child = content; commandLibraryDetailCard.Visibility = Visibility.Visible;
                commandLibraryDrawer.Visibility = Visibility.Collapsed; commandLibraryLayout.ColumnDefinitions[1].Width = new GridLength(330);
            }
        }

        void SetLibraryUnplaced(bool value)
        {
            if (commandLibraryRendering || Project == null) return;
            LibraryState().Unplaced = value; RenderCommandLibrary();
        }

        void RenderCommandLibrary()
        {
            if (commandLibraryGrid == null || Project == null) return;
            var state = LibraryState();
            commandLibraryRendering = true;
            try
            {
                state.Marked.IntersectWith(Project.buttons.Select(b => b.id));
                var counts = Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).GroupBy(i => i.buttonId).ToDictionary(g => g.Key, g => g.Count());
                var definitions = Project.buttons.GroupBy(b => b.id).Select(g => g.First()).ToList();
                if (!definitions.Any(b => b.id == state.PrimaryId)) state.PrimaryId = definitions.Select(b => b.id).FirstOrDefault();
                string query = state.Query ?? "";
                var rows = definitions.Select(b => new CommandLibraryRow { Button = b, State = state, References = counts.ContainsKey(b.id) ? counts[b.id] : 0 })
                    .Where(r => (!state.Unplaced || r.References == 0) && (query.Length == 0 || ((r.Caption ?? "") + " " + (r.Macro ?? "") + " " + (r.Tooltip ?? "")).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                commandLibrarySearch.Text = query; commandLibraryUnplaced.IsChecked = state.Unplaced;
                commandLibraryGrid.ItemsSource = rows;
                commandLibraryGrid.SelectedItem = rows.FirstOrDefault(r => r.Button.id == state.PrimaryId);
                commandLibrarySummary.Text = definitions.Count + " 个功能 · " + definitions.Count(b => !counts.ContainsKey(b.id)) + " 个未放置 · 当前显示 " + rows.Count + " 个；行选择用于详情，复选框用于批量操作。";
                var primary = definitions.FirstOrDefault(b => b.id == state.PrimaryId);
                RenderLibraryDetails(primary);
                var scroll = VisualChild<ScrollViewer>(commandLibraryGrid); if (scroll != null) scroll.ScrollToVerticalOffset(state.ScrollOffset);
            }
            finally { commandLibraryRendering = false; }
        }

        IList<RibbonButton> CommandLibrarySelection()
        {
            if (Project == null) return new List<RibbonButton>();
            var state = LibraryState();
            var marked = Project.buttons.Where(b => state.Marked.Contains(b.id)).GroupBy(b => b.id).Select(g => g.First()).ToList();
            if (marked.Count > 0) return marked;
            return Project.buttons.Where(b => b.id == state.PrimaryId).Take(1).ToList();
        }

        public void SelectLibraryCommand(string id)
        {
            if (Project == null) return;
            var button = Project.buttons.FirstOrDefault(b => b.id == id);
            if (button == null) return;
            var state = LibraryState(); state.PrimaryId = id; state.Query = ""; state.Unplaced = false;
            Selected = new Selection { Kind = "button", Button = button };
            RenderCommandLibrary();
            var row = commandLibraryGrid == null ? null : commandLibraryGrid.Items.OfType<CommandLibraryRow>().FirstOrDefault(r => r.Button.id == id);
            if (row != null) commandLibraryGrid.ScrollIntoView(row);
        }

        RibbonButton AddLibraryCommand()
        {
            if (Project == null || !IsDocumentView) return null;
            var button = new RibbonButton { id = Id("Button"), commandKey = "Macro:" + Guid.NewGuid().ToString(), caption = "新功能", macro = "", tooltipTitle = "", tooltipText = "" };
            RunEdit("新建未放置功能", () => { InsertEdit(Project.buttons, Project.buttons.Count, button); Selected = new Selection { Kind = "button", Button = button }; });
            LibraryState().PrimaryId = button.id; LibraryState().Unplaced = false; LibraryState().Query = "";
            RenderAll(); return button;
        }

        static string LibraryFieldValue(RibbonButton button, string field)
        {
            switch (field)
            {
                case "caption": return button.caption;
                case "macro": return button.macro;
                case "tooltipTitle": return button.tooltipTitle;
                case "tooltipText": return button.tooltipText;
                default: throw new ArgumentException("未知功能字段：" + field);
            }
        }

        List<Tuple<RibbonButton, Dictionary<string,string>>> PendingLibraryDrafts(Project project)
        {
            var pending = new List<Tuple<RibbonButton, Dictionary<string,string>>>();
            CommandLibraryState state;
            if (project == null || !commandLibraryStates.TryGetValue(project, out state)) return pending;
            foreach (var button in project.buttons.GroupBy(b => b.id).Select(g => g.First()))
            {
                Dictionary<string,string> draft;
                if (!state.Drafts.TryGetValue(button.id, out draft)) continue;
                var changed = draft.Where(p => !String.Equals(LibraryFieldValue(button, p.Key) ?? "", p.Value ?? "", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
                if (changed.Count > 0) pending.Add(Tuple.Create(button, changed));
            }
            return pending;
        }

        public bool HasPendingLibraryDrafts(Project project)
        {
            return PendingLibraryDrafts(project).Count > 0;
        }

        public Task ApplyPendingLibraryDrafts(Project project)
        {
            var pending = PendingLibraryDrafts(project);
            if (pending.Count == 0) return Task.FromResult(0);
            if (Project != project || !IsDocumentView || AnyToolWorking)
                throw new InvalidOperationException("请回到该 UIC 的功能库，应用或取消待应用的详情修改后再保存或关闭。");
            // Validate every definition before entering the transaction. Invalid edits stay
            // in Drafts so save/close can be stopped without losing user input.
            foreach (var edit in pending)
                if (edit.Item2.ContainsKey("caption") && String.IsNullOrWhiteSpace(edit.Item2["caption"]))
                    throw new ArgumentException("功能名称不能为空；请修正或取消待应用的详情修改。");
            RunEdit("应用功能详情草稿 " + pending.Count + " 项", () => {
                foreach (var edit in pending)
                    foreach (var field in edit.Item2) SetLibraryField(edit.Item1, field.Key, field.Value);
            });
            var state = LibraryState();
            foreach (var edit in pending) state.Drafts.Remove(edit.Item1.id);
            RenderAll();
            return Task.FromResult(0);
        }

        void SetLibraryField(RibbonButton button, string field, string value)
        {
            switch (field)
            {
                case "caption": SetEdit(button.caption, value, v => button.caption = v); break;
                case "macro": SetEdit(button.macro, value, v => button.macro = v); break;
                case "tooltipTitle": SetEdit(button.tooltipTitle, value, v => button.tooltipTitle = v); break;
                case "tooltipText": SetEdit(button.tooltipText, value, v => button.tooltipText = v); break;
                default: throw new ArgumentException("未知功能字段：" + field);
            }
        }

        void ApplyLibraryFields(IEnumerable<RibbonButton> source, Dictionary<string,string> fields, ImageAsset image = null, bool replaceImage = false)
        {
            if (Project == null || !IsDocumentView) return;
            var buttons = source.Where(b => Project.buttons.Contains(b)).Distinct().ToList();
            if (buttons.Count == 0) return;
            if (fields.ContainsKey("caption") && String.IsNullOrWhiteSpace(fields["caption"])) throw new ArgumentException("功能名称不能为空");
            RunEdit("功能定义修改 " + buttons.Count + " 项", () => {
                foreach (var button in buttons)
                {
                    foreach (var field in fields) SetLibraryField(button, field.Key, field.Value);
                    if (replaceImage) SetEdit(button.image, image, v => button.image = v);
                }
            });
            RenderAll();
        }

        void RenderLibraryDetails(RibbonButton button)
        {
            commandLibraryDetails.Children.Clear();
            if (button == null) { commandLibraryDetails.Children.Add(Ui.Text("当前没有功能。新建功能或导入已有 UIC。", 13, Ui.Muted)); return; }
            var project = Project;
            var libraryState = LibraryState();
            commandLibraryDetails.Children.Add(Ui.Text("功能详情", 17));
            var refs = Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Count(i => i.buttonId == button.id);
            commandLibraryDetails.Children.Add(Ui.Text("修改同步到 " + refs + " 个引用；大小和位置在编排视图设置。", 12, Ui.Muted));
            commandLibraryDetails.Children.Add(Ui.Text("字段修改后点击应用；未应用内容仅暂存，保存或关闭前需应用或取消。", 12, Ui.Muted));
            var values = new Dictionary<string,TextBox>();
            foreach (var field in new[] { "caption|名称", "macro|Macro", "tooltipTitle|Tooltip 标题", "tooltipText|Tooltip 正文" })
            {
                var parts = field.Split('|'); commandLibraryDetails.Children.Add(Ui.Text(parts[1], 12, Ui.Muted));
                var text = parts[0] == "caption" ? button.caption : parts[0] == "macro" ? button.macro : parts[0] == "tooltipTitle" ? button.tooltipTitle : button.tooltipText;
                Dictionary<string,string> draft;
                if (libraryState.Drafts.TryGetValue(button.id, out draft) && draft.ContainsKey(parts[0])) text = draft[parts[0]];
                var box = Ui.Input(text, parts[0] == "macro" || parts[0] == "tooltipText"); box.Tag = "command-library:" + parts[0]; values[parts[0]] = box; commandLibraryDetails.Children.Add(box);
                string fieldKey = parts[0];
                box.TextChanged += (s,e) => {
                    Dictionary<string,string> edits;
                    if (!libraryState.Drafts.TryGetValue(button.id, out edits)) { edits = new Dictionary<string,string>(); libraryState.Drafts[button.id] = edits; }
                    edits[fieldKey] = box.Text;
                    status.Text = HasPendingLibraryDrafts(project) ? "功能详情尚未应用；请应用或取消后再保存、关闭或部署" : "功能详情与已应用内容一致";
                };
            }
            commandLibraryDetails.Children.Add(Ui.Button("应用定义修改", () => Guard(() => {
                if (Project != project || !Project.buttons.Contains(button)) return Task.FromResult(0);
                ApplyLibraryFields(new[] { button }, values.ToDictionary(p => p.Key, p => p.Value.Text)); libraryState.Drafts.Remove(button.id); return Task.FromResult(0);
            }), true));
            commandLibraryDetails.Children.Add(Ui.Sync("取消待应用修改", () => { libraryState.Drafts.Remove(button.id); if (Project == project) RenderCommandLibrary(); }));
            var preview = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            var source = Ui.Image(button.image);
            if (source != null) { preview.Children.Add(new Image { Source = source, Width = 32, Height = 32, Margin = new Thickness(4) }); preview.Children.Add(new Image { Source = source, Width = 16, Height = 16, Margin = new Thickness(10, 4, 4, 4) }); }
            else preview.Children.Add(Ui.Text("尚未设置图标", 12, Ui.Muted));
            commandLibraryDetails.Children.Add(preview);
            commandLibraryDetails.Children.Add(Ui.Button("选择图标…", () => Guard(async () => { Selected = new Selection { Kind = "button", Button = button }; await Upload(); RenderCommandLibrary(); })));
            commandLibraryDetails.Children.Add(Ui.Button("移除图标", () => Guard(() => { if (Project == project) ApplyLibraryFields(new[] { button }, new Dictionary<string,string>(), null, true); return Task.FromResult(0); })));
            commandLibraryDetails.Children.Add(Ui.Button("查看使用位置（" + refs + "）", () => Guard(() => ShowCommandLocations(button.id))));
            commandLibraryDetails.Children.Add(Ui.Button("删除功能…", () => Guard(() => DeleteLibraryCommand(button))));
        }

        Task DeleteLibraryCommand(RibbonButton button)
        {
            if (Project == null || !Project.buttons.Contains(button)) return Task.FromResult(0);
            var refs = Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Count(i => i.buttonId == button.id);
            if (refs > 0) { status.Text = "功能仍有 " + refs + " 个引用；请先查看使用位置并移除引用，再删除功能。"; return Task.FromResult(0); }
            if (!Ui.Confirm("删除未放置功能“" + button.caption + "”？可使用撤销恢复。")) return Task.FromResult(0);
            RunEdit("删除未放置功能", () => { RemoveEdit(Project.buttons, button); Selected = new Selection { Kind = "project" }; });
            LibraryState().Marked.Remove(button.id); LibraryState().Drafts.Remove(button.id); RenderAll(); return Task.FromResult(0);
        }

        async Task EditLibraryBatch()
        {
            if (Project == null) return;
            // Batch editing deliberately requires checked rows; primary selection is only for details.
            var buttons = Project.buttons.Where(b => LibraryState().Marked.Contains(b.id)).Distinct().ToList();
            if (buttons.Count == 0) { status.Text = "请先勾选需要批量修改的功能。"; return; }
            var project = Project;
            var dialog = Ui.Dialog("批量修改功能定义 · " + buttons.Count + " 项", this, 690, 680);
            var panel = new StackPanel { Margin = new Thickness(20) }; var dock = new DockPanel(); dialog.Content = dock;
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) };
            DockPanel.SetDock(actions, Dock.Bottom); dock.Children.Add(actions); dock.Children.Add(Ui.Scroll(panel));
            panel.Children.Add(Ui.Text("仅修改勾选字段；所有引用同步更新，一次应用记为一步撤销。", 13, Ui.Muted));
            var checks = new Dictionary<string,CheckBox>(); var values = new Dictionary<string,TextBox>();
            foreach (var field in new[] { "caption|名称", "macro|Macro", "tooltipTitle|Tooltip 标题", "tooltipText|Tooltip 正文" })
            {
                var parts = field.Split('|'); var check = new CheckBox { Content = parts[1], Margin = new Thickness(0, 6, 0, 0) };
                var input = Ui.Input("", parts[0] == "macro" || parts[0] == "tooltipText"); input.IsEnabled = false;
                check.Checked += (s,e) => input.IsEnabled = true; check.Unchecked += (s,e) => input.IsEnabled = false;
                checks[parts[0]] = check; values[parts[0]] = input; panel.Children.Add(check); panel.Children.Add(input);
            }
            ImageAsset image = null; bool replaceImage = false; var note = Ui.Text("图标保持原值", 12, Ui.Muted); panel.Children.Add(note);
            panel.Children.Add(Ui.Button("替换所选功能的图标…", async () => {
                var path = Ui.Open("图片|*.png;*.jpg;*.jpeg"); if (path == null) return;
                var bytes = File.ReadAllBytes(path); if (bytes.Length > 12 * 1024 * 1024) throw new ArgumentException("图片不能超过 12 MiB");
                var result = await Api.Post<PreparedImage>("images/prepare", new { image = new ImageAsset { mediaType = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg", base64 = Convert.ToBase64String(bytes) } });
                if (Project != project) return; image = result.image; replaceImage = true; note.Text = "将替换为：" + Path.GetFileName(path);
            }));
            panel.Children.Add(Ui.Sync("移除所选功能图标", () => { image = null; replaceImage = true; note.Text = "将移除所选功能图标"; }));
            actions.Children.Add(Ui.Sync("取消", () => dialog.DialogResult = false));
            actions.Children.Add(Ui.Button("应用（一步撤销）", () => {
                if (Project != project) { dialog.DialogResult = false; return Task.FromResult(0); }
                ApplyLibraryFields(buttons, values.Where(p => checks[p.Key].IsChecked == true).ToDictionary(p => p.Key, p => p.Value.Text), image, replaceImage);
                dialog.DialogResult = true; return Task.FromResult(0);
            }, true));
            Chrome.Install(dialog); dialog.ShowDialog(); await Task.FromResult(0);
        }
    }
}
