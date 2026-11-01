using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

internal static partial class SetupTests
{
    static object Invoke(string name, params object[] args)
    { return typeof(Program).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Reject(Action action)
    {
        bool failed = false;
        try { action(); } catch (TargetInvocationException) { failed = true; }
        Check(failed, "Expected rejection");
    }
    static int Main(string[] args)
    {
        string fixtureRoot=Environment.GetEnvironmentVariable("YUZUHA_SETUP_TEST_ROOT")??Path.GetTempPath();
        string temp = Path.Combine(Path.GetFullPath(fixtureRoot), "YuzuhaSetupTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string root = Path.Combine(temp, "PmlTrigger.Test"), json = Path.Combine(temp, "client.json"), tx = Path.Combine(temp, "tx.json");
            Directory.CreateDirectory(root);
            string isolated = Path.Combine(temp, "UserProtection"); Directory.CreateDirectory(isolated);
            string programFile = Path.Combine(isolated, "sample.pmlobj"); File.WriteAllText(programFile, "old package");
            string oldDigest = Program.FileDigest(programFile), nextDigest = Program.Digest(Encoding.UTF8.GetBytes("new package"));
            File.WriteAllText(Path.Combine(isolated, ".setup-payload.json"), new JObject { ["files"] = new JArray(new JObject { ["path"] = "sample.pmlobj", ["sha256"] = oldDigest }) }.ToString());
            Program.ProtectUserData(isolated, "sample.pmlobj", nextDigest, true);
            File.WriteAllText(programFile, "user changes");
            Reject(() => Invoke("ProtectUserData", isolated, "sample.pmlobj", nextDigest, true));
            Check(File.ReadAllText(programFile) == "user changes", "User modification overwritten");
            foreach (string rel in new[] { "Knowledge/personal.sqlite3", ".yuzuha-state/workspace.json", "Apps/apps.json", "nested/../Apps/other.json", "UserPmlLib/custom.pmlfnc", "Projects/mine.txt", "Exports/table.csv", "nested/other.db" })
                Reject(() => Invoke("ProtectUserData", isolated, rel, nextDigest, true));
            File.WriteAllText(Path.Combine(isolated, ".yuzuha-user-data.json"), "{}");
            Reject(() => Invoke("ProtectUserData", isolated, "new-file.txt", nextDigest, true));
            Console.WriteLine("PASS: upgrades allow unchanged package files, retain local edits, refuse user folders/databases and marked user-directory targets.");
            Reject(()=>Invoke("IntegrationMode",new JObject{["environments"]=new JArray()}));
            Check(Program.IntegrationMode(new JObject{["integrationMode"]="none",["environments"]=new JArray()})=="none","Explicit files-only mode");
            Reject(()=>Invoke("IntegrationMode",new JObject{["integrationMode"]="none",["environments"]=new JArray(new JObject())}));
            string autoE3d=Path.Combine(temp,"AutoE3D"), autoPdms=Path.Combine(temp,"AutoPDMS");
            Directory.CreateDirectory(autoE3d);Directory.CreateDirectory(autoPdms);
            string autoInit=Path.Combine(autoE3d,"evars.init"),autoBat=Path.Combine(autoPdms,"evars.bat");
            File.WriteAllText(autoInit,"set pmllib=old\r\nset pmlui=old\r\n");
            File.WriteAllText(autoBat,"set pmllib=old\r\nset pdmsui=old\r\n");
            var autoCandidates=new JArray();
            foreach(var entry in new[]{new[]{autoInit,"E3D2.1"},new[]{autoBat,"PDMS"}})
                autoCandidates.Add(new JObject{["product"]=entry[1],["directory"]=Path.GetDirectoryName(entry[0]),["profile"]=entry[1],
                    ["files"]=new JArray(entry[0]),["preferredFiles"]=new JArray(entry[0]),["scanComplete"]=true});
            var autoDiscovery=new JObject{["candidates"]=autoCandidates,["warnings"]=new JArray()};
            var autoInput=new JObject{["root"]=root,["environments"]=new JArray()};
            var autoRequest=Program.AutoRequest(autoInput,autoDiscovery);
            Check(((JArray)autoRequest["environments"]).Count==2,"Automatic E3D+PDMS selection");
            string emptyManifest=Path.Combine(temp,"manifest.json"); File.WriteAllText(emptyManifest,"{\"files\":[]}");
            var managerRequest=new JObject{["integrationMode"]="manager",["root"]=root,["mcpJson"]="",["skillTarget"]="",["environments"]=new JArray()};
            Check(Program.IntegrationMode(managerRequest)=="manager","Manager handoff must be distinct from none");
            Reject(()=>Invoke("AutoRequest",managerRequest,autoDiscovery));
            Reject(()=>Invoke("CreatePlan",managerRequest,emptyManifest,temp));
            var managerManifest=new JObject{["version"]="fixture",["files"]=new JArray(new JObject{["path"]="runtime/desktop/YuzuhaToolkit.Desktop.exe",["sha256"]=Program.Digest(new byte[0])},new JObject{["path"]="runtime/manager/Yuzuha.Manager.Cli.exe",["sha256"]=Program.Digest(new byte[0])})};
            string managerManifestPath=Path.Combine(temp,"manager-manifest.json");File.WriteAllText(managerManifestPath,managerManifest.ToString());
            byte[] managerEnvBefore=File.ReadAllBytes(autoInit);var managerPlan=Program.CreatePlan(managerRequest,managerManifestPath,temp);
            Check(((JArray)managerPlan["changes"]).Count==0 && File.ReadAllBytes(autoInit).SequenceEqual(managerEnvBefore),"Manager mode modified environment or proposed environment writes");
            Check(Program.Preview(managerPlan).Contains("Manager preparation required"),"Manager preview omitted deferred preparation");
            managerRequest["environments"]=new JArray(new JObject{["path"]=autoInit});Reject(()=>Invoke("IntegrationMode",managerRequest));managerRequest["environments"]=new JArray();
            string managerReport=Path.Combine(temp,"manager-report");Directory.CreateDirectory(managerReport);Invoke("WriteResultWithMode",managerReport,"success",new JArray(),null,"manager");
            var managerResult=JObject.Parse(File.ReadAllText(Path.Combine(managerReport,"result.json")));Check((string)managerResult["avevaIntegration"]=="manager-preparation-required" && (bool)managerResult["managerPreparationRequired"],"Manager handoff result falsely claimed configured or none");
            Console.WriteLine("PASS: manager mode is distinct, has real required payload, does not plan/write EVARS, refuses selected environments/auto-connect, and reports preparation required.");
            var autoPlan=Program.CreatePlan(autoRequest,emptyManifest,temp);
            Check(Program.Profiles.SequenceEqual(new[] { "AM", "PDMS", "E3D2.1", "E3D3.1.6" }), "New plans must expose only maintained Host families");
            Check(((JArray)autoPlan["changes"]).Count==2 && Program.Preview(autoPlan).Contains(autoBat),"Automatic plan omitted environment");
            Check(File.ReadAllText(autoBat)=="set pmllib=old\r\nset pdmsui=old\r\n","Automatic preview changed BAT");
            autoCandidates[0]["preferredFiles"]=new JArray(autoInit,autoInit+".other");
            Reject(()=>Invoke("AutoRequest",autoInput,autoDiscovery));
            autoCandidates[0]["preferredFiles"]=new JArray(autoInit);autoCandidates[0]["profile"]="";
            Reject(()=>Invoke("AutoRequest",autoInput,autoDiscovery));
            autoCandidates[0]["profile"]="E3D2.1";autoCandidates[0]["scanComplete"]=false;
            Reject(()=>Invoke("AutoRequest",autoInput,autoDiscovery));
            autoCandidates[0]["scanComplete"]=true;autoDiscovery["warnings"]=new JArray("access denied");
            Reject(()=>Invoke("AutoRequest",autoInput,autoDiscovery));
            Reject(()=>Invoke("AutoRequest",autoInput,new JObject{["candidates"]=new JArray(),["warnings"]=new JArray()}));
            string report=Path.Combine(temp,"report");Directory.CreateDirectory(report);
            Invoke("WriteResult",report,"success",new JArray(),null);
            Check((string)JObject.Parse(File.ReadAllText(Path.Combine(report,"result.json")))["avevaIntegration"]=="not-requested","False integration success");
            Invoke("WriteResult",report,"success",new JArray(new JObject{["kind"]="environment",["status"]="update",["path"]=autoBat}),null);
            Check((string)JObject.Parse(File.ReadAllText(Path.Combine(report,"result.json")))["avevaIntegration"]=="configured-not-launch-tested","Missing integration summary");
            Console.WriteLine("PASS: empty environment refusal, explicit none mode, automatic two-environment plan, ambiguity/unknown/incomplete/empty refusal and truthful result summary.");
            var native = Program.NativeDiscoveryText(new JObject {
                ["warnings"] = new JArray("scan\r\nnotice"),
                ["candidates"] = new JArray(new JObject {
                    ["product"]="AVEVA 中文", ["version"]="12.1", ["profile"]="PDMS",
                    ["preferredFiles"]=new JArray(@"C:\AVEVA\evars.bat"),
                    ["files"]=new JArray(@"C:\AVEVA\evars.bat"), ["scanComplete"]=true,
                    ["registry"]="HKLM\r\n[spoof]"
                })
            });
            Check(native.Contains("count=1") && native.Contains("noInit=1") && native.Contains("multiple=0"), "Native discovery fields");
            Check(native.Contains("AVEVA 中文") && !native.Contains("\r\n[spoof]"), "Native discovery Unicode/injection handling");
            Check(!typeof(Program).Assembly.GetReferencedAssemblies().Any(x=>x.Name=="System.Windows.Forms"),"WinForms reference remained");
            Console.WriteLine("PASS: native discovery protocol and no WinForms assembly dependency.");
            Check(Program.RecommendProfile("AVEVA Plant 12.1.SP4","12.1.4.48",@"C:\AVEVA\Plant\PDMS12.1.SP4") == "PDMS", "Plant profile");
            Check(Program.RecommendProfile("AVEVA Everything3D 2.1.0","2.1.0.0",temp) == "E3D2.1", "E3D profile");
            Check(Program.RecommendProfile("AVEVA Marine","12.1.4.0",temp) == "AM", "Marine profile");
            Check(Program.RecommendProfile("AVEVA Marine","unknown",temp) == "AM", "Marine uses the maintained NET35 Host policy");
            Check(Program.RecommendProfile("AVEVA PDMS","12.0.0.0",temp) == "PDMS", "PDMS uses the maintained NET35 Host policy");
            Check(Program.RecommendProfile("AVEVA Everything3D","3.1.9.0",temp) == "E3D3.1.6", "E3D 3.1 family must use the supported 3.1.6 Host");
            Check(Program.RecommendProfile("AVEVA Everything3D","3.1.11.0",temp) == "E3D3.1.6", "E3D 3.1.11 Host policy");
            Check(Program.RecommendProfile("AVEVA Everything3D","4.0.0.0",temp) == "", "Unknown family guessed");
            Check(Program.PreferredFiles(new[]{"evars.bat","Evar.INIT"},"PDMS").SequenceEqual(new[]{"Evar.INIT"}), "INIT priority");
            Check(Program.PreferredFiles(new[]{"evars.bat"},"E3D2.1").Length==0,"E3D must not use BAT");
            string env=Path.Combine(temp,"evars.bat");
            var selection=new JObject{["path"]=env,["profile"]="PDMS",["confirmed"]=true,["noInitConfirmed"]=true,["encoding"]="auto"};
            foreach(var encoding in new Encoding[]{new UTF8Encoding(false),Encoding.GetEncoding(936),new UTF8Encoding(true),new UnicodeEncoding(false,true)}) {
                byte[] bytes=encoding.GetPreamble().Concat(encoding.GetBytes("rem 中文注释\r\nset pmllib=old\r\n\r\n")).ToArray();
                File.WriteAllBytes(env,bytes);
                byte[] changed=Convert.FromBase64String((string)Program.EvarChange(root,selection)["after"]);
                Check(changed.Take(encoding.GetPreamble().Length).SequenceEqual(encoding.GetPreamble()),"BOM changed");
                string decoded=encoding.GetString(changed.Skip(encoding.GetPreamble().Length).ToArray());
                Check(changed.Take(bytes.Length).SequenceEqual(bytes),"Original content/encoding changed");
                File.WriteAllBytes(env,changed);
                Check(Convert.FromBase64String((string)Program.EvarChange(root,selection)["after"]).SequenceEqual(changed),"Managed block not idempotent");
            }
            Console.WriteLine("PASS: product profiles, INIT preference, unknown version refusal, UTF8/GBK/UTF16 original byte preservation and idempotence.");
            string product=Path.Combine(temp,"Program Files (x86)","AVEVA","Everything3D2.10");
            Directory.CreateDirectory(product);
            string init=Path.Combine(product,"EVARS.INIT"),bat=Path.Combine(product,"evars.bat");
            string example="set aveva_design_plots=C:\\Program Files (x86)\\AVEVA\\Everything3D2.10\\PMLUI\\plots\\\r\n";
            File.WriteAllText(init,example,new UTF8Encoding(false)); File.WriteAllText(bat,"@echo off\r\n");
            string toolsCopy=Path.Combine(product,"PMLLIB","tools");Directory.CreateDirectory(toolsCopy);
            File.WriteAllText(Path.Combine(toolsCopy,"evars.init"),"copy");
            var found=new List<string>();var warnings=new JArray();object[] scanArgs={product,0,found,warnings,0};
            Invoke("FindEnvironmentFiles",scanArgs);
            Check(found.Contains(init),"EVARS.INIT was not discovered");
            Check(found.Count==2 && warnings.Count==0,"Product root must outrank tools copy without deep scan warning");
            Check(Program.IsEnvironmentInit("eVaRs.InIt"),"Case insensitive EVARS matching");
            Check(Program.PreferredFiles(found,"E3D2.1").SequenceEqual(new[]{init}),"E3D EVARS preference");
            Check(Program.PreferredFiles(found,"PDMS").SequenceEqual(new[]{init}),"PDMS EVARS preference");
            var picked=new JObject{["path"]=init,["profile"]="E3D2.1",["confirmed"]=true,["encoding"]="auto"};
            Check(((string)Program.EvarChange(root,picked)["afterText"]).Contains(example),"x86 assignment was changed/rejected");
            string unrelated="if exist \"%aveva_design_exe%daemon_file\" set cadc_ipcdir=%aveva_design_exe%\r\n";
            File.WriteAllText(init,unrelated+"set pmllib=old\r\nset pmlui=ui\r\n");
            var focused=Program.EvarChange(root,picked);
            Check(((string)focused["afterText"]).Contains(unrelated),"Unrelated IF changed");
            Check(!((string)focused["afterText"]).Contains("YuzuhaFramework"),"Unnecessary framework added");
            Check(((JArray)focused["lineEdits"]).Count==3,"Expected three line edits");
            picked["startupActions"]="rehash-all";
            var special=Program.EvarChange(root,picked);
            Check(((string)special["afterText"]).Contains("set YUZUHA_STARTUP_ACTIONS=rehash-all"),"Explicit startup policy absent");
            File.WriteAllBytes(init,Convert.FromBase64String((string)special["after"]));
            picked.Remove("startupActions");
            Check(Convert.FromBase64String((string)Program.EvarChange(root,picked)["after"]).SequenceEqual(File.ReadAllBytes(init)),"Omitted update lost prior startup policy");
            picked["startupActions"]="none";
            Check(((string)Program.EvarChange(root,picked)["afterText"]).Contains("set YUZUHA_STARTUP_ACTIONS=none"),"Explicit policy disable absent");
            picked["startupActions"]="arbitrary-command";
            Reject(()=>Invoke("EvarChange",root,picked)); picked.Remove("startupActions");
            Check(((JArray)focused["lineEdits"]).OfType<JObject>().All(x=>!((string)x["after"]).Contains("\"")),"Generated assignments must be unquoted");
            File.WriteAllText(init,unrelated+"set pmllib=old\r\nset pmlui=ui\r\nset YuzuhaFramework=net35\r\n");
            Check(((string)Program.EvarChange(root,picked)["afterText"]).Contains("set YuzuhaFramework=net48"),"Existing framework override not corrected");
            foreach(string bad in new[]{"set pmllib=y & echo injected","if exist x set pmllib=y","(set pmllib=y)","set pmllib=y > out","set pmllib=y^","set pmllib=x\r\nset pmllib=y"}) {
                File.WriteAllText(init,bad); Reject(()=>Invoke("EvarChange",root,picked));
            }
            File.WriteAllText(init,example);
            picked["path"]=bat;picked["profile"]="PDMS";picked["noInitConfirmed"]=true;
            Reject(()=>Invoke("EvarChange",root,picked));
            Console.WriteLine("PASS: EVARS discovery, x86 and unrelated IF preservation, focused three-line edits, ambiguous target refusal.");
            // Uploaded vendor scripts are read/copied only, never executed or packaged.
            for(int a=0;a<args.Length;a++) {
                byte[] source=File.ReadAllBytes(args[a]);
                string folder=Path.Combine(temp,"supplied-"+a);Directory.CreateDirectory(folder);
                string copy=Path.Combine(folder,Path.GetFileName(args[a]));File.WriteAllBytes(copy,source);
                string profile=Path.GetExtension(copy).Equals(".bat",StringComparison.OrdinalIgnoreCase)?"PDMS":"E3D2.1";
                var request=new JObject{["path"]=copy,["profile"]=profile,["confirmed"]=true,["noInitConfirmed"]=true,["encoding"]="auto"};
                var change=Program.EvarChange(root,request);
                byte[] result=Convert.FromBase64String((string)change["after"]);
                Check(result.Take(source.Length).SequenceEqual(source),"Supplied original bytes changed");
                Check(((JArray)change["lineEdits"]).Count==3,"Supplied file expected three additions");
                File.WriteAllBytes(copy,result);
                Check(Convert.FromBase64String((string)Program.EvarChange(root,request)["after"]).SequenceEqual(result),"Supplied update duplicated block");
                Check(File.ReadAllBytes(args[a]).SequenceEqual(source),"Uploaded original modified");
                Console.WriteLine("PASS: supplied file preserved, tail integration and idempotence: "+Path.GetFileName(copy));
            }
            // Execute only this tiny synthetic SET/FOR/ECHO harness, never a supplied script or CALL.
            string library=Path.Combine(temp,"Vendor Long Name","pmllib"),userui=Path.Combine(temp,"Vendor Long Name","pdmsui");
            Directory.CreateDirectory(library);Directory.CreateDirectory(userui);
            string harness="@echo off\r\nset \"pmllib="+library+"\\\"\r\nset \"pdmsui="+userui+"\\\"\r\n"+
                "set \"reports=%pdmsui%reports\\\"\r\nfor %%x in (\"%pmllib%\\\") do set pmllib=%%~dpsx\r\n"+
                "for %%x in (\"%pdmsui%\\\") do set pdmsui=%%~dpsx\r\nset \"baseline_lib=%pmllib%\"\r\nset \"baseline_ui=%pdmsui%\"\r\n";
            JArray harnessEdits;
            string harnessFile=Path.Combine(temp,"controlled-test.bat");
            File.WriteAllText(harnessFile,Program.EditEnvironmentLines(harness,root,"PDMS",true,"\r\n",out harnessEdits)+
                "echo BASE_LIB=%baseline_lib%\r\necho BASE_UI=%baseline_ui%\r\necho FINAL_LIB=%pmllib%\r\necho FINAL_UI=%pdmsui%\r\necho REPORTS=%reports%\r\necho PROFILE=%Yuzuha%\r\n",Encoding.ASCII);
            var processInfo=new ProcessStartInfo("cmd.exe","/d /c \"\""+harnessFile+"\"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=Process.Start(processInfo)) {
                string output=process.StandardOutput.ReadToEnd();string error=process.StandardError.ReadToEnd();process.WaitForExit();
                Check(process.ExitCode==0,"Controlled harness failed: "+error);
                var results=output.Split(new[]{"\r\n","\n"},StringSplitOptions.RemoveEmptyEntries).Where(x=>x.Contains("=")).ToDictionary(x=>x.Substring(0,x.IndexOf('=')),x=>x.Substring(x.IndexOf('=')+1));
                Check(results["FINAL_LIB"]==Path.Combine(root,"PMLLIB")+";"+results["BASE_LIB"],"Normalization damaged final PMLLIB");
                Check(results["FINAL_UI"]==Path.Combine(root,"PMLUI")+";"+results["BASE_UI"],"Normalization damaged final PDMSUI");
                Check(results["REPORTS"]==userui+"\\reports\\" && results["PROFILE"]=="PDMS","Derived directory/profile changed");
            }
            Console.WriteLine("PASS: controlled CMD normalization, final search lists, original derived directory and Yuzuha profile.");
            string original = "{\"unrelated\":42,\"mcpServers\":{\"Other\":{\"command\":\"other.exe\"}}}";
            File.WriteAllText(json, original);
            Reject(() => Invoke("Merge", json, root));
            Check(File.ReadAllText(json) == original, "Rejected MCP registration changed another client's data");
            var cliRequest = new JObject { ["integrationMode"] = "none", ["root"] = root, ["mcpJson"] = json, ["environments"] = new JArray() };
            Reject(() => Invoke("CreatePlan", cliRequest, emptyManifest, temp));
            Reject(() => Invoke("Preflight", root, json, tx));
            File.WriteAllText(tx, new JObject { ["root"] = root, ["config"] = json, ["before"] = Convert.ToBase64String(File.ReadAllBytes(json)), ["after"] = "modified" }.ToString());
            Reject(() => Invoke("Apply", tx));
            Check(File.ReadAllText(json) == original, "Legacy MCP entry changed client configuration");
            string skillSource = Path.Combine(temp, "skill-source"); Directory.CreateDirectory(skillSource);
            File.WriteAllText(Path.Combine(skillSource, "SKILL.md"), "CLI usage fixture");
            var skillManifest = new JObject { ["version"] = "0.3.4.2", ["files"] = new JArray(new JObject { ["path"] = "skill/SKILL.md", ["sha256"] = Program.FileDigest(Path.Combine(skillSource, "SKILL.md")) }) };
            string cliManifest = Path.Combine(temp, "cli-manifest.json"); File.WriteAllText(cliManifest, skillManifest.ToString());
            cliRequest["mcpJson"] = ""; cliRequest["skillTarget"] = Path.Combine(temp, "client-skills", "yuzuha-toolkit");
            var cliPlan = Program.CreatePlan(cliRequest, cliManifest, skillSource);
            Check(((JArray)cliPlan["changes"]).Count == 1 && (string)cliPlan["changes"][0]["kind"] == "skill", "CLI-only plan must retain optional Skill copying");
            Check(!Directory.Exists((string)cliRequest["skillTarget"]) && File.ReadAllText(json) == original, "Preview changed files");
            string oldPlan = Path.Combine(temp, "old-mcp-plan.json");
            cliPlan["request"]["mcpJson"] = json;
            File.WriteAllText(oldPlan, cliPlan.ToString());
            Reject(() => Invoke("VerifyPlan", oldPlan, Program.FileDigest(oldPlan), cliManifest, true));
            cliPlan["request"]["mcpJson"] = "";
            cliPlan["changes"][0]["kind"] = "mcp";
            File.WriteAllText(oldPlan, cliPlan.ToString());
            Reject(() => Invoke("VerifyPlan", oldPlan, Program.FileDigest(oldPlan), cliManifest, true));
            Check(File.ReadAllText(json) == original, "Rejected frozen plan changed client data");
            Reject(() => Invoke("CheckPath", Path.GetPathRoot(root)));
            Reject(() => Invoke("CheckPath", Path.Combine(temp, "WrongName")));
            string atomic = Path.Combine(temp, "atomic-fixture.json");
            Invoke("AtomicBytes", atomic, Encoding.UTF8.GetBytes("before"));
            Invoke("AtomicBytes", atomic, Encoding.UTF8.GetBytes("after"));
            Check(File.ReadAllText(atomic) == "after", "Atomic file replacement failed");
            Console.WriteLine("PASS: CLI-only plan and legacy entry reject MCP registration, retain other-client bytes, preserve Skill preview, reject old or forged MCP plans, and keep atomic file writes.");
            VerifyPmlIndexLifecycle(temp);
            VerifySkillAdoptionLifecycle(temp);
            Console.WriteLine("Artifacts: " + temp); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
