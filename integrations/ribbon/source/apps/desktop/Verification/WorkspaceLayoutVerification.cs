using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public async Task VerifyLayoutRestartPhase(string phase,string result)
        {
            var area=SystemParameters.WorkArea;
            if(phase=="seed")
            {
                Left=area.Left+24;Top=area.Top+24;Width=Math.Min(1100,area.Width-48);Height=Math.Min(720,area.Height-48);
                await OpenFunctionLocator();var tool=activeView;FloatAuxiliary(tool);
                tool.Floating.Left=area.Left+80;tool.Floating.Top=area.Top+90;tool.Floating.Width=600;tool.Floating.Height=420;
                await Library();ShowCurrentDocument();SetDockHeight(200);ToggleDockCollapsed();SaveWorkspaceLayoutNow();
                LayoutCheck(layoutSaveError==null,"layout atomic save: "+layoutSaveError);
                var saved=Json.Read<WorkspaceLayout>(File.ReadAllText(layoutPath));
                LayoutCheck(saved.Tools.Single(t=>t.Kind=="locator").Floating&&!saved.Tools.Single(t=>t.Kind=="history").Floating,"persist floating and docked states");
            }
            else if(phase=="restore")
            {
                var saved=Json.Read<WorkspaceLayout>(File.ReadAllText(layoutPath));var expected=saved.Tools.Single(t=>t.Kind=="locator");
                var tool=workspaceViews.Single(v=>v.Kind=="locator");var history=workspaceViews.Single(v=>v.Kind=="history");
                LayoutCheck(tool.Floating!=null&&history.Floating==null,"fresh process restores floating and docked tools");
                var fit=FitGeometry(expected.Geometry,WorkAreas(),420,300);var window=tool.Floating;
                LayoutCheck(Math.Abs(window.Left-fit.Left)<2&&Math.Abs(window.Top-fit.Top)<2&&Math.Abs(window.Width-fit.Width)<2&&Math.Abs(window.Height-fit.Height)<2,"fresh process restores saved auxiliary geometry");
                var mainFit=FitGeometry(saved.Main,WorkAreas(),MinWidth,MinHeight);
                LayoutCheck(Math.Abs(Left-mainFit.Left)<2&&Math.Abs(Top-mainFit.Top)<2,"main position restored");
                LayoutCheck(saved.DockCollapsed && dockCollapsed && saved.DockHeight >= 160, "restart restores dock height and collapsed state");
                ToggleDockCollapsed();
                ShowDockPreview(tool);await WorkspaceIdle();
                LayoutCheck(dockPreview!=null&&!dockPreview.IsHitTestVisible&&previewTool==tool,"non-interactive target preview visible");
                LayoutCheck(dockPreview.AdornedElement == dockPages && dockPages.TranslatePoint(new Point(),this).Y > ribbonPreview.TranslatePoint(new Point(),this).Y + 100, "preview targets bottom tools only");
                Ui.Capture(this,Path.ChangeExtension(result,"preview.png"));HideDockPreview();LayoutCheck(dockPreview==null&&tool.Floating.Opacity==1,"preview clears without docking or leaving window dimmed");
                tool.Floating.Close();await WorkspaceIdle();LayoutCheck(tool.Floating==null,"closing auxiliary docks");SaveWorkspaceLayoutNow();
                LayoutCheck(layoutSaveError==null,"redock atomic save: "+layoutSaveError);
                LayoutCheck(!Json.Read<WorkspaceLayout>(File.ReadAllText(layoutPath)).Tools.Single(t=>t.Kind=="locator").Floating,"redocked state reaches disk");
                var offscreen=FitGeometry(new WindowGeometry{Left=99999,Top=-99999,Width=8000,Height=5000},new[]{new Rect(-1200,0,1200,900),new Rect(0,0,1600,900)},420,300);
                LayoutCheck(offscreen.Left>=-1200&&offscreen.Top>=0&&offscreen.Right<=1600&&offscreen.Bottom<=900,"offscreen oversized geometry clamped");
                var negative=FitGeometry(new WindowGeometry{Left=-1100,Top=20,Width=600,Height=420},new[]{new Rect(-1200,0,1200,900),new Rect(0,0,1600,900)},420,300);
                LayoutCheck(negative.Left==-1100,"valid negative monitor coordinates preserved");
                LayoutCheck(!ValidGeometry(new WindowGeometry{Left=Double.NaN,Top=0,Width=600,Height=420}),"invalid geometry rejected");
            }
            else if(phase=="docked")
            {
                LayoutCheck(workspaceViews.Count(v=>v.Kind=="locator")==1&&workspaceViews.All(v=>v.Floating==null),"second restart restores docked state without extra windows");
            }
            else throw new InvalidOperationException("Unknown layout verification phase");
            File.WriteAllText(result,Json.Write(new{passed=true,phase,layoutPath,visibleAuxiliaries=workspaceViews.Count(v=>v.Floating!=null)}));
            closing=true;
        }
    }
}
