using System.Diagnostics;
using Core30.Core;

namespace Core30.Knowledge;

internal interface ISagWikiProcess
{
    string Run(ProcessStartInfo command, TimeSpan timeout);
}

/// <summary>Runs the external CLI without a shell or inherited toolbox pipes.</summary>
internal sealed class SagWikiProcess : ISagWikiProcess
{
    public string Run(ProcessStartInfo command, TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidDataException("SAG-Wiki 桥接需要 Windows。");
        var result = WindowsDetachedProcess.RunCaptured(command, timeout);
        if (result.ExitCode != 0)
            throw new IOException($"SAG-Wiki 退出码 {result.ExitCode}。{ErrorSummary(result.Error)}");
        return result.Output;
    }

    private static string ErrorSummary(string error)
    {
        var line = error.Split('\n', 2)[0].TrimEnd('\r');
        return line.Length <= 500 ? line : line[..500];
    }
}
