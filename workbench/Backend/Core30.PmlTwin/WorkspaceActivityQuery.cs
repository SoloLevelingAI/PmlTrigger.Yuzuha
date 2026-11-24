using System.Globalization;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Queries workspace runtime and deployment records through the P06 v2 XML contract.</summary>
    private static CoreData ReadWorkspaceActivity(FeatureContext context, CoreData request)
    {
        var filter = request.Children().Single();
        var category = Value(filter, "category");
        var state = Value(filter, "state");
        var query = Value(filter, "query").Trim();
        var limit = (int?)filter.Field("limit") ?? 100;
        var snapshot = WorkspaceActivity.Read(context);
        var runs = snapshot.Runs.Where(run => run.ProcessId != Environment.ProcessId
                && run.Matches(category, state, query)).Take(limit).ToArray();
        return new CoreData("result", new CoreField("count", runs.Length), new CoreField("inspected", snapshot.Inspected),
            new CoreField("hasOlderLogs", snapshot.HasOlderLogs), runs.Select(DescribeWorkspaceRun));
    }

    private static CoreData DescribeWorkspaceRun(WorkspaceRun run) =>
        new("activity", new CoreField("runId", run.RunId), new CoreField("feature", run.Feature),
            new CoreField("category", run.Category), new CoreField("operation", run.Operation),
            new CoreField("state", run.State), new CoreField("at", FormatUtc(run.At)),
            new CoreField("log", run.RelativePath), Optional("pid", run.ProcessId),
            string.IsNullOrEmpty(run.EntryPoint) ? null : new CoreField("entryPoint", run.EntryPoint),
            Optional("startTicks", run.StartTicks), Optional("milliseconds", run.Milliseconds),
            run.Events.Select(entry => new CoreData("event", new CoreField("at", FormatUtc(entry.At)),
                new CoreField("phase", entry.Phase), entry.Detail)));

    private static CoreField? Optional(string name, long? value) => value is { } number ? new CoreField(name, number) : null;
    private static string FormatUtc(DateTimeOffset time) => time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
