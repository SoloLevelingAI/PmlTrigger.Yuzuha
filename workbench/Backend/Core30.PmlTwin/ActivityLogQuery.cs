using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static CoreData FilterActivityRecords(FeatureContext context, CoreData request)
    {
        var bytes = ReadBounded(context.FilePath(Value(request, "file")), 2 * 1024 * 1024);
        var selected = ReadActivities(bytes).Where(activity => MatchesActivity(activity, request))
            .OrderByDescending(activity => activity.TimeUtc)
            .Take((int?)request.Field("limit") ?? 100).ToArray();
        return new CoreData("result", new CoreField("count", selected.Length),
            selected.Select(activity => new CoreData("activity",
                new CoreField("timeUtc", activity.TimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                new CoreField("callId", XmlText(activity.CallId)), new CoreField("tool", XmlText(activity.Tool)),
                new CoreField("phase", XmlText(activity.Phase)), new CoreField("milliseconds", activity.Milliseconds))));
    }

    private static IEnumerable<Activity> ReadActivities(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                yield return ParseActivity(line);
        }
    }

    private static Activity ParseActivity(string line)
    {
        var record = JsonSerializer.Deserialize<Activity>(line, Json);
        return record is { CallId.Length: > 0, Tool.Length: > 0, Phase.Length: > 0 }
            && record.TimeUtc != default
            ? record
            : throw new InvalidDataException("Activity requires a timestamp, call ID, tool and phase.");
    }

    // Match the former desktop filter's separation of unknown results and explicit failures.
    private static bool MatchesActivity(Activity activity, CoreData request)
    {
        var phase = Value(request, "phase", "all");
        var tool = Value(request, "tool");
        var query = Value(request, "query").Trim();
        return MatchesPhase(activity.Phase, phase)
            && (tool.Length == 0 || tool.Equals(activity.Tool, StringComparison.Ordinal))
            && (query.Length == 0 || new[] { activity.CallId, activity.Tool, activity.Summary, activity.Service }
                .Any(field => field?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static bool MatchesPhase(string actual, string filter) => filter switch
    {
        "all" => true,
        "unknown" => actual is "unknown" or "cancelled-unknown",
        _ => actual == filter
    };

    private sealed class Activity
    {
        public DateTimeOffset TimeUtc { get; init; }
        public string CallId { get; init; } = "";
        public string Tool { get; init; } = "";
        public string Phase { get; init; } = "";
        public long Milliseconds { get; init; }
        public string? Service { get; init; }
        public string? Summary { get; init; }
    }
}
