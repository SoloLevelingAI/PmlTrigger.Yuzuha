using System.Globalization;
using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Knowledge.Chunking;
using YuzuhaToolkit.Knowledge.Search;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Tests;

public sealed class BackendBehaviorTests
{
    [Fact]
    public void PmlArgumentsKeepOrderAndEscapeQuotes()
    {
        var call = PmlMethodSignatureGenerator.Build("Test", new DynamicParameter("bool", true),
            new DynamicParameter("string", "O'Brien 测试"), new DynamicParameter("real", "12.5"));
        Assert.Equal("!!Test(TRUE,'O''Brien 测试',12.5)", call);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("zh-CN")]
    public void PmlNumbersUseInvariantCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("!!Test(1.25)", PmlMethodSignatureGenerator.Build("Test", new DynamicParameter("double", 1.25)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("bool", "yes")]
    [InlineData("double", "1,25")]
    public void InvalidScalarValuesAreRejected(string type, string value) =>
        Assert.Throws<FormatException>(() => PmlMethodSignatureGenerator.Build("Test", new DynamicParameter(type, value)));

    [Fact]
    public void UnsupportedPmlTypesAreRejected() =>
        Assert.Throws<NotSupportedException>(() => PmlMethodSignatureGenerator.Build("Test", new DynamicParameter("object", "x")));

    [Theory]
    [InlineData("当前元素", "read-current-element")]
    [InlineData("CE", "read-current-element")]
    [InlineData("DBREF", "read-dbref")]
    [InlineData("global", "read-global")]
    public void BuiltinGuidesResolveAliasesWithoutDatabase(string query, string id)
    {
        using var document = JsonDocument.Parse(new BuiltinUsageTools().GetBuiltinUsage(query));
        var guide = Assert.Single(document.RootElement.EnumerateArray(), g => g.GetProperty("Id").GetString() == id);
        Assert.Equal("builtin", guide.GetProperty("Priority").GetString());
        Assert.NotEmpty(guide.GetProperty("Content").GetString()!);
        Assert.Contains("Explicit user choice", guide.GetProperty("OverridePolicy").GetString());
    }

    [Fact]
    public void BuiltinAsciiAliasesRespectWordBoundaries()
    {
        using var document = JsonDocument.Parse(new BuiltinUsageTools().GetBuiltinUsage("recipe"));
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public void ChineseCreateIntentProducesWeightedPmlQuery()
    {
        var variants = QueryPlanner.Plan("创建方盒");
        Assert.Contains(variants, v => v.FtsExpression == "\"NEW\" AND \"BOX\"" && v.Weight > 1);
        Assert.Equal(variants.Count, variants.Select(v => v.FtsExpression).Distinct().Count());
    }

    [Fact]
    public void FtsQuotesCannotBreakOutOfLiteral() =>
        Assert.Equal("\"a\"\" OR x\"", QueryPlanner.QuoteFts("a\" OR x"));

    [Fact]
    public void ChunkWindowsPreserveLineCoordinatesAndMakeProgress()
    {
        string[] lines = ["aa", "bb", "cc", "dd"];
        var chunks = TextWindows.SplitLongLines(lines, 10, 6, overlapLines: 20);
        Assert.Equal(10, chunks[0].StartLine);
        Assert.Equal(13, chunks[^1].EndLine);
        Assert.All(chunks, c => Assert.Equal(string.Join("\n", lines.Skip(c.StartLine - 10).Take(c.EndLine - c.StartLine + 1)), c.Body));
        Assert.True(chunks.Count <= lines.Length);
    }
}
