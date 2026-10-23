[CmdletBinding()]
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FrameProbe {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
  public static void Foreground(IntPtr h) {
    uint current=GetCurrentThreadId(), front=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
    bool attached=current!=front && AttachThreadInput(current,front,true);
    try { SetForegroundWindow(h); } finally { if(attached) AttachThreadInput(current,front,false); }
  }
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect rect);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h, IntPtr region);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
  [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
  [DllImport("gdi32.dll")] public static extern int GetRgnBox(IntPtr region, out Rect rect);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out int value, int size);
  [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] public static extern int Bounds(IntPtr h, int attribute, out Rect value, int size);
  [DllImport("dwmapi.dll")] public static extern int DwmFlush();
}
'@
[FrameProbe]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory for native frame evidence.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$exe = (Get-Item -LiteralPath $ExecutablePath).FullName
$screen = [Windows.Forms.Screen]::PrimaryScreen
$background = [Drawing.Color]::FromArgb(232,237,244)
$backdrop = [Windows.Forms.Form]::new()
$backdrop.FormBorderStyle = 'None'; $backdrop.StartPosition = 'Manual'; $backdrop.Bounds = $screen.Bounds
$backdrop.BackColor = $background; $backdrop.ShowInTaskbar = $false
$receipt = [ordered]@{ passed=$false; os=[Environment]::OSVersion.Version.ToString(); executableSha256=(Get-FileHash -LiteralPath $exe).Hash; captures=@(); scope='Published WPF executable; native screen capture including exterior. Isolated user state; no AVEVA execution.' }
function Settle { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 450; [FrameProbe]::DwmFlush() | Out-Null }
function Visible-Bounds([IntPtr]$Handle) {
    $rect=[FrameProbe+Rect]::new(); $null=[FrameProbe]::Bounds($Handle,9,[ref]$rect,16)
    $region=[FrameProbe]::CreateRectRgn(0,0,0,0)
    try {
        if ([FrameProbe]::GetWindowRgn($Handle,$region) -ne 0) {
            $outer=[FrameProbe+Rect]::new(); $null=[FrameProbe]::GetWindowRect($Handle,[ref]$outer)
            $null=[FrameProbe]::GetRgnBox($region,[ref]$rect)
            $rect.Left+=$outer.Left; $rect.Right+=$outer.Left; $rect.Top+=$outer.Top; $rect.Bottom+=$outer.Top
        }
    } finally { [FrameProbe]::DeleteObject($region) | Out-Null }
    return $rect
}
function Capture-Frame([IntPtr]$Handle,[string]$Name,[bool]$Normal) {
    [FrameProbe]::Foreground($Handle); Settle
    if ([FrameProbe]::GetForegroundWindow() -ne $Handle) { $process.Refresh(); throw ('Target is not foreground; screen capture refused. Exited='+$process.HasExited+' ExitCode='+$(if ($process.HasExited) {$process.ExitCode} else {'running'})+' Handle='+$Handle+' CurrentMain='+$process.MainWindowHandle) }
    $rect = Visible-Bounds $Handle
    $margin=if($Normal){36}else{0}
    $left=[Math]::Max($screen.Bounds.Left,$rect.Left-$margin); $top=[Math]::Max($screen.Bounds.Top,$rect.Top-$margin)
    $right=[Math]::Min($screen.Bounds.Right,$rect.Right+$margin); $bottom=[Math]::Min($screen.Bounds.Bottom,$rect.Bottom+$margin)
    $bitmap=[Drawing.Bitmap]::new($right-$left,$bottom-$top)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($left,$top,0,0,$bitmap.Size)
        $bitmap.Save((Join-Path $output ($Name+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        $corner=0; $rendering=0; $null=[FrameProbe]::DwmGetWindowAttribute($Handle,33,[ref]$corner,4)
        $null=[FrameProbe]::DwmGetWindowAttribute($Handle,1,[ref]$rendering,4)
        $region=[FrameProbe]::CreateRectRgn(0,0,0,0)
        try { $regionKind=[FrameProbe]::GetWindowRgn($Handle,$region) } finally { [FrameProbe]::DeleteObject($region) | Out-Null }
        $shadow=@(); $rounding=@()
        if ($Normal) {
            foreach ($distance in @(2,5,10,18,28)) {
                $pixel=$bitmap.GetPixel([int](($rect.Left+$rect.Right)/2)-$left,$rect.Bottom+$distance-$top)
                $shadow += @{distance=$distance; rgb=@($pixel.R,$pixel.G,$pixel.B); darkerThanBackdrop=($pixel.R -lt $background.R-1)}
            }
            foreach ($offset in @(0,1,2,3,4,6,10,14)) {
                $pixel=$bitmap.GetPixel($rect.Left+$offset-$left,$rect.Top+$offset-$top)
                $rounding += @{offset=$offset;rgb=@($pixel.R,$pixel.G,$pixel.B)}
            }
            if ($corner -ne 2 -or $rendering -ne 1 -or $regionKind -ne 0) { throw 'Native DWM rounding/frame contract is not active.' }
            if (@($shadow | Where-Object darkerThanBackdrop).Count -lt 2) { throw 'No measurable exterior shadow on the neutral backdrop.' }
        }
        return @{name=$Name;normal=$Normal;cornerPreference=$corner;nonClientRendering=$rendering;windowRegion=$regionKind;bounds=$rect;shadowSamples=$shadow;cornerSamples=$rounding}
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
$process=$null
try {
    $backdrop.Show(); $backdrop.Activate(); Settle
    foreach ($theme in @('Light','Dark')) {
        $backdrop.Activate(); [FrameProbe]::Foreground($backdrop.Handle); Settle
        $state=Join-Path $output ('state-'+$theme); [IO.Directory]::CreateDirectory($state) | Out-Null
        [IO.File]::WriteAllText((Join-Path $state 'desktop-display.json'),('{"theme":"'+$theme+'"}'))
        $start=[Diagnostics.ProcessStartInfo]::new($exe); $start.UseShellExecute=$false
        $start.Environment['YUZUHA_STATE_DIR']=$state
        $start.Environment['YUZUHA_USER_DIR']=Join-Path $output ('user-'+$theme)
        $start.Environment['YUZUHA_KNOWLEDGE_DIR']=Join-Path $output ('knowledge-'+$theme)
        $start.Environment['YUZUHA_WEBAPPS_DIR']=Join-Path $output ('apps-'+$theme)
        $process=[Diagnostics.Process]::Start($start)
        $deadline=[DateTime]::UtcNow.AddSeconds(25)
        do { Settle; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and !$process.HasExited -and [DateTime]::UtcNow -lt $deadline)
        if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw 'Published desktop did not create its window.' }
        $handle=$process.MainWindowHandle
        $width=[Math]::Min(2160,$screen.WorkingArea.Width-160); $height=[Math]::Min(1050,$screen.WorkingArea.Height-140)
        $x=$screen.WorkingArea.Left+[int](($screen.WorkingArea.Width-$width)/2); $y=$screen.WorkingArea.Top+50
        [FrameProbe]::SetWindowPos($handle,[IntPtr]::Zero,$x,$y,$width,$height,0x0040) | Out-Null
        Settle
        $receipt.captures += Capture-Frame $handle ($theme+'-normal') $true
        # Hit-test the actual resize area and custom caption before changing state.
        $point=[IntPtr]((($y+200) -shl 16) -bor ($x+2))
        $edge=[FrameProbe]::SendMessage($handle,0x84,[IntPtr]::Zero,$point).ToInt32()
        if ($edge -ne 10) { throw "Left edge lost native resize behavior: $edge" }
        $point=[IntPtr]((($y+22) -shl 16) -bor ($x+400))
        $caption=[FrameProbe]::SendMessage($handle,0x84,[IntPtr]::Zero,$point).ToInt32()
        if ($caption -ne 2) { throw "Caption lost native drag behavior: $caption" }
        [FrameProbe]::ShowWindow($handle,3) | Out-Null; Settle
        $receipt.captures += Capture-Frame $handle ($theme+'-maximized') $false
        $maxRect=Visible-Bounds $handle
        if ($maxRect.Bottom -gt $screen.WorkingArea.Bottom+1) {
            $outer=[FrameProbe+Rect]::new(); $null=[FrameProbe]::GetWindowRect($handle,[ref]$outer)
            throw ('Maximized window exceeds work area. DWM='+($maxRect|ConvertTo-Json -Compress)+' outer='+($outer|ConvertTo-Json -Compress)+' work='+$screen.WorkingArea)
        }
        [FrameProbe]::ShowWindow($handle,9) | Out-Null; Settle
        $receipt.captures += Capture-Frame $handle ($theme+'-restored') $true
        [FrameProbe]::ShowWindow($handle,6) | Out-Null; Settle
        if (![FrameProbe]::IsIconic($handle)) { throw 'Minimize failed.' }
        [FrameProbe]::ShowWindow($handle,9) | Out-Null; Settle
        [FrameProbe]::SetWindowPos($handle,[IntPtr]::Zero,$x+35,$y+25,$width-90,$height-60,0x0040) | Out-Null; Settle
        $receipt.captures += Capture-Frame $handle ($theme+'-resized') $true
        $receipt[$theme+'HitTests']=@{resize=$edge;caption=$caption}
        $process.CloseMainWindow() | Out-Null
        if (!$process.WaitForExit(5000)) { throw 'Desktop did not close normally.' }
        $process.Dispose(); $process=$null
    }
    $receipt.passed=$true
} finally {
    if ($process -and !$process.HasExited) { $process.CloseMainWindow() | Out-Null; if (!$process.WaitForExit(3000)) { $process.Kill() } }
    $backdrop.Close(); $backdrop.Dispose()
    $receipt.finishedUtc=[DateTime]::UtcNow.ToString('o')
    $receipt | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $output 'native-window.json') -Encoding utf8
}
Write-Output "Native window evidence: $output"
