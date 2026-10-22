using System.Xml.Linq;
using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Desktop;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Tests;

public sealed class DesktopPresentationTests
{
    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "YuzuhaToolkit.Development.slnx"))) folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
    private static Dictionary<string, string> Palette(string mode)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(Path.Combine(Root(), "src", "YuzuhaToolkit.Desktop", "Themes", mode + ".xaml")).Root!.Elements()
            .Where(node => node.Name.LocalName == "SolidColorBrush").ToDictionary(node => (string)node.Attribute(x + "Key")!, node => (string)node.Attribute("Color")!);
    }

    [Fact]
    public void BothThemesProvideEveryDesignTokenAndAllDynamicBrushes()
    {
        var light = Palette("Light"); var dark = Palette("Dark");
        Assert.Equal(light.Keys.Order(), dark.Keys.Order());
        Assert.True(light.Count >= 60);
        using var design = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "design", "figcraft", "theme-tokens.json")));
        foreach (var token in design.RootElement.GetProperty("variables").EnumerateArray())
        foreach (var (palette, index) in new[] { (light, 1), (dark, 2) })
        {
            var value = token.GetProperty("values").GetProperty("VariableCollectionId:1/mode" + index).GetProperty("value");
            var expected = "#" + string.Concat(new[] { "r", "g", "b" }.Select(channel => ((int)Math.Round(value.GetProperty(channel).GetDouble() * 255)).ToString("X2")));
            Assert.Equal(expected, palette[token.GetProperty("name").GetString()!.Replace('/', '.')]);
        }
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = XDocument.Load(Path.Combine(Root(), "src", "YuzuhaToolkit.Desktop", "Themes", "Light.xaml")).Root!.Elements().Select(node => (string)node.Attribute(x + "Key")!).ToArray();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src", "YuzuhaToolkit.Desktop"), "*.xaml", SearchOption.TopDirectoryOnly))
        foreach (var attribute in XDocument.Load(file).Descendants().Attributes())
        {
            var value = attribute.Value;
            if (!value.StartsWith("{DynamicResource ", StringComparison.Ordinal)) continue;
            Assert.Contains(value[17..^1], keys);
        }
    }

    [Fact]
    public void BothThemesKeepTextAndSemanticFeedbackReadable()
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            var p = Palette(mode);
            foreach (var (text, surface) in new[] { ("Text.Primary", "Surface.Card"), ("Text.Secondary", "Surface.Card"), ("Text.Tertiary", "Surface.Card"), ("Text.OnAccent", "Accent.Brand"), ("Text.Warning", "Surface.TintWarning"), ("Text.Danger", "Surface.TintDanger"), ("Text.Success", "Surface.TintSuccess") })
                Assert.True(Contrast(p[text], p[surface]) >= 4.5, mode + ": " + text + " on " + surface);
            Assert.NotEqual(p["Text.Danger"], p["Text.Success"]);
            Assert.NotEqual(p["Text.Warning"], p["Text.Success"]);
        }
    }

    [Fact]
    public void ThemePreferenceDoesNotSaveLaunchDraftsOrFunctionPermissions()
    {
        var root = Path.Combine(Path.GetTempPath(), "yuzuha-theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var workspace = Path.Combine(root, "workspace.json"); File.WriteAllText(workspace, "untouched launch settings");
            Assert.Equal("Light", DesktopDisplaySettings.ReadTheme(root));
            DesktopDisplaySettings.SaveTheme(root, "Dark");
            Assert.Equal("Dark", DesktopDisplaySettings.ReadTheme(root));
            Assert.Equal("untouched launch settings", File.ReadAllText(workspace));
            Assert.Throws<ArgumentException>(() => DesktopDisplaySettings.SaveTheme(root, "unknown"));
            Assert.Equal(2, Directory.GetFiles(root).Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EmptyActivityDoesNotInventLatencyOrSuccess()
    {
        var summary = DesktopActivitySummary.Create([], DateTimeOffset.Parse("2026-10-21T12:00:00+08:00"));
        Assert.Equal(0, summary.Completed);
        Assert.Null(summary.AverageMilliseconds);
        Assert.Null(summary.SuccessPercent);
        Assert.All(summary.Days, day => Assert.Equal(0, day.Total));
    }

    [Fact]
    public void ActivityCountsCompletedToolsAndKeepsUnknownInDenominator()
    {
        var at = DateTimeOffset.Parse("2026-10-21T12:00:00+08:00");
        var summary = DesktopActivitySummary.Create([Entry(at, "read_current_element", "started", 0), Entry(at, "read_current_element", "succeeded", 100), Entry(at, "registered_call", "failed", 200), Entry(at, "registered_call", "unknown", 300), Entry(at, "launch_aveva", "succeeded", 9000)], at);
        Assert.Equal(3, summary.Completed); Assert.Equal(1, summary.Successful);
        Assert.Equal(200, summary.AverageMilliseconds); Assert.Equal(100d / 3, summary.SuccessPercent);
        Assert.Equal(3, summary.Days.Sum(day => day.Total));
    }

    [Fact]
    public void ActivityUsesDisplayDayAndNeverFillsMissingDaysWithSuccess()
    {
        var now = DateTimeOffset.Parse("2026-10-21T12:00:00+08:00");
        var summary = DesktopActivitySummary.Create([Entry(DateTimeOffset.Parse("2026-10-20T16:30:00Z"), "query", "succeeded", 10), Entry(now.AddDays(-10), "query", "failed", 30)], now);
        Assert.Equal(7, summary.Days.Count);
        Assert.Equal(1, summary.Days[^1].Total);
        Assert.All(summary.Days.Take(6), day => Assert.Equal(0, day.Total));
        Assert.Equal(2, summary.Completed); // The metric covers retained events; the chart covers seven display days.
    }

    private static ActivityEntry Entry(DateTimeOffset time, string tool, string phase, long milliseconds) => new(time, "fixture", 1, Guid.NewGuid().ToString("N"), tool, phase, milliseconds, null, "", "");
    private static double Contrast(string a, string b)
    {
        static double L(string hex)
        {
            var rgb = new[] { 1, 3, 5 }.Select(start => Convert.ToInt32(hex.Substring(start, 2), 16) / 255d).Select(c => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4)).ToArray();
            return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
        }
        var x = L(a); var y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
}
