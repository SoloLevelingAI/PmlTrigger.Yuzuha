using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    /// <summary>F02 v2 leaves the applied target available for inspection and an explicit rollback.</summary>
    private static CoreData RunDeploymentWorkflow(FeatureContext workspace, CoreData request)
    {
        var command = request.Children().Single();
        return DeploymentOperations.Require(command.Name.LocalName)(workspace, command);
    }

    private static CoreData PreviewDeployment(FeatureContext workspace, CoreData command)
    {
        var plan = ReadDeploymentPlan(workspace, command);
        var review = new CoreData("deployment",
            new CoreField("source", Read(command, "source")), new CoreField("baseline", Read(command, "baseline")),
            new CoreField("sourceSha256", SnapshotDigest(plan.Changes.Select(change => change.File))),
            new CoreField("baselineSha256", SnapshotDigest(plan.Baseline)),
            new CoreField("appliedSha256", SnapshotDigest(ProjectedFiles(plan))));
        var run = new FeatureContext(CreateRun(workspace, "F02"));
        var digest = SaveReview(run, review);
        return ReviewResult("preview", "Previewed", run, digest, new CoreData(review),
            new CoreData("changes", plan.Changes.Select(change => new CoreData("file",
                new CoreField("path", change.File.RelativePath), new CoreField("action", change.Action),
                new CoreField("sha256", change.File.Hash)))));
    }

    private static CoreData ApplyDeployment(FeatureContext workspace, CoreData command) =>
        UseDeploymentReview(workspace, command, (run, review) => PublishDeployment(workspace, run, review));

    private static CoreData InspectDeploymentRun(FeatureContext workspace, CoreData command) =>
        UseDeploymentReview(workspace, command);

    private static CoreData RollbackDeployment(FeatureContext workspace, CoreData command) =>
        UseDeploymentReview(workspace, command, (run, review) => RollBackDeployment(run, review, command));

    private static CoreData UseDeploymentReview(FeatureContext workspace, CoreData command,
        Action<FeatureContext, CoreData>? update = null)
    {
        var run = OpenReviewedRun(workspace, "F02", command);
        using var gate = LockReviewedRun(run);
        var review = ReadReview(run, command);
        update?.Invoke(run, review);
        return InspectDeployment(run, review, command.Name.LocalName, Read(command, "planSha256"));
    }

    private static DeploymentPlan ReadDeploymentPlan(FeatureContext workspace, CoreData selection)
    {
        var (source, baseline) = ReadDeploymentPaths(workspace, selection);
        return PlanDeployment(workspace, source, baseline);
    }

    private static void PublishDeployment(FeatureContext workspace, FeatureContext run, CoreData review)
    {
        var output = run.FilePath("output");
        if (Directory.Exists(output))
            return;
        var plan = ReadDeploymentPlan(workspace, review);
        RequireMatchingSha256(Read(review, "sourceSha256"), SnapshotDigest(plan.Changes.Select(change => change.File)),
            "来源文件已变化，请重新预览。");
        RequireMatchingSha256(Read(review, "baselineSha256"), SnapshotDigest(plan.Baseline), "基线文件已变化，请重新预览。");
        WriteDeploymentCopies(run, output, plan, review);
    }

    private static void WriteDeploymentCopies(FeatureContext run, string output, DeploymentPlan plan, CoreData review)
    {
        var stage = run.FilePath($"output-{Guid.NewGuid():N}.pending");
        try
        {
            var baseline = Path.Combine(stage, "baseline");
            var target = Path.Combine(stage, "target");
            CopyDeploymentFiles(plan.Baseline, baseline);
            CopyDeploymentFiles(ProjectedFiles(plan), target);
            RequireMatchingSha256(Read(review, "appliedSha256"), SnapshotDigest(DeploymentSnapshot(run, target)),
                "生成的部署副本与预览不符。");
            MoveReviewedDirectory(stage, output);
        }
        catch
        {
            DiscardPendingDeployment(stage);
            throw;
        }
    }

    private static void DiscardPendingDeployment(string stage)
    {
        try { Directory.Delete(stage, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private static IEnumerable<DeploymentFile> ProjectedFiles(DeploymentPlan plan) =>
        plan.Baseline.Concat(plan.Changes.Select(change => change.File))
            .GroupBy(file => DigestPath(file.RelativePath), StringComparer.Ordinal).Select(group => group.Last());

    private static string SnapshotDigest(IEnumerable<DeploymentFile> files) =>
        FileContent.Sha256(JsonSerializer.SerializeToUtf8Bytes(files
            .Select(file => new { Path = DigestPath(file.RelativePath), file.Hash })
            .OrderBy(file => file.Path, StringComparer.Ordinal)));

    private static string DigestPath(string path)
    {
        var relative = path.Replace('\\', '/');
        return OperatingSystem.IsWindows() ? relative.ToUpperInvariant() : relative;
    }

    private static CoreData InspectDeployment(FeatureContext run, CoreData review, string action, string digest)
    {
        if (!Directory.Exists(run.FilePath("output")))
            return ReviewResult(action, "Previewed", run, digest, new CoreData(review));
        var view = ReadDeploymentView(run, review);
        return ReviewResult(action, view.State, run, digest, new CoreField("target", view.Path),
            new CoreField("targetSha256", view.Digest), new CoreField("matchesReview", view.Matches),
            new CoreData("files", DeploymentFiles(view.Files)));
    }

    private static DeploymentView ReadDeploymentView(FeatureContext run, CoreData review)
    {
        var target = run.FilePath("output/target");
        var archived = run.FilePath("output/applied");
        var state = Directory.Exists(run.FilePath("output/baseline"))
            ? (Directory.Exists(target) ? "Applied" : "RollbackPending") : "RolledBack";
        var path = state == "RollbackPending" ? archived : target;
        var files = DeploymentSnapshot(run, path);
        var digest = SnapshotDigest(files);
        var expected = Read(review, state == "RolledBack" ? "baselineSha256" : "appliedSha256");
        return new(state, path, files, digest, expected.Equals(digest, StringComparison.OrdinalIgnoreCase));
    }

    private static void RollBackDeployment(FeatureContext run, CoreData review, CoreData command)
    {
        var view = ReadDeploymentView(run, review);
        if (view.State == "RolledBack")
        {
            RequireMatchingSha256(Read(review, "baselineSha256"), view.Digest, "已回滚的目标含后续修改。");
            return;
        }
        RequireMatchingSha256(Read(command, "targetSha256"), view.Digest, "部署副本已变化，请重新核对。");
        RequireMatchingSha256(Read(review, "appliedSha256"), view.Digest, "部署副本含后续修改，无法按原计划回滚。");
        var backup = run.FilePath("output/baseline");
        RequireMatchingSha256(Read(review, "baselineSha256"), SnapshotDigest(DeploymentSnapshot(run, backup)), "基线副本已变化。");
        var target = run.FilePath("output/target");
        if (view.State == "Applied")
            MoveReviewedDirectory(target, run.FilePath("output/applied"));
        // If interrupted after archiving the applied copy, retry can still restore the checked baseline.
        MoveReviewedDirectory(backup, target);
    }

    private sealed record DeploymentView(string State, string Path, DeploymentFile[] Files, string Digest, bool Matches);
}
