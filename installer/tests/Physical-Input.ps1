Add-Type @'
using System;
using System.Threading;
using System.Runtime.InteropServices;

public static class SetupPhysicalInput {
  [StructLayout(LayoutKind.Sequential)]
  public struct Rect { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)]
  public struct Point { public int X, Y; }
  [DllImport("user32")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
  [DllImport("user32")] static extern IntPtr WindowFromPoint(Point point);
  [DllImport("user32")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
  [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
  [DllImport("user32")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32")] static extern bool SetCursorPos(int x,int y);
  [DllImport("user32")] static extern bool GetCursorPos(out Point point);
  [DllImport("user32")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
  [DllImport("user32")] public static extern bool IsWindowEnabled(IntPtr hwnd);
  [DllImport("user32")] public static extern IntPtr GetWindow(IntPtr hwnd,uint relation);
  [DllImport("user32")] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint message,IntPtr wp,IntPtr lp,
    uint flags,uint timeout,out UIntPtr answer);

  public static void KeepFixtureVisible(IntPtr hwnd) {
    const uint preservePlacement = 0x0001 | 0x0002 | 0x0010;
    if(!SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, preservePlacement))
      throw new InvalidOperationException("Could not keep the disposable test window visible.");
  }

  public static bool Ping(IntPtr hwnd) {
    UIntPtr answer;
    return SendMessageTimeout(hwnd,0,IntPtr.Zero,IntPtr.Zero,2,500,out answer) != IntPtr.Zero;
  }
  private static void PointAt(IntPtr owner,int x,int y) {
    SetForegroundWindow(owner);
    SetCursorPos(x,y);
    Point cursor;
    GetCursorPos(out cursor);
    var target=GetAncestor(WindowFromPoint(cursor),2);
    if(Math.Abs(cursor.X-x)>20 || Math.Abs(cursor.Y-y)>20 || target!=owner)
      throw new InvalidOperationException(String.Format("Expected {0} at ({1},{2}), found {3} at ({4},{5}), foreground {6}.",
        owner,x,y,target,cursor.X,cursor.Y,GetForegroundWindow()));
  }
  public static void Click(IntPtr owner, IntPtr element, bool checkbox) {
    var dpi=SetThreadDpiAwarenessContext(new IntPtr(-4));
    try {
      Rect rect;
      if(GetAncestor(element,2)!=owner || !GetWindowRect(element,out rect) || !IsWindowEnabled(element))
        throw new InvalidOperationException("The test control is not actionable.");
      PointAt(owner,checkbox ? rect.Left+14 : (rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2);
      mouse_event(2,0,0,0,UIntPtr.Zero);
      Thread.Sleep(40);
      mouse_event(4,0,0,0,UIntPtr.Zero);
    } finally { if(dpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(dpi); }
  }
  public static int Drag(IntPtr window) {
    var dpi=SetThreadDpiAwarenessContext(new IntPtr(-4));
    try {
      Rect before, after;
      if(!IsWindowEnabled(window) || !GetWindowRect(window,out before)) return 0;
      var x=before.Left+205; var y=before.Top+36;
      PointAt(window,x,y);
      mouse_event(2,0,0,0,UIntPtr.Zero);
      try {
        Thread.Sleep(30);
        for(var step=1; step<=12; ++step) {
          SetCursorPos(x+step*8,y+step*4);
          Thread.Sleep(16);
        }
      } finally { mouse_event(4,0,0,0,UIntPtr.Zero); }
      Thread.Sleep(110);
      GetWindowRect(window,out after);
      return after.Left-before.Left;
    } finally { if(dpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(dpi); }
  }
}
'@
