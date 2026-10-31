using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        void VerifyCompactViewport()
        {
            double originalWidth = Width;
            UpdateLayout();
            var toolbar = VisualChild<WrapPanel>(Content as DependencyObject);
            LayoutCheck(toolbar != null, "document toolbar present in tab content");
            var ribbonTop = documentCommands.TranslatePoint(new Point(0,0),this); var tabsTop = activeView.Tab.TranslatePoint(new Point(0,0),this);
            LayoutCheck(ribbonTop.Y + documentCommands.ActualHeight <= tabsTop.Y + 1, "global Ribbon above browser tabs");
            foreach (double width in new[] { 1280.0, 1050.0 })
            {
                Width = width;
                UpdateLayout();
                foreach (var button in PreviewButtons(toolbar).Concat(PreviewButtons(ribbonPreview)).Where(b => b.IsVisible))
                {
                    var point = button.TranslatePoint(new Point(0, 0), this);
                    LayoutCheck(point.X >= -1 && point.X + button.ActualWidth <= ActualWidth + 1 && point.Y + button.ActualHeight <= ActualHeight - 25, "commands fit compact viewport " + width);
                }
            }
            Width = originalWidth;
            UpdateLayout();
        }
        public async Task VerifyDocumentSessions(string fixture, string mon, string evidence)
        {
            var backup = Json.Copy(Project);
            var first = workspaceViews.First(v => v.Kind == "document" && v.Document == activeDocument);
            WorkspaceView second = null;
            try
            {
                SelectWorkspaceView(first);
                Load(Json.Copy(backup));
                Selected = new Selection { Kind = "group", Tab = Project.tabs.First(), Group = Project.tabs.First().groups.First() };
                int firstCount = Project.buttons.Count;
                await Add("button");
                var firstButton = Selected.Button;
                firstButton.caption = "File A edit";
                string firstId = firstButton.id;
                int firstUndo = undoEdits.Count;
                var firstProject = Project;
                string secondPath = Path.Combine(fixture, "SecondFixture.uic");
                File.WriteAllText(secondPath, await Api.Text("uic/export", await Api.Post<Project>("projects/normalize", backup)));
                await ImportPath(secondPath);
                second = activeView;
                var secondDoc = activeDocument;
                LayoutCheck(second != first && Project.documentId != firstProject.documentId, "two independent UIC documents open");
                LayoutCheck(undoEdits.Count == 0 && !CanUndo, "new document has independent empty undo");
                Selected = new Selection { Kind = "group", Tab = Project.tabs.First(), Group = Project.tabs.First().groups.First() };
                int secondCount = Project.buttons.Count;
                await Add("button");
                string secondId = Selected.Button.id;
                var secondProject = Project;
                int secondUndo = undoEdits.Count;
                SelectWorkspaceView(first);
                LayoutCheck(Object.ReferenceEquals(Project, firstProject) && Selected.Button.id == firstId && undoEdits.Count == firstUndo, "A edit and selection retained");
                await UndoEdit();
                LayoutCheck(Project.buttons.Count == firstCount && secondProject.buttons.Count == secondCount + 1, "A undo never affects B");
                await RedoEdit();
                LayoutCheck(Selected.Button.id == firstId, "A redo preserves identity");
                SelectWorkspaceView(second);
                LayoutCheck(undoEdits.Count == secondUndo && Selected.Button.id == secondId, "B selection and undo retained");
                await UndoEdit();
                LayoutCheck(Project.buttons.Count == secondCount && firstProject.buttons.Count == firstCount + 1, "B undo never affects A");
                await RedoEdit();
                var saving = SaveDraft("edit");
                SelectWorkspaceView(first);
                await saving;
                await secondDoc.Queue;
                var persisted = await Api.Post<Project>("workspace/open", new
                {
                    documentId = secondDoc.Project.documentId
                });
                LayoutCheck(persisted.buttons.Any(b => b.id == secondId) && !persisted.buttons.Any(b => b.id == firstId), "in-flight save remains bound to B");
                LayoutCheck(Object.ReferenceEquals(Project, firstProject), "save response does not replace active A");
                await ImportPath(backup.origin.sourcePath);
                LayoutCheck(activeView == first && undoEdits.Count == firstUndo, "opening same path activates existing edits");
                await Deploy(false);
                var deploy = activeView;
                LayoutCheck(deploy.Page != null && deploy.Kind == "deployment", "independent deployment tool");
                await Deploy(true); var modules = activeView;
                LayoutCheck(modules.Kind == "registration" && modules != deploy, "registration is a separate global tool");
                SelectWorkspaceView(second); await Deploy(true);
                LayoutCheck(activeView == modules && workspaceViews.Count(v=>v.Kind=="registration")==1, "registration tool singleton across files");
                SelectWorkspaceView(first); Selected = new Selection { Kind="group", Tab=Project.tabs.First(), Group=Project.tabs.First().groups.First() }; await Add("button");
                await OpenWorkspaceTool(deploy); LayoutCheck(activeView==deploy, "existing deployment tab activated");
                ShowCurrentDocument();
                foreach (var button in PreviewButtons(ribbonPreview).Where(b => b.DataContext is Selection && ((Selection)b.DataContext).Item != null && ((Selection)b.DataContext).Item.size == "small"))
                {
                    LayoutCheck(button.HorizontalContentAlignment == HorizontalAlignment.Left && ((StackPanel)button.Content).HorizontalAlignment == HorizontalAlignment.Left, "small command icon columns left aligned");
                }
                string beforeCloseId = secondDoc.Project.documentId;
                await CloseWorkspaceView(second);
                second = null;
                LayoutCheck(!workspaceViews.Any(v => v.Kind == "document" && v.Document == secondDoc) && workspaceViews.Contains(deploy) && workspaceViews.Contains(modules), "closing UIC retains global tool tabs");
                var reopened = await Api.Post<Project>("workspace/open", new
                {
                    documentId = beforeCloseId
                });
                LayoutCheck(reopened.buttons.Any(b => b.id == secondId), "close preserves latest draft");
                await OpenDocument(reopened);
                second = activeView;
                var pending = second.Document.Queue;
                var failed = new TaskCompletionSource<int>();
                failed.SetException(new IOException("simulated save failure"));
                second.Document.Queue = failed.Task;
                saveQueue = failed.Task;
                bool refused = false;
                try
                {
                    await CloseWorkspaceView(second);
                }
                catch (IOException) { refused = true; }
                LayoutCheck(refused && workspaceViews.Contains(second), "save failure retains open tab");
                second.Document.Queue = pending;
                saveQueue = pending;
                await CloseWorkspaceView(second);
                second = null;
                SelectWorkspaceView(first);
                // Keep a second file and both tool tabs visible in the native evidence, without changing the real installation.
                await OpenDocument(reopened);
                second = activeView;
                SelectWorkspaceView(first);
                await Deploy(false);
                await Deploy(true);
                ShowCurrentDocument();
                VerifyCompactViewport();
                Ui.Capture(this, evidence);
            }
            finally { if (second != null && workspaceViews.Contains(second)) { foreach (var view in workspaceViews.Where(v => v.Kind == "document" && v.Document == second.Document).ToList()) RemoveWorkspaceView(view); } SelectWorkspaceView(first); Load(backup); ShowCurrentDocument(); }
        }
    }
}
