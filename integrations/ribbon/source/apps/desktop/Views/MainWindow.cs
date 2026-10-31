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
    public class Selection
    {
        public RibbonTab Tab; public RibbonGroup Group; public RibbonItem Item; public RibbonButton Button; public string Kind;
    }
    public partial class MainWindow : Window
    {
        public ApiClient Api; public Project Project; public Selection Selected = new Selection { Kind = "project" };
        readonly TreeView tree = new TreeView { BorderThickness = new Thickness(0), Background = Brushes.White, Margin = new Thickness(12) };
        readonly StackPanel inspector = new StackPanel { Margin = new Thickness(16) }, ribbonPreview = new StackPanel(), projectOverview = new StackPanel { Margin = new Thickness(22) };
        readonly TextBlock status = Ui.Text("连接后端…", 11), title = Ui.Text("Ribbon Studio", 14), summary = Ui.Text("", 11, Ui.Muted);
        readonly WrapPanel previewTabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; readonly DispatcherTimer autosave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RibbonStudio", "Desktop", "settings.json");
        Task saveQueue = Task.FromResult(0); bool loading, dirty, closing, closePending, operationBusy; int revision, epoch; bool smoke;
        public MainWindow(ApiClient api, bool test = false, string layoutFile = null)
        {
            Api = api;
            layoutPath = layoutFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RibbonStudio", "Desktop", "workspace-layout.json");
            smoke = test;
            Title = "Ribbon Studio · Ribbon 主视角工作区 0.10";
            Width = 1280;
            Height = 840;
            MinWidth = 1050;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(241, 243, 246));
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12;
            Build();
            TrackWorkspaceLayout();
            Closed += (s,e) => CloseAuxiliaries();
            autosave.Tick += async (s, e) => { autosave.Stop(); try { await SaveDraft("edit"); } catch (Exception error) { status.Text = "草稿未保存：" + error.Message; } };
            Closing += async (s, e) => { if (closing || smoke) return; e.Cancel = true; if (closePending || operationBusy || AnyToolWorking) return; closePending = true; try { await FlushAllDocuments(); SaveWorkspaceLayoutNow(); closing = true; ScheduleClose(); } catch (Exception error) { Ui.Error(error); if (Ui.Confirm("尚有未记录编辑，仍要关闭吗？")) { closing = true; ScheduleClose(); } else closePending = false; } };
            PreviewKeyDown += async (s, e) => { if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { e.Handled = true; await Guard(NewProject); return; } if ((Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) && e.Key == Key.Tab) { e.Handled = true; CycleWorkspaceTabs(Keyboard.Modifiers == ModifierKeys.Control ? 1 : -1); return; } if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W) { e.Handled = true; await Guard(() => CloseWorkspaceView(workspaceViews.FirstOrDefault(v => v.Kind == "document" && v.Document == activeDocument))); return; } if (!IsDocumentView && e.Key != Key.O && e.Key != Key.S) return; if (HistoryShortcut(e.OriginalSource as DependencyObject, e.Key, Keyboard.Modifiers)) { e.Handled = true; await Guard(e.Key == Key.Z ? (Func<Task>)UndoEdit : RedoEdit); return; } if (CurrentCommandView == "layout" && Keyboard.Modifiers == ModifierKeys.Control && Ui.Ancestor<TextBox>(e.OriginalSource as DependencyObject) == null && Ui.Ancestor<ComboBox>(e.OriginalSource as DependencyObject) == null && Ui.Ancestor<DataGrid>(e.OriginalSource as DependencyObject) == null && (e.Key == Key.X || e.Key == Key.C || e.Key == Key.V)) { e.Handled = true; await Guard(() => e.Key == Key.V ? PasteLayout() : CopyLayout(e.Key == Key.X)); return; } if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { e.Handled = true; await Guard(() => SaveProject()); } else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O) { e.Handled = true; await Guard(() => Import()); } else if (CurrentCommandView == "layout" && e.Key == Key.F2 && Ui.Ancestor<TextBox>(e.OriginalSource as DependencyObject) == null && Ui.Ancestor<ComboBox>(e.OriginalSource as DependencyObject) == null && Selected.Kind != "project") { e.Handled = true; await Guard(RenameSelection); } else if (CurrentCommandView == "layout" && e.Key == Key.Delete && Ui.Ancestor<TextBox>(e.OriginalSource as DependencyObject) == null && Ui.Ancestor<ComboBox>(e.OriginalSource as DependencyObject) == null && Selected.Kind != "project") { e.Handled = true; await Guard(Delete); } };
        }
        void ScheduleClose()
        {
            Dispatcher.BeginInvoke(new Action(Close));
        }
        async Task Guard(Func<Task> action)
        {
            if (operationBusy || AnyToolWorking)
            {
                status.Text = "请等待当前操作完成";
                return;
            }
            operationBusy = true;
            Chrome.Busy(this, true);
            try
            {
                await action();
            }
            catch (Exception error) { Ui.Error(error); status.Text = error.Message; }
            finally { operationBusy = false; Chrome.Busy(this, false); }
        }
        void Build()
        {
            workPages.Style = (Style)Application.Current.FindResource("WorkspaceTabs");
            workPages.ItemContainerStyle = (Style)Application.Current.FindResource("WorkspaceTab");
            var root = new DockPanel();
            Content = root;
            var header = new DockPanel { Background = Ui.Blue, Height = 38, LastChildFill = true };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var brand = Ui.Text("  R   Ribbon Studio  /  UIC Designer", 14, Brushes.White);
            brand.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(brand);
            var headStatus = Ui.Text("工作区 0.10     ", 10, new SolidColorBrush(Color.FromRgb(192, 211, 234)));
            headStatus.HorizontalAlignment = HorizontalAlignment.Right;
            headStatus.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(headStatus);
            var tools = new WrapPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
            var horizontal = new ScrollViewer { Content = tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Brushes.White };
            documentCommands = horizontal;
            DockPanel.SetDock(horizontal, Dock.Top);
            root.Children.Add(horizontal);
            BuildGlobalCommandBar(tools);
            brand.ContextMenu = new ContextMenu();
            var libraryMenu = new MenuItem { Header = "管理 / 历史" }; libraryMenu.Click += async (s, e) => await Guard(Library); brand.ContextMenu.Items.Add(libraryMenu);
            var settingsMenu = new MenuItem { Header = "后端设置" }; settingsMenu.Click += async (s, e) => await Guard(Settings); brand.ContextMenu.Items.Add(settingsMenu);
            var foot = new DockPanel { Height = 32, Background = Brushes.White, Margin = new Thickness(0, 1, 0, 0) };
            DockPanel.SetDock(foot, Dock.Bottom);
            root.Children.Add(foot);
            status.Margin = new Thickness(14, 4, 0, 4);
            foot.Children.Add(status);
            var backend = Ui.Text(" API  " + Api.Address + "   ", 10, Ui.Muted);
            backend.HorizontalAlignment = HorizontalAlignment.Right;
            foot.Children.Add(backend);
            var workspace = new Grid();
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            designLayout = workspace;
            designHost = new Grid();
            designHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            designHost.RowDefinitions.Add(new RowDefinition());

            Grid.SetRow(workspace, 1); designHost.Children.Add(workspace);
            workPages.Items.Add(newDocumentTab);
            root.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = workPages });
            InstallWorkspaceSelection();
            var left = new DockPanel { Background = Brushes.White, Margin = new Thickness(0, 1, 1, 0) };
            Grid.SetColumn(left, 0);
            var leftCard = Ui.Card(left); leftCard.Padding = new Thickness(0); leftCard.Margin = new Thickness(12, 10, 0, 12);
            workspace.Children.Add(leftCard);
            var leftHeading = Ui.Text("RIBBON 结构", 12, Ui.Muted);
            leftHeading.Margin = new Thickness(20, 19, 0, 10);
            leftHeading.Text = "RIBBON 结构";
            DockPanel.SetDock(leftHeading, Dock.Top);
            left.Children.Add(leftHeading);
            left.Children.Add(tree);
            tree.SelectedItemChanged += (s, e) => { if (loading) return; var node = tree.SelectedItem as TreeViewItem; if (node != null && node.Tag is Selection) { Selected = (Selection)node.Tag; if (Selected.Tab != null) activePreviewTab = Selected.Tab; RenderInspector(); RenderPreview(); } };
            var center = new Grid { Margin = new Thickness(16, 16, 16, 12) };
            ribbonCenter = center; Grid.SetColumn(center, 1); workspace.Children.Add(center);
            center.SizeChanged += (s,e) => ClampDockHeight();
            center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 180 });
            center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            dockRow = new RowDefinition { Height = new GridLength(294), MinHeight = 38 }; center.RowDefinitions.Add(dockRow);
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) }; heading.Children.Add(title); heading.Children.Add(summary); center.Children.Add(heading);
            var previewCard = Ui.Card(new ScrollViewer { Content = ribbonPreview, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            previewCard.Padding = new Thickness(12); previewCard.CornerRadius = new CornerRadius(6); previewCard.Margin = new Thickness(0); Grid.SetRow(previewCard, 1); center.Children.Add(previewCard);
            var split = new GridSplitter { Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            Grid.SetRow(split, 2); center.Children.Add(split); split.DragCompleted += (s,e) => { if (!dockCollapsed) dockHeight = dockRow.ActualHeight; QueueLayoutSave(); };
            BuildFunctionTables(center);
            var right = new DockPanel { Background = Brushes.White, Margin = new Thickness(1, 1, 0, 0) };
            Grid.SetColumn(right, 2);
            var rightCard = Ui.Card(right); rightCard.Padding = new Thickness(0); rightCard.Margin = new Thickness(0, 10, 12, 12); Grid.SetColumn(rightCard, 2);
            workspace.Children.Add(rightCard);
            inspectorScroll = Ui.Scroll(inspector);
            right.Children.Add(inspectorScroll);
            BuildCommandWorkspace();
            Chrome.Install(this, header);
        }
        Button Tool(string text, string icon, Func<Task> action)
        {
            bool large = new[] { "加载文件", "保存快照", "新建 UIC", "部署 UIC" }.Contains(text);
            var button = Ui.Button("", () => Guard(async () => { if (text != "加载文件" && text != "新建 UIC" && text != "部署 UIC" && !IsDocumentView) ShowCurrentDocument(); await action(); }));
            button.Style = (Style)Application.Current.FindResource("RibbonTool");
            button.Tag = large ? "Large" : "Small";
            button.Width = large ? 66 : 104;
            button.Height = large ? 76 : 27;
            button.MinHeight = 0;
            button.Margin = new Thickness(1);
            button.Padding = new Thickness(6, 3, 6, 3);
            var panel = new StackPanel { Orientation = large ? Orientation.Vertical : Orientation.Horizontal, HorizontalAlignment = large ? HorizontalAlignment.Center : HorizontalAlignment.Left };
            var image = Ui.Icon(icon, large ? 32 : 16);
            image.Margin = large ? new Thickness(0, 0, 0, 7) : new Thickness(0, 0, 8, 0);
            panel.Children.Add(image);
            var caption = Ui.Text(text, 11);
            caption.Margin = new Thickness(0);
            caption.TextAlignment = large ? TextAlignment.Center : TextAlignment.Left;
            caption.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(caption);
            button.HorizontalContentAlignment = large ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            button.Content = panel;
            button.ToolTip = text;
            return button;
        }
        void ToolGroup(Panel parent, string caption, Button[] buttons)
        {
            var group = new DockPanel { Margin = new Thickness(6, 0, 6, 0) };
            var label = Ui.Text(caption, 10, Ui.Muted);
            label.Margin = new Thickness(0, 5, 0, 5);
            label.TextAlignment = TextAlignment.Center;
            DockPanel.SetDock(label, Dock.Bottom);
            group.Children.Add(label);
            var row = new StackPanel { Orientation = Orientation.Horizontal, MinHeight = 78 };
            foreach (var large in buttons.Where(b => Convert.ToString(b.Tag) == "Large"))
                row.Children.Add(large);
            var small = buttons.Where(b => Convert.ToString(b.Tag) != "Large").ToList();
            if (small.Count > 0 && buttons.Any(b => Convert.ToString(b.Tag) == "Large"))
                row.Children.Add(new Border { Width = 1, Background = Ui.Line, Margin = new Thickness(7, 2, 8, 2) });
            for (int i = 0; i < small.Count; i += 3)
            {
                var column = new StackPanel();
                foreach (var button in small.Skip(i).Take(3))
                    column.Children.Add(button);
                row.Children.Add(column);
            }
            group.Children.Add(row);
            parent.Children.Add(new Border { Child = group, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0), Margin = new Thickness(0, 6, 0, 5) });
        }

        public void Load(Project project)
        {
            epoch++;
            autosave.Stop();
            activePreviewTab = null;
            if (clipboardCut)
            {
                clipboardSelection = null;
                clipboardProject = null;
            }
            Project = project;
            dirty = false;
            Selected = new Selection { Kind = "project" };
            foreach (var tab in project.tabs)
            {
                foreach (var group in tab.groups)
                {
                    var item = group.items.FirstOrDefault();
                    if (item == null)
                        continue;
                    var button = project.buttons.FirstOrDefault(b => b.id == item.buttonId);
                    if (button != null)
                    {
                        Selected = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button };
                        break;
                    }
                }
                if (Selected.Kind == "button")
                    break;
            }
            ResetEditHistory();
            CaptureSession(false);
            UpdateWorkspaceHeaders();
            RenderAll();
            Remember();
        }

        void Changed(string action = "编辑", bool recordHistory = true)
        {
            if (loading)
                return;
            revision++;
            dirty = true;
            CaptureSession(false);
            UpdateWorkspaceHeaders();
            status.Text = "编辑中 · 尚未记录";
            autosave.Stop();
            autosave.Start();
        }






























        readonly TabControl workPages = new TabControl { BorderThickness = new Thickness(0), Background = Brushes.Transparent }; bool openingDeployment;



    }
}
