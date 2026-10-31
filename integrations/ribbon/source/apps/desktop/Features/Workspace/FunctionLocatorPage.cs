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
    public sealed class FunctionLocatorRow
    {
        internal Project Project;
        internal RibbonItem Reference;
        public string ButtonId { get; internal set; }
        public int ReferenceIndex { get; internal set; }
        public string Caption { get; internal set; }
        public string Macro { get; internal set; }
        public string Tooltip { get; internal set; }
        public string FileName { get; internal set; }
        public string Path { get; internal set; }
        public string Tab { get; internal set; }
        public string Group { get; internal set; }
        public string Size { get; internal set; }
        public int Uses { get; internal set; }
        public string Placement { get { return ReferenceIndex < 0 ? "未放置" : (ReferenceIndex + 1) + " / " + Uses; } }
    }

    // One workspace tool, independent of document-local selection and layout state.
    public sealed class FunctionLocatorPage : UserControl
    {
        readonly MainWindow owner; readonly bool openedOnly;
        readonly List<Project> sources = new List<Project>();
        readonly List<FunctionLocatorRow> rows = new List<FunctionLocatorRow>();
        readonly DockPanel root = new DockPanel { Margin = new Thickness(18, 12, 18, 12) };
        readonly TextBox search = Ui.Input();
        readonly ComboBox sourceFilter = Ui.Combo();
        readonly TextBlock summary = Ui.Text("从已打开文件加入功能来源", 12, Ui.Muted);
        readonly TextBlock feedback = Ui.Text("选择结果，定位到对应 UIC 的 Ribbon 编排。", 12, Ui.Muted);
        readonly TextBlock selection = Ui.Text("", 12);
        readonly DataGrid grid = Ui.Grid("功能名称|Caption", "Macro|Macro", "UIC 完整路径|Path", "菜单页|Tab", "分组|Group", "大小|Size", "使用位置|Placement");
        bool rebuilding, busy;

        public int SourceCount { get { return sources.Count; } }
        public int ResultCount { get { return grid.Items.Count; } }
        public bool IsWorking { get { return busy; } }
        internal FunctionLocatorRow[] Rows() { return rows.ToArray(); }

        public FunctionLocatorPage(MainWindow main, bool compact = false)
        {
            if (main == null) throw new ArgumentNullException("main");
            owner = main; openedOnly = compact; Content = root;
            var top = new StackPanel();
            top.Children.Add(Ui.Text("功能定位", 20));
            top.Children.Add(Ui.Text("跨多个 UIC 查找功能与实际使用位置。相同名称的功能按来源文件分别保留。", 12, Ui.Muted));
            var actions = new WrapPanel { Margin = new Thickness(-4, 4, 0, 4) };
            actions.Children.Add(Ui.Button("添加 UIC…", () => Run(AddUic)));
            actions.Children.Add(Ui.Button("从已打开文件加入 / 刷新", Reload));
            actions.Children.Add(Ui.Button("定位到 Ribbon", () => Run(LocateSelection), true));
            top.Children.Add(actions);
            var filters = new WrapPanel();
            search.Width = 320; search.ToolTip = "搜索功能名称、Macro、Tooltip";
            search.TextChanged += (s, e) => { if (!rebuilding) RenderResults(); };
            filters.Children.Add(search);
            sourceFilter.MinWidth = 250; sourceFilter.MaxWidth = 550;
            sourceFilter.Margin = new Thickness(12, 4, 0, 10);
            sourceFilter.ToolTip = "按来源 UIC 筛选；结果中保留完整路径";
            sourceFilter.SelectionChanged += (s, e) => { if (!rebuilding) RenderResults(); };
            filters.Children.Add(sourceFilter); top.Children.Add(filters); top.Children.Add(summary);
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            bottom.Children.Add(selection); bottom.Children.Add(feedback);
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            grid.Name = "WorkspaceFunctionLocatorGrid"; grid.FontSize = 13; grid.RowHeight = Double.NaN;
            grid.MinRowHeight = 42; grid.MinColumnWidth = 65;
            grid.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            grid.Columns[1].Width = new DataGridLength(1.2, DataGridLengthUnitType.Star);
            grid.Columns[2].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star);
            grid.Columns[3].Width = 100; grid.Columns[4].Width = 100; grid.Columns[5].Width = 80; grid.Columns[6].Width = 90;
            foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 7, 6, 7)));
                column.ElementStyle = style;
            }
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("Path")));
            grid.RowStyle = rowStyle;
            grid.SelectionChanged += (s, e) => RenderSelection();
            grid.MouseDoubleClick += async (s, e) => { if (grid.SelectedItem is FunctionLocatorRow) await Run(LocateSelection); };
            root.Children.Add(Ui.TableCard(grid));
            if (compact) {
                root.Margin = new Thickness(8, 2, 8, 4);
                top.Children[0].Visibility = Visibility.Collapsed; top.Children[1].Visibility = Visibility.Collapsed;
                actions.Children[0].Visibility = Visibility.Collapsed;
                actions.Margin = new Thickness(0); search.Width = 190; sourceFilter.MinWidth = 150; sourceFilter.MaxWidth = 240;
                bottom.Margin = new Thickness(0); selection.Visibility = Visibility.Collapsed; summary.Visibility = Visibility.Collapsed;
                feedback.FontSize = 10; grid.FontSize = 11; grid.MinRowHeight = 30; grid.RowHeight = 32;
                grid.Columns[2].Header = "来源 UIC"; ((DataGridTextColumn)grid.Columns[2]).Binding = new System.Windows.Data.Binding("FileName");
                grid.Columns[3].Width = 70; grid.Columns[4].Width = 75; grid.Columns[5].Width = 45; grid.Columns[6].Width = 60;

            }
            Rebuild();
        }

        static string SourcePath(Project project)
        {
            if (project.origin != null && !String.IsNullOrWhiteSpace(project.origin.sourcePath)) return project.origin.sourcePath;
            if (project.preferredBinding != null && !String.IsNullOrWhiteSpace(project.preferredBinding.filePath)) return project.preferredBinding.filePath;
            return "";
        }
        static bool SameSource(Project a, Project b)
        {
            if (Object.ReferenceEquals(a, b)) return true;
            if (!String.IsNullOrEmpty(a.documentId) && !String.IsNullOrEmpty(b.documentId) && a.documentId == b.documentId) return true;
            string ap = SourcePath(a), bp = SourcePath(b);
            if (ap.Length == 0 || bp.Length == 0) return false;
            try { return String.Equals(System.IO.Path.GetFullPath(ap), System.IO.Path.GetFullPath(bp), StringComparison.OrdinalIgnoreCase); }
            catch { return String.Equals(ap, bp, StringComparison.OrdinalIgnoreCase); }
        }
        static string SourceName(Project project)
        {
            string path = SourcePath(project);
            if (!String.IsNullOrEmpty(path)) return path;
            return (project.@namespace ?? "新建 UIC") + "（未保存到文件）";
        }
        void Include(Project project)
        {
            if (project == null) return;
            int index = sources.FindIndex(p => SameSource(p, project));
            if (index < 0) sources.Add(project); else sources[index] = project;
        }

        public void SetSearch(string query) { search.Text = query ?? ""; }
        public Task Reload()
        {
            return Run(() =>
            {
                if (openedOnly) sources.Clear(); foreach (var project in owner.OpenProjects()) Include(project);
                Rebuild(); feedback.Text = "已刷新来源与功能使用位置。";
                return Task.FromResult(0);
            });
        }
        async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true; root.IsEnabled = false;
            try { await action(); }
            catch (Exception error) { feedback.Text = "操作未完成：" + error.Message; }
            finally { busy = false; root.IsEnabled = true; }
        }
        async Task AddUic()
        {
            var paths = Ui.OpenMany("UIC 文件|*.uic");
            if (paths.Length == 0) return;
            var errors = new List<string>();
            foreach (var path in paths)
            {
                try { await owner.ImportPath(path); foreach (var project in owner.OpenProjects()) Include(project); }
                catch (Exception error) { errors.Add(path + "：" + error.Message); }
            }
            Rebuild();
            feedback.Text = errors.Count == 0 ? "已添加 UIC 来源。选择功能后定位到实际编排位置。" : String.Join("\n", errors);
            await owner.OpenFunctionLocator();
        }
        void Rebuild()
        {
            Project selectedSource = (sourceFilter.SelectedItem as ComboBoxItem) == null ? null : ((ComboBoxItem)sourceFilter.SelectedItem).Tag as Project;
            rows.Clear();
            foreach (var project in sources)
            {
                string path = SourceName(project);
                var definitions = (project.buttons ?? new List<RibbonButton>()).GroupBy(b => b.id).Select(g => g.First());
                foreach (var button in definitions)
                {
                    int count = 0;
                    var placements = new List<FunctionLocatorRow>();
                    foreach (var tab in project.tabs ?? new List<RibbonTab>())
                        foreach (var group in tab.groups ?? new List<RibbonGroup>())
                            foreach (var item in (group.items ?? new List<RibbonItem>()).Where(i => i.buttonId == button.id))
                                placements.Add(new FunctionLocatorRow { Project = project, Reference = item, ButtonId = button.id, ReferenceIndex = count++, Caption = button.caption, Macro = button.macro, Tooltip = (button.tooltipTitle ?? "") + " " + (button.tooltipText ?? ""), FileName = project.origin == null ? project.@namespace : project.origin.fileName, Path = path, Tab = tab.caption, Group = group.caption, Size = item.size == "small" ? "小按钮" : "大按钮" });
                    if (placements.Count == 0)
                        placements.Add(new FunctionLocatorRow { Project = project, ButtonId = button.id, ReferenceIndex = -1, Caption = button.caption, Macro = button.macro, Tooltip = (button.tooltipTitle ?? "") + " " + (button.tooltipText ?? ""), FileName = project.origin == null ? project.@namespace : project.origin.fileName, Path = path, Tab = "—", Group = "未放置", Size = "—" });
                    foreach (var row in placements) { row.Uses = count; rows.Add(row); }
                }
            }
            rebuilding = true;
            try
            {
                sourceFilter.Items.Clear(); sourceFilter.Items.Add(new ComboBoxItem { Content = "全部 UIC 来源", Tag = null });
                foreach (var project in sources) sourceFilter.Items.Add(new ComboBoxItem { Content = SourceName(project), Tag = project });
                sourceFilter.SelectedIndex = 0;
                if (selectedSource != null)
                    foreach (var item in sourceFilter.Items.OfType<ComboBoxItem>())
                        if (item.Tag is Project && SameSource((Project)item.Tag, selectedSource)) { sourceFilter.SelectedItem = item; break; }
            }
            finally { rebuilding = false; }
            RenderResults();
        }
        void RenderResults()
        {
            var filter = sourceFilter.SelectedItem as ComboBoxItem;
            var project = filter == null ? null : filter.Tag as Project;
            string query = search.Text.Trim();
            var selectedRow = grid.SelectedItem as FunctionLocatorRow;
            var visible = rows.Where(r => (project == null || SameSource(project, r.Project)) && (query.Length == 0 || ((r.Caption ?? "") + " " + (r.Macro ?? "") + " " + (r.Tooltip ?? "")).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            grid.ItemsSource = visible;
            if (selectedRow != null) grid.SelectedItem = visible.FirstOrDefault(r => Object.ReferenceEquals(r.Project, selectedRow.Project) && r.ButtonId == selectedRow.ButtonId && Object.ReferenceEquals(r.Reference, selectedRow.Reference));
            summary.Text = sources.Count + " 个 UIC 来源 · " + rows.Select(r => new { r.Project, r.ButtonId }).Distinct().Count() + " 个功能 · 当前显示 " + visible.Count + " 个位置（含未放置功能）";
            RenderSelection();
        }
        void RenderSelection()
        {
            var row = grid.SelectedItem as FunctionLocatorRow;
            selection.Text = row == null ? "未选择定位结果" : row.Caption + "\n" + row.Path + "\n" + (row.ReferenceIndex < 0 ? "未放置功能；定位后可选择分组放入 Ribbon。" : row.Tab + " / " + row.Group + " · " + row.Placement);
        }
        async Task LocateSelection()
        {
            var row = grid.SelectedItem as FunctionLocatorRow;
            if (row == null) { feedback.Text = "请先选择一条功能使用位置。"; return; }
            var project = owner.OpenProjects().FirstOrDefault(p => SameSource(p, row.Project)) ?? row.Project;
            int index = -1;
            if (row.Reference != null)
            {
                var placements = (project.tabs ?? new List<RibbonTab>()).SelectMany(t => t.groups ?? new List<RibbonGroup>()).SelectMany(g => g.items ?? new List<RibbonItem>()).Where(i => i.buttonId == row.ButtonId).ToList();
                index = placements.FindIndex(i => Object.ReferenceEquals(i, row.Reference));
                if (index < 0) { feedback.Text = "该位置已变化，请刷新后重新选择，避免定位到其他引用。"; return; }
            }
            if (!(project.buttons ?? new List<RibbonButton>()).Any(b => b.id == row.ButtonId)) { feedback.Text = "该功能已删除，请刷新来源。"; return; }
            await owner.LocateWorkspaceCommand(project, row.ButtonId, index);
            feedback.Text = "已定位：" + row.Caption + " · " + row.Path;
        }
    }
}
