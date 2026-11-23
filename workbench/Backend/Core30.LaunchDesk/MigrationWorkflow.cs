using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    /// <summary>F04 v2 keeps the exact reviewed mapping and publishes the two resulting files together.</summary>
    private static CoreData RunMigrationWorkflow(FeatureContext workspace, CoreData request)
    {
        var command = request.Children().Single();
        return MigrationOperations.Require(command.Name.LocalName)(workspace, command);
    }

    private static CoreData PreviewMigration(FeatureContext workspace, CoreData command)
    {
        var source = ReadMigrationWorkspace(workspace.FilePath(Read(command, "source")));
        var baseline = ReadMigrationWorkspace(workspace.FilePath(Read(command, "baseline")));
        var shared = SelectReferencedRecords(source.Data);
        var bundle = source.Codec.Write(shared);
        var result = MergeWorkspace(workspace, baseline.Data, shared);
        var copy = baseline.Codec.Write(result.Data);
        var run = new FeatureContext(CreateRun(workspace, "F04"));
        var staged = WriteMigrationFiles(Directory.CreateDirectory(run.FilePath("staged")).FullName, bundle, copy);
        var review = BuildMigrationReview(command, source.Hash, baseline.Hash, staged, result);
        var digest = SaveReview(run, review);
        return ReviewResult("preview", "Previewed", run, digest, new CoreData(review));
    }

    private static CoreData BuildMigrationReview(CoreData selection, string sourceHash, string baselineHash,
        (string Bundle, string Copy) staged, MigrationResult result) =>
        new("migration", new CoreField("source", Read(selection, "source")),
            new CoreField("baseline", Read(selection, "baseline")), new CoreField("sourceSha256", sourceHash),
            new CoreField("baselineSha256", baselineHash), new CoreField("bundleSha256", FileHash(staged.Bundle)),
            new CoreField("copySha256", FileHash(staged.Copy)),
            new CoreData("mapping", result.Notes.Select(note => new CoreData("note", note))),
            new CoreData("imported", result.Profiles.Select(DescribeMigrationProfile)));

    private static CoreData ApplyMigration(FeatureContext workspace, CoreData command) =>
        UseMigrationReview(workspace, command, (run, review) => PublishMigration(workspace, run, review));

    private static CoreData VerifyMigrationRun(FeatureContext workspace, CoreData command) =>
        UseMigrationReview(workspace, command);

    private static CoreData UseMigrationReview(FeatureContext workspace, CoreData command,
        Action<FeatureContext, CoreData>? update = null)
    {
        var run = OpenReviewedRun(workspace, "F04", command);
        using var gate = LockReviewedRun(run);
        var review = ReadReview(run, command);
        update?.Invoke(run, review);
        return InspectMigration(workspace, run, review, command.Name.LocalName, Read(command, "planSha256"));
    }

    private static void PublishMigration(FeatureContext workspace, FeatureContext run, CoreData review)
    {
        var output = run.FilePath("output");
        if (Directory.Exists(output))
            return;
        RequireMatchingSha256(Read(review, "sourceSha256"), FileHash(workspace.FilePath(Read(review, "source"))),
            "来源配置已变化，请重新预览。");
        RequireMatchingSha256(Read(review, "baselineSha256"), FileHash(workspace.FilePath(Read(review, "baseline"))),
            "基线配置已变化，请重新预览。");
        RequireMatchingSha256(Read(review, "bundleSha256"), FileHash(run.FilePath("staged/share.json")), "预览分享包已变化。");
        RequireMatchingSha256(Read(review, "copySha256"), FileHash(run.FilePath("staged/workspace.json")), "预览迁移副本已变化。");
        MoveReviewedDirectory(run.FilePath("staged"), output);
    }

    private static CoreData InspectMigration(FeatureContext workspace, FeatureContext run, CoreData review, string action, string digest)
    {
        if (!Directory.Exists(run.FilePath("output")))
            return ReviewResult(action, "Previewed", run, digest, new CoreData(review));
        var bundle = run.FilePath("output/share.json");
        var copy = run.FilePath("output/workspace.json");
        return ReviewResult(action, "Published", run, digest, new CoreField("bundle", bundle), new CoreField("copy", copy),
            new CoreField("bundleMatchesReview", MatchesMigrationHash(bundle, Read(review, "bundleSha256"))),
            new CoreField("copyMatchesReview", MatchesMigrationHash(copy, Read(review, "copySha256"))),
            new CoreField("sourceUnchanged", MatchesMigrationHash(workspace.FilePath(Read(review, "source")), Read(review, "sourceSha256"))),
            new CoreField("baselineUnchanged", MatchesMigrationHash(workspace.FilePath(Read(review, "baseline")), Read(review, "baselineSha256"))),
            new CoreData(review));
    }

    private static bool MatchesMigrationHash(string path, string expected) =>
        File.Exists(path) && expected.Equals(FileHash(path), StringComparison.OrdinalIgnoreCase);
}
