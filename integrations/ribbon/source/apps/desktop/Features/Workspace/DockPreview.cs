using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        [StructLayout(LayoutKind.Sequential)] struct CursorPoint { public int X,Y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
        sealed class DockPreviewAdorner : Adorner
        {
            readonly WorkspaceView view; readonly MainWindow owner;
            public DockPreviewAdorner(MainWindow main,WorkspaceView target):base(main.dockPages){owner=main;view=target;IsHitTestVisible=false;}
            protected override void OnRender(DrawingContext dc)
            {
                double width=AdornedElement.RenderSize.Width,height=AdornedElement.RenderSize.Height;
                if(width<30||height<70)return;
                var blue=new SolidColorBrush(Color.FromRgb(49,89,232));var wash=new SolidColorBrush(Color.FromArgb(28,49,89,232));
                dc.DrawRoundedRectangle(wash,new Pen(blue,2),new Rect(8,48,width-16,height-56),10,10);
                var point=owner.DockSlot(view).TranslatePoint(new Point(),owner.dockPages);var tab=Rect.Intersect(new Rect(point.X,point.Y,owner.DockSlot(view).ActualWidth,owner.DockSlot(view).ActualHeight),new Rect(0,0,width,44));
                if(!tab.IsEmpty&&tab.Width>2)dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(231,238,255)),new Pen(blue,2),tab,7,7);
                if(!tab.IsEmpty&&tab.Width>45) { var caption=new FormattedText(owner.ToolCaption(view),CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(owner.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),11,blue,VisualTreeHelper.GetDpi(owner).PixelsPerDip);caption.MaxTextWidth=Math.Max(10,tab.Width-18);caption.Trimming=TextTrimming.CharacterEllipsis;dc.DrawText(caption,new Point(tab.Left+9,tab.Top+10)); }
                string label="松开后停靠到底部「"+owner.ToolCaption(view)+"」页签";
                var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(owner.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),13,blue,VisualTreeHelper.GetDpi(owner).PixelsPerDip);
                var hint=new FormattedText("移出高亮区域可继续浮动",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(owner.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),11,Ui.Muted,VisualTreeHelper.GetDpi(owner).PixelsPerDip);
                double card=Math.Min(width-32,Math.Max(330,text.Width+40)),left=(width-card)/2;
                dc.DrawRoundedRectangle(Brushes.White,new Pen(new SolidColorBrush(Color.FromRgb(185,203,246)),1),new Rect(left,66,card,64),10,10);
                dc.DrawText(text,new Point(left+18,78));dc.DrawText(hint,new Point(left+18,101));
            }
        }
        DockPreviewAdorner dockPreview;AdornerLayer dockPreviewLayer;WorkspaceView previewTool;
        bool PointerInDockZone(WorkspaceView view)
        {
            if(view.Floating==null||view.CancelDockDrag||AnyToolWorking||!IsVisible||WindowState==WindowState.Minimized)return false;
            CursorPoint cursor;if(!GetCursorPos(out cursor))return false;
            var point=dockPages.PointFromScreen(new Point(cursor.X,cursor.Y));
            return point.X>=0&&point.X<=dockPages.ActualWidth&&point.Y>=-8&&point.Y<=44;
        }
        void ShowDockPreview(WorkspaceView view)
        {
            if(previewTool==view&&dockPreview!=null){dockPreview.InvalidateVisual();return;}
            HideDockPreview();dockPreviewLayer=AdornerLayer.GetAdornerLayer(dockPages);if(dockPreviewLayer==null)return;
            previewTool=view;dockPreview=new DockPreviewAdorner(this,view);dockPreviewLayer.Add(dockPreview);
            if(view.Floating!=null)view.Floating.Opacity=0.70;
        }
        void HideDockPreview()
        {
            if(previewTool!=null&&previewTool.Floating!=null)previewTool.Floating.Opacity=1;
            if(dockPreview!=null&&dockPreviewLayer!=null)dockPreviewLayer.Remove(dockPreview);
            dockPreview=null;dockPreviewLayer=null;previewTool=null;
        }
        void UpdateDockPreview(WorkspaceView view)
        {
            if(PointerInDockZone(view))ShowDockPreview(view);else HideDockPreview();
        }
    }
}
