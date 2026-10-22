using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Pml;

// AVEVA dependencies are synthetic; the RPC service and MARKDB wrapper are production source.
namespace YuzuhaToolkit.PmlHost
{
    internal static class PmlCommandRpcHost
    {
        internal static string PipeName;
        internal static int ProcessId { get { return Process.GetCurrentProcess().Id; } }
        internal static long ProcessStartTimeUtcTicks { get { return Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks; } }
        internal static int MainThreadId { get { return 7; } }
        internal static readonly PmlCommandMethod Target = new PmlCommandMethod();
        internal static void AssertMainThread() { /* Synthetic RPC fixture does not claim an AVEVA UI thread. */ }
        internal static PmlCommandMethod GetTarget() { return Target; }
    }
    internal sealed class PmlCommandMethod
    {
        private readonly Dictionary<string,string> variables = new Dictionary<string,string>();
        internal bool FailBegin, FailEnd;
        internal string Model { get { return "Design"; } }
        internal bool RunPmlCommand(string command)
        {
            if (command.StartsWith("MARKDB ",StringComparison.Ordinal)) return !FailBegin;
            if (command == "ENDMARKDB") return !FailEnd;
            if (command.StartsWith("$M\"",StringComparison.Ordinal))
            {
                string path=command.Substring(3,command.Length-4);
                foreach(string line in File.ReadAllLines(path)) Assign(line);
                return true;
            }
            Assign(command); return true;
        }
        private void Assign(string command)
        {
            Match match=Regex.Match(command,@"^!!([A-Za-z][A-Za-z0-9_]*) = '([^']*)'$");
            if(match.Success) variables[match.Groups[1].Value]=match.Groups[2].Value;
            if(command.StartsWith("!!",StringComparison.Ordinal) && command.EndsWith(".delete()",StringComparison.Ordinal)) variables.Remove(command.Substring(2,command.Length-11));
        }
        internal string RunPmlCommandString(string command) { string value; return variables.TryGetValue(command.Substring(2),out value)?value:"NotRun"; }
        internal string[] GetPmlBfsList(string command,string globalVar,bool delete) { return new string[]{globalVar,delete.ToString()}; }
    }
    internal static class GetRequireFromE3D
    {
        internal static PmlArrayWindowResult GetPmlVariableWindow(string globalVar,int start,int items,int characters)
        { return PmlArrayWindow.Read(5,start,items,characters,delegate(int index){return "row"+index;}); }
    }
    internal sealed class PmlFormQueryHost : global::NativeFormFixture { }
}
