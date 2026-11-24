using System.Globalization;
using System.Text;
using System.Text.Json;
using Core30.Core;

namespace Core30.PmlTwin;

public sealed record PmlRecordedFrame(string[] Values, string? ImagePath, string ImageError);
public sealed record PmlRecordedEvent(string Id, string Control, DateTimeOffset Time, string Callback, string State,
    string Detail, PmlRecordedFrame Before, PmlRecordedFrame? After);

/// <summary>Reads append-only native recorder evidence for a specific process and form.</summary>
public static class PmlFormRecordingLog
{
    public static IReadOnlyList<PmlRecordedEvent> Read(PmlLiveSession session, string formName, DateTimeOffset since) =>
        Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuzuhaToolkit", "Records"),
            session, formName, since);

    internal static IReadOnlyList<PmlRecordedEvent> Read(string root, PmlLiveSession session, string formName, DateTimeOffset since)
    {
        if (!Directory.Exists(root)) return [];
        var scope = new FeatureContext(root);
        var processStart = new DateTimeOffset(session.StartTicks, TimeSpan.Zero);
        var after = since > processStart ? since : processStart;
        var logs = Directory.EnumerateDirectories(root, "*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint })
            .Select(path => scope.FilePath(Path.Combine(Path.GetFileName(path), "session.jsonl")))
            .Where(File.Exists).Where(path => File.GetLastWriteTimeUtc(path) >= after.UtcDateTime)
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(16);
        var events = new Dictionary<string, PmlRecordedEvent>(StringComparer.Ordinal);
        foreach (var path in logs)
        {
            var folder = new FeatureContext(Path.GetDirectoryName(path)!);
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = OpenTail(stream);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length > 524_288 || TryRead(line, folder, session.Pid, formName, after) is not { } record)
                    continue;
                if (!events.TryGetValue(record.Id, out var existing) || existing.After is null)
                    events[record.Id] = record;
            }
        }

        return events.Values.OrderByDescending(entry => entry.Time).Take(40).ToArray();
    }

    private static StreamReader OpenTail(FileStream stream)
    {
        var skipFirst = stream.Length > 4 * 1024 * 1024;
        if (skipFirst) stream.Seek(-4 * 1024 * 1024, SeekOrigin.End);
        var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        if (skipFirst) reader.ReadLine();
        return reader;
    }

    private static PmlRecordedEvent? TryRead(string line, FeatureContext folder, int pid, string form, DateTimeOffset since)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            var id = row.String("recordId");
            var phase = row.String("phase");
            if (row.Integer("processId") != pid || !Guid.TryParseExact(id, "N", out _) || phase is not ("Before" or "After"))
                return null;
            var before = row.Field("before");
            if (!DateTimeOffset.TryParse(before.String("utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc)
                || utc < since)
                return null;
            var beforeValues = row.Field("beforeArray");
            var control = Scalar(ValueAt(beforeValues, 1));
            var separator = control.IndexOf('.');
            if (separator < 1 || !control[..separator].TrimStart('!').Equals(form, StringComparison.OrdinalIgnoreCase))
                return null;

            var completed = phase == "After";
            var afterValues = row.Field("array");
            return new(id, control, utc, Scalar(ValueAt(beforeValues, 3)),
                completed ? Scalar(ValueAt(afterValues, 3)) : "awaitingAfter",
                completed ? Scalar(ValueAt(afterValues, 4)) : "",
                Frame(folder, before, ValueAt(beforeValues, 4)),
                completed ? Frame(folder, row.Field("after"), ValueAt(afterValues, 5)) : null);
        }
        catch (JsonException) { return null; } // An append in progress can leave an incomplete final line.
    }

    private static PmlRecordedFrame Frame(FeatureContext folder, JsonElement frame, JsonElement values)
    {
        var image = frame.String("image");
        var error = frame.String("error");
        string? path = null;
        if (image.Length > 0)
        {
            var name = Path.GetFileName(image);
            try
            {
                if (!image.Equals($"images/{name}", StringComparison.Ordinal) ||
                    !name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("截图路径格式无效。");
                var candidate = folder.FilePath(image);
                if (File.Exists(candidate)) path = candidate;
                else if (error.Length == 0) error = "截图文件已不存在。";
            }
            catch (InvalidDataException failure) { error = failure.Message; }
        }

        return new(values.Items().Select(value => Scalar(value.Field("value"))).ToArray(), path, error);
    }

    private static JsonElement ValueAt(JsonElement array, long index)
    {
        foreach (var row in array.Items())
        {
            // The AVEVA Host writes PML REAL indexes as JSON numbers such as 1.0.
            var position = row.Field("index");
            if (position.ValueKind == JsonValueKind.Number && position.TryGetDecimal(out var number) && number == index)
                return row.Field("value");
        }
        return default;
    }

    private static string Scalar(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? ""
        : value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False ? value.GetRawText() : "";
}
