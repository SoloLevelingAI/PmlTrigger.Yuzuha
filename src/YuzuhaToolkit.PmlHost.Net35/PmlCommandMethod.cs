using System;
using System.Collections.Generic;
using Aveva.PDMS.PMLNet;
using CmdAm = Aveva.Pdms.Utilities.CommandLine;

namespace YuzuhaToolkit.PmlHost
{
    [PMLNetCallable()]
    public class PmlCommandMethod
    {
        [PMLNetCallable()]
        public PmlCommandMethod()
        {
            Model = "Unknown";
            PmlCommandRpcHost.Attach(this);
        }

        internal string Model { get; private set; }

        [PMLNetCallable()]
        public void Assign(PmlCommandMethod that)
        {
            if (that != null)
                Model = that.Model;
        }

        [PMLNetCallable()]
        public void RefreshModel(string model)
        {
            Model = NormalizeModel(model);
            PmlCommandRpcHost.Attach(this);
        }

        [PMLNetCallable()]
        public string GetRpcServerStatus()
        {
            return PmlCommandRpcHost.IsRunning ? "RUNNING" : "STOPPED";
        }

        [PMLNetCallable()]
        public bool RunPmlCommand(string pmlCommand)
        {
            ValidateCommand(pmlCommand);
            return CmdAm.Command.CreateCommand(pmlCommand).Run();
        }

        [PMLNetCallable()]
        public string RunPmlCommandString(string pmlCommand)
        {
            ValidateCommand(pmlCommand);
            const string globalVar = "YUZUHAPMLRPCSTRING";
            CmdAm.Command command = CmdAm.Command.CreateCommand(
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

        public string[] GetPmlBfsList(
            string pmlCommand,
            string globalVar,
            bool deleteGlobalVar)
        {
            List<string> rows = GetRequireFromE3D.GetPmlVariableList(
                pmlCommand,
                globalVar,
                deleteGlobalVar);
            return rows.ToArray();
        }

        private static string NormalizeModel(string model)
        {
            return String.IsNullOrEmpty(model) || model.Trim().Length == 0
                ? "Unknown"
                : model.Trim();
        }

        private static void ValidateCommand(string pmlCommand)
        {
            if (String.IsNullOrEmpty(pmlCommand) ||
                pmlCommand.Trim().Length == 0)
                throw new ArgumentException(
                    "PML command cannot be empty.",
                    "pmlCommand");
        }
    }
}
