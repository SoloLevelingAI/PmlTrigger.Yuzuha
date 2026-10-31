using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace RibbonStudio
{
 public partial class MainWindow
 {
  void ShowCurrentDocument() { var v=workspaceViews.FirstOrDefault(x=>x.Kind=="document" && x.Document==activeDocument)??workspaceViews.FirstOrDefault(x=>x.Kind=="document"); if(v!=null) SelectWorkspaceView(v); }
  Task ShowDesign() { ShowCurrentDocument(); return Task.FromResult(0); }
  internal Project[] OpenProjects() { CaptureSession(); return workspaceViews.Where(v=>v.Kind=="document").Select(v=>v.Document.Project).ToArray(); }
  internal Task SaveOpenProjects() { return FlushAllDocuments(); }
  internal Task ShowLibrary() { return Library(); }
  internal async Task OpenUicLocation(string path) { if(!ActivateOpenPath(path)) await ImportPath(path); }
  public async Task OpenSavedLocation(string path) { if(ActivateOpenPath(path)) return; var docs=await Api.Get<Documents>("api/v1/workspace/documents");var doc=docs.documents.FirstOrDefault(d=>StudioToolPage.Same(d.sourcePath,path));if(doc!=null) await OpenDocument(await Api.Post<Project>("workspace/open",new{documentId=doc.id}));else await ImportPath(path); }
  internal async Task OpenTool(bool registration, string path=null) { await Deploy(registration); var view=workspaceViews.FirstOrDefault(v=>v.Kind==(registration?"registration":"deployment")); if(view!=null && view.Page!=null && path!=null) view.Page.SelectPath(path); }
  async Task Deploy(bool registration)
  {
   if(openingDeployment) return; openingDeployment=true;
   try {
    await FlushAllDocuments();
    string kind=registration?"registration":"deployment";
    var view=workspaceViews.FirstOrDefault(v=>v.Kind==kind);
    if(view==null) { view=AddWorkspaceView(kind,activeDocument); view.Page=new StudioToolPage(this,registration,true); view.Tab.Content=view.Page; }
    SelectWorkspaceView(view); await view.Page.Reload();
   } finally { openingDeployment=false; }
  }
  void InstallWorkspaceSelection()
  {
   newDocumentTab.ToolTip="打开或新建 UIC；辅助工具在底部";
   var menu=new ContextMenu();
   foreach(var label in new[]{"打开 UIC（可多选）","新建 UIC"}) {
    var text=label; var item=new MenuItem{Header=label}; item.Click+=async(s,e)=>{ if(text.StartsWith("打开 UIC")) await Guard(Import); else if(text=="新建 UIC") await Guard(NewProject); else if(text=="功能定位") await Guard(OpenFunctionLocator); else if(text=="UIC ↔ XML") await Guard(()=>OpenRegistrationExplorer()); else await Guard(()=>Deploy(false)); }; menu.Items.Add(item);
   }
   newDocumentTab.PreviewMouseLeftButtonDown+=(s,e)=>{ e.Handled=true; menu.PlacementTarget=newDocumentTab; menu.IsOpen=true; };
   workPages.SelectionChanged+=async(s,e)=>{ if(e.Source!=workPages||switchingWorkspace) return; var tab=workPages.SelectedItem as TabItem;var v=tab==null?null:tab.Tag as WorkspaceView;if(v==null)return;if(AnyToolWorking&&v!=activeView){switchingWorkspace=true;workPages.SelectedItem=activeView.Tab;switchingWorkspace=false;return;}if(v.Kind!="document")await Guard(()=>OpenWorkspaceTool(v));else ActivateWorkspaceView(v); };
  }
 }
}
