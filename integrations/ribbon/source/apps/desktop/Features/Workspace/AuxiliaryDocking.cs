using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        double? beforeTileMinWidth, beforeTileMinHeight;
        string ToolCaption(WorkspaceView view) { return view.HeaderLabel == null ? view.Kind : view.HeaderLabel.Text; }
        void WireAuxiliaryTab(TabItem tab)
        {
            Point down = new Point(); bool tracking = false;
            tab.PreviewMouseLeftButtonDown += (s,e) => {
                if (Ui.Ancestor<Button>(e.OriginalSource as DependencyObject) != null || AnyToolWorking || e.GetPosition(tab).Y > 38) return;
                var view = tab.Tag as WorkspaceView; if (view == null) return;
                if (view.Floating != null) { view.Floating.Activate(); e.Handled = true; return; }
                SelectWorkspaceView(view); down = tab.PointToScreen(e.GetPosition(tab)); tracking = true; tab.CaptureMouse(); e.Handled = true;
            };
            tab.PreviewMouseLeftButtonUp += (s,e) => { tracking = false; if (tab.IsMouseCaptured) tab.ReleaseMouseCapture(); };
            tab.LostMouseCapture += (s,e) => tracking = false;
            tab.PreviewMouseMove += (s,e) => {
                if (!tracking || e.LeftButton != MouseButtonState.Pressed || AnyToolWorking) return;
                var at = tab.PointToScreen(e.GetPosition(tab)); if (Math.Abs(at.Y-down.Y) < 22) return;
                tracking = false; tab.ReleaseMouseCapture(); var view = tab.Tag as WorkspaceView; FloatAuxiliary(view, at);
                if (view != null && view.Floating != null && Mouse.LeftButton == MouseButtonState.Pressed) { try { view.Floating.DragMove(); } catch (InvalidOperationException) { } TryDockAtTabs(view); }
            };
            var menu = new ContextMenu();
            var detach = new MenuItem { Header = "拖出为辅助窗 ↗" }; detach.Click += (s,e) => FloatAuxiliary(tab.Tag as WorkspaceView); menu.Items.Add(detach);
            var tile = new MenuItem { Header = "单屏并排" }; tile.Click += (s,e) => { var view = tab.Tag as WorkspaceView; FloatAuxiliary(view); if (view != null) TileAuxiliary(view); }; menu.Items.Add(tile);
            var dock = new MenuItem { Header = "停靠回主窗口" }; dock.Click += (s,e) => DockAuxiliary(tab.Tag as WorkspaceView, true); menu.Items.Add(dock);
            menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("收起 / 展开停靠区", () => { ToggleDockCollapsed(); return Task.FromResult(0); }));
            menu.Opened += (s,e) => { var view = tab.Tag as WorkspaceView; detach.IsEnabled = view != null && !AnyToolWorking && view.Floating == null; tile.IsEnabled = view != null && !AnyToolWorking; dock.IsEnabled = view != null && !AnyToolWorking && view.Floating != null; };
            tab.ContextMenu = menu;
        }
        void EnsureAuxiliaryHost(WorkspaceView view)
        {
            if (view.Kind == "document" || view.AuxiliaryBody != null || view.Tab.Content == null) return;
            var content = view.Tab.Content as UIElement; if (content == null) return;
            view.Tab.Content = null;
            var body = new DockPanel();
            var bar = new DockPanel { Height = view.Kind == "history" || view.Kind == "registration" ? 32 : 0, Background = new SolidColorBrush(Color.FromRgb(248,250,253)) };
            var pop = Ui.Sync("拖出 ↗", () => FloatAuxiliary(view)); pop.Height = 26; pop.MinHeight = 0; pop.Margin = new Thickness(6,2,12,2); pop.Padding = new Thickness(10,2,10,2); DockPanel.SetDock(pop,Dock.Right); bar.Children.Add(pop);
            var fold = Ui.Sync("收起", ToggleDockCollapsed); fold.Height = 26; fold.MinHeight = 0; fold.Margin = new Thickness(0,2,4,2); DockPanel.SetDock(fold,Dock.Right); bar.Children.Add(fold);
            var enlarge = Ui.Sync("放大停靠区", () => SetDockHeight(dockHeight > 320 ? 294 : 430)); enlarge.Height = 26; enlarge.MinHeight = 0; enlarge.Margin = new Thickness(0,2,4,2); DockPanel.SetDock(enlarge,Dock.Right); bar.Children.Add(enlarge);
            if (view.Kind == "history" || view.Kind == "registration") { var back = Ui.Button("‹ 返回登记查询", () => Guard(() => OpenRegistrationExplorer())); back.Height = 26; back.MinHeight = 0; DockPanel.SetDock(back,Dock.Left); bar.Children.Add(back); }
            var grip = Ui.Text("⠿  " + ToolCaption(view),11,Ui.Muted); grip.Margin = new Thickness(18,7,0,0); bar.Children.Add(grip);
            DockPanel.SetDock(bar,Dock.Top); body.Children.Add(bar); body.Children.Add(content);
            view.AuxiliaryBody = body; view.AuxiliaryGrip = bar; view.Tab.Content = body;
        }
        void FloatAuxiliary(WorkspaceView view, Point? pointer = null)
        {
            if (view == null || view.Kind == "document" || AnyToolWorking) return;
            if (view.Floating != null) { view.Floating.Activate(); return; }
            EnsureAuxiliaryHost(view); if (view.AuxiliaryBody == null) return;
            var area = SystemParameters.WorkArea;
            var window = new Window { Owner=this,Title=ToolCaption(view)+" · 辅助窗",Width=Math.Min(760,area.Width-24),Height=Math.Min(760,area.Height-24),MinWidth=420,MinHeight=300,FontFamily=FontFamily,FontSize=12,Background=Background,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual };
            window.Left=Math.Max(area.Left,Math.Min(Left+Width*.5,area.Right-window.Width)); window.Top=Math.Max(area.Top,Math.Min(Top+45,area.Bottom-window.Height));
            if(pointer.HasValue) { var point=PointFromScreen(pointer.Value); window.Left=Math.Max(area.Left,Math.Min(Left+point.X-160,area.Right-window.Width)); window.Top=Math.Max(area.Top,Math.Min(Top+point.Y-18,area.Bottom-window.Height)); }
            if(view.LastFloatingGeometry!=null)ApplyGeometry(window,view.LastFloatingGeometry,420,300);
            var slot = DockSlot(view); if (slot.Tag == view) slot.Content = null;
            view.Tab.Content=null; view.Floating=window; view.AuxiliaryGrip.Visibility=Visibility.Collapsed;
            var root=new DockPanel(); var header=new DockPanel{Height=38,Background=Ui.Blue};
            var controls=new StackPanel{Orientation=Orientation.Horizontal}; DockPanel.SetDock(controls,Dock.Right);header.Children.Add(controls);
            var dock=Ui.Sync("↙ 停靠",()=>DockAuxiliary(view,true));var tile=Ui.Sync("单屏并排",()=>TileAuxiliary(view));
            foreach(var button in new[]{dock,tile}) { button.Style=(Style)Application.Current.FindResource("CaptionButton");button.Width=82;button.Height=38;button.MinHeight=0;button.Padding=new Thickness(5,0,5,0);button.Margin=new Thickness(0);controls.Children.Add(button); }
            WindowChrome.SetIsHitTestVisibleInChrome(controls,true);
            var title=Ui.Text("  "+ToolCaption(view)+" · 辅助窗",12,Brushes.White);title.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(title);
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            view.FloatingHost=new ContentControl{Content=view.AuxiliaryBody};root.Children.Add(view.FloatingHost);window.Content=root;
            var placeholder=new StackPanel{Margin=new Thickness(16)};placeholder.Children.Add(Ui.Text("此工具已移到辅助窗",17));placeholder.Children.Add(Ui.Sync("激活辅助窗",()=>window.Activate()));placeholder.Children.Add(Ui.Sync("停靠回主窗口",()=>DockAuxiliary(view,true)));if (slot.Tag == view) slot.Content=placeholder;
            window.Closing+=(s,e)=>{if(view.Floating!=window||closing)return;e.Cancel=true;if(AnyToolWorking){status.Text="请等待当前操作完成后停靠";return;}Dispatcher.BeginInvoke(new Action(()=>DockAuxiliary(view,false)));};
            window.LocationChanged+=(s,e)=>{QueueLayoutSave();if(view.MovingAuxiliary)UpdateDockPreview(view);};window.SizeChanged+=(s,e)=>QueueLayoutSave();window.StateChanged+=(s,e)=>QueueLayoutSave();
            window.SourceInitialized+=(s,e)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source.AddHook((IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)=>{
                if(msg==0x0231){view.CancelDockDrag=false;view.MovingAuxiliary=false;}
                if(msg==0x0216){view.MovingAuxiliary=true;UpdateDockPreview(view);}
                if(msg==0x0214){view.MovingAuxiliary=false;HideDockPreview();}
                if(msg==0x001F||msg==0x0100&&w.ToInt32()==27){view.CancelDockDrag=true;HideDockPreview();}
                if(msg==0x0232){bool moved=view.MovingAuxiliary;view.MovingAuxiliary=false;Dispatcher.BeginInvoke(new Action(()=>{if(moved)TryDockAtTabs(view);else HideDockPreview();QueueLayoutSave();}));}
                return IntPtr.Zero;});};
            Chrome.Install(window,header);
            if(activeView==view) { var other=workspaceViews.FirstOrDefault(v=>v.Kind=="document")??workspaceViews.FirstOrDefault(v=>v!=view&&v.Floating==null);if(other!=null)SelectWorkspaceView(other); }
            window.Show(); if(!restoringLayout)window.Activate(); QueueLayoutSave();
        }
        void TryDockAtTabs(WorkspaceView view)
        {
            bool dock=PointerInDockZone(view);HideDockPreview();if(dock)DockAuxiliary(view,true);
        }
        void DockAuxiliary(WorkspaceView view,bool select)
        {
            if(view==null||view.Floating==null||AnyToolWorking)return;
            HideDockPreview();var window=view.Floating;view.LastFloatingGeometry=CaptureGeometry(window);view.Floating=null;view.FloatingHost.Content=null;view.FloatingHost=null;view.AuxiliaryGrip.Visibility=Visibility.Visible;PresentDockedBody(view);window.Close();
            if(!workspaceViews.Any(v=>v.Floating!=null)){if(beforeTileMinWidth.HasValue)MinWidth=beforeTileMinWidth.Value;if(beforeTileMinHeight.HasValue)MinHeight=beforeTileMinHeight.Value;beforeTileMinWidth=null;beforeTileMinHeight=null;}
            if(select)SelectWorkspaceView(view);QueueLayoutSave();
        }
        void TileAuxiliary(WorkspaceView view)
        {
            if(view.Floating==null||AnyToolWorking)return;var area=SystemParameters.WorkArea;var window=view.Floating;
            if(!beforeTileMinWidth.HasValue){beforeTileMinWidth=MinWidth;beforeTileMinHeight=MinHeight;}
            WindowState=WindowState.Normal;window.WindowState=WindowState.Normal;
            if(area.Width>=1160) { MinWidth=Math.Min(MinWidth,740);double left=Math.Max(740,Math.Min(area.Width-420,area.Width*.60));Left=area.Left;Top=area.Top;Width=left;Height=area.Height;window.Left=area.Left+left;window.Top=area.Top;window.Width=area.Width-left;window.Height=area.Height; }
            else { MinHeight=Math.Min(MinHeight,360);double top=Math.Max(360,area.Height*.55);Left=area.Left;Top=area.Top;Width=area.Width;Height=top;window.Left=area.Left;window.Top=area.Top+top;window.Width=area.Width;window.Height=Math.Max(300,area.Height-top); }
        }
        void CloseAuxiliaries()
        {
            HideDockPreview();
            foreach(var view in workspaceViews.Where(v=>v.Floating!=null).ToList()){var window=view.Floating;view.Floating=null;view.FloatingHost.Content=null;view.FloatingHost=null;window.Close();}
        }
    }
}
