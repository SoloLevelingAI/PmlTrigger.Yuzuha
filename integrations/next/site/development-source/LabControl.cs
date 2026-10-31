using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LaunchDesk.Development.RemoteProjectLab
{
    public static class LabControl
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "status")
                {
                    Console.WriteLine(DevelopmentSwitch.IsEnabled() ? "Development lab enabled for this machine/user." : "Development lab disabled.");
                    return 0;
                }
                if (args.Length == 2 && args[1] == "--local" && (args[0] == "enable" || args[0] == "disable"))
                {
                    DevelopmentSwitch.SetLocal(args[0] == "enable");
                    Console.WriteLine("Local switch updated; no VM was inspected or started.");
                    return 0;
                }
                if(args.Length==3 && args[0]=="fingerprint" && args[1]=="--model")
                { Console.WriteLine(LabSettings.Read(args[2]).Fingerprint()); return 0; }
                if (args.Length >= 8 && args[0] == "run" && args[1] == "--runtime" && args[3]=="--model" && args[5]=="--confirm-target")
                {
                    DevelopmentSwitch.RequireEnabled();
                    AddinGate.RequireLab();
                    string action=args[7];
                    bool simple=args.Length==8 && new[]{"probe","start","stop"}.Contains(action);
                    bool snapshot=args.Length==9 && action=="snapshot" && args[8].Length>0 && args[8].Length<=100 && args[8].All(c=>Char.IsLetterOrDigit(c)||c=='-');
                    if(!simple && !snapshot)throw new ArgumentException("Invalid explicit action.");
                    string runner=Path.Combine(Path.GetFullPath(args[2]),"LabVix.exe");
                    if(!File.Exists(runner))throw new FileNotFoundException("Build the runner from the same source revision.");
                    string modelPath=Path.GetFullPath(args[4]);
                    if(modelPath.Contains("\"") || args[6].Length!=64 || !args[6].All(Uri.IsHexDigit))throw new ArgumentException("Invalid model path or confirmation.");
                    var start=new ProcessStartInfo(runner,"--model \""+modelPath+"\" --confirm-target "+args[6]+" "+action+(snapshot?" "+args[8]:""))
                        {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(runner)};
                    using(var process=Process.Start(start)){process.WaitForExit();return process.ExitCode;}
                }
                Console.WriteLine("LabControl status | enable --local | disable --local | fingerprint --model <file> | run --runtime <folder> --model <file> --confirm-target <hash> probe|start|stop|snapshot <name>");
                return 2;
            }
            catch (InvalidOperationException error) { Console.Error.WriteLine(error.Message); return 78; }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
        }
    }
}
