using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public sealed class WindowGeometry
        {
            public double Left { get; set; } public double Top { get; set; }
            public double Width { get; set; } public double Height { get; set; }
            public bool Maximized { get; set; }
        }
        public sealed class AuxiliaryLayout
        {
            public string Kind { get; set; } public bool Floating { get; set; }
            public WindowGeometry Geometry { get; set; }
        }
        public sealed class WorkspaceLayout
        {
            public int SchemaVersion { get; set; } public WindowGeometry Main { get; set; }
            public bool TemporaryMinimums { get; set; }
            public double DockHeight { get; set; } public bool DockCollapsed { get; set; } public string ComparisonScope { get; set; }
            public string ActiveTool { get; set; } public List<AuxiliaryLayout> Tools { get; set; }
        }
        readonly string layoutPath;
        string layoutSaveError;
        [System.Runtime.InteropServices.DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
        static extern bool MoveLayoutFile(string existing,string destination,int flags);
        bool layoutReady, restoringLayout;
        readonly DispatcherTimer layoutSaveTimer = new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(550) };
        void TrackWorkspaceLayout()
        {
            layoutSaveTimer.Tick+=(s,e)=>{layoutSaveTimer.Stop();SaveWorkspaceLayoutNow();};
            LocationChanged+=(s,e)=>QueueLayoutSave();SizeChanged+=(s,e)=>QueueLayoutSave();StateChanged+=(s,e)=>QueueLayoutSave();
            Closed+=(s,e)=>{layoutReady=false;layoutSaveTimer.Stop();};
        }
        void QueueLayoutSave()
        {
            if(!layoutReady||restoringLayout||closing)return;
            layoutSaveTimer.Stop();layoutSaveTimer.Start();
        }
        static WindowGeometry CaptureGeometry(Window window)
        {
            var bounds=window.WindowState==WindowState.Normal?new Rect(window.Left,window.Top,window.Width,window.Height):window.RestoreBounds;
            return new WindowGeometry {Left=bounds.Left,Top=bounds.Top,Width=bounds.Width,Height=bounds.Height,Maximized=window.WindowState==WindowState.Maximized};
        }
        static bool Finite(double value) { return !Double.IsNaN(value)&&!Double.IsInfinity(value); }
        static bool ValidGeometry(WindowGeometry value) { return value!=null&&Finite(value.Left)&&Finite(value.Top)&&Finite(value.Width)&&Finite(value.Height)&&value.Width>0&&value.Height>0; }
        Rect[] WorkAreas()
        {
            var source=PresentationSource.FromVisual(this);var transform=source==null||source.CompositionTarget==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;
            return System.Windows.Forms.Screen.AllScreens.OrderByDescending(s=>s.Primary).Select(s=>{var r=s.WorkingArea;return Rect.Transform(new Rect(r.X,r.Y,r.Width,r.Height),transform);}).ToArray();
        }
        static Rect FitGeometry(WindowGeometry saved, Rect[] areas, double minWidth, double minHeight)
        {
            if(areas.Length==0)areas=new[]{new Rect(0,0,1280,840)};
            Rect wanted=ValidGeometry(saved)?new Rect(saved.Left,saved.Top,saved.Width,saved.Height):new Rect(areas[0].Left+24,areas[0].Top+24,minWidth,minHeight);
            var area=areas.OrderByDescending(a=>{var intersection=Rect.Intersect(a,wanted);return intersection.IsEmpty?0:intersection.Width*intersection.Height;}).First();
            double width=Math.Min(area.Width,Math.Max(Math.Min(minWidth,area.Width),wanted.Width)),height=Math.Min(area.Height,Math.Max(Math.Min(minHeight,area.Height),wanted.Height));
            return new Rect(Math.Max(area.Left,Math.Min(wanted.Left,area.Right-width)),Math.Max(area.Top,Math.Min(wanted.Top,area.Bottom-height)),width,height);
        }
        void ApplyGeometry(Window window,WindowGeometry geometry,double minWidth,double minHeight)
        {
            if(!ValidGeometry(geometry))return;
            var bounds=FitGeometry(geometry,WorkAreas(),minWidth,minHeight);
            window.WindowState=WindowState.Normal;window.WindowStartupLocation=WindowStartupLocation.Manual;
            window.MinWidth=Math.Min(minWidth,bounds.Width);window.MinHeight=Math.Min(minHeight,bounds.Height);
            window.Left=bounds.Left;window.Top=bounds.Top;window.Width=bounds.Width;window.Height=bounds.Height;
            if(geometry.Maximized)window.WindowState=WindowState.Maximized;
        }
        WorkspaceLayout CaptureWorkspaceLayout()
        {
            return new WorkspaceLayout {SchemaVersion=1,DockHeight=dockCollapsed?dockHeight:dockRow.ActualHeight,DockCollapsed=dockCollapsed,ComparisonScope=functionScope.SelectedIndex==1?"open":"current",Main=CaptureGeometry(this),TemporaryMinimums=beforeTileMinWidth.HasValue,ActiveTool=activeDockView!=null&&activeDockView.Floating==null?activeDockView.Kind:null,
                Tools=workspaceViews.Where(v=>v.Kind!="document").Select(v=>new AuxiliaryLayout {Kind=v.Kind,Floating=v.Floating!=null,Geometry=v.Floating==null?v.LastFloatingGeometry:CaptureGeometry(v.Floating)}).ToList()};
        }
        void SaveWorkspaceLayoutNow()
        {
            if(!layoutReady||restoringLayout)return;
            layoutSaveTimer.Stop();string temporary=null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(layoutPath));temporary=layoutPath+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllText(temporary,Json.Write(CaptureWorkspaceLayout()),new UTF8Encoding(false));
                if(!MoveLayoutFile(temporary,layoutPath,9))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                layoutSaveError=null;
            }
            catch(Exception e){layoutSaveError=e.ToString();status.Text="窗口布局未保存："+e.Message;}
            finally {if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch{} }
        }
        async Task RestoreWorkspaceLayout()
        {
            restoringLayout=true;
            try
            {
                if(!File.Exists(layoutPath))return;
                var saved=Json.Read<WorkspaceLayout>(File.ReadAllText(layoutPath));
                if(saved==null||saved.SchemaVersion!=1)throw new InvalidDataException("布局版本不支持");
                var initial=workspaceViews.FirstOrDefault(v=>v.Kind=="document");
                if(saved.TemporaryMinimums){beforeTileMinWidth=MinWidth;beforeTileMinHeight=MinHeight;}
                ApplyGeometry(this,saved.Main,saved.TemporaryMinimums?740:MinWidth,saved.TemporaryMinimums?360:MinHeight);
                foreach(var item in (saved.Tools??new List<AuxiliaryLayout>()).Where(t=>t!=null).GroupBy(t=>t.Kind).Select(g=>g.First()))
                {
                    try
                    {
                        switch(item.Kind){case "locator":await OpenFunctionLocator();break;case "explorer":await OpenRegistrationExplorer();break;case "deployment":await Deploy(false);break;case "registration":await Deploy(true);break;case "history":await Library();break;default:continue;}
                        var view=workspaceViews.First(v=>v.Kind==item.Kind);view.LastFloatingGeometry=ValidGeometry(item.Geometry)?item.Geometry:null;
                        if(item.Floating)FloatAuxiliary(view);
                    }
                    catch(Exception e){status.Text="部分辅助页恢复失败："+e.Message;}
                }
                if (saved.DockHeight >= 160 && Finite(saved.DockHeight)) SetDockHeight(Math.Min(650, saved.DockHeight));
                selectingDock = true; functionScope.SelectedIndex = saved.ComparisonScope == "open" ? 1 : 0; selectingDock = false; await UpdateComparisonScope();
                var active=workspaceViews.FirstOrDefault(v=>v.Kind==saved.ActiveTool&&v.Floating==null)??initial;
                if(active!=null)SelectWorkspaceView(active);
                if (saved.DockCollapsed) { dockCollapsed = false; ToggleDockCollapsed(); }
                if(!workspaceViews.Any(v=>v.Floating!=null)&&beforeTileMinWidth.HasValue){MinWidth=beforeTileMinWidth.Value;MinHeight=beforeTileMinHeight.Value;beforeTileMinWidth=null;beforeTileMinHeight=null;}
                status.Text="已恢复上次停靠与浮动布局"+(Project==null?" · 后端尚未连接":"");
            }
            catch(Exception e){status.Text="布局无法恢复，使用默认布局："+e.Message;try{File.Copy(layoutPath,layoutPath+".invalid-"+Guid.NewGuid().ToString("N"));}catch{} }
            finally {restoringLayout=false;layoutReady=true;}
        }
    }
}
