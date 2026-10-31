using System;using System.IO;using System.Diagnostics;using System.Runtime.InteropServices;using System.Threading;using System.Management;using System.Linq;
namespace LaunchDesk.Development.RemoteProjectLab.Guest
{
class GuestTestAwake {
 [DllImport("kernel32.dll")]static extern uint SetThreadExecutionState(uint flags);
 static int Main(string[] args){if(Environment.MachineName!="ZHANGSH")return 2;using(var q=new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True")){var n=q.Get().Cast<ManagementObject>().ToArray();if(n.Length!=1||!String.Equals((string)n[0]["MACAddress"],"00:0C:29:8D:9E:E9",StringComparison.OrdinalIgnoreCase))return 3;}
 if(args[0]=="start"){var p=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"hold"){UseShellExecute=false,CreateNoWindow=true});File.WriteAllText(@"C:\Users\Public\Documents\LaunchDeskLab-awake.pid",p.Id.ToString());return 0;}
 if(SetThreadExecutionState(0x80000001)==0)return 4;try{Thread.Sleep(20*60*1000);}finally{SetThreadExecutionState(0x80000000);}return 0;}
}
}
