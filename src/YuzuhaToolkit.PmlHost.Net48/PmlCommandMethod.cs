using System;
using Aveva.Core.PMLNet;
using CmdAm = Aveva.Core.Utilities.CommandLine;

namespace YuzuhaToolkit.PmlHost;

/// <summary>
///     PMLNet entry point. PML must construct this class on the AVEVA main
///     thread before the modern client invokes an RPC operation.
/// </summary>
[PMLNetCallable]
public class PmlCommandMethod
{
    private static readonly FormRecordWriter _records = new FormRecordWriter();

    /// <summary>Append one PML ARRAY and UI JPEG to the process session JSONL.</summary>
    /// <param name="record">要完整保存的 PML ARRAY 对应 Hashtable 数据。</param>
    [PMLNetCallable]
    public bool Log(System.Collections.Hashtable record) { return _records.Write(record); }

    /// <summary>保存操作前数组与截图，返回待配对记录编号；失败时返回空编号。</summary>
    /// <param name="record">要完整保存的 PML ARRAY 对应 Hashtable 数据。</param>
    [PMLNetCallable]
    public string BeginLog(System.Collections.Hashtable record) { return _records.Begin(record); }

    /// <summary>按记录编号追加操作后数组与截图；完成后同编号不能重复配对。</summary>
    /// <param name="record">要完整保存的 PML ARRAY 对应 Hashtable 数据。</param>
    /// <param name="recordId">BeginLog 返回的待配对操作记录编号。</param>
    [PMLNetCallable]
    public bool EndLog(System.Collections.Hashtable record, string recordId) { return _records.Complete(record, recordId); }

    /// <summary>读取最近一次记录尝试使用的 JSONL 路径。</summary>
    [PMLNetCallable]
    public string GetLastLogPath() { return _records.LastPath; }

    /// <summary>读取最近一次数组或截图记录的错误，不清空此前记录。</summary>
    [PMLNetCallable]
    public string GetLastLogError() { return _records.LastError; }

    /// <summary>在当前 AVEVA 主线程创建 PMLNet 目标并绑定 PID 专用 RPC 服务。</summary>
    [PMLNetCallable]
    public PmlCommandMethod()
    {
        Model = "Unknown";
        PmlCommandRpcHost.Attach(this);
    }

    /// <summary>Host 绑定的当前 AVEVA 模块标识。</summary>
    internal string Model { get; private set; }

    /// <summary>从另一个 PMLNet 对象复制模块标识，不执行模型命令。</summary>
    /// <param name="that">用于复制模块身份的另一个 PMLNet 对象。</param>
    [PMLNetCallable]
    public void Assign(PmlCommandMethod that)
    {
        if (that != null)
            Model = that.Model;
    }

    /// <summary>更新当前模块名称，并把本对象绑定为主线程 RPC 目标。</summary>
    /// <param name="model">Host 当前运行的 AVEVA 模块名称。</param>
    [PMLNetCallable]
    public void RefreshModel(string model)
    {
        Model = NormalizeModel(model);
        PmlCommandRpcHost.Attach(this);
    }

    /// <summary>读取当前进程 RPC 监听状态，返回 RUNNING 或 STOPPED。</summary>
    [PMLNetCallable]
    public string GetRpcServerStatus()
    {
        return PmlCommandRpcHost.IsRunning ? "RUNNING" : "STOPPED";
    }

    /// <summary>在当前 AVEVA 主线程执行一次非空命令并返回 AVEVA 执行标志。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    [PMLNetCallable]
    public bool RunPmlCommand(string pmlCommand)
    {
        ValidateCommand(pmlCommand);
        return CmdAm.Command.CreateCommand(pmlCommand).Run();
    }

    /// <summary>执行返回字符串的表达式，并在 finally 中清理临时 PML 全局变量。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    [PMLNetCallable]
    public string RunPmlCommandString(string pmlCommand)
    {
        ValidateCommand(pmlCommand);
        const string globalVar = "YUZUHAPMLRPCSTRING";
        var command = CmdAm.Command.CreateCommand(
            "!!" + globalVar + " = " + pmlCommand);
        try
        {
            if (!command.Run())
                throw new InvalidOperationException(
                    "AVEVA Command.Run returned false.");

            return command.GetPMLVariableString(globalVar);
        }
        finally
        {
            CmdAm.Command.CreateCommand(
                "!!" + globalVar + ".delete()").Run();
        }
    }

    // Called by the RPC service only. PMLNet does not reliably expose
    // string-array return values, so this method is not PMLNetCallable.
    /// <summary>读取 PML 全局数组并返回完整字符串行；不经 PMLNet 返回数组。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    /// <param name="globalVar">用于承载结果的 PML 全局数组变量名。</param>
    /// <param name="deleteGlobalVar">读取结束时是否清理该结果全局变量。</param>
    public string[] GetPmlBfsList(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar)
    {
        var rows = GetRequireFromE3D.GetPmlVariableList(
            pmlCommand,
            globalVar,
            deleteGlobalVar);
        return rows.ToArray();
    }

    private static void ValidateCommand(string pmlCommand)
    {
        if (string.IsNullOrEmpty(pmlCommand) ||
            pmlCommand.Trim().Length == 0)
            throw new ArgumentException(
                "PML command cannot be empty.",
                "pmlCommand");
    }

    private static string NormalizeModel(string model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? "Unknown"
            : model.Trim();
    }
}
