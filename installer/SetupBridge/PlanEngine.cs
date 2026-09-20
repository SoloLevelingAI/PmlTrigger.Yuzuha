using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    internal static readonly string[] Profiles = { "AM", "PDMS", "E3D2.1", "E3D3.1.0", "E3D3.1.6" };
    const string Owner2 = "Yuzuha.Inno.Plan.v2";
    const string RegPath = @"Software\YuzuhaToolkit\Installations";
    internal static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    internal static string FileDigest(string path) { return File.Exists(path) ? Digest(File.ReadAllBytes(path)) : null; }
    static Exception Bad(string text) { return new Exception("安装计划 / Installation plan: " + text); }
    internal static string Absolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith("\\\\") || path.Length < 3 || path[1] != ':' ||
            path.Substring(2).Contains(":")) throw Bad("需要本机绝对路径 / Local absolute path required: " + path);
        string full = Full(path); string cursor = full;
        while (!string.IsNullOrEmpty(cursor))
        {
            if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw Bad("不允许链接路径 / Reparse path refused: " + cursor);
            cursor = Path.GetDirectoryName(cursor);
        }
        return full;
    }
    static string Inside(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw Bad("Invalid relative payload path");
        string path = Absolute(Path.Combine(root, relative));
        if (!path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) throw Bad("Payload path escapes root");
        return path;
    }
    static JObject ExistingState(string root)
    {
        if (!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any()) return null;
        if (!File.Exists(Marker(root))) throw Bad("目录不是本安装器管理的空目录；旧安装请另行迁移 / Nonempty unowned directory; legacy migration is separate.");
        var state = ReadObject(Marker(root));
        if ((string)state["owner"] != Owner2 || !Same((string)state["root"], root))
            throw Bad("管理标记不匹配 / Ownership mismatch (including preview1 installations).");
        return state;
    }
    static JObject Change(string path, byte[] after, string kind, string beforeText = null, string afterText = null)
    {
        byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
        return new JObject { ["path"] = path, ["kind"] = kind,
            ["beforeSha256"] = before == null ? null : Digest(before), ["afterSha256"] = Digest(after),
            ["before"] = before == null ? null : Convert.ToBase64String(before), ["after"] = Convert.ToBase64String(after),
            ["beforeText"] = beforeText, ["afterText"] = afterText,
            ["action"] = before == null ? "create" : Digest(before) == Digest(after) ? "unchanged" : "update" };
    }
    static byte[] Encode(Encoding encoding, bool bom, string text)
    { return (bom ? encoding.GetPreamble() : new byte[0]).Concat(encoding.GetBytes(text)).ToArray(); }
    internal static JObject EvarChange(string root, JObject selected)
    {
        string path = Absolute((string)selected["path"]), profile = (string)selected["profile"];
        if (!Profiles.Contains(profile)) throw Bad("请选择精确 Host 配置 / Select an exact Host profile.");
        if ((bool?)selected["confirmed"] != true) throw Bad("需确认该文件是实际启动配置 / Confirm the active environment file.");
        if (!File.Exists(path)) throw Bad("环境文件不存在 / Environment file missing: " + path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".bat" && ext != ".init") throw Bad("Only BAT-style .INIT/.BAT supported.");
        bool legacy = profile == "AM" || profile == "PDMS";
        if (!legacy && ext != ".init") throw Bad("E3D 必须选择有效 INIT / E3D requires active INIT.");
        if (legacy && ext == ".bat")
        {
            if ((bool?)selected["noInitConfirmed"] != true) throw Bad("需确认此启动环境没有适用 INIT / Confirm no applicable INIT for BAT fallback.");
            if (Directory.GetFiles(Path.GetDirectoryName(path)).Any(IsEnvironmentInit))
                throw Bad("同目录已有 INIT，不能自动回退 BAT / INIT exists next to BAT.");
        }
        byte[] bytes = File.ReadAllBytes(path); Encoding encoding; bool bom = false; int offset = 0;
        if (bytes.Take(3).SequenceEqual(new byte[] {239,187,191})) { encoding = new UTF8Encoding(true, true); bom = true; offset = 3; }
        else if (bytes.Take(2).SequenceEqual(new byte[] {255,254})) { encoding = new UnicodeEncoding(false,true,true); bom = true; offset = 2; }
        else if (bytes.Take(2).SequenceEqual(new byte[] {254,255})) { encoding = new UnicodeEncoding(true,true,true); bom = true; offset = 2; }
        else
        {
            string choice = (string)selected["encoding"] ?? "auto";
            // A one-byte mapping preserves every original byte without guessing an ANSI code page.
            // The managed block is ASCII unless the chosen installation path needs non-ASCII characters.
            if (choice == "auto") {
                if(root.Any(c=>c>127)) throw Bad("无 BOM 文件与非 ASCII 安装路径：请在高级设置明确编码 / No-BOM file with non-ASCII install path: select encoding in advanced settings.");
                encoding=Encoding.GetEncoding(28591,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
            }
            else if (choice == "utf-8") encoding = new UTF8Encoding(false,true);
            else if (choice == "system") encoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage,
                EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            else throw Bad("encoding must be auto, utf-8 or system; BOM is detected automatically.");
        }
        string before = encoding.GetString(bytes, offset, bytes.Length - offset);
        if (!Encode(encoding, bom, before).SequenceEqual(bytes)) throw Bad("Encoding does not round-trip; select the correct encoding.");
        string nl = before.Contains("\r\n") ? "\r\n" : "\n";
        string start = "rem >>> Yuzuha managed settings", end = "rem <<< Yuzuha managed settings";
        int starts = Regex.Matches(before, "(?im)^" + Regex.Escape(start) + @"\r?$").Count;
        int ends = Regex.Matches(before, "(?im)^" + Regex.Escape(end) + @"\r?$").Count;
        if (starts != ends || starts > 1) throw Bad("Malformed/duplicate managed block.");
        string body = Regex.Replace(before, "(?ims)^" + Regex.Escape(start) + @"\r?\n.*?^" + Regex.Escape(end) + @"(?:\r?\n|$)", "");
        if (body.IndexOf(start, StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf(end,StringComparison.OrdinalIgnoreCase) >= 0)
            throw Bad("Malformed managed block.");
        if (root.IndexOfAny(new[] {'&','|','<','>','!','^','(',')','%'}) >= 0) throw Bad("Install path contains batch metacharacters.");
        JArray lineEdits;
        string after=EditEnvironmentLines(body,root,profile,legacy,nl,out lineEdits);
        if(starts>0) lineEdits.Insert(0,new JObject{["variable"]="previous managed block",["line"]=0,
            ["before"]=before.Substring(before.IndexOf(start,StringComparison.OrdinalIgnoreCase),
                before.IndexOf(end,StringComparison.OrdinalIgnoreCase)+end.Length-before.IndexOf(start,StringComparison.OrdinalIgnoreCase)),
            ["after"]="(末尾接入块将按以下内容更新 / Integration block replaced as shown below)"});
        if(before==after)lineEdits.Clear();
        byte[] result=Encode(encoding,bom,after);
        // Display decoding never controls the bytes written for auto/no-BOM files.
        if(!bom && ((string)selected["encoding"]??"auto")=="auto") {
            Encoding display;
            try { new UTF8Encoding(false,true).GetString(bytes); display=new UTF8Encoding(false,true); }
            catch(DecoderFallbackException) { display=Encoding.Default; }
            foreach(JObject edit in lineEdits) foreach(string key in new[]{"before","after"})
                if((string)edit[key]!=null && ((string)edit[key]).All(c=>c<=255)) edit[key]=display.GetString(encoding.GetBytes((string)edit[key]));
            var change=Change(path,result,"environment",display.GetString(bytes),display.GetString(result));
            change["lineEdits"]=lineEdits;return change;
        }
        var encodedChange=Change(path, result, "environment", before, after);
        encodedChange["lineEdits"]=lineEdits;return encodedChange;
    }
    internal static JObject CreatePlan(JObject request, string manifestPath, string skillSource)
    {
        IntegrationMode(request);
        string root = Absolute((string)request["root"]); CheckPath(root);
        var prior = ExistingState(root); var changes = new JArray(); var managed = prior?["managed"] as JArray ?? new JArray();
        foreach (JObject old in managed)
            if (FileDigest((string)old["path"]) != (string)old["afterSha256"])
                throw Bad("先检查上次安装后修改的托管文件 / Managed file changed since last install: " + old["path"]);
        var manifest = ReadObject(manifestPath);
        string config = (string)request["mcpJson"] ?? "";
        if (config.Length > 0)
        {
            config = Absolute(config);
            if (config.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)) throw Bad("MCP config must be outside installation.");
            var merged = Merge(config,root);
            changes.Add(Change(config,Utf8.GetBytes(merged.ToString(Formatting.Indented)+Environment.NewLine),"mcp",
                File.Exists(config)?File.ReadAllText(config):"(new file)",merged.ToString(Formatting.Indented)));
        }
        foreach (JObject selected in request["environments"] as JArray ?? new JArray()) changes.Add(EvarChange(root,selected));
        string skill = (string)request["skillTarget"] ?? "";
        if (skill.Length > 0)
        {
            skill = Absolute(skill);
            if (!Same(Path.GetFileName(skill),"yuzuha-toolkit") || skill.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase) ||
                root.StartsWith(skill+"\\",StringComparison.OrdinalIgnoreCase)) throw Bad("Skill target must be a separate yuzuha-toolkit directory.");
            var priorPaths = new HashSet<string>(managed.OfType<JObject>().Select(x=>(string)x["path"]),StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(skill) && Directory.EnumerateFiles(skill,"*",SearchOption.AllDirectories).Any(x=>!priorPaths.Contains(x)))
                throw Bad("Existing Skill is not managed by this installation; no overwrite.");
            foreach (JObject item in (JArray)manifest["files"])
            {
                string rel=(string)item["path"]; if (!rel.StartsWith("skill/",StringComparison.Ordinal)) continue;
                string source=Inside(Absolute(skillSource),rel.Substring(6));
                if(FileDigest(source)!=(string)item["sha256"]) throw Bad("Bundled Skill checksum mismatch.");
                changes.Add(Change(Inside(skill,rel.Substring(6)),File.ReadAllBytes(source),"skill"));
            }
        }
        var payload = new JArray();
        foreach (JObject item in (JArray)manifest["files"])
        {
            string path=Inside(root,(string)item["path"]); string before=FileDigest(path), after=(string)item["sha256"];
            payload.Add(new JObject { ["path"]=path,["relative"]=item["path"],["beforeSha256"]=before,["afterSha256"]=after,
                ["action"]=before==null?"create":before==after?"unchanged":"update" });
        }
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(JObject c in changes)
        {
            if(!seen.Add((string)c["path"])) throw Bad("Multiple changes target the same file.");
            if(((string)c["path"]).StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)) throw Bad("External configuration cannot overlap payload.");
        }
        return new JObject { ["schema"]=2,["id"]=Guid.NewGuid().ToString("N"),["createdUtc"]=DateTime.UtcNow.ToString("o"),
            ["request"]=request.DeepClone(),["root"]=root,["manifestSha256"]=FileDigest(manifestPath),["payload"]=payload,
            ["changes"]=changes,["priorState"]=prior,["registryKey"]=RegPath+"\\"+Digest(Utf8.GetBytes(root.ToUpperInvariant())).Substring(0,16) };
    }
    internal static string Preview(JObject plan)
    {
        var b=new StringBuilder("# Yuzuha 安装预览 / Installation preview\r\n\r\n");
        b.AppendLine("AVEVA: "+(IntegrationMode((JObject)plan["request"])=="aveva"?
            "接入配置 / configure selected environments":"未请求接入（仅文件/客户端） / NOT requested (files/client only)"));
        b.AppendLine("Environment targets: "+(((JObject)plan["request"])["environments"] as JArray)?.Count);
        b.AppendLine("目录 / Root: "+plan["root"]); b.AppendLine("计划 ID / Plan ID: "+plan["id"]);
        b.AppendLine("注册表 / Registry (HKCU, 64-bit): "+plan["registryKey"]);
        foreach(JObject c in (JArray)plan["changes"])
        {
            b.AppendLine("\r\n## "+c["kind"]+" / "+c["action"]+" : "+c["path"]);
            b.AppendLine("SHA256: "+c["beforeSha256"]+" -> "+c["afterSha256"]);
            if(c["lineEdits"] is JArray) {
                foreach(JObject edit in (JArray)c["lineEdits"]) {
                    b.AppendLine("\r\n"+edit["variable"]+" | 行 / Line: "+edit["line"]);
                    if(edit["placement"]!=null)b.AppendLine((string)edit["placement"]);
                    b.AppendLine("修改前 / BEFORE: "+((string)edit["before"]??"(无 / absent)"));
                    b.AppendLine("修改后 / AFTER:  "+edit["after"]);
                }
                if(!c["lineEdits"].HasValues)b.AppendLine("无变化 / No changes");
                continue;
            }
            if(c["beforeText"]?.Type==JTokenType.String) { b.AppendLine("--- 修改前 / BEFORE ---"); b.AppendLine((string)c["beforeText"]); }
            if(c["afterText"]?.Type==JTokenType.String) { b.AppendLine("+++ 修改后 / AFTER +++"); b.AppendLine((string)c["afterText"]); }
        }
        b.AppendLine("\r\n## 安装文件明细 / Payload details (before -> after SHA256)");
        foreach(JObject p in (JArray)plan["payload"]) b.AppendLine(p["action"]+" "+p["relative"]+" | "+p["beforeSha256"]+" -> "+p["afterSha256"]);
        b.AppendLine("\r\n已有知识库、日志、自定义 Profile 不删除。修改前备份；文件变化后重新预览。\r\nExisting data is retained. Backups precede edits. Changed files require a new preview.");
        return b.ToString();
    }
    internal static void SavePlan(JObject plan,string output)
    {
        output=Absolute(output); if(File.Exists(output)) throw Bad("Plan output already exists; choose a fresh path.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output,plan.ToString(Formatting.Indented),Utf8);
        File.WriteAllText(output+".md",Preview(plan),Utf8); File.WriteAllText(output+".sha256",FileDigest(output),Utf8);
    }
    static JObject VerifyPlan(string planFile,string expectedHash,string manifestPath,bool beforeInstall)
    {
        if(!Same(FileDigest(planFile),expectedHash)) throw Bad("计划 SHA256 不匹配 / Plan checksum mismatch.");
        var plan=ReadObject(planFile); if((int?)plan["schema"]!=2 || (string)plan["manifestSha256"]!=FileDigest(manifestPath)) throw Bad("Package/plan mismatch.");
        IntegrationMode((JObject)plan["request"]);
        string root=Absolute((string)plan["root"]); CheckPath(root);
        Guid planId;
        if(!Guid.TryParseExact((string)plan["id"],"N",out planId)) throw Bad("Invalid plan ID.");
        if(!Same(root,Absolute((string)plan["request"]["root"]))) throw Bad("Request root differs from plan.");
        string expectedRegistry=RegPath+"\\"+Digest(Utf8.GetBytes(root.ToUpperInvariant())).Substring(0,16);
        if((string)plan["registryKey"]!=expectedRegistry) throw Bad("Invalid registry target.");
        var expectedFiles=(JArray)ReadObject(manifestPath)["files"];
        if(expectedFiles.Count!=((JArray)plan["payload"]).Count) throw Bad("Payload inventory differs from package.");
        foreach(JObject expected in expectedFiles)
            if(((JArray)plan["payload"]).OfType<JObject>().Count(x=>(string)x["relative"]==(string)expected["path"] &&
                (string)x["afterSha256"]==(string)expected["sha256"])!=1) throw Bad("Payload manifest mismatch.");
        foreach(JObject item in (JArray)plan["payload"])
        {
            if(!Same((string)item["path"],Inside(root,(string)item["relative"]))) throw Bad("Invalid payload path.");
            if(FileDigest((string)item["path"])!=(string)item[beforeInstall?"beforeSha256":"afterSha256"])
                throw Bad("安装文件与预览不一致 / Payload changed: "+item["path"]);
        }
        var externalPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(JObject item in (JArray)plan["changes"])
        {
            string external=Absolute((string)item["path"]);
            if(!externalPaths.Add(external) || Same(external,root) || external.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase))
                throw Bad("Duplicate or overlapping external path.");
            string kind=(string)item["kind"];
            var request=(JObject)plan["request"];
            bool permitted=kind=="mcp" && Same(external,Absolute((string)request["mcpJson"]));
            if(kind=="environment") permitted=(request["environments"] as JArray??new JArray()).OfType<JObject>().Any(x=>Same(external,Absolute((string)x["path"])) && (bool?)x["confirmed"]==true);
            if(kind=="skill") {
                string target=Absolute((string)request["skillTarget"]);
                permitted=Same(Path.GetFileName(target),"yuzuha-toolkit") && external.StartsWith(target+"\\",StringComparison.OrdinalIgnoreCase);
            }
            if(!permitted) throw Bad("External path not selected in request.");
            string original=(string)item["before"];
            if((original==null?null:Digest(Convert.FromBase64String(original)))!=(string)item["beforeSha256"]) throw Bad("Invalid original content.");
            if(FileDigest((string)item["path"])!=(string)item["beforeSha256"]) throw Bad("配置已变化，请重新预览 / Configuration changed: "+item["path"]);
            if(Digest(Convert.FromBase64String((string)item["after"]))!=(string)item["afterSha256"]) throw Bad("Invalid proposed content.");
            string action=(string)item["beforeSha256"]==null?"create":(string)item["beforeSha256"]==(string)item["afterSha256"]?"unchanged":"update";
            if((string)item["action"]!=action) throw Bad("Invalid proposed action.");
        }
        if(beforeInstall) { ExistingState(root); CheckProcesses(root); }
        if(beforeInstall) foreach(JObject c in (JArray)plan["changes"])
            if((string)c["action"]!="unchanged" && File.Exists((string)c["path"]))
                using(var writable=new FileStream((string)c["path"],FileMode.Open,FileAccess.Write,FileShare.Read)) { }
        return plan;
    }
    static string RunDirectory(JObject plan)
    { Guid id; if(!Guid.TryParseExact((string)plan["id"],"N",out id)) throw Bad("Invalid plan ID.");
      return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YuzuhaToolkit","SetupReports",id.ToString("N")); }
    static void AtomicBytes(string path,byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".yuzuha-"+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllBytes(temp,content);
        try { if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    static void BeginPlan(string planFile,string hash,string manifest)
    {
        var plan=VerifyPlan(planFile,hash,manifest,true); string report=RunDirectory(plan); Directory.CreateDirectory(report);
        var snapshots=new JArray();
        // Back up every existing file that Setup will overwrite, plus generated state.
        foreach(string path in ((JArray)plan["payload"]).OfType<JObject>().Where(x=>(string)x["action"]!="unchanged").Select(x=>(string)x["path"])
            .Concat(new[]{Marker((string)plan["root"])}))
        {
            string backup=null; if(File.Exists(path)){backup=Path.Combine(report,"payload-"+snapshots.Count+".bak");File.Copy(path,backup,false);}
            snapshots.Add(new JObject{["path"]=path,["backup"]=backup});
        }
        File.WriteAllText(Path.Combine(report,"payload-backups.json"),snapshots.ToString(),Utf8);
        File.Copy(planFile,Path.Combine(report,"plan.json"),false);
        File.WriteAllText(Path.Combine(report,"preview.md"),Preview(plan),Utf8);
        File.WriteAllText(Path.Combine(report,"result.json"),new JObject{["status"]="installing",["utc"]=DateTime.UtcNow.ToString("o")}.ToString(),Utf8);
    }
    static void ApplyPlan(string planFile,string hash,string manifest)
    {
        var plan=VerifyPlan(planFile,hash,manifest,false); string report=RunDirectory(plan),root=(string)plan["root"];
        var actual=new JArray(); var touched=new List<JObject>();
        var state=new JObject{["owner"]=Owner2,["root"]=root,["request"]=plan["request"].DeepClone(),
            ["managed"]=(plan["priorState"] as JObject)?["managed"]?.DeepClone()??new JArray(),["lastReport"]=report,["registryKey"]=plan["registryKey"]};
        try
        {
            foreach(JObject c in (JArray)plan["changes"])
            {
                string path=(string)c["path"],backup=null;
                if(FileDigest(path)!=(string)c["beforeSha256"]) throw Bad("File changed during commit: "+path);
                if((string)c["action"]!="unchanged")
                {
                    if(File.Exists(path)){backup=Path.Combine(report,"external-"+touched.Count+".bak");File.Copy(path,backup,false);}
                    touched.Add(c); AtomicBytes(path,Convert.FromBase64String((string)c["after"]));
                }
                var managed=(JArray)state["managed"]; var old=managed.OfType<JObject>().FirstOrDefault(x=>Same((string)x["path"],path));
                string original=old==null?(string)c["before"]:(string)old["original"];
                if(old!=null) old.Remove();
                managed.Add(new JObject{["path"]=path,["kind"]=c["kind"],["original"]=original,["afterSha256"]=c["afterSha256"]});
                if(FileDigest(path)!=(string)c["afterSha256"]) throw Bad("Written configuration checksum mismatch: "+path);
                actual.Add(new JObject{["path"]=path,["kind"]=c["kind"],["status"]=c["action"],["beforeSha256"]=c["beforeSha256"],["afterSha256"]=FileDigest(path),["backup"]=backup});
            }
            AtomicBytes(Marker(root),Utf8.GetBytes(state.ToString(Formatting.Indented)));
            using(var baseKey=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
            using(var key=baseKey.CreateSubKey((string)plan["registryKey"]))
            {
                key.SetValue("InstallLocation",root);key.SetValue("InstallGuide",Path.Combine(root,"INSTALL.md"));
                key.SetValue("Version","0.3.2-setup-preview11");key.SetValue("LastReport",report);
            }
            foreach(JObject p in (JArray)plan["payload"]) actual.Add(new JObject{["path"]=p["path"],["kind"]="payload",["status"]=p["action"],["afterSha256"]=FileDigest((string)p["path"])});
            WriteResult(report,"success",actual,null);
        }
        catch(Exception ex)
        {
            var recovery=new JArray();
            foreach(var c in touched.AsEnumerable().Reverse())
            {
                string path=(string)c["path"];
                try
                {
                    if(FileDigest(path)!=(string)c["afterSha256"]) throw Bad("Concurrent change; backup retained");
                    if(c["before"].Type==JTokenType.Null) File.Delete(path); else AtomicBytes(path,Convert.FromBase64String((string)c["before"]));
                    recovery.Add(path+": rolled back");
                }
                catch(Exception r){ recovery.Add(path+": "+r.Message); }
            }
            WriteResult(report,"failed",actual,ex.Message+"\r\n"+recovery); throw;
        }
    }
    static void WriteResult(string report,string status,JArray actual,string error)
    {
        int environments=actual.OfType<JObject>().Count(x=>(string)x["kind"]=="environment");
        string integration=status!="success"?"not-confirmed":environments>0?"configured-not-launch-tested":"not-requested";
        string summary="AVEVA: "+integration+"; environment files: "+environments;
        File.WriteAllText(Path.Combine(report,"result.json"),new JObject{["status"]=status,["utc"]=DateTime.UtcNow.ToString("o"),
            ["avevaIntegration"]=integration,["environmentCount"]=environments,["actual"]=actual,["error"]=error}.ToString(Formatting.Indented),Utf8);
        File.WriteAllText(Path.Combine(report,"result.md"),"# 实际安装结果 / Actual result\r\n\r\n"+status+"\r\n"+error+"\r\n"+
            summary+"\r\n"+
            string.Join("\r\n",actual.OfType<JObject>().Select(x=>x["status"]+" | "+x["path"]+" | SHA256 "+x["afterSha256"]+" | backup "+x["backup"])),Utf8);
    }
    static void AbortPlan(string planFile)
    {
        var plan=ReadObject(planFile); string report=RunDirectory(plan),list=Path.Combine(report,"payload-backups.json");
        if(!File.Exists(list)) return;
        var issues=new JArray();
        string resultFile=Path.Combine(report,"result.json");
        if(File.Exists(resultFile) && (string)ReadObject(resultFile)["status"]=="installing")
            WriteResult(report,"failed-before-config-commit",new JArray(),"Setup interrupted or verification failed; inspect Inno log and recovery.json.");
        foreach(JObject item in JArray.Parse(File.ReadAllText(list)))
        {
            string path=(string)item["path"],backup=(string)item["backup"];
            try
            {
                var p=((JArray)plan["payload"]).OfType<JObject>().FirstOrDefault(x=>Same((string)x["path"],path));
                string current=FileDigest(path);
                if(p!=null && current!=null && current!=(string)p["afterSha256"] && current!=(string)p["beforeSha256"])
                    throw Bad("Concurrent payload modification; do not overwrite");
                if(backup!=null) AtomicBytes(path,File.ReadAllBytes(backup));
                else if(p!=null && File.Exists(path)) File.Delete(path);
            }
            catch(Exception ex){issues.Add(path+": "+ex.Message);}
        }
        File.WriteAllText(Path.Combine(report,"recovery.json"),new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["issues"]=issues,
            ["note"]="Payload restoration attempted; inspect Windows uninstall metadata after interrupted updates."}.ToString(),Utf8);
    }
    static void UninstallPlan(string root,bool checkOnly)
    {
        Absolute(root);CheckPath(root);CheckProcesses(root);var state=ExistingState(root);
        if(state==null) throw Bad("Missing installation state.");
        foreach(JObject c in (JArray)state["managed"])
            if(FileDigest((string)c["path"])!=(string)c["afterSha256"]) throw Bad("卸载前需处理外部配置变更 / External file changed; review before uninstall: "+c["path"]);
        if(checkOnly) return;
        string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YuzuhaToolkit","SetupReports","uninstall-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(report); var actual=new JArray();
        foreach(JObject c in (JArray)state["managed"])
        {
            string path=(string)c["path"], backup=Path.Combine(report,actual.Count+".bak");
            if(FileDigest(path)!=(string)c["afterSha256"]) throw Bad("Concurrent external change during uninstall.");
            File.Copy(path,backup,false);
            if(c["original"].Type==JTokenType.Null) File.Delete(path);else AtomicBytes(path,Convert.FromBase64String((string)c["original"]));
            actual.Add(new JObject{["path"]=path,["status"]="restored",["afterSha256"]=FileDigest(path),["backup"]=backup});
        }
        using(var baseKey=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
        {
            using(var key=baseKey.OpenSubKey((string)state["registryKey"]))
                if(key!=null && !Same(Convert.ToString(key.GetValue("InstallLocation")),root)) throw Bad("Registry ownership changed.");
            baseKey.DeleteSubKey((string)state["registryKey"],false);
        }
        WriteResult(report,"uninstalled-external-config",actual,null);
    }
    static bool NewCommand(string[] args)
    {
        switch(args[0])
        {
            case "discover": File.WriteAllText(args[1],Discover().ToString(Formatting.Indented),Utf8); return true;
            case "locate":
                var installations=new JArray();
                using(var baseKey=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
                using(var registry=baseKey.OpenSubKey(RegPath))
                    if(registry!=null)foreach(string name in registry.GetSubKeyNames())using(var item=registry.OpenSubKey(name))
                    {
                        string location=Convert.ToString(item.GetValue("InstallLocation"));
                        installations.Add(new JObject{["root"]=location,["guide"]=item.GetValue("InstallGuide")?.ToString(),
                            ["stateFileExists"]=File.Exists(Marker(location)),["registryKey"]=RegPath+"\\"+name});
                    }
                File.WriteAllText(args[1],installations.ToString(Formatting.Indented),Utf8);return true;
            case "plan": SavePlan(CreatePlan(ReadObject(args[1]),args[2],args[3]),args[4]); return true;
            case "plan-auto": CreateAutomaticPlan(args[1],args[2],args[3],args[4]); return true;
            case "discover-native": ExportNativeDiscovery(args[1]); return true;
            case "render":
                var rendered=ReadObject(args[1]);
                if(!Same((string)rendered["root"],Absolute(args[2]))) throw Bad("安装目录与批准计划不一致 / Install directory differs from plan.");
                File.WriteAllText(args[3],Preview(rendered),Utf8); return true;
            case "begin-plan": BeginPlan(args[1],args[2],args[3]); return true;
            case "apply-plan": ApplyPlan(args[1],args[2],args[3]); return true;
            case "attach":
                var attach=VerifyPlan(args[1],args[2],args[3],true);
                if(((JArray)attach["payload"]).OfType<JObject>().Any(x=>(string)x["action"]!="unchanged")) throw Bad("Attach cannot install/update payload.");
                BeginPlan(args[1],args[2],args[3]);ApplyPlan(args[1],args[2],args[3]);return true;
            case "abort-plan": AbortPlan(args[1]); return true;
            case "check-remove": UninstallPlan(Full(args[1]),true); return true;
            case "remove-plan": UninstallPlan(Full(args[1]),false); return true;
            default: return false;
        }
    }
}
