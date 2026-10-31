using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        readonly Dictionary<Project, string> commandViews = new Dictionary<Project, string>();
        readonly Dictionary<string, Button> commandViewButtons = new Dictionary<string, Button>();
        FrameworkElement commandLibraryHost, commandReviewHost;
        StackPanel commandReviewRows;
        TextBlock commandReviewSummary;
        Project reviewedProject;
        int reviewedRevision = -1;
        Validation commandValidation;
        void RequireAppliedCommandDrafts(Project project)
        {
            if (HasPendingLibraryDrafts(project)) throw new InvalidOperationException("功能库详情尚有未应用修改，请先点击应用或取消修改；当前文档将保持打开。");
        }
        void EditCommandDefinition(RibbonButton button, string field)
        {
            ShowCommandDefinitionEditor(button, field);
        }
        ListBox commandPicker;
        bool renderingCommandPicker;
        void BuildCommandPicker(Panel parent)
        {
            var tabs = new TabControl { BorderThickness = new Thickness(0) };
            var available = new DockPanel();
            var place = Ui.Button("放入所选分组…", () => Guard(() => ChooseCommandPlacement(commandPicker.SelectedItems.Cast<RibbonButton>().Select(b => b.id))));
            DockPanel.SetDock(place, Dock.Bottom); available.Children.Add(place);
            commandPicker = new ListBox { BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Extended, DisplayMemberPath = "caption", Margin = new Thickness(8) };
            commandPicker.SelectionChanged += (s, e) => { if (renderingCommandPicker || commandPicker.SelectedItem == null) return; Selected = new Selection { Kind = "button", Button = (RibbonButton)commandPicker.SelectedItem }; RenderInspector(); };
            available.Children.Add(commandPicker);
            tabs.Items.Add(new TabItem { Header = "可用功能", Content = available });
            tabs.Items.Add(new TabItem { Header = "布局结构", Content = tree });
            parent.Children.Add(tabs);
        }
        void RenderCommandPicker()
        {
            if (commandPicker == null || Project == null) return;
            var selected = commandPicker.SelectedItems.Cast<RibbonButton>().Where(b => Project.buttons.Contains(b)).ToList();
            renderingCommandPicker = true;
            try { commandPicker.ItemsSource = Project.buttons.ToList(); foreach (var b in selected) commandPicker.SelectedItems.Add(b); }
            finally { renderingCommandPicker = false; }
        }
        public string CurrentCommandView
        {
            get { string kind; return Project != null && commandViews.TryGetValue(Project, out kind) ? kind : "layout"; }
        }
        void BuildGlobalCommandBar(Panel tools)
        {
            ToolGroup(tools, "文件", new[] { Tool("加载文件", "paste", Import), Tool("新建 UIC", "copy", NewProject), Tool("保存快照", "save", SaveProject), Tool("导出 UIC", "format", Export), Tool("导出项目 JSON", "copy", ExportProject) });
            ToolGroup(tools, "设计", new[] { Tool("添加菜单页", "copy", () => Add("tab")), Tool("添加 Group", "paste", () => Add("group")), Tool("添加按钮", "copy", () => Add("button")), Tool("检查项目", "find", Validate) });
            undoButton = Tool("撤销", "undo", UndoEdit); redoButton = Tool("重做", "redo", RedoEdit);
            undoButton.ToolTip = "撤销文档操作 (Ctrl+Z)"; redoButton.ToolTip = "重做文档操作 (Ctrl+Y)";
            ToolGroup(tools, "撤销 / 重做", new[] { undoButton, redoButton });
            var more = new ContextMenu(); more.Items.Add(Ui.Menu("管理 / 历史", () => Guard(Library))); more.Items.Add(Ui.Menu("后端设置", () => Guard(Settings)));
            more.Items.Add(Ui.Menu("恢复默认停靠布局", () => { ResetDockLayout(); return Task.FromResult(0); }));
            var moreButton = Tool("更多 ▾", "select", () => { more.IsOpen = true; return Task.FromResult(0); }); more.PlacementTarget = moreButton;
            ToolGroup(tools, "工具", new[] { Tool("部署 UIC", "paste", () => Deploy(false)), Tool("UIC ↔ XML", "find", () => OpenRegistrationExplorer()), moreButton });
            UpdateHistoryButtons();
        }
        void BuildCommandWorkspace()
        {
            // A UIC document is always a Ribbon editor. Workspace tools live once in their own tabs.
            commandLibraryHost = BuildCommandLibraryView();
            comparisonLayers.Children.Add(commandLibraryHost);
            commandReviewHost = BuildCommandReviewView();
            comparisonLayers.Children.Add(commandReviewHost);
            CommandPlacementNavigate = () => SwitchCommandView("layout");
            ApplyCommandView();
        }
        public void SwitchCommandView(string kind)
        {
            if (!new[] { "library", "layout", "review" }.Contains(kind)) throw new ArgumentException("未知工作视图");
            if (AnyToolWorking) return;
            ShowCurrentDocument();
            if (Project == null) return;
            // Commit any grid editor before its containing view is hidden.
            if (compactFunctions != null) { compactFunctions.CommitEdit(DataGridEditingUnit.Cell, true); compactFunctions.CommitEdit(DataGridEditingUnit.Row, true); }
            if (expandedFunctions != null) { expandedFunctions.CommitEdit(DataGridEditingUnit.Cell, true); expandedFunctions.CommitEdit(DataGridEditingUnit.Row, true); }
            commandViews[Project] = kind;
            if (kind == "layout") { RenderTree(); RenderInspector(); RenderPreview(); RenderCommandPicker(); }
            if (functionScope != null) { selectingDock = true; functionScope.SelectedIndex = 0; selectingDock = false; }
            RenderCommandLibrary(); ApplyCommandView();
            if (kind != "layout") { SelectComparisonDock(); SetDockHeight(430); }
        }
        void ApplyCommandView()
        {
            if (commandLibraryHost == null) return;
            string kind = CurrentCommandView;
            commandLibraryHost.Visibility = kind == "library" ? Visibility.Visible : Visibility.Collapsed;
            commandReviewHost.Visibility = kind == "review" ? Visibility.Visible : Visibility.Collapsed;
            bool expanded = activeDocument != null && activeDocument.TableExpanded;
            designLayout.Visibility = Visibility.Visible;
            comparisonContent.Visibility = kind == "layout" ? Visibility.Visible : Visibility.Collapsed;
            expandedFunctionHost.Visibility = kind == "layout" && expanded ? Visibility.Visible : Visibility.Collapsed;
            compactFunctionHost.Visibility = kind == "layout" && !expanded ? Visibility.Visible : Visibility.Collapsed;
            foreach (var pair in commandViewButtons)
            {
                pair.Value.Style = (Style)Application.Current.FindResource(pair.Key == kind ? (object)"PrimaryButton" : typeof(Button));
                pair.Value.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, pair.Value.Content + (pair.Key == kind ? "，当前视图" : ""));
            }
            ApplyComparisonScope();
            if (kind == "review") RenderCommandReview();
        }
        FrameworkElement BuildCommandReviewView()
        {
            var root = new DockPanel { Margin = new Thickness(18) };
            var top = new StackPanel();
            top.Children.Add(Ui.Text("检查与部署", 20));
            top.Children.Add(Ui.Text("先修复功能与布局问题，再进入真实写入预览。更新 UIC 与新增模块登记分开处理。", 12, Ui.Muted));
            commandReviewSummary = Ui.Text("尚未运行后端校验", 13); top.Children.Add(commandReviewSummary);
            var actions = new WrapPanel();
            actions.Children.Add(Ui.Button("重新检查", () => Guard(RefreshCommandReview), true));
            actions.Children.Add(Ui.Button("保存快照", () => Guard(SaveProject)));
            actions.Children.Add(Ui.Button("打开更新 / 部署预览", () => Guard(() => Deploy(false))));
            actions.Children.Add(Ui.Button("单独管理模块登记", () => Guard(() => Deploy(true))));
            top.Children.Add(actions); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            commandReviewRows = new StackPanel(); root.Children.Add(Ui.Scroll(commandReviewRows)); return root;
        }
        public async Task RefreshCommandReview()
        {
            if (Project == null) return;
            RequireAppliedCommandDrafts(Project);
            var project = Project; int at = revision;
            var result = await Api.Post<Validation>("projects/validate", Json.Copy(project));
            if (Project != project || at != revision) { status.Text = "文档已变化，请重新检查"; return; }
            reviewedProject = project; reviewedRevision = at; commandValidation = result;
            RenderCommandReview();
        }
        void ReviewRow(string heading, string detail, Action locate = null)
        {
            var body = new DockPanel { Margin = new Thickness(4) };
            if (locate != null) { var button = Ui.Sync("定位问题", locate); DockPanel.SetDock(button, Dock.Right); body.Children.Add(button); }
            var text = new StackPanel(); text.Children.Add(Ui.Text(heading, 14)); text.Children.Add(Ui.Text(detail, 12, Ui.Muted)); body.Children.Add(text);
            commandReviewRows.Children.Add(Ui.Card(body));
        }
        void LocateValidationIssue(string path)
        {
            // Validation paths refer to the unchanged document revision, never a guessed object.
            if (reviewedProject != Project || reviewedRevision != revision) { status.Text = "结果已过期，请重新检查后定位"; return; }
            var match = System.Text.RegularExpressions.Regex.Match(path ?? "", @"^buttons\[(\d+)\]");
            if (match.Success)
            {
                int index = Int32.Parse(match.Groups[1].Value);
                if (index < Project.buttons.Count) { string id = Project.buttons[index].id; SwitchCommandView("library"); SelectLibraryCommand(id); }
                return;
            }
            match = System.Text.RegularExpressions.Regex.Match(path ?? "", @"^tabs\[(\d+)\](?:\.groups\[(\d+)\])?(?:\.items\[(\d+)\])?");
            if (match.Success)
            {
                int ti = Int32.Parse(match.Groups[1].Value); if (ti >= Project.tabs.Count) return;
                var tab = Project.tabs[ti]; var scope = new Selection { Kind = "tab", Tab = tab };
                if (match.Groups[2].Success) { int gi = Int32.Parse(match.Groups[2].Value); if (gi >= tab.groups.Count) return; scope.Group = tab.groups[gi]; scope.Kind = "group"; }
                if (match.Groups[3].Success && scope.Group != null) { int ii = Int32.Parse(match.Groups[3].Value); if (ii >= scope.Group.items.Count) return; scope.Item = scope.Group.items[ii]; scope.Button = Project.buttons.FirstOrDefault(b => b.id == scope.Item.buttonId); scope.Kind = "button"; }
                Selected = scope; activePreviewTab = tab; SwitchCommandView("layout"); RenderAll();
            }
            else { Selected = new Selection { Kind = "project" }; SwitchCommandView("layout"); RenderAll(); }
        }
        void RenderCommandReview()
        {
            if (commandReviewRows == null || Project == null) return;
            commandReviewRows.Children.Clear();
            bool current = reviewedProject == Project && reviewedRevision == revision && commandValidation != null;
            commandReviewSummary.Text = current ? (commandValidation.valid ? "后端校验通过；实际部署仍需核对目标并确认" : "后端发现 " + commandValidation.issues.Count + " 个问题") : "尚未检查当前修订；请运行后端校验";
            if (current) foreach (var issue in commandValidation.issues) { string path = issue.path; ReviewRow("字段检查 · " + path, issue.message, () => LocateValidationIssue(path)); }
            var used = new HashSet<string>(Project.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Select(i => i.buttonId));
            int unused = Project.buttons.Count(b => !used.Contains(b.id));
            ReviewRow("功能库存 · " + unused + " 个未放置", "未放置功能可以保留并保存；部署是否可用由后端检查完整内容。", () => SwitchCommandView("library"));
            ReviewRow("布局与视觉复核", "核对分组内容中心、文字基线、图标有效范围和长文本裁切；当前不会用主观分数自动调整布局。", () => SwitchCommandView("layout"));
            ReviewRow("操作验证", "功能库修改 → 多处引用同步 → 移除单个引用 → 撤销恢复。预览按钮不执行 AVEVA Macro。");
        }
    }
}
