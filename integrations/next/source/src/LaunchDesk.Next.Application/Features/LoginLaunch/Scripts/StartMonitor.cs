using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class StartMonitor(IProcessHost processes, IFileSystem files)
{
    public IProcessHandle Execute(LaunchRequest profile, ISecretHandle? secret)
    {
        if (!profile.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !files.FileExists(profile.InitializationFile))
            throw new ArgumentException("Monitor requires EXE and existing initialization file.");

        return processes.Start(AvevaProcessCommand.Create(profile, BuildArguments(profile),
            ProcessInvocation.Native, ProcessObservation.DescendantsMayDetach), secret);
    }

    // Eight ordered arguments; the placeholder is resolved only inside the process host.
    public static string[] BuildArguments(LaunchRequest profile)
    {
        var fields = new[] { profile.Product, profile.InitializationFile, profile.Project,
            profile.User, profile.Mdb, profile.Module };
        if (fields.Any(value => string.IsNullOrWhiteSpace(value) ||
            value.Any(char.IsControl) || value.Contains('"')))
            throw new ArgumentException("Monitor fields are incomplete or contain invalid characters.");

        return ["PROD", profile.Product, "init", profile.InitializationFile,
            profile.Project, profile.User + "/" + AvevaProcessCommand.SecretSlot,
            "/" + profile.Mdb.TrimStart('/'), profile.Module];
    }
}
