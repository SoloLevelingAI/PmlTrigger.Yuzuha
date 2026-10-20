using System;
using System.Collections.Generic;
using CmdAm = Aveva.Pdms.Utilities.CommandLine;

namespace YuzuhaToolkit.PmlHost
{
    /// <summary>通过 AVEVA 命令读取全局数组，逐项返回字符串并清理临时变量。</summary>
    internal static class GetRequireFromE3D
    {
        /// <summary>执行数组赋值、SIZE 和逐项读取，结束时按选项清理全局与临时变量。</summary>
        /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
        /// <param name="globalVar">用于承载结果的 PML 全局数组变量名。</param>
        /// <param name="deleteGlobalVar">读取结束时是否清理该结果全局变量。</param>
        internal static List<string> GetPmlVariableList(
            string pmlCommand,
            string globalVar,
            bool deleteGlobalVar)
        {
            ValidateGlobalName(globalVar, "globalVar");

            const string sizeVar = "YUZUHAPMLRPCSIZE";
            const string itemVar = "YUZUHAPMLRPCITEM";
            List<string> result = new List<string>();

            CmdAm.Command assign = CmdAm.Command.CreateCommand(
                "!!" + globalVar + " = " + pmlCommand);
            CmdAm.Command getSize = CmdAm.Command.CreateCommand(
                "!!" + sizeVar + " = !!" + globalVar + ".SIZE()");

            try
            {
                if (!assign.Run() || !getSize.Run())
                    throw new InvalidOperationException(
                        "PML list command or SIZE() query failed.");

                double rawSize = getSize.GetPMLVariableReal(sizeVar);
                if (rawSize < 0 || rawSize > Int32.MaxValue ||
                    rawSize != Math.Truncate(rawSize))
                    throw new InvalidOperationException(
                        "PML list returned an invalid size: " + rawSize);

                int count = (int)rawSize;
                for (int index = 1; index <= count; index++)
                {
                    CmdAm.Command readItem = CmdAm.Command.CreateCommand(
                        "VAR !!" + itemVar + " VAR !!" + globalVar +
                        "[" + index + "]");
                    if (!readItem.Run())
                        throw new InvalidOperationException(
                            "Failed to read PML list item " + index + ".");

                    result.Add(readItem.GetPMLVariableString(itemVar));
                }

                return result;
            }
            finally
            {
                CmdAm.Command.CreateCommand(
                    "!!" + itemVar + ".delete()").Run();
                CmdAm.Command.CreateCommand(
                    "!!" + sizeVar + ".delete()").Run();
                if (deleteGlobalVar)
                    CmdAm.Command.CreateCommand(
                        "!!" + globalVar + ".delete()").Run();
            }
        }

        private static void ValidateGlobalName(
            string value,
            string parameterName)
        {
            if (String.IsNullOrEmpty(value) || value.Trim().Length == 0)
                throw new ArgumentException(
                    "A PML global variable name is required.",
                    parameterName);

            string name = value.Trim();
            for (int index = 0; index < name.Length; index++)
            {
                char character = name[index];
                if (!(Char.IsLetterOrDigit(character) || character == '_'))
                    throw new ArgumentException(
                        "PML global variable names may contain only letters, " +
                        "digits, and underscore.",
                        parameterName);
            }
        }
    }
}
