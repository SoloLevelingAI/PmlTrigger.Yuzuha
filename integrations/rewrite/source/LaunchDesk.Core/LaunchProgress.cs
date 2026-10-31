namespace LaunchDesk.Core;

public enum LaunchPhase { Checking, Starting, Running, Unconfirmed, Failed, Exited, Cancelled }
public sealed record LaunchObservation(int AliveProcesses,int ReadyWindows,bool RootExited,int? ExitCode,DateTimeOffset CheckedAt);
public sealed record LaunchProgress(LaunchPhase Phase,string Title,string Detail)
{
 public bool Busy=>Phase is LaunchPhase.Checking or LaunchPhase.Starting;
 public string Color=>Phase switch{LaunchPhase.Running=>"#238266",LaunchPhase.Failed=>"#BF5260",LaunchPhase.Unconfirmed=>"#AF781C",LaunchPhase.Checking or LaunchPhase.Starting=>"#4E5DDB",_=>"#6D7890"};
 public string Background=>Phase switch{LaunchPhase.Running=>"#EAF6EF",LaunchPhase.Failed=>"#FBEFF1",LaunchPhase.Unconfirmed=>"#FFF5DF",LaunchPhase.Checking or LaunchPhase.Starting=>"#EEF0FF",_=>"#F1F3F9"};
 public string Icon=>Phase switch{LaunchPhase.Running=>"Check",LaunchPhase.Failed or LaunchPhase.Unconfirmed=>"Warning",LaunchPhase.Checking or LaunchPhase.Starting=>"Refresh",_=>"Info"};
 public static LaunchProgress Checking()=>new(LaunchPhase.Checking,"正在检查启动条件","正在检查程序入口与许可连接…");
 public static LaunchProgress Submitted(int pid)=>new(LaunchPhase.Starting,"正在启动",$"已提交启动，正在等待应用窗口 · PID {pid}");
 public static LaunchProgress Observe(LaunchProgress previous,LaunchObservation observation,DateTimeOffset started)
 {
  if(observation.ReadyWindows>0)return new(LaunchPhase.Running,"已启动 · 窗口已出现","已检测到应用窗口；项目登录和许可签出请以应用内结果为准。");
  if(previous.Phase==LaunchPhase.Running&&observation.AliveProcesses==0)return new(LaunchPhase.Exited,"已退出","本次启动的应用进程已全部退出。");
  if(observation.AliveProcesses==0&&observation.RootExited&&observation.ExitCode is not null and not 0)
   return new(LaunchPhase.Failed,"启动失败",$"启动进程异常退出，退出码 {observation.ExitCode}。请检查应用报错、项目和许可设置。");
  if(previous.Phase==LaunchPhase.Running)return previous;
  var elapsed=observation.CheckedAt-started;
  if(elapsed>=TimeSpan.FromSeconds(90))return new(LaunchPhase.Unconfirmed,"尚未确认启动",observation.AliveProcesses>0?"进程仍在运行，但尚未检测到应用窗口。可查看应用提示，窗口出现后状态会自动更新。":"启动入口已退出，尚未发现模块窗口。请检查应用是否报错或通过其他进程启动。");
  return new(LaunchPhase.Starting,"正在启动",observation.RootExited&&observation.AliveProcesses>0?"启动入口已交接，正在等待模块进程的窗口…":observation.RootExited?"启动入口已退出，继续等待模块窗口…":$"正在等待应用窗口 · 已等待 {Math.Max(0,(int)elapsed.TotalSeconds)} 秒");
 }
}
