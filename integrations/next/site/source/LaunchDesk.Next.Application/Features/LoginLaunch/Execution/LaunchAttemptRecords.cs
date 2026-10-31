using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Execution;

// All stage names and redacted snapshots for this feature live together.
internal sealed class LaunchAttemptRecords(IRecordLog log, LaunchRequest request)
{
    private Guid? runId;
    private int? processId;
    internal bool HasCreatedProcess => processId.HasValue;

    internal Task<bool> SubmittedAsync() => log.AppendAsync(request.AttemptId, null,
        "Submitted", "Checking", new Dictionary<string, string>
        {
            ["ConfigurationId"] = request.ConfigurationId,
            ["Version"] = request.Version.ToString(),
            ["InputSnapshot"] = System.Text.Json.JsonSerializer.Serialize(request with
            { Environment = request.Environment.Where(p => !LaunchDesk.Next.Domain.Security.SensitiveFields.ContainsSecret(p.Key)).ToDictionary(p=>p.Key,p=>p.Value) }),
            ["ExecutablePath"] = request.ExecutablePath,
            ["Project"] = request.Project,
            ["User"] = request.User,
            ["Mdb"] = request.Mdb,
            ["Module"] = request.Module
        });

    internal Task<bool> ValidationPassedAsync() =>
        log.AppendAsync(request.AttemptId, null, "Validation", "Valid");
    internal Task<bool> LicenceCheckedAsync() =>
        log.AppendAsync(request.AttemptId, null, "Licence", request.CheckLicence ? "Reachable" : "Disabled");
    internal Task<bool> RequireBeforeStartAsync() => log.AppendAsync(request.AttemptId, null,
        "CreateIntent", "Starting", new Dictionary<string, string>
        {
            ["Protocol"] = request.Protocol.ToString(),
            ["LicenceCheck"] = request.CheckLicence.ToString(),
            ["ArgumentPlan"] = string.Join(" | ", LaunchValidation.BuildArguments(request))
        });

    internal LaunchResult MarkCreated(int id)
    {
        processId = id;
        runId = Guid.NewGuid();
        return new(request.AttemptId, runId, LaunchStatus.Created,
            "Process created; application login is not confirmed.", processId);
    }

    internal Task<bool> CreatedAsync() => log.AppendAsync(request.AttemptId, runId,
        "Created", "Created", new Dictionary<string, string> { ["ProcessId"] = processId.ToString()! });
    internal LaunchResult Reject(string message) => new(request.AttemptId, null, LaunchStatus.Rejected, message);

    internal async Task<LaunchResult> FinishAsync(LaunchStatus status, string message)
    {
        var persisted = await log.AppendAsync(request.AttemptId, runId, "Result", status.ToString(),
            new Dictionary<string, string> { ["Reason"] = message });
        return HasCreatedProcess && !persisted
            ? new(request.AttemptId, runId, LaunchStatus.NeedsVerification,
                "Process may exist; evidence could not be saved. Verify manually; do not retry automatically.", processId)
            : new(request.AttemptId, runId, status, message, processId);
    }
}
