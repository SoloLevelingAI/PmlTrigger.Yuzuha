using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        sealed class DocumentSession
        {
            public Project Project; public Selection Selection; public RibbonTab RibbonTab;
            public List<HistoryEntry> Undo = new List<HistoryEntry>(), Redo = new List<HistoryEntry>();
            public string FunctionQuery; public bool TableExpanded; public HashSet<object> TableMarked = new HashSet<object>();
            public bool AdvancedExpanded; public bool Dirty; public int Revision; public Task Queue = Task.FromResult(0); public string Status = "", FocusField; public HashSet<string> Collapsed = new HashSet<string>(); public int Caret; public double InspectorOffset, TreeOffset;
        }
        sealed class WorkspaceView
        {
            public string Kind; public DocumentSession Document; public TabItem Tab; public TextBlock HeaderLabel; public StudioToolPage Page; public object Controller;
            public WindowGeometry LastFloatingGeometry; public bool MovingAuxiliary, CancelDockDrag;
            public Window Floating; public UIElement AuxiliaryBody; public FrameworkElement AuxiliaryGrip; public ContentControl FloatingHost;
        }
        readonly TabItem newDocumentTab = new TabItem { Header = "＋", ToolTip = "新建 UIC (Ctrl+N)，加载文件 (Ctrl+O)" };
        readonly List<WorkspaceView> workspaceViews = new List<WorkspaceView>(); DocumentSession activeDocument; WorkspaceView activeView; Grid designHost; bool switchingWorkspace;
        ScrollViewer inspectorScroll;
        bool IsDocumentView
        {
            get
            {
                return activeDocument != null && Project != null;
            }
        }
        bool AnyToolWorking
        {
            get
            {
                return workspaceViews.Any(v => v.Page != null && v.Page.IsWorking || v.Controller is RegistrationExplorerPage && ((RegistrationExplorerPage)v.Controller).IsWorking);
            }
        }
        string DocumentName(DocumentSession doc)
        {
            return doc.Project.origin == null ? doc.Project.@namespace : doc.Project.origin.fileName;
        }
        static string TreeKey(Selection selection)
        {
            return selection.Kind + "|" + (selection.Tab == null ? "" : selection.Tab.id) + "|" + (selection.Group == null ? "" : selection.Group.id);
        }
        static T VisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
                return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var match = child as T;
                if (match != null)
                    return match;
                match = VisualChild<T>(child);
                if (match != null)
                    return match;
            }
            return null;
        }
        void CaptureSession(bool captureView = true)
        {
            if (activeDocument == null || Project == null)
                return;
            activeDocument.Project = Project;
            activeDocument.Selection = Selected;
            activeDocument.RibbonTab = activePreviewTab;
            activeDocument.Undo = undoEdits;
            activeDocument.Redo = redoEdits;
            activeDocument.Dirty = dirty;
            activeDocument.Revision = revision;
            activeDocument.Queue = saveQueue;
            activeDocument.Status = status.Text;
            if (captureView && IsDocumentView)
            {
                activeDocument.Collapsed = new HashSet<string>(TreeNodes(tree.Items).Where(node => node.Tag is Selection && !node.IsExpanded).Select(node => TreeKey((Selection)node.Tag)));
                activeDocument.AdvancedExpanded = inspector.Children.OfType<Expander>().Any(e => e.IsExpanded);
                activeDocument.InspectorOffset = inspectorScroll == null ? 0 : inspectorScroll.VerticalOffset;
                var scroll = VisualChild<ScrollViewer>(tree);
                if (scroll != null)
                    activeDocument.TreeOffset = scroll.VerticalOffset;
                var box = Keyboard.FocusedElement as TextBox;
                if (box != null && Ui.Ancestor<ScrollViewer>(box) == inspectorScroll)
                {
                    activeDocument.FocusField = Convert.ToString(box.Tag);
                    activeDocument.Caret = box.CaretIndex;
                }
            }
        }
        void UpdateWorkspaceHeaders()
        {
            foreach (var view in workspaceViews)
            {
                if (view.HeaderLabel == null)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    view.HeaderLabel = Ui.Text("", 11);
                    view.HeaderLabel.TextWrapping = TextWrapping.NoWrap; view.HeaderLabel.TextTrimming = TextTrimming.CharacterEllipsis; view.HeaderLabel.MaxWidth = 164;
                    view.HeaderLabel.Margin = new Thickness(2, 0, 10, 0);
                    view.HeaderLabel.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(view.HeaderLabel);
                    var target = view;
                    var close = Ui.Button("×", () => Guard(() => CloseWorkspaceView(target)));
                    close.Width = 22;
                    close.MinHeight = 0;
                    close.Height = 22;
                    close.Padding = new Thickness(0);
                    close.BorderThickness = new Thickness(0);
                    close.Background = Brushes.Transparent;
                    close.Foreground = Ui.Muted;
                    close.Margin = new Thickness(1, 0, 0, 0);
                    close.ToolTip = "关闭标签 (Ctrl+W)";
                    row.Children.Add(close);
                    view.Tab.Header = row;
                }
                view.HeaderLabel.Text = view.Kind == "document" ? DocumentName(view.Document) + (view.Document.Dirty ? " •" : "") : view.Kind == "deployment" ? "批量部署" : view.Kind == "locator" ? "按钮 / 查找" : view.Kind == "explorer" ? "UIC ↔ XML" : view.Kind == "history" ? "历史 / 管理" : "登记变更";
                view.Tab.ToolTip = view.Kind == "document" ? (view.Document.Project.origin == null ? "新建 UIC" : view.Document.Project.origin.sourcePath ?? view.Document.Project.origin.fileName) + "\n独立编辑、选择和撤销栈" : "独立工具页；再次打开直接跳转";
            }
        }
        WorkspaceView AddWorkspaceView(string kind, DocumentSession doc)
        {
            var view = new WorkspaceView { Kind = kind, Document = doc, Tab = new TabItem() };
            view.Tab.Tag = view;
            workspaceViews.Add(view);
            if (kind == "document") workPages.Items.Insert(Math.Max(0, workPages.Items.Count - 1), view.Tab);
            UpdateWorkspaceHeaders();

            QueueLayoutSave();
            return view;
        }
        void CycleWorkspaceTabs(int offset)
        {
            if (workspaceViews.Count == 0)
                return;
            int at = workspaceViews.IndexOf(activeView);
            SelectWorkspaceView(workspaceViews[(at + offset + workspaceViews.Count) % workspaceViews.Count]);
        }
        void SelectWorkspaceView(WorkspaceView view)
        {
            if (view == null)
                return;
            if (view.Floating != null) { view.Floating.Activate(); return; }
            EnsureAuxiliaryHost(view);
            if (AnyToolWorking && view != activeView)
            {
                status.Text = "正在写入，请等待完成后切换";
                return;
            }
            switchingWorkspace = true;
            try
            {
                if (view.Kind == "document") workPages.SelectedItem = view.Tab;
                else ShowDockView(view);
                ActivateWorkspaceView(view);
            }
            finally { switchingWorkspace = false; }
        }
        void ActivateWorkspaceView(WorkspaceView view)
        {
            if (view.Kind != "document") { activeView = view; activeDockView = view; UpdateHistoryButtons(); QueueLayoutSave(); return; }
            if (view == activeView) return;
            if (compactFunctions != null) { compactFunctions.CommitEdit(DataGridEditingUnit.Cell, true); compactFunctions.CommitEdit(DataGridEditingUnit.Row, true); expandedFunctions.CommitEdit(DataGridEditingUnit.Cell, true); expandedFunctions.CommitEdit(DataGridEditingUnit.Row, true); }
            autosave.Stop();
            var outgoing = activeDocument;
            CaptureSession();
            var previousDocument = workspaceViews.FirstOrDefault(v => v.Kind == "document" && v.Document == activeDocument);
            if (previousDocument != null) previousDocument.Tab.Content = null;
            activeView = view;
            if (view.Kind != "document")
            {
                UpdateHistoryButtons(); UpdateWorkspaceHeaders();
                if (outgoing != null && outgoing.Dirty) SaveSessionInBackground(outgoing);
                return;
            }
            activeDocument = view.Document;
            Project = activeDocument.Project;
            Selected = activeDocument.Selection ?? new Selection { Kind = "project" };
            activePreviewTab = activeDocument.RibbonTab;
            undoEdits = activeDocument.Undo;
            redoEdits = activeDocument.Redo;
            dirty = activeDocument.Dirty;
            revision = activeDocument.Revision;
            saveQueue = activeDocument.Queue;
            status.Text = activeDocument.Status;
            epoch++;
            if (clipboardCut)
            {
                clipboardSelection = null;
                clipboardProject = null;
                clipboardCut = false;
            }
            if (view.Kind == "document")
            {
                view.Tab.Content = designHost;
                RenderAll();
                foreach (var node in TreeNodes(tree.Items))
                {
                    var scope = node.Tag as Selection;
                    if (scope != null && activeDocument.Collapsed.Contains(TreeKey(scope)))
                        node.IsExpanded = false;
                }
                foreach (var expander in inspector.Children.OfType<Expander>())
                    expander.IsExpanded = activeDocument.AdvancedExpanded;
                var restored = activeDocument;
                Dispatcher.BeginInvoke(new Action(() => { if (activeDocument != restored || !IsDocumentView) return; inspectorScroll.ScrollToVerticalOffset(restored.InspectorOffset); var scroll = VisualChild<ScrollViewer>(tree); if (scroll != null) scroll.ScrollToVerticalOffset(restored.TreeOffset); if (!String.IsNullOrEmpty(restored.FocusField)) { var box = InspectorFields().FirstOrDefault(b => Convert.ToString(b.Tag) == restored.FocusField); if (box != null) { box.Focus(); box.CaretIndex = Math.Min(restored.Caret, box.Text.Length); } } }));
            }
            UpdateHistoryButtons();
            UpdateWorkspaceHeaders();
            if (outgoing != null && outgoing != activeDocument && outgoing.Dirty)
                SaveSessionInBackground(outgoing);
            if (dirty && IsDocumentView)
                autosave.Start();
            Remember();
        }
        async void SaveSessionInBackground(DocumentSession doc)
        {
            try
            {
                await QueueDocumentSave(doc, "edit");
            }
            catch (Exception error) { doc.Status = "草稿未保存：" + error.Message; if (activeDocument == doc) status.Text = doc.Status; UpdateWorkspaceHeaders(); }
        }
        async Task OpenWorkspaceTool(WorkspaceView view)
        {
            if (view.Page != null) { SelectWorkspaceView(view); await view.Page.Reload(); }
            else if (view.Controller is FunctionLocatorPage) { SelectWorkspaceView(view); await ((FunctionLocatorPage)view.Controller).Reload(); }
            else if (view.Controller is RegistrationExplorerPage) { SelectWorkspaceView(view); await ((RegistrationExplorerPage)view.Controller).Reload(); }
        }
        public async Task OpenDocument(Project project, bool replace = false)
        {
            CaptureSession();
            var existing = workspaceViews.FirstOrDefault(v => v.Kind == "document" && (!String.IsNullOrEmpty(project.documentId) && v.Document.Project.documentId == project.documentId || project.origin != null && v.Document.Project.origin != null && !String.IsNullOrEmpty(project.origin.sourcePath) && String.Equals(project.origin.sourcePath, v.Document.Project.origin.sourcePath, StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                if (replace)
                    RequireAppliedCommandDrafts(existing.Document.Project);
                if (replace)
                    await FlushDocument(existing.Document);
                SelectWorkspaceView(existing);
                if (replace)
                    Load(project);
                return;
            }
            var doc = new DocumentSession { Project = project, Selection = new Selection { Kind = "project" } };
            var view = AddWorkspaceView("document", doc);
            SelectWorkspaceView(view);
            Load(project);
        }
        bool ActivateOpenPath(string path)
        {
            var view = workspaceViews.FirstOrDefault(v => v.Kind == "document" && v.Document.Project.origin != null && !String.IsNullOrEmpty(v.Document.Project.origin.sourcePath) && String.Equals(Path.GetFullPath(v.Document.Project.origin.sourcePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            if (view == null)
                return false;
            SelectWorkspaceView(view);
            return true;
        }
        async Task CloseWorkspaceView(WorkspaceView view)
        {
            if (view == null || AnyToolWorking)
                return;
            if(view.Floating!=null) DockAuxiliary(view,false);
            if (view.Kind == "document")
            {
                if (HasPendingLibraryDrafts(view.Document.Project))
                {
                    SelectWorkspaceView(view); SwitchCommandView("library");
                    status.Text = "详情尚有未应用修改，请先应用或取消后再关闭文件";
                    return;
                }
                if (view.Document == activeDocument)
                    CaptureSession();
                await FlushDocument(view.Document);
            }
            RemoveWorkspaceView(view);
            if (!workspaceViews.Any(v => v.Kind == "document")) await NewProject();
            else if (activeView == null) SelectWorkspaceView(workspaceViews.Last(v => v.Kind == "document"));
            UpdateWorkspaceHeaders();
        }
        void RemoveWorkspaceView(WorkspaceView view)
        {
            if (view.Kind == "document" && view.Document == activeDocument)
            {
                CaptureSession();
                view.Tab.Content = null;
                activeView = null;
                activeDocument = null;
                Project = null;
            }
            switchingWorkspace = true;
            try
            {
                workPages.Items.Remove(view.Tab);
                workspaceViews.Remove(view);
            }
            finally { switchingWorkspace = false; }
            QueueLayoutSave();
        }
        public async Task FlushDocumentById(string id)
        {
            var view = workspaceViews.FirstOrDefault(v => v.Kind == "document" && v.Document.Project.documentId == id);
            if (view != null)
            {
                if (view.Document == activeDocument)
                    CaptureSession();
                await FlushDocument(view.Document);
            }
        }
        async Task FlushDocument(DocumentSession doc)
        {
            await doc.Queue;
            if (doc.Dirty || String.IsNullOrEmpty(doc.Project.documentId))
                await QueueDocumentSave(doc, "edit");
        }
        async Task FlushAllDocuments()
        {
            foreach (var pendingDocument in workspaceViews.Where(v => v.Kind == "document").Select(v => v.Document).Distinct())
                RequireAppliedCommandDrafts(pendingDocument.Project);
            autosave.Stop();
            CaptureSession();
            foreach (var doc in workspaceViews.Where(v => v.Kind == "document").Select(v => v.Document).Distinct().ToList())
                await FlushDocument(doc);
        }
    }
}
