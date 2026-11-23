namespace Core30.Core;

/// <summary>Records the current Windows user's consent to command-line use of an existing launch credential.</summary>
public static class AiLaunchAuthorization
{
    public const string Variable = "PMLTWIN_ALLOW_AI_LAUNCH";

    public static bool IsEnabled => OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User) == "1";

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("LaunchDesk 的已保存凭据使用 Windows 凭据管理器。");
        Environment.SetEnvironmentVariable(Variable, enabled ? "1" : null, EnvironmentVariableTarget.User);
    }
}
