using System.Collections.ObjectModel;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

internal static class AvevaProcessCommand
{
    internal const string SecretSlot = "<secret>";

    internal static string MdbArgument(string mdb)
    {
        var name = mdb.TrimStart('/');
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("MDB must contain a name.");
        return "/" + name;
    }

    // This feature chooses AVEVA's environment; the process host only applies the command.
    internal static ProcessCommand Create(LaunchRequest profile, string[] arguments,
        ProcessInvocation invocation, ProcessObservation observation)
    {
        var environment = new Dictionary<string, string>(profile.Environment);
        if (!string.IsNullOrWhiteSpace(profile.LicenceServer))
            environment["AVEVA_LICENSE_SERVER_LIST"] = profile.LicenceServer;

        return new(profile.ExecutablePath, profile.WorkingDirectory, arguments,
            new ReadOnlyDictionary<string, string>(environment), invocation, observation,
            SecretSlot, ["LICENSE", "LICENCE"]);
    }
}
