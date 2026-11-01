using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

/// <summary>安装桥接程序：发现环境、生成可审阅计划并按显式入口执行安装事务。</summary>
internal static partial class Program
{
    /// <summary>只接受明确布尔接管选项；默认拒绝未托管目录，启用时必须指定 Skill 目标。</summary>
    internal static bool AdoptSkill(JObject request)
    {
        var option=request["adoptSkill"];
        if(option==null) return false;
        if(option.Type!=JTokenType.Boolean) throw Bad("adoptSkill must be a strict boolean.");
        bool enabled=(bool)option;
        if(enabled && (request["skillTarget"]?.Type!=JTokenType.String || string.IsNullOrWhiteSpace((string)request["skillTarget"])))
            throw Bad("adoptSkill requires an explicit existing Skill target.");
        return enabled;
    }

    static string SkillRelative(string relative)
    {
        if(string.IsNullOrWhiteSpace(relative) || relative.Length>1024 || relative.Contains("\\") || relative.Contains(":") ||
            relative.Any(char.IsControl) || Path.IsPathRooted(relative) || relative.Split('/').Any(p=>p=="" || p=="." || p==".."))
            throw Bad("Invalid relative Skill inventory path.");
        return relative;
    }
    static string SkillTarget(JObject request,string root)
    {
        string target=Absolute((string)request["skillTarget"]);
        if(!Same(Path.GetFileName(target),"yuzuha-toolkit") || Same(root,target) || target.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase) || root.StartsWith(target+"\\",StringComparison.OrdinalIgnoreCase))
            throw Bad("Skill target must be a separate yuzuha-toolkit directory.");
        return target;
    }
    static JArray SkillInventory(string target,bool required)
    {
        FileAttributes attributes;
        try { attributes=File.GetAttributes(target); }
        catch(FileNotFoundException) { if(required)throw Bad("Skill adoption requires an existing directory.");return new JArray(); }
        catch(DirectoryNotFoundException) { if(required)throw Bad("Skill adoption requires an existing directory.");return new JArray(); }
        if((attributes&FileAttributes.Directory)==0 || (attributes&FileAttributes.ReparsePoint)!=0)throw Bad("Skill target must be an ordinary directory.");
        var entries=new List<JObject>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var pending=new Stack<string>();pending.Push(target);long total=0;
        while(pending.Count>0)
        {
            string directory=Absolute(pending.Pop());
            foreach(string item in Directory.GetFileSystemEntries(directory))
            {
                if(entries.Count>=2048)throw Bad("Skill inventory exceeds 2048 entries.");
                string file=Absolute(item);string relative=SkillRelative(file.Substring(target.Length+1).Replace('\\','/'));
                if(!seen.Add(relative))throw Bad("Duplicate Skill inventory path.");
                var kind=File.GetAttributes(file);
                if((kind&FileAttributes.ReparsePoint)!=0)throw Bad("Skill links and junctions are refused.");
                if((kind&FileAttributes.Directory)!=0) { entries.Add(new JObject{["relative"]=relative,["kind"]="directory"});pending.Push(file);continue; }
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var hash=SHA256.Create())
                {
                    long length=stream.Length;
                    if(length>4*1024*1024 || total+length>16*1024*1024)throw Bad("Skill inventory exceeds its file or total byte budget.");
                    total+=length;string digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                    if(stream.Position!=length)throw Bad("Skill file changed while hashing.");
                    entries.Add(new JObject{["relative"]=relative,["kind"]="file",["sha256"]=digest,["length"]=length});
                }
            }
        }
        return new JArray(entries.OrderBy(e=>(string)e["relative"],StringComparer.OrdinalIgnoreCase));
    }
    static string SkillText(byte[] bytes)
    {
        Encoding encoding=new UTF8Encoding(false,true);int offset=0;
        if(bytes.Take(3).SequenceEqual(new byte[]{239,187,191}))offset=3;
        else if(bytes.Take(2).SequenceEqual(new byte[]{255,254})){encoding=new UnicodeEncoding(false,true,true);offset=2;}
        else if(bytes.Take(2).SequenceEqual(new byte[]{254,255})){encoding=new UnicodeEncoding(true,true,true);offset=2;}
        string text=encoding.GetString(bytes,offset,bytes.Length-offset);
        if(text.Contains("\0"))throw Bad("Skill target text is corrupt or unsupported; no adoption performed.");
        return text;
    }
    static byte[] SkillBytes(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length>4*1024*1024)throw Bad("Skill file exceeds 4 MiB.");
            var bytes=new byte[(int)stream.Length];int read=0;
            while(read<bytes.Length) { int amount=stream.Read(bytes,read,bytes.Length-read);if(amount==0)throw Bad("Skill file changed while reading.");read+=amount; }
            if(stream.ReadByte()!=-1)throw Bad("Skill file changed while reading.");return bytes;
        }
    }
    static JObject AddSkillChanges(JObject request,string root,JArray managed,JObject manifest,string skillSource,JArray changes)
    {
        bool adopt=AdoptSkill(request);string selected=(string)request["skillTarget"]??"";
        if(selected.Length==0)return null;
        string target=SkillTarget(request,root);var inventory=SkillInventory(target,adopt);
        var priorPaths=new HashSet<string>(managed.OfType<JObject>().Select(x=>(string)x["path"]),StringComparer.OrdinalIgnoreCase);
        if(!adopt && inventory.OfType<JObject>().Any(x=>(string)x["kind"]=="file" && !priorPaths.Contains(Inside(target,(string)x["relative"]))))
            throw Bad("Existing Skill is not managed by this installation; no overwrite.");
        var packageFiles=((JArray)manifest["files"]).OfType<JObject>().Where(x=>((string)x["path"]).StartsWith("skill/",StringComparison.Ordinal)).ToArray();
        if(packageFiles.Length==0)throw Bad("Selected package contains no runtime Skill files.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long plannedBytes=inventory.OfType<JObject>().Where(e=>(string)e["kind"]=="file").Sum(e=>(long)e["length"]);
        foreach(var item in packageFiles)
        {
            string relative=SkillRelative(((string)item["path"]).Substring(6));if(!seen.Add(relative))throw Bad("Duplicate packaged Skill target.");
            if(inventory.OfType<JObject>().Any(x=>Same((string)x["relative"],relative) && (string)x["kind"]=="directory"))throw Bad("Packaged Skill target conflicts with an existing directory.");
            string ancestor=Path.GetDirectoryName(relative.Replace('/',Path.DirectorySeparatorChar));
            while(!string.IsNullOrEmpty(ancestor))
            {
                string name=ancestor.Replace('\\','/');
                if(inventory.OfType<JObject>().Any(x=>Same((string)x["relative"],name) && (string)x["kind"]=="file"))throw Bad("Packaged Skill parent conflicts with an existing file.");
                ancestor=Path.GetDirectoryName(ancestor);
            }
            string source=Inside(Absolute(skillSource),relative),path=Inside(target,relative);
            var bytes=SkillBytes(source);if(Digest(bytes)!=(string)item["sha256"])throw Bad("Bundled Skill checksum mismatch.");
            var change=Change(path,bytes,"skill");
            var frozen=inventory.OfType<JObject>().SingleOrDefault(x=>Same((string)x["relative"],relative) && (string)x["kind"]=="file");
            plannedBytes+=bytes.Length-((long?)frozen?["length"]??0);if(plannedBytes>16*1024*1024)throw Bad("Planned Skill scope exceeds its byte budget.");
            if((string)change["beforeSha256"]!=(string)frozen?["sha256"])throw Bad("Skill changed while freezing the plan.");
            change["beforeText"]=(string)change["before"]==null?null:SkillText(Convert.FromBase64String((string)change["before"]));
            change["afterText"]=SkillText(bytes);
            change["ownershipAction"]=priorPaths.Contains(path)?"retained":(string)change["before"]==null?"create":"adopt";
            changes.Add(change);
        }
        SkillExpectedAfter(inventory,changes,target);
        if(!JToken.DeepEquals(inventory,SkillInventory(target,adopt)))throw Bad("Skill directory changed while freezing the plan.");
        return adopt?new JObject{["schema"]=1,["target"]=target,["sourceMarkerSha256"]=FileDigest(Marker(root)),["entries"]=inventory}:null;
    }
    static void VerifySkillScope(JObject plan,JArray expectedFiles)
    {
        var request=(JObject)plan["request"];bool adopt=AdoptSkill(request);var changes=((JArray)plan["changes"]).OfType<JObject>().Where(x=>(string)x["kind"]=="skill").ToArray();
        string selected=(string)request["skillTarget"]??"";
        if(selected.Length==0) { if(changes.Length!=0 || (plan["skillAdoption"]!=null && plan["skillAdoption"].Type!=JTokenType.Null))throw Bad("Skill scope missing from request.");return; }
        string target=SkillTarget(request,(string)plan["root"]);
        var expected=expectedFiles.OfType<JObject>().Where(x=>((string)x["path"]).StartsWith("skill/",StringComparison.Ordinal)).ToArray();
        if(expected.Length==0 || expected.Length!=changes.Length)throw Bad("Skill changes differ from the complete packaged Skill scope.");
        foreach(var item in expected)
        {
            string path=Inside(target,SkillRelative(((string)item["path"]).Substring(6)));
            if(changes.Count(c=>Same((string)c["path"],path) && (string)c["afterSha256"]==(string)item["sha256"])!=1)throw Bad("Skill change does not match the package manifest.");
        }
        var frozenManaged=(plan["priorState"] as JObject)?["managed"] as JArray??new JArray();
        var frozenOwned=new HashSet<string>(frozenManaged.OfType<JObject>().Select(x=>(string)x["path"]),StringComparer.OrdinalIgnoreCase);
        foreach(var change in changes)
        {
            string before=(string)change["before"];
            if((string)change["beforeText"]!=(before==null?null:SkillText(Convert.FromBase64String(before))) ||
                (string)change["afterText"]!=SkillText(Convert.FromBase64String((string)change["after"])))throw Bad("Skill preview text differs from its frozen bytes.");
            string ownership=frozenOwned.Contains((string)change["path"])?"retained":before==null?"create":"adopt";
            if((string)change["ownershipAction"]!=ownership || (ownership=="adopt" && !adopt))throw Bad("Skill ownership label differs from the frozen original ownership.");
        }
        if(adopt)VerifySkillAdoptionSnapshot(plan,false);
        else
        {
            if(plan["skillAdoption"]!=null && plan["skillAdoption"].Type!=JTokenType.Null)throw Bad("Unexpected Skill adoption metadata.");
            JObject current=null;
            if(File.Exists(Marker((string)plan["root"])))current=ExistingState((string)plan["root"]);
            else if(plan["priorState"] is JObject)throw Bad("Prior installation ownership disappeared.");
            var managed=(current?["managed"] as JArray)??new JArray();
            var priorPaths=new HashSet<string>(managed.OfType<JObject>().Select(x=>(string)x["path"]),StringComparer.OrdinalIgnoreCase);
            if(SkillInventory(target,false).OfType<JObject>().Any(x=>(string)x["kind"]=="file" && !priorPaths.Contains(Inside(target,(string)x["relative"]))))
                throw Bad("Existing Skill is not managed by this installation; no overwrite.");
        }
    }
    static void VerifySkillAdoptionSnapshot(JObject plan,bool afterChanges)
    {
        if(!AdoptSkill((JObject)plan["request"]))return;
        var snapshot=plan["skillAdoption"] as JObject;
        string target=SkillTarget((JObject)plan["request"],(string)plan["root"]);
        if(snapshot==null || (int?)snapshot["schema"]!=1 || (string)snapshot["target"]!=target || snapshot["entries"]?.Type!=JTokenType.Array ||
            (string)snapshot["sourceMarkerSha256"]!=FileDigest(Marker((string)plan["root"])))throw Bad("Skill adoption snapshot/installation ownership is missing or changed.");
        var current=File.Exists(Marker((string)plan["root"]))?ExistingState((string)plan["root"]):null;
        if(!JToken.DeepEquals(current,plan["priorState"]?.Type==JTokenType.Null?null:plan["priorState"]))throw Bad("Skill adoption prior ownership differs from the current installation record.");
        var expected=afterChanges?SkillExpectedAfter((JArray)snapshot["entries"],(JArray)plan["changes"],target):(JArray)snapshot["entries"].DeepClone();
        if(!JToken.DeepEquals(expected,SkillInventory(target,true)))throw Bad("Skill inventory changed; keep the files and create a new preview.");
    }
    static JArray SkillExpectedAfter(JArray inventory,JArray changes,string target)
    {
        var expected=(JArray)inventory.DeepClone();
        foreach(var change in changes.OfType<JObject>().Where(c=>(string)c["kind"]=="skill"))
        {
            string relative=SkillRelative(((string)change["path"]).Substring(target.Length+1).Replace('\\','/'));
            var old=expected.OfType<JObject>().FirstOrDefault(x=>Same((string)x["relative"],relative));
            if(old!=null) { relative=(string)old["relative"];old.Remove(); }
            else
            {
                var parentCase=expected.OfType<JObject>().Where(x=>(string)x["kind"]=="directory" && relative.StartsWith((string)x["relative"]+"/",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>((string)x["relative"]).Length).FirstOrDefault();
                if(parentCase!=null) { string prefix=(string)parentCase["relative"];relative=prefix+relative.Substring(prefix.Length); }
            }
            expected.Add(new JObject{["relative"]=relative,["kind"]="file",["sha256"]=change["afterSha256"],["length"]=Convert.FromBase64String((string)change["after"]).Length});
            string parent=Path.GetDirectoryName(relative.Replace('/',Path.DirectorySeparatorChar));
            while(!string.IsNullOrEmpty(parent))
            {
                string name=parent.Replace('\\','/');
                if(!expected.OfType<JObject>().Any(x=>Same((string)x["relative"],name)))expected.Add(new JObject{["relative"]=name,["kind"]="directory"});
                parent=Path.GetDirectoryName(parent);
            }
        }
        if(expected.Count>2048 || expected.OfType<JObject>().Where(x=>(string)x["kind"]=="file").Sum(x=>(long)x["length"])>16*1024*1024)throw Bad("Planned Skill inventory exceeds its entry or byte budget.");
        return new JArray(expected.OfType<JObject>().OrderBy(e=>(string)e["relative"],StringComparer.OrdinalIgnoreCase));
    }
}
