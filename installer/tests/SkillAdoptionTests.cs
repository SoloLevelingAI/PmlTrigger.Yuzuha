using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

/// <summary>只在隔离临时文件验证 Skill 显式接管、快照拒绝和原字节恢复，不运行产品安装器。</summary>
internal static partial class SetupTests
{
    static void VerifySkillAdoptionLifecycle(string temp)
    {
        string area=Path.Combine(temp,"skill-adoption"),root=Path.Combine(area,"PmlTrigger.SkillAdoption"),source=Path.Combine(area,"source");
        string target=Path.Combine(area,"client","yuzuha-toolkit"),manifest=Path.Combine(area,"manifest.json"),marker=Path.Combine(root,".yuzuha-inno.json");
        Directory.CreateDirectory(root);Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        Action<string,string,string> put=(directory,relative,text)=>{string file=Path.Combine(directory,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text,new UTF8Encoding(false));};
        put(source,"SKILL.md","identical package CLI instructions\n");put(source,"references/lifecycle.md","new Manager guide\n");put(source,"new/created.md","new package reference\n");
        put(target,"SKILL.md","identical package CLI instructions\n");put(target,"personal/keep.txt","PRIVATE_EXTRA_MUST_NOT_LEAK\n");
        string lifecycle=Path.GetFullPath(Path.Combine(target,"references/lifecycle.md")),extra=Path.GetFullPath(Path.Combine(target,"personal/keep.txt")),sameFile=Path.Combine(target,"SKILL.md"),created=Path.GetFullPath(Path.Combine(target,"new/created.md"));
        byte[] original=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("PRIVATE_ORIGINAL_SKILL\r\n保留原字节\r\n")).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(lifecycle));File.WriteAllBytes(lifecycle,original);byte[] sameBefore=File.ReadAllBytes(sameFile),extraBefore=File.ReadAllBytes(extra);
        Action writeManifest=()=>File.WriteAllText(manifest,new JObject{["version"]="0.3.4.2",["files"]=new JArray(Directory.GetFiles(source,"*",SearchOption.AllDirectories).Select(file=>new JObject{["path"]="skill/"+file.Substring(source.Length+1).Replace('\\','/'),["sha256"]=Program.FileDigest(file)}))}.ToString());
        writeManifest();
        var request=new JObject{["integrationMode"]="none",["root"]=root,["mcpJson"]="",["skillTarget"]=target,["environments"]=new JArray()};
        Reject(()=>Invoke("CreatePlan",request,manifest,source));
        foreach(JToken invalid in new JToken[]{new JValue("true"),new JValue(1),JValue.CreateNull(),new JArray()})
        { request["adoptSkill"]=invalid;Reject(()=>Invoke("CreatePlan",request,manifest,source)); }
        request["adoptSkill"]=true;request["skillTarget"]="";Reject(()=>Invoke("CreatePlan",request,manifest,source));request["skillTarget"]=target;
        var absent=(JObject)request.DeepClone();absent["skillTarget"]=Path.Combine(area,"absent","yuzuha-toolkit");Reject(()=>Invoke("CreatePlan",absent,manifest,source));
        var plan=Program.CreatePlan(request,manifest,source);
        Check(((JArray)plan["changes"]).Count==3,"Adoption scope must contain exactly packaged Skill targets");
        Check(((JArray)plan["changes"]).OfType<JObject>().Count(c=>(string)c["ownershipAction"]=="adopt")==2,"Unchanged matching file was not explicitly adopted");
        Check(((JArray)plan["skillAdoption"]["entries"]).OfType<JObject>().Any(e=>(string)e["relative"]=="personal/keep.txt"),"Extra file absent from complete frozen inventory");
        string preview=Program.Preview(plan);Check(preview.Contains("Explicit adoption") && preview.Contains("PRIVATE_ORIGINAL_SKILL") && preview.Contains("new Manager guide") && !preview.Contains("PRIVATE_EXTRA_MUST_NOT_LEAK"),"Adoption preview must show owned before/after and preserve extra contents privately");
        Check(File.ReadAllBytes(lifecycle).SequenceEqual(original) && !File.Exists(created),"Adoption preview wrote target bytes");
        string planFile=Path.Combine(area,"plan.json");var console=new StringWriter();var originalConsole=Console.Out;
        try { Console.SetOut(console);Program.SavePlan(plan,planFile); } finally { Console.SetOut(originalConsole); }
        Check(!console.ToString().Contains("PRIVATE_ORIGINAL_SKILL"),"Saving a private preview leaked original text to console");
        Action populate=()=>{foreach(JObject item in (JArray)JObject.Parse(File.ReadAllText(manifest))["files"]){string file=Path.Combine(root,((string)item["path"]).Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(file));File.Copy(Path.Combine(source,((string)item["path"]).Substring(6)),file,true);}File.Copy(manifest,Path.Combine(root,".setup-payload.json"),true);};
        populate();
        Action verify=()=>Invoke("VerifyPlan",planFile,Program.FileDigest(planFile),manifest,false);
        verify();
        put(target,"added.txt","concurrent addition");Reject(verify);File.Delete(Path.Combine(target,"added.txt"));
        put(target,"personal/keep.txt","concurrent extra edit");Reject(verify);File.WriteAllBytes(extra,extraBefore);
        File.Delete(extra);Reject(verify);File.WriteAllBytes(extra,extraBefore);
        string addedDirectory=Path.Combine(target,"new-empty-directory");Directory.CreateDirectory(addedDirectory);Reject(verify);Directory.Delete(addedDirectory);
        File.AppendAllText(lifecycle,"concurrent packaged-target edit");Reject(verify);File.WriteAllBytes(lifecycle,original);
        File.Delete(sameFile);Reject(verify);File.WriteAllBytes(sameFile,sameBefore);
        File.WriteAllBytes(lifecycle,new byte[]{255,254,97});Reject(()=>Invoke("CreatePlan",request,manifest,source));File.WriteAllBytes(lifecycle,original);
        File.WriteAllBytes(lifecycle,new byte[]{97,0,98});Reject(()=>Invoke("CreatePlan",request,manifest,source));File.WriteAllBytes(lifecycle,original);
        VerifySkillAdoptionLinkRefusal(area);
        int forgedNumber=0;
        Action<Action<JObject>> forged=mutate=>{var copy=(JObject)plan.DeepClone();mutate(copy);string file=Path.Combine(area,"forged-"+(forgedNumber++)+".json");File.WriteAllText(file,copy.ToString());Reject(()=>Invoke("VerifyPlan",file,Program.FileDigest(file),manifest,false));};
        forged(p=>p.Remove("skillAdoption"));
        forged(p=>((JArray)p["skillAdoption"]["entries"])[0]["relative"]="../escape");
        forged(p=>((JArray)p["skillAdoption"]["entries"]).Add(((JArray)p["skillAdoption"]["entries"])[0].DeepClone()));
        forged(p=>((JArray)p["changes"])[0].Remove());
        forged(p=>((JArray)p["changes"])[0]["beforeText"]="forged review text");
        forged(p=>{var c=(JObject)((JArray)p["changes"])[0];byte[] bytes=Encoding.UTF8.GetBytes("forged proposed bytes");c["after"]=Convert.ToBase64String(bytes);c["afterSha256"]=Program.Digest(bytes);});
        File.WriteAllText(marker,"temporary ownership change");Reject(verify);
        string addedMarkerReport=Path.Combine(area,"new-marker-rejected");Directory.CreateDirectory(addedMarkerReport);Reject(()=>Invoke("CommitConfigurationChanges",plan,addedMarkerReport,new Action<JObject,JArray>((state,actual)=>{})));
        Check(File.ReadAllBytes(lifecycle).SequenceEqual(original) && !File.Exists(created),"New marker was ignored before commit");File.Delete(marker);verify();
        byte[] tooLarge=new byte[4*1024*1024+1];File.WriteAllBytes(Path.Combine(target,"oversized.txt"),tooLarge);Reject(()=>Invoke("CreatePlan",request,manifest,source));File.Delete(Path.Combine(target,"oversized.txt"));
        Console.WriteLine("PASS: Skill adoption is explicit strict-bool, complete inventory/hash frozen, no preview writes or console text leaks; addition/edit/deletion/directory/link/ownership drift, forged scope/text/content and byte-budget violations are refused.");

        string rollback=Path.Combine(area,"rollback");Directory.CreateDirectory(rollback);
        Reject(()=>Invoke("CommitConfigurationChanges",plan,rollback,new Action<JObject,JArray>((state,actual)=>{throw new IOException("fixture finalization failure");})));
        Check(File.ReadAllBytes(lifecycle).SequenceEqual(original) && File.ReadAllBytes(sameFile).SequenceEqual(sameBefore) && File.ReadAllBytes(extra).SequenceEqual(extraBefore) && !File.Exists(created),"Existing transaction did not restore adopted originals after failure");
        // Empty directories left by the existing rollback engine remain user-safe;
        // regenerate the exact inventory after a failed transaction.
        root=Path.Combine(area,"PmlTrigger.SkillAdoptionRetry");marker=Path.Combine(root,".yuzuha-inno.json");Directory.CreateDirectory(root);request["root"]=root;
        var retry=Program.CreatePlan(request,manifest,source);
        populate();
        string report=Path.Combine(area,"success");Directory.CreateDirectory(report);JObject first=null;
        Invoke("CommitConfigurationChanges",retry,report,new Action<JObject,JArray>((state,actual)=>{first=(JObject)state.DeepClone();Invoke("AtomicBytes",marker,Encoding.UTF8.GetBytes(state.ToString()));}));
        var managed=(JArray)first["managed"];
        Check(managed.Count==3 && !managed.OfType<JObject>().Any(c=>(string)c["path"]==extra),"Extra file was claimed as installer-owned");
        Check(managed.OfType<JObject>().Single(c=>(string)c["path"]==lifecycle)["original"].ToString()==Convert.ToBase64String(original),"First original byte backup was lost");
        Check(managed.OfType<JObject>().Single(c=>(string)c["path"]==sameFile)["original"].ToString()==Convert.ToBase64String(sameBefore),"Unchanged adopted file has no original recovery bytes");
        File.Delete(extra);Directory.Delete(Path.GetDirectoryName(extra));
        var ordinary=(JObject)request.DeepClone();ordinary["adoptSkill"]=false;var ordinaryPlan=Program.CreatePlan(ordinary,manifest,source);
        var noTarget=(JObject)ordinary.DeepClone();noTarget["skillTarget"]="";var noTargetPlan=Program.CreatePlan(noTarget,manifest,source);
        int rejectedOwnershipNumber=0;
        Action<JObject,bool> rejectFrozen=(copy,commit)=>
        {
            string file=Path.Combine(area,"ownership-rejected-"+(rejectedOwnershipNumber++)+".json");File.WriteAllText(file,copy.ToString());
            Reject(()=>Invoke("VerifyPlan",file,Program.FileDigest(file),manifest,false));
            if(commit)
            {
                string folder=file+".report";Directory.CreateDirectory(folder);
                Reject(()=>Invoke("CommitConfigurationChanges",copy,folder,new Action<JObject,JArray>((state,actual)=>{})));
            }
            Check(File.ReadAllBytes(sameFile).SequenceEqual(sameBefore) && File.ReadAllText(lifecycle)=="new Manager guide\n" && File.Exists(created),"Rejected frozen ownership changed Skill targets");
        };
        foreach(var valid in new[]{ordinaryPlan,noTargetPlan})
        {
            string validFile=Path.Combine(area,"valid-scope-"+Guid.NewGuid().ToString("N")+".json");Program.SavePlan(valid,validFile);Invoke("VerifyPlan",validFile,Program.FileDigest(validFile),manifest,false);
            var missingContext=(JObject)valid.DeepClone();missingContext.Remove("markerBeforeSha256");rejectFrozen(missingContext,true);
            var missingPrior=(JObject)valid.DeepClone();missingPrior.Remove("priorState");rejectFrozen(missingPrior,true);
            var rewrittenOriginal=(JObject)valid.DeepClone();rewrittenOriginal["priorState"]["managed"][0]["original"]=Convert.ToBase64String(Encoding.UTF8.GetBytes("forged first original"));rejectFrozen(rewrittenOriginal,true);
            string hidden=Path.Combine(area,"unselected-client.txt");File.WriteAllText(hidden,"unselected client must remain unchanged");
            var hiddenOwnership=(JObject)valid.DeepClone();((JArray)hiddenOwnership["priorState"]["managed"]).Add(new JObject{["path"]=hidden,["kind"]="skill",["afterSha256"]=Program.FileDigest(hidden),["original"]=Convert.ToBase64String(Encoding.UTF8.GetBytes("forged external restore"))});rejectFrozen(hiddenOwnership,true);
            Check(File.ReadAllText(hidden)=="unselected client must remain unchanged","Unselected client was changed by rejected priorState forgery");
        }
        var falseText=(JObject)ordinaryPlan.DeepClone();falseText["changes"][0]["beforeText"]="unrelated review text";rejectFrozen(falseText,false);
        var falseLabel=(JObject)ordinaryPlan.DeepClone();falseLabel["changes"][0]["ownershipAction"]="create";rejectFrozen(falseLabel,false);
        byte[] validMarker=File.ReadAllBytes(marker);
        File.AppendAllText(marker," ");rejectFrozen((JObject)noTargetPlan.DeepClone(),true);File.WriteAllBytes(marker,validMarker);
        File.Delete(marker);rejectFrozen((JObject)noTargetPlan.DeepClone(),true);File.WriteAllBytes(marker,validMarker);
        foreach(Action<JObject> corrupt in new Action<JObject>[] { entry=>entry.Remove("original"),entry=>entry["original"]=3,entry=>entry["original"]="not valid base64!" })
        {
            var bad=(JObject)first.DeepClone();var badEntry=((JArray)bad["managed"]).OfType<JObject>().Single(c=>(string)c["path"]==lifecycle);corrupt(badEntry);File.WriteAllText(marker,bad.ToString());
            Reject(()=>Invoke("CreatePlan",request,manifest,source));
            var badPlan=(JObject)noTargetPlan.DeepClone();badPlan["priorState"]=bad;badPlan["markerBeforeSha256"]=Program.FileDigest(marker);rejectFrozen(badPlan,true);
            string folder=Path.Combine(area,"corrupt-restore-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);Reject(()=>Invoke("RestoreManagedFiles",(JArray)bad["managed"],folder));Check(!Directory.GetFiles(folder).Any(),"Corrupt original wrote backups or restored earlier entries before refusal");
            Check(File.ReadAllText(lifecycle)=="new Manager guide\n" && File.ReadAllBytes(sameFile).SequenceEqual(sameBefore) && File.Exists(created),"Malformed original deleted or changed managed data");File.WriteAllBytes(marker,validMarker);
        }
        Console.WriteLine("PASS: every plan/commit binds raw marker hash and complete priorState even with adopt=false or no Skill target; missing/forged ownership/original/context and normal preview text/label tampering fail before external writes.");
        put(target,"personal/keep.txt","PRIVATE_EXTRA_MUST_NOT_LEAK\n");
        put(source,"references/lifecycle.md","second package guide\n");writeManifest();var upgrade=Program.CreatePlan(request,manifest,source);populate();
        string upgradeFile=Path.Combine(area,"upgrade.json");Program.SavePlan(upgrade,upgradeFile);Invoke("VerifyPlan",upgradeFile,Program.FileDigest(upgradeFile),manifest,false);
        string nextReport=Path.Combine(area,"upgrade-success");Directory.CreateDirectory(nextReport);JObject next=null;
        Invoke("CommitConfigurationChanges",upgrade,nextReport,new Action<JObject,JArray>((state,actual)=>{next=(JObject)state.DeepClone();}));
        Check(((JArray)next["managed"]).OfType<JObject>().Single(c=>(string)c["path"]==lifecycle)["original"].ToString()==Convert.ToBase64String(original),"Later update replaced the first original bytes");
        string restore=Path.Combine(area,"uninstall-files");Directory.CreateDirectory(restore);Invoke("RestoreManagedFiles",(JArray)next["managed"],restore);
        Check(File.ReadAllBytes(lifecycle).SequenceEqual(original) && File.ReadAllBytes(sameFile).SequenceEqual(sameBefore) && File.ReadAllBytes(extra).SequenceEqual(extraBefore) && !File.Exists(created) && Directory.Exists(target),"Shared uninstall loop lost originals, extra data or the existing directory");
        Console.WriteLine("PASS: real temporary-file adoption reuses backups/rollback/managed.original and the real uninstall loop; unchanged targets retain originals, ordinary managed upgrades remain supported, first bytes survive updates, and extras/directories remain untouched.");
    }

    static void VerifySkillAdoptionLinkRefusal(string ordinaryArea)
    {
        string parent=Environment.GetEnvironmentVariable("YUZUHA_SETUP_LINK_TEST_ROOT")??ordinaryArea;
        string area=Path.Combine(Path.GetFullPath(parent),"skill-link-"+Guid.NewGuid().ToString("N")),root=Path.Combine(area,"PmlTrigger.SkillLink"),source=Path.Combine(area,"source"),target=Path.Combine(area,"client","yuzuha-toolkit");
        Directory.CreateDirectory(root);Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source,"SKILL.md"),"new packaged Skill");File.WriteAllText(Path.Combine(target,"SKILL.md"),"original Skill");
        string manifest=Path.Combine(area,"manifest.json");File.WriteAllText(manifest,new JObject{["version"]="fixture",["files"]=new JArray(new JObject{["path"]="skill/SKILL.md",["sha256"]=Program.FileDigest(Path.Combine(source,"SKILL.md"))})}.ToString());
        var request=new JObject{["integrationMode"]="none",["root"]=root,["mcpJson"]="",["skillTarget"]=target,["adoptSkill"]=true,["environments"]=new JArray()};
        var plan=Program.CreatePlan(request,manifest,source);string planFile=Path.Combine(area,"plan.json");Program.SavePlan(plan,planFile);
        Directory.CreateDirectory(Path.Combine(root,"skill"));File.Copy(Path.Combine(source,"SKILL.md"),Path.Combine(root,"skill/SKILL.md"));
        Invoke("VerifyPlan",planFile,Program.FileDigest(planFile),manifest,false);
        string outside=Path.Combine(area,"outside"),junction=Path.Combine(target,"linked");Directory.CreateDirectory(outside);
        string prepared=Environment.GetEnvironmentVariable("YUZUHA_SETUP_LINK_JUNCTION");
        if(!string.IsNullOrEmpty(prepared))
        {
            prepared=Path.GetFullPath(prepared);
            Check(prepared.StartsWith(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Prepared junction escaped the explicit link fixture root");
            Check((File.GetAttributes(prepared)&FileAttributes.ReparsePoint)!=0,"Prepared link is not a real ReparsePoint");
            Directory.Move(prepared,junction);
        }
        else using(var process=Process.Start(new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+junction+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}))
        { string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();process.WaitForExit();Check(process.ExitCode==0,"Temporary junction fixture could not be created: "+output+error); }
        try
        {
            Check((File.GetAttributes(junction)&FileAttributes.ReparsePoint)!=0,"Link fixture is not a real ReparsePoint");
            Reject(()=>Invoke("VerifyPlan",planFile,Program.FileDigest(planFile),manifest,false));Reject(()=>Invoke("CreatePlan",request,manifest,source));
        }
        finally
        {
            Check(Path.GetFullPath(junction).StartsWith(Path.GetFullPath(area)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Junction cleanup escaped fixture");
            if(string.IsNullOrEmpty(prepared))Directory.Delete(junction);else Directory.Move(junction,prepared);
        }
    }
}
