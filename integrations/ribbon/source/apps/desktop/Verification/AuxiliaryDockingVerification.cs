using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public async Task VerifyAuxiliaryDocking(string evidence)
        {
            var doc=workspaceViews.First(v=>v.Kind=="document"&&v.Document==activeDocument);
            double originalLeft=Left,originalTop=Top,originalWidth=Width,originalHeight=Height,originalMinWidth=MinWidth,originalMinHeight=MinHeight;
            WorkspaceView tool=null;
            try
            {
                await OpenFunctionLocator();tool=activeView;var controller=tool.Controller;var body=tool.AuxiliaryBody;
                LayoutCheck(body!=null,"tool starts docked");
                int visible=Application.Current.Windows.Cast<Window>().Count(w=>w.IsVisible);
                FloatAuxiliary(tool);await WorkspaceIdle();
                LayoutCheck(tool.Floating!=null&&tool.Floating.IsVisible&&tool.FloatingHost.Content==body,"floating reuses existing visual tree");
                LayoutCheck(tool.AuxiliaryGrip.Visibility==Visibility.Collapsed,"floating window has a single title area");
                LayoutCheck(Application.Current.Windows.Cast<Window>().Count(w=>w.IsVisible)==visible+1&&IsEnabled,"modeless auxiliary does not block main editor");
                int tabs=workspaceViews.Count;await OpenFunctionLocator();await WorkspaceIdle();
                LayoutCheck(workspaceViews.Count==tabs&&tool.Controller==controller&&tool.Floating!=null,"repeat open activates same floating controller");
                TileAuxiliary(tool);await WorkspaceIdle();
                var area=SystemParameters.WorkArea;var auxiliary=tool.Floating;
                LayoutCheck(Left>=area.Left-1&&Top>=area.Top-1&&Left+Width<=area.Right+1&&Top+Height<=area.Bottom+1,"main fits single screen");
                LayoutCheck(auxiliary.Left>=area.Left-1&&auxiliary.Top>=area.Top-1&&auxiliary.Left+auxiliary.Width<=area.Right+1&&auxiliary.Top+auxiliary.Height<=area.Bottom+1,"auxiliary fits single screen");
                LayoutCheck(!new Rect(Left,Top,Width,Height).IntersectsWith(new Rect(auxiliary.Left+1,auxiliary.Top+1,auxiliary.Width-2,auxiliary.Height-2)),"single screen layout does not overlap");
                Ui.Capture(tool.Floating,System.IO.Path.ChangeExtension(evidence,"auxiliary.png"));
                tool.Floating.Close();await WorkspaceIdle();
                LayoutCheck(tool.Floating==null&&DockSlot(tool).Content==body&&tool.Controller==controller,"close returns tool without losing state");
                LayoutCheck(tool.AuxiliaryGrip.Visibility==Visibility.Visible,"docked drag affordance restored");
                LayoutCheck(MinWidth==originalMinWidth&&MinHeight==originalMinHeight,"redock restores main minimums");
                SelectWorkspaceView(doc);SwitchCommandView("layout");int before=Application.Current.Windows.Cast<Window>().Count(w=>w.IsVisible);
                ShowCommandDefinitionEditor(Project.buttons.First(),"macro");await WorkspaceIdle();
                LayoutCheck(CurrentCommandView=="layout"&&InspectorFields().Any(b=>Convert.ToString(b.Tag)=="Macro")&&Application.Current.Windows.Cast<Window>().Count(w=>w.IsVisible)==before,"definition editing remains inside same UIC");
                SwitchCommandView("layout");await Library();var history=activeView;
                LayoutCheck(history.Kind=="history"&&Application.Current.Windows.Cast<Window>().Count(w=>w.IsVisible)==before,"history uses workspace rather than modal");
                await Library();LayoutCheck(activeView==history&&workspaceViews.Count(v=>v.Kind=="history")==1,"history view singleton");
                SelectWorkspaceView(doc);Ui.Capture(this,evidence);
            }
            finally { if(tool!=null&&tool.Floating!=null)DockAuxiliary(tool,false);MinWidth=originalMinWidth;MinHeight=originalMinHeight;Left=originalLeft;Top=originalTop;Width=originalWidth;Height=originalHeight;SelectWorkspaceView(doc);SwitchCommandView("layout"); }
        }
    }
}
