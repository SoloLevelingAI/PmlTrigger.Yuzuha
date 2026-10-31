using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public async Task OpenFunctionLocator()
        {
            var view = workspaceViews.First(v => v.Kind == "locator");
            selectingDock = true; functionScope.SelectedIndex = 1; selectingDock = false;
            await UpdateComparisonScope(); SelectWorkspaceView(view);
        }
        public async Task OpenRegistrationExplorer(string path = null)
        {
            var view = workspaceViews.FirstOrDefault(v => v.Kind == "explorer");
            if (view == null)
            {
                view = AddWorkspaceView("explorer", null);
                var page = new RegistrationExplorerPage(this, true); view.Controller = page; view.Tab.Content = page;
            }
            SelectWorkspaceView(view);
            await ((RegistrationExplorerPage)view.Controller).Reload();
            if (path != null) ((RegistrationExplorerPage)view.Controller).SelectPath(path);
        }
        public async Task LocateWorkspaceCommand(Project source, string buttonId, int referenceIndex)
        {
            var view = workspaceViews.FirstOrDefault(v => v.Kind == "document" &&
                (Object.ReferenceEquals(v.Document.Project, source) || !String.IsNullOrEmpty(source.documentId) && v.Document.Project.documentId == source.documentId));
            if (view == null) { await OpenDocument(source); view = activeView; }
            else SelectWorkspaceView(view);
            SwitchCommandView("layout");
            var button = Project.buttons.FirstOrDefault(b => b.id == buttonId);
            if (button == null) { status.Text = "该功能已经变化，请刷新功能定位结果"; return; }
            activeDocument.TableExpanded = false;
            var refs = CommandReferenceLocations(buttonId);
            if (referenceIndex >= 0 && referenceIndex < refs.Count) LocateCommandReference(refs[referenceIndex]);
            else if (referenceIndex >= 0) { status.Text = "该放置位置已变化，请刷新功能定位结果"; return; }
            else { Selected = new Selection { Kind = "button", Button = button }; RenderAll(); }
            UpdateLayout();
        }
        void ShowCommandDefinitionEditor(RibbonButton button, string focusField)
        {
            if(Project==null || button==null || !Project.buttons.Contains(button))return;
            var reference = CommandReferenceLocations(button.id).FirstOrDefault(r => r.Item == Selected.Item) ?? CommandReferenceLocations(button.id).FirstOrDefault();
            Selected = reference ?? new Selection { Kind = "button", Button = button };
            SwitchCommandView("layout"); RenderInspector(); RenderPreview();
            if (focusField != null) { string label = focusField == "macro" ? "Macro" : focusField == "caption" ? "按钮名称" : focusField == "tooltipTitle" ? "Tooltip 标题" : focusField == "tooltipText" ? "Tooltip 正文" : "唯一标识"; var box = InspectorFields().FirstOrDefault(b => Convert.ToString(b.Tag) == label); if (box != null) { box.Focus(); box.SelectAll(); } }
        }
    }
}
