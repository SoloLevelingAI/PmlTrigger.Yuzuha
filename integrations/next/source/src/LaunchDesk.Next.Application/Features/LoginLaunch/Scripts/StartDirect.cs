using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class StartDirect(IProcessHost processes)
{
    public IProcessHandle Execute(LaunchRequest profile, ISecretHandle? secret) =>
        processes.Start(AvevaProcessCommand.Create(profile, [],
            ProcessInvocation.Native, ProcessObservation.SingleProcess), secret);
}
