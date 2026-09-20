using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    // EVARS is the actual product filename; singular EVAR is compatibility-only.
    internal static bool IsEnvironmentInit(string path)
    { return new[]{"evars.init","evar.init"}.Contains(Path.GetFileName(path).ToLowerInvariant()); }
    // Match only shipped profiles; registry bitness is not the Host framework.
    internal static string RecommendProfile(string product, string version, string location)
    {
        string name=(product??"").ToLowerInvariant();
        Version v;
        if(!Version.TryParse(version,out v)) return "";
        if(name.Contains("everything3d")) {
            if(v.Major==2 && v.Minor==1) return "E3D2.1";
            if(v.Major==3 && v.Minor==1 && (v.Build==0 || v.Build==6)) return "E3D3.1."+v.Build;
            return "";
        }
        if(v.Major!=12 || v.Minor!=1) return "";
        if(name.Contains("marine")) return "AM";
        if(name.Contains("pdms") || (name.Contains("plant") && location.IndexOf("PDMS",StringComparison.OrdinalIgnoreCase)>=0)) return "PDMS";
        return "";
    }
    internal static string[] PreferredFiles(IEnumerable<string> files,string profile)
    {
        var all=files.ToArray();
        var init=all.Where(x=>Path.GetExtension(x).Equals(".init",StringComparison.OrdinalIgnoreCase)).ToArray();
        return init.Length>0 || profile.StartsWith("E3D") ? init : all.Where(x=>Path.GetExtension(x).Equals(".bat",StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    internal static JObject Discover()
    {
        var candidates = new JArray(); var warnings = new JArray(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                {
                    using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    if (key != null) foreach (var name in key.GetSubKeyNames())
                    using (var item = key.OpenSubKey(name))
                    {
                        if (item == null) continue;
                        string display = Convert.ToString(item.GetValue("DisplayName"));
                        if (!System.Text.RegularExpressions.Regex.IsMatch(display, @"(?i)AVEVA.*(Everything3D|PDMS|Marine|Plant)|^PDMS") ||
                            display.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        AddCandidate(candidates, warnings, seen, display, Convert.ToString(item.GetValue("DisplayVersion")),
                            Convert.ToString(item.GetValue("InstallLocation")), hive + "/" + view + "/" + item.Name);
                    }
                    using (var key = baseKey.OpenSubKey(@"SOFTWARE\AVEVA"))
                        if (key != null) WalkVendor(key, 0, hive + "/" + view, candidates, warnings, seen);
                }
            }
            catch (Exception ex) { warnings.Add(hive + "/" + view + ": " + ex.Message); }
        }
        return new JObject { ["candidates"] = candidates, ["warnings"] = warnings };
    }
    static void WalkVendor(RegistryKey key, int depth, string evidence, JArray candidates, JArray warnings, HashSet<string> seen)
    {
        if (depth > 7) return;
        foreach (string value in new[] { "InstallLocation", "InstallDir", "InstallPath", "Path" })
        {
            string path = Convert.ToString(key.GetValue(value));
            if (Path.IsPathRooted(path)) AddCandidate(candidates, warnings, seen, key.Name, "", path, evidence + "/" + key.Name);
        }
        foreach (string name in key.GetSubKeyNames())
        {
            try { using (var child = key.OpenSubKey(name)) if (child != null) WalkVendor(child, depth + 1, evidence, candidates, warnings, seen); }
            catch (Exception ex) { warnings.Add(key.Name + "\\" + name + ": " + ex.Message); }
        }
    }
    static void AddCandidate(JArray candidates, JArray warnings, HashSet<string> seen, string product, string version, string location, string evidence)
    {
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathRooted(location)) {
            warnings.Add(product+": registry installation directory missing/invalid; select location manually. "+evidence); return;
        }
        location = Full(location);
        if (!seen.Add(location)) return;
        if (!Directory.Exists(location)) { warnings.Add(product+": installation directory missing: "+location); return; }
        var paths = new List<string>(); int count = 0; int warningsBefore=warnings.Count;
        FindEnvironmentFiles(location, 0, paths, warnings, ref count);
        string profile=RecommendProfile(product,version,location+" "+string.Join(" ",paths));
        var preferred=PreferredFiles(paths,profile);
        candidates.Add(new JObject { ["product"] = product, ["version"] = version, ["directory"] = location,
            ["profile"]=profile,["preferredFiles"]=new JArray(preferred),
            ["scanComplete"]=warnings.Count==warningsBefore,
            ["registry"] = evidence, ["files"] = new JArray(paths),
            ["note"] = "Candidate only: confirm the active launcher and select the matching Host profile / 候选信息，需确认实际启动配置与 Host" });
    }
    static void FindEnvironmentFiles(string directory, int depth, List<string> found, JArray warnings, ref int count)
    {
        // Breadth-first: inspect product roots before large sample-data subtrees.
        var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(directory,0));
        while(queue.Count>0) {
            if(++count>300) { warnings.Add("扫描范围提示 / Search limit (300 directories; not a permission error): "+directory); break; }
            var item=queue.Dequeue();
            try {
                if((File.GetAttributes(item.Item1)&FileAttributes.ReparsePoint)!=0) continue;
                foreach(string path in Directory.GetFiles(item.Item1))
                    if(IsEnvironmentInit(path) || new[]{"evars.bat","evar.bat"}.Contains(Path.GetFileName(path).ToLowerInvariant()))found.Add(path);
                // Product-root EVARS takes precedence over copies in tools/sample trees.
                if(item.Item2==0 && found.Any()) return;
                if(item.Item2<3) foreach(string child in Directory.GetDirectories(item.Item1)) {
                    string leaf=Path.GetFileName(child).ToLowerInvariant();
                    if(new[]{"pmllib","pmlui","samples","sample","examples","help","documentation","docs"}.Contains(leaf)) continue;
                    queue.Enqueue(Tuple.Create(child,item.Item2+1));
                }
            } catch(Exception ex) {warnings.Add(item.Item1+": "+ex.Message);}
        }
    }
}
