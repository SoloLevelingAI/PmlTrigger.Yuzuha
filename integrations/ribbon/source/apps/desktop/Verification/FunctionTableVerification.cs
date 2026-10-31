using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public async Task VerifyFunctionTable(string mon, string evidence)
        {
            var backup = Json.Copy(Project); var document = activeDocument;
            try
            {
                var rows = functionRows.Where(r => r.Scope.Item != null).Take(2).ToList(); LayoutCheck(rows.Count==2,"table exposes layout rows");
                var oldMacro = rows.Select(r=>r.Macro).ToArray(); var oldTooltip = rows.Select(r=>r.TooltipText).ToArray(); var oldImage = rows.Select(r=>r.Scope.Button.image).ToArray(); var oldSizes=rows.Select(r=>r.Size).ToArray();
                var groups=Project.tabs.SelectMany(t=>t.groups).ToList(); var order=groups.Select(g=>g.items.ToList()).ToList();
                var untouched=Project.buttons.Where(b=>!rows.Any(r=>r.Scope.Button==b)).Select(b=>b.macro).ToList();
                foreach(var r in rows) r.Marked=true;
                int undoCount=undoEdits.Count;
                ApplyFunctionBatch(rows,new Dictionary<string,string>{{"macro","show !!BulkFixture"},{"tooltipText","Tooltip 批量 <>&"}},groups.Last(),"large",null,true);
                LayoutCheck(undoEdits.Count==undoCount+1 && rows.All(r=>r.Macro=="show !!BulkFixture" && r.TooltipText=="Tooltip 批量 <>&" && r.Scope.Button.image==null),"batch attributes in one incremental edit");
                LayoutCheck(rows.All(r=>groups.Last().items.Contains(r.Scope.Item)),"batch Group move");
                await UndoEdit();
                LayoutCheck(rows.Select(r=>r.Macro).SequenceEqual(oldMacro) && rows.Select(r=>r.TooltipText).SequenceEqual(oldTooltip) && rows.Select(r=>r.Size).SequenceEqual(oldSizes),"undo all batch properties");
                LayoutCheck(rows.Select(r=>r.Scope.Button.image).SequenceEqual(oldImage),"undo restores actual images");
                for(int i=0;i<groups.Count;i++)LayoutCheck(groups[i].items.SequenceEqual(order[i]),"undo restores exact group order");
                LayoutCheck(Project.buttons.Where(b=>!rows.Any(r=>r.Scope.Button==b)).Select(b=>b.macro).SequenceEqual(untouched),"unselected buttons unchanged");
                await RedoEdit(); LayoutCheck(rows.All(r=>r.Macro=="show !!BulkFixture"),"redo batch"); await UndoEdit();
                int rowUndo=undoEdits.Count; var cell=functionRows.First(); cell.Macro="show !!CellFixture"; LayoutCheck(undoEdits.Count==rowUndo+1 && cell.Scope.Button.macro=="show !!CellFixture","cell binding setter records edit"); await UndoEdit();
                SetFunctionExpanded(true); LayoutCheck(document.TableExpanded && undoEdits.Count==rowUndo,"table view shares same undo"); Ui.Capture(this,evidence); SetFunctionExpanded(false);
                await Flush(); string path=Project.origin.sourcePath; var instance=Project; int beforeTabs=workspaceViews.Count; await OpenUicLocation(path.ToUpperInvariant()); LayoutCheck(Project==instance && workspaceViews.Count==beforeTabs,"path navigation preserves existing UIC");
                await Deploy(true); var registry=activeView; registry.Page.ExtraMons.Add(mon); await registry.Page.Reload(); registry.Page.SelectPath(path); await Deploy(true); LayoutCheck(activeView==registry && workspaceViews.Count(v=>v.Kind=="registration")==1,"existing registration tab reused");
                var scan=await Api.Post<RegistrationScan>("registration/scan",new{monPaths=new[]{mon}}); var refs=scan.rows.Where(r=>StudioToolPage.Same(r.uicPath,path)).ToList();
                LayoutCheck(refs.Count>=3 && refs.Select(r=>r.xmlPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()==refs.Count,"real XML registration scan without duplicates");
                LayoutCheck(refs.All(r=>r.version!="3.1" && r.version!="12.1 SP4"),"unknown fixture version does not use design sample");
                Ui.Capture(this,Path.ChangeExtension(evidence,"registration.png")); ShowCurrentDocument();
            }
            finally { ShowCurrentDocument(); if(activeDocument!=document) SelectWorkspaceView(workspaceViews.First(v=>v.Kind=="document"&&v.Document==document)); Load(backup); SetFunctionExpanded(false); }
        }
    }
}
