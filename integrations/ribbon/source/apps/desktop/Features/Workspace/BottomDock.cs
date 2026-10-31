using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        readonly TabControl dockPages = new TabControl();
        readonly TabItem compareDock = new TabItem { Header = "按钮 / 查找" }, xmlDock = new TabItem { Header = "UIC ↔ XML" }, deployDock = new TabItem { Header = "部署" };
        WorkspaceView activeDockView;
        Grid ribbonCenter;
        RowDefinition dockRow;
        double dockHeight = 294;
        bool dockCollapsed, selectingDock;
        DockPanel comparisonContent, compactFunctionHost, comparisonActions;
        TextBlock globalScopeHint;
        Grid comparisonLayers;
        ContentControl locatorContent;
        Button popDockButton;
        ComboBox functionScope;
        TextBox localFunctionSearch;
        TabItem DockSlot(WorkspaceView view) { return view.Kind == "deployment" ? deployDock : new[] { "explorer", "registration", "history" }.Contains(view.Kind) ? xmlDock : compareDock; }
        void BuildBottomDock(Panel center)
        {
            dockPages.Style = (Style)Application.Current.FindResource("DockTabs");
            dockPages.ItemContainerStyle = (Style)Application.Current.FindResource("DockTab");
            foreach (var slot in new[] { compareDock, xmlDock, deployDock }) { dockPages.Items.Add(slot); WireAuxiliaryTab(slot); }
            var panel = new DockPanel();
            var scope = new DockPanel { Margin = new Thickness(10, 2, 8, 0) };
            functionScope = Ui.Combo(); functionScope.Width = 170; functionScope.Margin = new Thickness(0, 2, 8, 2);
            functionScope.Items.Add("当前 UIC · 可编辑"); functionScope.Items.Add("已打开 UIC · 只读查找"); functionScope.SelectedIndex = 0;
            functionScope.SelectionChanged += async (s,e) => { if (selectingDock) return; await Guard(UpdateComparisonScope); };
            DockPanel.SetDock(functionScope, Dock.Left); scope.Children.Add(functionScope);
            globalScopeHint = Ui.Text("只读查找 · 双击跳到已有文件", 10, Ui.Muted); globalScopeHint.Visibility = Visibility.Collapsed; DockPanel.SetDock(globalScopeHint,Dock.Left); scope.Children.Add(globalScopeHint); scope.Children.Add(comparisonActions);
            DockPanel.SetDock(scope, Dock.Top); panel.Children.Add(scope);
            comparisonLayers = new Grid(); comparisonLayers.Children.Add(comparisonContent);
            locatorContent = new ContentControl { Visibility = Visibility.Collapsed }; comparisonLayers.Children.Add(locatorContent); panel.Children.Add(comparisonLayers);
            var view = AddWorkspaceView("locator", null); view.Tab.Content = panel; EnsureAuxiliaryHost(view); ShowDockView(view);
            foreach (var slot in new[] { xmlDock, deployDock }) {
                var placeholder = new StackPanel { Margin = new Thickness(18) };
                placeholder.Children.Add(Ui.Text(slot == xmlDock ? "UIC 与 XML 登记" : "按需部署 UIC", 15));
                placeholder.Children.Add(Ui.Text(slot == xmlDock ? "查看真实登记数量、产品版本与路径；管理登记时单独确认。" : "选择文件与可复用配置，预览后再写入。", 11, Ui.Muted)); slot.Content = placeholder;
            }
            dockPages.SelectionChanged += async (s,e) => {
                if (e.Source != dockPages || selectingDock) return;
                var slot = dockPages.SelectedItem as TabItem; var tool = slot == null ? null : slot.Tag as WorkspaceView;
                if (AnyToolWorking) { selectingDock = true; dockPages.SelectedItem = activeDockView == null ? compareDock : DockSlot(activeDockView); selectingDock = false; return; }
                if (tool != null) SelectWorkspaceView(tool);
                else if (slot == xmlDock) await Guard(() => OpenRegistrationExplorer());
                else if (slot == deployDock) await Guard(() => Deploy(false));
                else SelectComparisonDock();
                QueueLayoutSave();
            };
            var dockSurface = new Grid(); dockSurface.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = dockPages });
            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Height = 36, Margin = new Thickness(0,0,5,0), Background = new SolidColorBrush(Color.FromRgb(245,248,253)) };
            var fold = Ui.Sync("—", ToggleDockCollapsed); fold.ToolTip = "收起 / 展开停靠区";
            var enlarge = Ui.Sync("↗", () => SetDockHeight(dockRow.ActualHeight > 320 ? 294 : 430)); enlarge.ToolTip = "放大停靠区，保留 Ribbon 预览";
            var pop = Ui.Sync("⋯", () => { var slot = dockPages.SelectedItem as TabItem; if (slot != null && slot.ContextMenu != null) { slot.ContextMenu.PlacementTarget = popDockButton; slot.ContextMenu.IsOpen = true; } }); popDockButton = pop; pop.ToolTip = "拖出、单屏并排与停靠";
            foreach (var button in new[] { fold, enlarge, pop }) { button.MinHeight = 0; button.Height = 28; button.Width = 28; button.Margin = new Thickness(1,4,1,4); button.Padding = new Thickness(0); button.BorderThickness = new Thickness(0); button.Background = Brushes.Transparent; controls.Children.Add(button); }
            dockSurface.Children.Add(controls); Grid.SetRow(dockSurface, 3); center.Children.Add(dockSurface);
        }
        async Task UpdateComparisonScope()
        {
            var view = workspaceViews.First(v => v.Kind == "locator");
            bool global = functionScope.SelectedIndex == 1;
            if (global && view.Controller == null) { view.Controller = new FunctionLocatorPage(this, true); locatorContent.Content = view.Controller; }
            if (global) { if (Project != null) commandViews[Project] = "layout"; await ((FunctionLocatorPage)view.Controller).Reload(); }
            ApplyComparisonScope();
        }
        void ApplyComparisonScope()
        {
            bool global = functionScope != null && functionScope.SelectedIndex == 1;
            if (comparisonActions != null) comparisonActions.Visibility = global ? Visibility.Collapsed : Visibility.Visible;
            if (globalScopeHint != null) globalScopeHint.Visibility = global ? Visibility.Visible : Visibility.Collapsed;
            if (locatorContent != null) locatorContent.Visibility = global ? Visibility.Visible : Visibility.Collapsed;
            if (comparisonContent != null) comparisonContent.Visibility = !global && CurrentCommandView == "layout" ? Visibility.Visible : Visibility.Collapsed;
            if (commandLibraryHost != null) commandLibraryHost.Visibility = !global && CurrentCommandView == "library" ? Visibility.Visible : Visibility.Collapsed;
            if (commandReviewHost != null) commandReviewHost.Visibility = !global && CurrentCommandView == "review" ? Visibility.Visible : Visibility.Collapsed;
        }
        void SelectComparisonDock() { var view = workspaceViews.FirstOrDefault(v => v.Kind == "locator"); if (view != null) SelectWorkspaceView(view); }
        void ShowDockView(WorkspaceView view)
        {
            var slot = DockSlot(view); EnsureAuxiliaryHost(view);
            view.Tab.Content = null; slot.Content = null;
            slot.Content = view.AuxiliaryBody; slot.Tag = view;
            activeDockView = view;
            selectingDock = true; dockPages.SelectedItem = slot; selectingDock = false;
            if (dockCollapsed) { dockCollapsed = false; SetDockHeight(dockHeight); }
        }
        void PresentDockedBody(WorkspaceView view)
        {
            var slot = DockSlot(view);
            if (slot.Tag == view) { slot.Content = null; slot.Content = view.AuxiliaryBody; }
            else view.Tab.Content = view.AuxiliaryBody;
        }
        void SetDockHeight(double height)
        {
            dockHeight = Math.Max(160, height); dockCollapsed = false;
            ClampDockHeight(); dockRow.Height = new GridLength(Math.Min(dockHeight, dockRow.MaxHeight)); QueueLayoutSave();
        }
        void ClampDockHeight()
        {
            if (ribbonCenter == null || dockRow == null || ribbonCenter.ActualHeight < 1) return;
            dockRow.MaxHeight = Math.Max(120, ribbonCenter.ActualHeight - ribbonCenter.RowDefinitions[0].ActualHeight - 188);
        }
        void ToggleDockCollapsed()
        {
            if (dockCollapsed) SetDockHeight(dockHeight);
            else { dockHeight = Math.Max(160, dockRow.ActualHeight); dockCollapsed = true; dockRow.Height = new GridLength(38); QueueLayoutSave(); }
        }
        void ResetDockLayout()
        {
            foreach (var view in workspaceViews.Where(v => v.Floating != null).ToArray()) DockAuxiliary(view, false);
            SetDockHeight(294); SelectComparisonDock();
        }
    }
}
