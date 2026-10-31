using System.Collections.Concurrent;
using System.Text.Json;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Execution;

// Technical admission bookkeeping is kept off the coordinator's functional reading path.
internal sealed class LaunchAdmission
{
    private sealed record Attempt(string Fingerprint, TaskCompletionSource<LaunchResult> Completion);
    private readonly ConcurrentDictionary<Guid, Attempt> attempts = new();
    private readonly ConcurrentDictionary<string, byte> busy = new();

    internal async Task<LaunchResult> ExecuteOnceAsync(LaunchRequest request, Func<Task<LaunchResult>> execute)
    {
        var fingerprint = JsonSerializer.Serialize(request);
        var fresh = new Attempt(fingerprint, new(TaskCreationOptions.RunContinuationsAsynchronously));
        var registered = attempts.GetOrAdd(request.AttemptId, fresh);
        if (registered != fresh)
            return registered.Fingerprint == fingerprint
                ? await registered.Completion.Task
                : new(request.AttemptId, null, LaunchStatus.Rejected,
                    "AttemptId was already used for different input.");

        LaunchResult result;
        if (!busy.TryAdd(request.ConfigurationId, 0))
            result = new(request.AttemptId, null, LaunchStatus.Busy,
                "Configuration is already checking or starting.");
        else
        {
            try
            {
                result = await execute();
                if (result.RunId is null) Release(request.ConfigurationId);
            }
            catch
            {
                Release(request.ConfigurationId);
                result = new(request.AttemptId, null, LaunchStatus.NeedsVerification,
                    "Execution evidence failed unexpectedly; verify manually before a new attempt.");
            }
        }
        fresh.Completion.TrySetResult(result);
        return result;
    }

    internal void Release(string configurationId) => busy.TryRemove(configurationId, out _);
}
