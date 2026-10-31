using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

// Reachability only. Neither a TCP response nor a running service proves licence checkout.
public sealed class CheckAvevaLicence(ITcpProbe tcp, IWindowsServiceProbe services)
{
    public async Task<bool> ExecuteAsync(LaunchRequest profile, CancellationToken ct)
    {
        if (!profile.CheckLicence) return true;
        ct.ThrowIfCancellationRequested();

        var rmsReady = profile.UseLocalRmsService
            ? services.IsRunning(profile.LocalRmsServiceName)
            : await tcp.ConnectAsync(profile.LicenceServer, profile.LicencePort, ct);
        var rulesReady = !profile.RequireRules ||
            await tcp.ConnectAsync(profile.LicenceServer, profile.RulesPort, ct);
        return rmsReady && rulesReady;
    }
}
