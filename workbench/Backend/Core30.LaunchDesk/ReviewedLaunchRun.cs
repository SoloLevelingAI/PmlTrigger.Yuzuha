using System.Xml;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private static FeatureContext OpenReviewedRun(FeatureContext workspace, string feature, CoreData command) =>
        new(workspace.FilePath(Path.Combine("launchdesk-runs", feature, Read(command, "id"))));

    private static FileStream LockReviewedRun(FeatureContext run) =>
        new(run.FilePath("workflow.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private static string SaveReview(FeatureContext run, CoreData review)
    {
        var path = run.FilePath("review.xml");
        CoreXml.Write(review).Save(path);
        return FileHash(path);
    }

    private static CoreData ReadReview(FeatureContext run, CoreData command)
    {
        var bytes = FileContent.ReadBounded(run.FilePath("review.xml"), 32 * 1024 * 1024);
        FileContent.RequireMatchingSha256(Read(command, "planSha256"), FileContent.Sha256(bytes), "计划内容已变化，请重新预览。");
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        return CoreXml.Read(XElement.Load(reader));
    }

    private static CoreData ReviewResult(string action, string state, FeatureContext run, string digest, params object[] details) =>
        new("result", new CoreField("action", action), new CoreField("state", state),
            new CoreField("id", Path.GetFileName(run.Root)), new CoreField("planSha256", digest), details);

    // Windows scanners can briefly hold a new directory. Retry the same rename with both names intact.
    private static void MoveReviewedDirectory(string source, string target)
    {
        int[] delays = [100, 200, 400];
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Move(source, target);
                return;
            }
            catch (Exception error) when (attempt < delays.Length && IsTransientDirectoryMove(error))
            {
                Thread.Sleep(delays[attempt]);
            }
        }
    }

    private static bool IsTransientDirectoryMove(Exception error) => OperatingSystem.IsWindows() &&
        error is IOException or UnauthorizedAccessException && (error.HResult & 0xffff) is 5 or 32 or 33;
}
