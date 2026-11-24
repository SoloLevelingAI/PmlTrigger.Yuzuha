using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>安装桥接程序：发现环境、生成可审阅计划并按显式入口执行安装事务。</summary>
internal static partial class Program
{
    // Keep scalar paths intact while vendor scripts derive directories and normalize short paths.
    // Add the search lists only after that work and any returning custom-config calls finish.
    /// <summary>保留原有行序并在受管理块中生成环境变量修改与逐行预览。</summary>
    /// <param name="body">该章节清洗后的完整正文。</param>
    /// <param name="root">经校验的安装或来源根目录。</param>
    /// <param name="profile">显式选定或由发现结果推荐的 AVEVA Profile。</param>
    /// <param name="legacy">是否使用 AM/PDMS 的旧 Framework 环境规则。</param>
    /// <param name="nl">原文件使用的换行序列。</param>
    /// <param name="edits">用于保存逐项前后差异的预览集合。</param>
    /// <param name="startupActions">可选的显式启动策略；省略时不添加特殊启动变量。</param>
    internal static string EditEnvironmentLines(string body,string root,string profile,bool legacy,string nl,out JArray edits,string startupActions=null)
    {
        try
        {
            List<YuzuhaToolkit.Deployment.EnvironmentLineEdit> sharedEdits;
            var result=YuzuhaToolkit.Deployment.EnvironmentTextEditor.Edit(body,root,profile,legacy,nl,out sharedEdits,startupActions);
            edits=new JArray(sharedEdits.Select(e=>new JObject{["variable"]=e.Variable,["line"]=e.Line,["before"]=e.Before,["after"]=e.After,["placement"]=e.Placement}));
            return result;
        }
        catch(InvalidDataException error) { throw Bad(error.Message); }

    }
}
