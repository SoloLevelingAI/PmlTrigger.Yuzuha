using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    internal static string IntegrationMode(JObject request)
    {
        string mode=(string)request["integrationMode"]??"aveva";
        var rows=request["environments"] as JArray;
        if(request["environments"]!=null && rows==null) throw Bad("environments must be an array.");
        if(mode!="aveva" && mode!="none") throw Bad("integrationMode must be aveva or none.");
        if(mode=="aveva" && (rows==null || rows.Count==0))
            throw Bad("AVEVA 尚未接入：环境列表为空。运行 plan-auto 自动发现并生成预览；仅安装文件/客户端时必须明确 integrationMode=none。 / No AVEVA environments selected.");
        if(mode=="none" && rows!=null && rows.Count>0) throw Bad("integrationMode=none conflicts with selected AVEVA environments.");
        return mode;
    }
    internal static JObject AutoRequest(JObject input,JObject discovery)
    {
        if((string)input["integrationMode"]=="none") throw Bad("plan-auto requires AVEVA integration; use plan for explicit none mode.");
        if((input["environments"] as JArray)?.Count>0) throw Bad("Explicit environments supplied; use plan instead of plan-auto.");
        var request=(JObject)input.DeepClone(); var rows=new JArray(); var problems=new JArray();
        foreach(JObject candidate in (JArray)discovery["candidates"])
        {
            var files=(JArray)candidate["preferredFiles"]; string profile=(string)candidate["profile"];
            if(files.Count!=1 || !Profiles.Contains(profile) || (bool?)candidate["scanComplete"]!=true) {
                problems.Add(candidate["product"]+": "+candidate["directory"]+" — missing/ambiguous configuration, unsupported profile or incomplete scan"); continue;
            }
            string file=(string)files[0];
            rows.Add(new JObject{["path"]=file,["profile"]=profile,["encoding"]="auto",["confirmed"]=true,
                ["noInitConfirmed"]=!((JArray)candidate["files"]).Values<string>().Any(x=>Path.GetExtension(x).Equals(".init",StringComparison.OrdinalIgnoreCase))});
        }
        // Never quietly omit an installed product that could not be resolved.
        foreach(var warning in (JArray)discovery["warnings"]) problems.Add(warning.DeepClone());
        if(rows.Count==0 || problems.Count>0) throw Bad("自动计划未生成；请查看 discovery JSON 并明确选择环境，不能以空计划继续。 / Automatic discovery needs review:\r\n"+problems);
        request["integrationMode"]="aveva"; request["environments"]=rows;
        return request;
    }
    static void CreateAutomaticPlan(string requestFile,string manifest,string skill,string output)
    {
        output=Absolute(output);
        if(File.Exists(output) || File.Exists(output+".discovery.json")) throw Bad("Use a new plan output path.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var discovery=Discover();
        File.WriteAllText(output+".discovery.json",discovery.ToString(),Utf8);
        var request=AutoRequest(ReadObject(requestFile),discovery);
        var plan=CreatePlan(request,manifest,skill);
        plan["discovery"]=discovery;
        SavePlan(plan,output);
        Console.WriteLine("PREVIEW ONLY / 仅生成预览: "+output+".md");
        Console.WriteLine("AVEVA environments: "+((JArray)request["environments"]).Count+". Approval required before installation.");
    }
}
