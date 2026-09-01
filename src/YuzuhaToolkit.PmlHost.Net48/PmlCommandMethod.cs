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
    [PMLNetCallable]
    public PmlCommandMethod()
    {
        Model = "Unknown";
        PmlCommandRpcHost.Attach(this);
    }

    internal string Model { get; private set; }

    [PMLNetCallable]
    public void Assign(PmlCommandMethod that)
    {
        if (that != null)
            Model = that.Model;
    }

    [PMLNetCallable]
    public void RefreshModel(string model)
    {
        Model = NormalizeModel(model);
        PmlCommandRpcHost.Attach(this);
    }

    [PMLNetCallable]
    public string GetRpcServerStatus()
    {
        return PmlCommandRpcHost.IsRunning ? "RUNNING" : "STOPPED";
    }

    [PMLNetCallable]
    public bool RunPmlCommand(string pmlCommand)
    {
        ValidateCommand(pmlCommand);
        return CmdAm.Command.CreateCommand(pmlCommand).Run();
    }

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
