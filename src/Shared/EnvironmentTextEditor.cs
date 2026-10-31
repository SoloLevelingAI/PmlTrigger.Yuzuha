#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace YuzuhaToolkit.Deployment
{
    // One source compiled into the NET48 installer and NET10 manager.
    internal sealed class EnvironmentLineEdit
    {
        internal string Variable { get; private set; }
        internal int Line { get; private set; }
        internal string Before { get; private set; }
        internal string After { get; private set; }
        internal string Placement { get; private set; }
        internal EnvironmentLineEdit(string variable,int line,string before,string after,string placement)
        { Variable=variable;Line=line;Before=before;After=after;Placement=placement; }
    }
    internal static class EnvironmentTextEditor
    {
        internal static string Edit(string body,string root,string profile,bool legacy,string nl,
            out List<EnvironmentLineEdit> edits,string startupActions=null,bool includeFramework=false)
        {
        edits=new List<EnvironmentLineEdit>();
        if(Regex.IsMatch(body,@"(?m)^\s*\[") || body.Contains("\x1a")) throw new InvalidDataException("不是支持的批处理格式 / Unsupported batch file format.");
        string ui=legacy?"pdmsui":"pmlui";
        if(startupActions!=null && startupActions!="none" && startupActions!="rehash-all")
            throw new InvalidDataException("startupActions must be none or rehash-all.");
        var targets=new[]{"pmllib",ui,"Yuzuha","YuzuhaFramework"}.Concat(startupActions==null?new string[0]:new[]{"YUZUHA_STARTUP_ACTIONS"}).ToArray();
        var direct=targets.ToDictionary(x=>x,x=>new List<int>(),StringComparer.OrdinalIgnoreCase);
        var normalized=targets.ToDictionary(x=>x,x=>new List<int>(),StringComparer.OrdinalIgnoreCase);
        var lines=Regex.Matches(body,@"[^\r\n]*(?:\r\n|\n|\r|$)").Cast<Match>().Where(x=>x.Length>0).ToArray();
        int depth=0;bool continuation=false;
        var labels=new HashSet<string>(lines.Select(x=>x.Value.Trim()).Where(x=>Regex.IsMatch(x,@"^:[\w.-]+$")).Select(x=>x.Substring(1)),StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<lines.Length;i++) {
            string line=lines[i].Value.TrimEnd('\r','\n'),trim=line.TrimStart().TrimStart('@');
            if(Regex.IsMatch(trim,@"(?i)^(rem(?:\s|$)|::)"))continue;
            if(Regex.IsMatch(trim,@"(?i)^exit\b|\bexit\s+/b\b"))throw new InvalidDataException("文件存在提前退出，无法确认末尾接入 / Early exit requires review: "+line);
            foreach(Match jump in Regex.Matches(trim,@"(?i)\bgoto\s+([^\s&|]+)")) {
                string destination=jump.Groups[1].Value;
                if(destination.StartsWith(":") || !labels.Contains(destination))throw new InvalidDataException("无法确认跳转后的接入位置 / Unresolved jump: "+line);
            }
            foreach(string name in targets) {
                if(!Regex.IsMatch(line,@"(?i)\bset\s+""?"+Regex.Escape(name)+@"\s*="))continue;
                if(depth!=0 || continuation)throw new InvalidDataException("目标赋值处于块或续行中 / Target inside block or continuation: "+line);
                var simple=Regex.Match(line,@"(?i)^\s*@?set\s+""?"+Regex.Escape(name)+@"=(.*)$");
                if(simple.Success) {
                    string value=simple.Groups[1].Value;
                    if(Regex.IsMatch(value,@"[&|<>!^]") || line.Count(c=>c=='"')%2!=0)throw new InvalidDataException("目标赋值含复合语法 / Compound target assignment: "+line);
                    direct[name].Add(i);continue;
                }
                // Recognize exactly the supplied PDMS scalar short-directory conversion, not arbitrary FOR.
                var shortPath=Regex.Match(line,@"(?i)^\s*@?for\s+%%(?<loop>[a-z])\s+in\s*\(\s*""%"+Regex.Escape(name)+@"%\\?""\s*\)\s+do\s+set\s+"+Regex.Escape(name)+@"=%%~dps(?<last>[a-z])\s*$");
                if((name=="pmllib" || name==ui) && shortPath.Success && shortPath.Groups["loop"].Value==shortPath.Groups["last"].Value) {
                    normalized[name].Add(i);continue;
                }
                throw new InvalidDataException("未识别的目标变量处理 / Unrecognized target processing: "+line);
            }
            if(trim.StartsWith(")"))depth=Math.Max(0,depth-1);
            if(trim=="(" || (Regex.IsMatch(trim,@"(?i)^(if|for)\s|^\)?\s*else\b") && trim.TrimEnd().EndsWith("(")))depth++;
            continuation=line.TrimEnd().EndsWith("^");
        }
        if(depth!=0 || continuation)throw new InvalidDataException("文件末尾不是独立插入位置 / File ends inside block/continuation.");
        foreach(string name in targets) {
            if(direct[name].Count>1 || normalized[name].Count>1)throw new InvalidDataException("目标有多处未识别赋值 / Multiple target assignments: "+name);
            if(normalized[name].Count>0 && (direct[name].Count!=1 || normalized[name][0]<direct[name][0]))throw new InvalidDataException("短路径转换顺序不明确 / Unresolved normalization order: "+name);
        }
        var block=new StringBuilder("rem >>> Yuzuha managed settings"+nl);
        var values=new Dictionary<string,string> {
            {"pmllib",Path.Combine(root,"PMLLIB")+";%pmllib%"},
            {ui,Path.Combine(root,"PMLUI")+";%"+ui+"%"}, {"Yuzuha",profile}
        };
        if(includeFramework || direct["YuzuhaFramework"].Count>0)values.Add("YuzuhaFramework",legacy?"net35":"net48");
        if(startupActions!=null)values.Add("YUZUHA_STARTUP_ACTIONS",startupActions);
        int insertion=lines.Length+2;
        foreach(var item in values) {
            string added="set "+item.Key+"="+item.Value;
            block.Append(added+nl);
            edits.Add(new EnvironmentLineEdit(item.Key,insertion++,null,added,"文件末尾，原赋值/短路径转换/自定义配置之后 / At end, after original assignments, normalization and custom configuration"));
        }
        block.Append("rem <<< Yuzuha managed settings"+nl);
        return body+(body.Length==0 || body.EndsWith("\n")?"":nl)+block;
        }
    }
}
