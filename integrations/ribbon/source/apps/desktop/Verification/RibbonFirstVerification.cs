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
        public async Task VerifyRibbonFirstDock(string evidence)
        {
            ShowCurrentDocument(); SwitchCommandView("layout");
            Selected = CommandReferenceLocations(Project.buttons.First(b => CommandReferenceLocations(b.id).Count > 0).id).First(); activePreviewTab = Selected.Tab; RenderAll();
            var doc = activeDocument; var selected = Selected; var undo = undoEdits; int steps = undo.Count;
            double width = Width, height = Height;
            try
            {
                LayoutCheck(dockPages.Items.Count == 3 && workPages.Items.OfType<TabItem>().All(t => t == newDocumentTab || ((WorkspaceView)t.Tag).Kind == "document"), "three bottom tools, document-only browser tabs");
                foreach (double viewport in new[] { 1280.0, 1050.0 })
                {
                    Width = viewport; Height = 840; await WorkspaceIdle();
                    foreach (string kind in new[] { "locator", "explorer", "deployment" })
                    {
                        if (kind == "locator") { selectingDock = true; functionScope.SelectedIndex = 0; selectingDock = false; ApplyComparisonScope(); SelectComparisonDock(); }
                        else if (kind == "explorer") await OpenRegistrationExplorer(); else await Deploy(false);
                        SetDockHeight(430); await WorkspaceIdle();
                        LayoutCheck(ribbonCenter.RowDefinitions[1].ActualHeight >= 179 && ribbonPreview.IsVisible && designLayout.IsVisible, "Ribbon remains visible when tools expand: " + kind + "/" + viewport);
                        var bottom = dockPages.TranslatePoint(new Point(0, dockPages.ActualHeight), this);
                        LayoutCheck(bottom.Y <= ActualHeight - 25 && dockPages.ActualHeight >= 120, "dock remains inside window");
                        LayoutCheck(activeDocument == doc && Selected.Button == selected.Button && Selected.Item == selected.Item && undoEdits == undo && undo.Count == steps, "tool switch preserves document selection and delta undo: " + kind + "/" + viewport + " doc=" + (activeDocument == doc) + " button=" + (Selected.Button == selected.Button) + " item=" + (Selected.Item == selected.Item) + " stack=" + (undoEdits == undo) + " steps=" + undo.Count + "/" + steps);
                        LayoutCheck(InspectorFields().Any(b => Convert.ToString(b.Tag) == "Macro") && InspectorFields().Any(b => Convert.ToString(b.Tag) == "Tooltip 正文"), "full properties remain available");
                    }
                }
                Width = 1280; SetDockHeight(294); await WorkspaceIdle();
                SelectComparisonDock(); selectingDock = true; functionScope.SelectedIndex = 0; selectingDock = false; ApplyComparisonScope();
                localFunctionSearch.Text = "__no_matching_ribbon_fixture__";
                LayoutCheck(compactFunctions.Items.Count == 0 && undo.Count == steps, "local filter is not a document mutation");
                localFunctionSearch.Text = "";
                await OpenFunctionLocator(); var locator = (FunctionLocatorPage)workspaceViews.First(v => v.Kind == "locator").Controller;
                LayoutCheck(locator.SourceCount == OpenProjects().Length && CommandVisuals<DataGrid>(locator).All(g => g.IsReadOnly), "open-file scope is read-only and limited to open UICs");
                LayoutCheck(undo.Count == steps && activeDocument == doc, "cross-file scope does not mutate document");
                selectingDock = true; functionScope.SelectedIndex = 0; selectingDock = false; ApplyComparisonScope();
                ShowCurrentDocument(); SetFunctionExpanded(true); await WorkspaceIdle();
                LayoutCheck(expandedFunctions.IsVisible && ribbonCenter.RowDefinitions[1].ActualHeight >= 179, "editable detailed table never replaces Ribbon");
                SetFunctionExpanded(false); SetDockHeight(294); await WorkspaceIdle(); Ui.Capture(this, evidence);
                File.WriteAllText(Path.ChangeExtension(evidence,"json"), Json.Write(new { passed=true, bottomTabs=3, documentOnlyBrowserTabs=true, widths=new[]{1280,1050}, minimumRibbonHeight=180, readOnlyCrossFileScope=true, deltaHistoryPreserved=true }));
            }
            finally { Width=width; Height=height; ShowCurrentDocument(); SetDockHeight(294); }
        }
    }
}
