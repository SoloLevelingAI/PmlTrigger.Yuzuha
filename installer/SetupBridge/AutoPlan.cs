using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>安装桥接程序：发现环境、生成可审阅计划并按显式入口执行安装事务。</summary>
internal static partial class Program
{
    /// <summary>从安装请求解析 AVEVA 接入模式，拒绝未明确选择的空环境请求。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    internal static string IntegrationMode(JObject request)
    {
        string mode=(string)request["integrationMode"]??"aveva";
        var rows=request["environments"] as JArray;
        if(request["environments"]!=null && rows==null) throw Bad("environments must be an array.");
        if(mode!="aveva" && mode!="none" && mode!="manager") throw Bad("integrationMode must be manager, aveva or none.");
        if(mode=="aveva" && (rows==null || rows.Count==0))
            throw Bad("AVEVA 尚未接入：环境列表为空。运行 plan-auto 自动发现并生成预览；交给管理器准备时明确 integrationMode=manager，仅安装文件/客户端时明确 integrationMode=none。 / No AVEVA environments selected.");
        if(mode!="aveva" && rows!=null && rows.Count>0) throw Bad("integrationMode="+mode+" conflicts with selected AVEVA environments.");
        return mode;
    }
    /// <summary>新管理器交接计划必须包含可安装的桌面和管理 CLI 入口，旧显式模式不受此新增清单要求影响。</summary>
    internal static void ValidateManagerManifest(JObject manifest)
    {
        var files=manifest["files"] as JArray;
        foreach(string path in new[]{"runtime/desktop/YuzuhaToolkit.Desktop.exe","runtime/manager/Yuzuha.Manager.Cli.exe"})
            if(files==null || files.OfType<JObject>().Count(x=>(string)x["path"]==path && RegexHash((string)x["sha256"]))!=1)
                throw Bad("Manager preparation requires Desktop and Manager CLI payload: "+path);
    }
    static bool RegexHash(string value) { return value!=null && value.Length==64 && value.All(c=>Uri.IsHexDigit(c)); }
    /// <summary>依据发现结果和显式选择生成可审阅安装请求，不执行变更。</summary>
    /// <param name="input">需要规范化的调用方输入或安装请求对象。</param>
    /// <param name="discovery">用于重新核验候选的发现器或本机发现结果。</param>
    internal static JObject AutoRequest(JObject input,JObject discovery)
    {
        if((string)input["integrationMode"]=="none" || (string)input["integrationMode"]=="manager") throw Bad("plan-auto requires AVEVA integration; use plan for explicit manager/none mode.");
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
