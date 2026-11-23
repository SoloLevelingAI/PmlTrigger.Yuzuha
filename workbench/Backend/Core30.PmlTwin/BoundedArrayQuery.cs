using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private static CoreData ReadArrayWindow(FeatureContext context, CoreData request)
    {
        var input = ReadArrayRequest(context, request);
        var window = ReadWindow(input);
        return new CoreData("result", new CoreField("source", "local-array"),
            new CoreField("indexUpperBound", input.Rows.Length),
            new CoreField("start", input.Start), new CoreField("nextIndex", window.NextIndex),
            new CoreField("truncated", window.NextIndex != 0), new CoreField("characters", window.Characters),
            window.Rows.Select((row, offset) => new CoreData("row",
                new CoreField("index", input.Start + offset), XmlText(row))));
    }

    private static ArrayPage ReadWindow(ArrayRequest input)
    {
        var offset = input.Start - 1;
        var available = Math.Max(0, input.Rows.Length - offset);
        var candidates = input.Rows.Skip(offset).Take(input.Count).Select(ArrayRowText);
        var page = new List<string>();
        var remaining = input.MaxCharacters;
        foreach (var row in candidates)
        {
            if (row.Length > remaining)
                break;
            page.Add(row);
            remaining -= row.Length;
        }
        RequireFirstRowFits(available, page.Count);
        var next = page.Count < available ? input.Start + page.Count : 0;
        return new ArrayPage(page, next, input.MaxCharacters - remaining);
    }

    private static string ArrayRowText(string? value) => value ?? string.Empty;

    private static void RequireFirstRowFits(int available, int read)
    {
        if (available > 0 && read == 0)
            throw new InvalidDataException("PML_ARRAY_ITEM_EXCEEDS_BUDGET: first row exceeds the character limit.");
    }

    private static ArrayRequest ReadArrayRequest(FeatureContext context, CoreData request)
    {
        var bytes = ReadBounded(context.FilePath(Value(request, "file")), 4 * 1024 * 1024);
        return new(ParseArray(bytes), (int?)request.Field("start") ?? 1,
            (int?)request.Field("count") ?? 100, (int?)request.Field("maxCharacters") ?? 16384);
    }

    private static string?[] ParseArray(byte[] bytes) =>
        JsonSerializer.Deserialize<string?[]>(StripUtf8Bom(bytes), Json)
        ?? throw new InvalidDataException("Expected a JSON array of strings or null slots.");

    private sealed record ArrayRequest(string?[] Rows, int Start, int Count, int MaxCharacters);
    private sealed record ArrayPage(IReadOnlyList<string> Rows, int NextIndex, int Characters);
}
