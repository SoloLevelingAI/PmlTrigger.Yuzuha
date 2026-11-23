using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

internal sealed partial class PmlDeployment
{
    private static CoreData VerifyPlan(FeatureContext context, CoreData command)
    {
        var id = Attribute(command, "id");
        var digest = Attribute(command, "planSha256");
        var operation = OpenOperation(context, "urn:core30:P03:v6", id, digest);
        var phasePath = JournalPath(context, id, "phase");
        var phase = ReadPhase(phasePath);
        if (phase is Phase.Prepared)
            return ReportVerification(operation.Plan, id, digest, phase, "Prepared");
        if (phase is Phase.Applying or Phase.RollingBack)
            return ReportVerification(operation.Plan, id, digest, phase, "InProgress");

        // Open an existing lock for reading. A verification never creates a deployment lock or target.
        using var gate = OpenVerificationGate(operation.FilePath(DeploymentLock), out var busy);
        if (busy || ReadPhase(phasePath) != phase)
            return ReportVerification(operation.Plan, id, digest, ReadPhase(phasePath), "InProgress");

        var pending = operation.FilePath(PendingDeployment);
        if (File.Exists(pending))
            return ReportVerification(operation.Plan, id, digest, phase, "InProgress");

        var rollback = phase == Phase.RolledBack;
        var files = operation.Files.Select(file => VerifyFile(operation, file, rollback)).ToArray();
        if (ReadPhase(phasePath) != phase)
            return ReportVerification(operation.Plan, id, digest, ReadPhase(phasePath), "InProgress");
        var status = files.Any(file => file.Field("status")?.Value == "Changed") ? "Changed"
            : files.Any(file => file.Field("status")?.Value == "Unavailable") ? "Unavailable"
            : "Verified";
        return ReportVerification(operation.Plan, id, digest, phase, status, files);
    }

    private static FileStream? OpenVerificationGate(string path, out bool busy)
    {
        busy = false;
        try { return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
        {
            busy = true;
            return null;
        }
    }

    private static CoreData VerifyFile(DeploymentOperation operation, ResolvedFile file, bool rollback)
    {
        var expected = rollback ? file.Change.Before : file.Change.After;
        try
        {
            var path = operation.FilePath(file.RelativePath);
            if (Directory.Exists(path))
                return FileEvidence(file.RelativePath, expected, null, "Changed", "当前位置是目录");
            var actual = ReadIfPresent(path, file.Limit);
            var status = SameBytes(expected, actual) ? "Match" : "Changed";
            return FileEvidence(file.RelativePath, expected, actual, status);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            return FileEvidence(file.RelativePath, expected, null, "Unavailable", error.Message);
        }
    }

    private static CoreData FileEvidence(string path, byte[]? expected, byte[]? actual, string status, string? detail = null) =>
        new("file", new CoreField("path", path), new CoreField("status", status),
            new CoreField("expectedPresent", expected is not null),
            new CoreField("expectedSha256", expected is null ? "" : Sha256(expected)),
            status == "Unavailable" ? null : new CoreField("observedPresent", actual is not null || detail is not null),
            actual is null ? null : new CoreField("observedSha256", Sha256(actual)),
            detail is null ? null : new CoreField("detail", detail));

    private static CoreData ReportVerification(DeploymentPlan plan, string id, string digest, Phase phase, string status,
        CoreData[]? files = null)
    {
        var result = Report(plan, id, digest, phase);
        result.Add(new CoreData("verification", new CoreField("status", status),
            new CoreField("basis", phase == Phase.RolledBack ? "baseline" : "deployment"), files ?? []));
        return result;
    }
}
