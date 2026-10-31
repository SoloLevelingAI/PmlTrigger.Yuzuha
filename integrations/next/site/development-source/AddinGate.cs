using System;
using System.Diagnostics;
using System.IO;
namespace LaunchDesk.Development.RemoteProjectLab
{
    public static class AddinGate
    {
        // Use the shared .NET backend catalog, never a second XML parser in Framework scripts.
        public static void RequireLab()
        {
            string tool=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LaunchDesk.Next.Development.exe");
            if(!File.Exists(tool))throw new InvalidOperationException("Developer Addin host missing; use the matching development package.");
            var start=new ProcessStartInfo(tool,"addin-check --id development.remote-project-lab")
                {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=Process.Start(start))
            {
                var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(10000)) { process.Kill();throw new InvalidOperationException("Addin check timed out; no VM action performed."); }
                System.Threading.Tasks.Task.WaitAll(output,error);
                if(process.ExitCode!=0)throw new InvalidOperationException("Developer VM Feature is not enabled by a valid Addin. "+error.Result.Trim());
            }
        }
    }
}
