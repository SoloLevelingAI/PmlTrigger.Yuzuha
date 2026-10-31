using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Domain.Processes;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class StartMarineBatch(IProcessHost processes)
{
    public IProcessHandle Execute(LaunchRequest profile, ISecretHandle? secret)
    {
        if (!profile.ExecutablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) &&
            !profile.ExecutablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Marine requires BAT or CMD.");

        return processes.Start(AvevaProcessCommand.Create(profile, BuildArguments(profile),
            ProcessInvocation.WindowsBatch, ProcessObservation.DescendantsMayDetach), secret);
    }

    public static string[] BuildArguments(LaunchRequest profile)
    {
        if (!new[] { profile.Project, profile.User, profile.Mdb, profile.Module }
            .All(CommandLineRules.IsLiteralBatchToken))
            throw new ArgumentException("Marine fields cannot be passed literally through batch processing.");

        return [profile.Module, "noconsole", profile.Project,
            profile.User + "/" + AvevaProcessCommand.SecretSlot, "/" + profile.Mdb.TrimStart('/')];
    }
}
