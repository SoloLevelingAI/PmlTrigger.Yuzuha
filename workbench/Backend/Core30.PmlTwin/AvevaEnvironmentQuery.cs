using System.Xml.Linq;

using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Runs the read-only local inventory and serializes evidence and coverage separately.</summary>
    private static CoreData ScanLocalAvevaEnvironment(CoreData request, AvevaEnvironmentScanner scanner)
    {
        var options = ReadScanOptions(request);
        var snapshot = scanner.Scan(options);
        return DescribeEnvironment(options, snapshot);
    }

    private static EnvironmentScanOptions ReadScanOptions(CoreData request)
    {
        var mode = (string?)request.Field("mode") ?? "standard";
        var defaultDepth = mode == "fixed-drives" ? 18 : 6;
        var defaultDirectories = mode == "fixed-drives" ? 250_000 : 30_000;
        var depth = (int?)request.Field("maxDepth") ?? defaultDepth;
        var limit = (int?)request.Field("maxDirectories") ?? defaultDirectories;
        var roots = request.Children().Select(root => RequireLocalRoot(Value(root, "path"))).ToArray();
        RequireCustomRoot(mode, roots);
        return new EnvironmentScanOptions(mode, depth, limit, roots);
    }

    private static string RequireLocalRoot(string path) =>
        WindowsAvevaEnvironmentInventory.NormalizeLocalPath(path)
        ?? throw new InvalidDataException("扫描目录须为本机绝对路径。");

    private static void RequireCustomRoot(string mode, string[] roots)
    {
        if (mode == "custom" && roots.Length == 0)
            throw new InvalidDataException("指定目录模式需要至少一个 root。");
    }

    private static CoreData DescribeEnvironment(EnvironmentScanOptions options, AvevaEnvironmentSnapshot snapshot) =>
        new("result", new CoreField("mode", options.Mode), new CoreField("maxDepth", options.MaxDepth),
            new CoreField("maxDirectories", options.MaxDirectories),
            new CoreData("installations", snapshot.Installations.Select(DescribeInstallation)),
            new CoreData("processes", snapshot.Processes.Select(DescribeProcess)),
            DescribeCoverage(snapshot.Coverage));

    private static CoreData DescribeInstallation(AvevaInstallation item) =>
        new("installation", new CoreField("product", item.Product), new CoreField("release", item.Release),
            new CoreField("binaryVersion", item.BinaryVersion), new CoreField("directory", item.Directory),
            new CoreField("entryPoint", item.EntryPoint), new CoreField("launchFilesFound", item.LaunchFilesFound),
            new CoreField("vendor", item.Vendor), new CoreField("source", item.Source),
            item.Evidence.Select(file => new CoreData("file", file)));

    private static CoreData DescribeProcess(AvevaProcess item) =>
        new("process", new CoreField("product", item.Product), new CoreField("pid", item.ProcessId),
            new CoreField("startTicks", item.StartTicks), new CoreField("executable", item.Executable));

    private static CoreData DescribeCoverage(ScanCoverage coverage) =>
        new("coverage", new CoreField("visited", coverage.Visited), new CoreField("unreadable", coverage.Unreadable),
            new CoreField("reparseSkipped", coverage.ReparseSkipped), new CoreField("depthLimited", coverage.DepthLimited),
            new CoreField("budgetReached", coverage.BudgetReached), new CoreField("registryViews", coverage.RegistryViews),
            coverage.Roots.Select(root => new CoreData("root", new CoreField("path", root.Path), new CoreField("source", root.Source),
                new CoreField("state", root.State), new CoreField("visited", root.Visited))),
            coverage.Issues.Select(issue => new CoreData("issue", new CoreField("source", issue.Source),
                new CoreField("path", issue.Path), new CoreField("reason", issue.Reason))));
}
