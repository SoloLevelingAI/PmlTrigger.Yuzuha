using System.Security;

namespace Core30.Core;

/// <summary>Controls the current Windows user's PmlTwin command channel.</summary>
public static class PmlChannelMode
{
    public const string Variable = "PMLTWIN_PML_MODE";
    public const string ReadOnly = "read-only";
    public const string AllowExecution = "allow-execution";
    public const string DeniedCode = "PML_READ_ONLY";
    public const string DeniedMessage = "当前为人工编辑模式。可用结构化查询和经确认授权的只读函数，普通 PML 指令已拦截。";

    public static string Read()
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User);
            return value is null || value.Trim().Equals(AllowExecution, StringComparison.OrdinalIgnoreCase)
                ? AllowExecution : ReadOnly;
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException)
        {
            return ReadOnly;
        }
    }

    public static void SetReadOnly(bool enabled) =>
        Environment.SetEnvironmentVariable(Variable, enabled ? ReadOnly : AllowExecution, EnvironmentVariableTarget.User);
}
