using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class ResolveLoginSecret(ICredentialStore credentials)
{
    public ResolvedLoginSecret Execute(LaunchRequest profile, ISecretHandle? supplied) =>
        profile.PasswordPolicy == PasswordPolicy.Stored
            ? new(credentials.Read(profile.CredentialReference), ownsHandle: true)
            : new(supplied, ownsHandle: false);
}

public sealed class ResolvedLoginSecret(ISecretHandle? value, bool ownsHandle) : IDisposable
{
    public ISecretHandle? Value { get; } = value;
    public void Dispose()
    {
        if (ownsHandle) Value?.Dispose();
    }
}
