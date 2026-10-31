using System;using System.IO;using System.Text;using System.Linq;using System.Diagnostics;using System.Threading;using System.Management;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Runtime.InteropServices;
namespace LaunchDesk.Development.RemoteProjectLab.Host
{
class HostNativeFnfTest {
 [DllImport("kernel32.dll")]static extern bool SetStdHandle(int id,IntPtr handle);
 static readonly string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fnf7-native-20261028");
 static int Main(){LaunchDesk.Development.RemoteProjectLab.DevelopmentSwitch.RequireEnabled();Directory.CreateDirectory(Root);string secret=Console.ReadLine();if(String.IsNullOrEmpty(secret)||secret.Contains('"'))throw new Exception("Unsupported credential");var before=Process.GetProcessesByName("des").Select(p=>p.Id).ToArray();string[] tokens={"PROD","E3D","init",@"C:\Program Files (x86)\AVEVA\Everything3D3.1\launch.init","mar","SYSTEM/"+secret,"/ALL_NO_MDS","design"};
 var psi=new ProcessStartInfo(@"C:\Program Files (x86)\AVEVA\Everything3D3.1\mon.exe",String.Join(" ",tokens.Select(t=>t.Any(Char.IsWhiteSpace)?"\""+t+"\"":t))){UseShellExecute=false,WorkingDirectory=@"C:\Users\Public\Documents\AVEVA\USERDATA",CreateNoWindow=false};psi.EnvironmentVariables["AVEVA_LICENSE_SERVER_LIST"]="4545@192.168.244.10";psi.EnvironmentVariables["LSHOST"]="192.168.244.10";psi.EnvironmentVariables["LSFORCEHOST"]="192.168.244.10";
 // A WPF caller has no console pipeline. Replicate its native handles without redirecting vendor IPC/control streams.
 SetStdHandle(-10,IntPtr.Zero);SetStdHandle(-11,IntPtr.Zero);SetStdHandle(-12,IntPtr.Zero);DateTime start=DateTime.UtcNow;var monitor=Process.Start(psi);var samples=new List<object>();var owned=new HashSet<int>();
 for(int i=0;i<36;i++){using(var q=new ManagementObjectSearcher("SELECT ProcessId,ParentProcessId,ExecutablePath FROM Win32_Process WHERE Name='des.exe'"))foreach(ManagementObject row in q.Get()){int pid=Convert.ToInt32(row["ProcessId"]);if(!before.Contains(pid)&&Convert.ToInt32(row["ParentProcessId"])==monitor.Id)owned.Add(pid);}var modules=new List<object>();foreach(int pid in owned){try{var p=Process.GetProcessById(pid);modules.Add(new{pid,exe=p.MainModule.FileName,title=p.MainWindowTitle,running=!p.HasExited});}catch{}}
 samples.Add(new{utc=DateTime.UtcNow.ToString("o"),modules});File.WriteAllText(Path.Combine(Root,"result.json"),new JavaScriptSerializer().Serialize(new{startUtc=start.ToString("o"),monitorPid=monitor.Id,workingDirectory=psi.WorkingDirectory,temp=Environment.GetEnvironmentVariable("TEMP"),tmp=Environment.GetEnvironmentVariable("TMP"),nativeStandardHandles=true,standardStreamsRedirected=false,createNoWindow=false,secretPersisted=false,ownedModuleIds=owned.ToArray(),samples}));if(monitor.HasExited&&owned.Count>0&&modules.Count==0)break;Thread.Sleep(2500);}
 return 0;}
}
}
