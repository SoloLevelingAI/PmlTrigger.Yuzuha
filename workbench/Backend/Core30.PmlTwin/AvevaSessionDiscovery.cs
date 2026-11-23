using System.Diagnostics;
using System.Xml.Linq;
using static Core30.Core.FileContent;

using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static CoreData InspectAvevaSession(CoreData request, ISessionInventory sessions) =>
        request.Child("identity") is { } identity ? ConfirmProcess(identity) : DiscoverSessions(sessions);

    private static CoreData ReadAvevaHostStatus(CoreData request, IAvevaHostProbe hostProbe)
    {
        var identity = ConfirmProcess(request);
        identity.SetField("mode", "host-status");
        identity.Add(hostProbe.ReadStatus((int)identity.Field("pid")!, (long)identity.Field("startTicks")!));
        return identity;
    }

    private static CoreData DiscoverSessions(ISessionInventory discovery)
    {
        var discovered = discovery.Find();
        return new CoreData("result", new CoreField("mode", "discovery"),
            new CoreField("count", discovered.Count),
            discovered.Select(session => new CoreData("candidate",
                new CoreField("pid", session.ProcessId),
                new CoreField("startTicks", session.StartTicks),
                new CoreField("product", session.Product), new CoreField("project", ""),
                new CoreField("pipeDetected", false), new CoreField("hostVerified", false))));
    }

    private static CoreData ConfirmProcess(CoreData identity)
    {
        var pid = (int)identity.Field("pid")!;
        var ticks = (long)identity.Field("startTicks")!;
        using var process = Process.GetProcessById(pid);
        var currentTicks = process.StartTime.ToUniversalTime().Ticks;
        RequireSameStart(ticks, currentTicks);
        return new CoreData("result", new CoreField("mode", "os-identity"),
            new CoreField("pid", pid), new CoreField("startTicks", currentTicks),
            new CoreField("process", XmlText(process.ProcessName)));
    }

    private static void RequireSameStart(long expected, long actual)
    {
        if (expected != actual)
            throw new InvalidDataException("Process start time changed. Rediscover the target PID.");
    }
}
