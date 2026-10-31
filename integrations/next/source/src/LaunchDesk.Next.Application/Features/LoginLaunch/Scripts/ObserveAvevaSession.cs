using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class ObserveAvevaSession(IRecordLog log, TimeSpan threshold)
{
    private readonly ConcurrentDictionary<Guid, LaunchEvent> latest = new();
    private long sequence;

    public void Publish(LaunchResult result) =>
        latest[result.RunId!.Value] = new(Interlocked.Increment(ref sequence), result);
    public LaunchResult? GetLatest(Guid runId) => latest.TryGetValue(runId, out var value) ? value.Result : null;
    public void Begin(IProcessHandle process, LaunchRequest request, Guid runId, Action releaseAdmission) =>
        _ = ObserveProcessAsync(process, request, runId, releaseAdmission);

    private async Task ObserveProcessAsync(IProcessHandle process, LaunchRequest request,
        Guid runId, Action releaseAdmission)
    {
        var began = DateTimeOffset.UtcNow;
        var status = LaunchStatus.Created;
        try
        {
            while (true)
            {
                var next = process.HasExited ? LaunchStatus.Exited
                    : process.HasWindow ? LaunchStatus.WindowObserved
                    : DateTimeOffset.UtcNow - began > threshold ? LaunchStatus.Unconfirmed : LaunchStatus.Starting;
                if (next is LaunchStatus.WindowObserved or LaunchStatus.Unconfirmed or LaunchStatus.Exited)
                    releaseAdmission();
                if (next != status)
                {
                    status = next;
                    var persisted = await log.AppendAsync(request.AttemptId, runId, "Observation", status.ToString());
                    var message = !persisted ? "Observation evidence failed; verify target application manually."
                        : status == LaunchStatus.WindowObserved
                            ? "Window observed; verify project login in the target application." : status.ToString();
                    Publish(new(request.AttemptId, runId,
                        persisted ? status : LaunchStatus.NeedsVerification, message, process.Id));
                }
                if (next == LaunchStatus.Exited) break;
                await Task.Delay(250);
            }
        }
        catch
        {
            Publish(new(request.AttemptId, runId, LaunchStatus.NeedsVerification,
                "Process observation failed; verify manually.", process.Id));
        }
        finally
        {
            releaseAdmission();
            process.Dispose();
        }
    }

    public async IAsyncEnumerable<LaunchEvent> ObserveAsync(Guid runId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        long seen = -1;
        while (!ct.IsCancellationRequested)
        {
            if (latest.TryGetValue(runId, out var value) && value.Sequence > seen)
            {
                seen = value.Sequence;
                yield return value;
                if (value.Result.Status == LaunchStatus.Exited) yield break;
            }
            await Task.Delay(100, ct);
        }
    }
}
