using System;
using System.Collections.Generic;
using CmdAm = Aveva.Core.Utilities.CommandLine;

namespace YuzuhaToolkit.PmlHost;

internal static class GetRequireFromE3D
{
    internal static List<string> GetPmlVariableList(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar)
    {
        ValidateGlobalName(globalVar, "globalVar");

        const string sizeVar = "YUZUHAPMLRPCSIZE";
        const string itemVar = "YUZUHAPMLRPCITEM";
        var result = new List<string>();

        var assign = CmdAm.Command.CreateCommand(
            "!!" + globalVar + " = " + pmlCommand);
        var getSize = CmdAm.Command.CreateCommand(
            "!!" + sizeVar + " = !!" + globalVar + ".SIZE()");

        try
        {
            if (!assign.Run() || !getSize.Run())
                throw new InvalidOperationException(
                    "PML list command or SIZE() query failed.");

            var rawSize = getSize.GetPMLVariableReal(sizeVar);
            if (rawSize < 0 || rawSize > int.MaxValue ||
                rawSize != Math.Truncate(rawSize))
                throw new InvalidOperationException(
                    "PML list returned an invalid size: " + rawSize);

            var count = (int)rawSize;
            for (var index = 1; index <= count; index++)
            {
                var readItem = CmdAm.Command.CreateCommand(
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

    private static void ValidateGlobalName(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            throw new ArgumentException(
                "A PML global variable name is required.",
                parameterName);

        var name = value.Trim();
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (!(char.IsLetterOrDigit(character) || character == '_'))
                throw new ArgumentException(
                    "PML global variable names may contain only letters, " +
                    "digits, and underscore.",
                    parameterName);
        }
    }
}
