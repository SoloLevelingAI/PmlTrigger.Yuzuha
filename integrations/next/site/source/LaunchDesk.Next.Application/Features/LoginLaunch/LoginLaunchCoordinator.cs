using LaunchDesk.Next.Application.Features.LoginLaunch.Execution;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public sealed class LoginLaunchCoordinator(
    CheckSavedProfile checkSavedProfile,
    CheckAvevaLicence checkAvevaLicence,
    ResolveLoginSecret resolveLoginSecret,
    StartMonitor startMonitor,
    StartMarineBatch startMarineBatch,
    StartDirect startDirect,
    IRecordLog records,
    TimeSpan? observationThreshold = null) : ILaunchService
{
    private readonly LaunchAdmission admission = new();
    private readonly ObserveAvevaSession observer = new(records, observationThreshold ?? TimeSpan.FromSeconds(90));

    public Task<LaunchResult> ExecuteAsync(LaunchRequest request, ISecretHandle? secret = null,
        CancellationToken ct = default)
    {
        var frozen = LaunchInput.FromRequest(request).CreateSnapshot(request.AttemptId);
        return admission.ExecuteOnceAsync(frozen, () => ExecuteLoginAsync(frozen, secret, ct, false));
    }

    public Task<LaunchResult> ExecuteSnapshotAsync(LaunchRequest request, ISecretHandle? secret = null,
        CancellationToken ct = default)
    {
        var frozen=LaunchInput.FromRequest(request).CreateSnapshot(request.AttemptId);
        return admission.ExecuteOnceAsync(frozen,()=>ExecuteLoginAsync(frozen,secret,ct,true));
    }

    // Read top to bottom: these are the concrete login steps, not a configurable workflow engine.
    private async Task<LaunchResult> ExecuteLoginAsync(LaunchRequest request,
        ISecretHandle? suppliedSecret, CancellationToken ct, bool capturedDraft)
    {
        var evidence = new LaunchAttemptRecords(records, request);
        try
        {
            if (!await evidence.SubmittedAsync())
                return evidence.Reject("Evidence could not be persisted; process was not started.");
            ct.ThrowIfCancellationRequested();

            var error = LaunchValidation.Validate(request);
            if (error != null) return await evidence.FinishAsync(LaunchStatus.Rejected, error);
            if (!await evidence.ValidationPassedAsync())
                return evidence.Reject("Validation evidence failed; process was not started.");

            error = capturedDraft ? null : await checkSavedProfile.ExecuteAsync(request, ct);
            if (error != null) return await evidence.FinishAsync(LaunchStatus.Rejected, error);
            if (!await checkAvevaLicence.ExecuteAsync(request, ct))
                return await evidence.FinishAsync(LaunchStatus.Rejected,
                    "Licence endpoint is unreachable. This check does not confirm licence checkout.");
            if (!await evidence.LicenceCheckedAsync())
                return evidence.Reject("Licence evidence failed; process was not started.");

            using var secret = resolveLoginSecret.Execute(request, suppliedSecret);
            if (request.PasswordPolicy != PasswordPolicy.None && secret.Value is null)
                return await evidence.FinishAsync(LaunchStatus.Cancelled, "Password was not provided.");

            if (!await evidence.RequireBeforeStartAsync())
                return evidence.Reject("Creation evidence failed; process was not started.");
            ct.ThrowIfCancellationRequested();

            var process = request.Protocol switch
            {
                EntryProtocol.Monitor => startMonitor.Execute(request, secret.Value),
                EntryProtocol.Marine => startMarineBatch.Execute(request, secret.Value),
                _ => startDirect.Execute(request, secret.Value)
            };
            var created = evidence.MarkCreated(process.Id);
            observer.Publish(created);
            var persisted = await evidence.CreatedAsync();
            observer.Begin(process, request, created.RunId!.Value,
                () => admission.Release(request.ConfigurationId));
            return persisted ? created : created with
            {
                Status = LaunchStatus.NeedsVerification,
                Message = "Process created but evidence failed; verify manually. Do not retry automatically."
            };
        }
        catch (OperationCanceledException)
        {
            return await evidence.FinishAsync(evidence.HasCreatedProcess
                ? LaunchStatus.NeedsVerification : LaunchStatus.Cancelled,
                "Launch cancelled before process creation.");
        }
        catch (ProcessStartUncertainException)
        {
            return await evidence.FinishAsync(LaunchStatus.NeedsVerification,
                "Process creation is uncertain; verify manually. Do not retry automatically.");
        }
        catch (Exception)
        {
            return await evidence.FinishAsync(evidence.HasCreatedProcess
                ? LaunchStatus.NeedsVerification : LaunchStatus.Failed,
                evidence.HasCreatedProcess ? "Process may exist; verify manually. Do not retry automatically."
                    : "Launch failed before confirmed creation; check paths, protocol and credential policy.");
        }
    }

    public LaunchResult? GetLatest(Guid runId) => observer.GetLatest(runId);
    public IAsyncEnumerable<LaunchEvent> ObserveAsync(Guid runId, CancellationToken ct = default) =>
        observer.ObserveAsync(runId, ct);
}
