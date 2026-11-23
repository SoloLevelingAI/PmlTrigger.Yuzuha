using System.Text.RegularExpressions;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

/// <summary>Reads literal SET assignments in the selected EVARS file without running it.</summary>
internal sealed class PmlEnvironment
{
    private static readonly Regex SetLine = new(
        """^\s*@?set[ \t]+(?<quoted>")?(?<key>[A-Za-z_][A-Za-z0-9_()]*)[ \t]*=(?<value>[^"\r\n&|<>^]*)(?(quoted)")[ \t]*$""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Variable = new(@"%(?<name>[A-Za-z_][A-Za-z0-9_()]*)%",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public string Path { get; }
    public string Assignment { get; private set; } = "";
    public int AssignmentLine { get; private set; }
    public PmlSearchRoot[] Roots { get; private set; } = [];
    public PmlSearchRoot[] UiRoots { get; private set; } = [];
    public PmlSearchRoot[] PdmsUiRoots { get; private set; } = [];
    public string UiVariable { get; private set; } = "PMLUI";
    public string ProgramDirectory { get; private set; } = "";
    public PmlSearchRoot[] ActiveUiRoots => UiVariable == "PDMSUI" ? PdmsUiRoots : UiRoots;
    private string YuzuhaVersion { get; set; } = "";

    private PmlEnvironment(string path) => Path = path;

    public static PmlEnvironment Parse(string path, string text, string? product = null)
    {
        var environment = new PmlEnvironment(path);
        environment.ReadAssignments(text, product);
        return environment;
    }

    private void ReadAssignments(string text, string? product)
    {
        var folder = System.IO.Path.GetDirectoryName(Path)! + System.IO.Path.DirectorySeparatorChar;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["aveva_design_installed_dir"] = folder, ["pdms_installed_dir"] = folder,
            ["aveva_design_exe"] = folder, ["pdmsexe"] = folder
        };
        using var lines = new StringReader(text);
        for (var number = 1; lines.ReadLine() is { } line; number++)
            ReadAssignment(line, number, folder, values);
        UiVariable = product switch
        {
            "AM" or "PDMS" => "PDMSUI",
            "E3D" => "PMLUI",
            _ => values.ContainsKey("PDMSUI") && !values.ContainsKey("PMLUI") ? "PDMSUI" : "PMLUI"
        };
        YuzuhaVersion = values.GetValueOrDefault("Yuzuha", "");
        ProgramDirectory = values.GetValueOrDefault("YUZUHA_PROGRAM_ROOT", "");
    }

    private void ReadAssignment(string line, int number, string folder, Dictionary<string, string> values)
    {
        if (ReadSetting(line) is not { } setting)
            return;
        var expanded = ExpandSetting(setting, folder, values);
        values[setting.Key] = expanded;
        if (setting.Key.Equals("PMLLIB", StringComparison.OrdinalIgnoreCase))
            SetSearchRoots(setting.Value, expanded, number, folder);
        else if (setting.Key.Equals("PMLUI", StringComparison.OrdinalIgnoreCase))
            UiRoots = ReadRoots(expanded, folder);
        else if (setting.Key.Equals("PDMSUI", StringComparison.OrdinalIgnoreCase))
            PdmsUiRoots = ReadRoots(expanded, folder);
    }

    // The Marine launcher passes its installation directory as the first argument to evars.bat.
    private string ExpandSetting((string Key, string Value) setting, string folder, Dictionary<string, string> values) =>
        System.IO.Path.GetFileName(Path).Equals("evars.bat", StringComparison.OrdinalIgnoreCase) &&
        setting.Key.Equals("pdmsexe", StringComparison.OrdinalIgnoreCase) && setting.Value.Equals("%~dp1", StringComparison.OrdinalIgnoreCase)
            ? folder : Expand(setting.Value, folder, values);

    internal static (string Key, string Value)? ReadSetting(string line)
    {
        var match = SetLine.Match(line);
        return match.Success ? (match.Groups["key"].Value, match.Groups["value"].Value.Trim()) : null;
    }

    private static string Expand(string raw, string folder, Dictionary<string, string> values)
    {
        var scriptValue = raw.Replace("%~dp0", folder, StringComparison.OrdinalIgnoreCase);
        var length = scriptValue.Length;
        return Variable.Replace(scriptValue, variable =>
        {
            var name = variable.Groups["name"].Value;
            var value = values.GetValueOrDefault(name) ?? Environment.GetEnvironmentVariable(name) ?? variable.Value;
            length += value.Length - variable.Length;
            return length <= 128 * 1024 ? value : throw new InvalidDataException("EVARS 变量展开超过读取预算。");
        });
    }

    private void SetSearchRoots(string raw, string expanded, int number, string folder)
    {
        Assignment = raw;
        AssignmentLine = number;
        Roots = ReadRoots(expanded, folder);
    }

    private static PmlSearchRoot[] ReadRoots(string expanded, string folder) =>
        expanded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => PmlSearchRoot.FromValue(value, folder))
            .DistinctBy(root => root.Path, StringComparer.OrdinalIgnoreCase).ToArray();

    public CoreData Describe() => new("environment", new CoreField("evars", Path),
        new CoreField("assignmentLine", AssignmentLine), new CoreField("legacyActive", Roots.Any(root => root.Legacy)),
        new CoreField("yuzuhaVersion", YuzuhaVersion), new CoreField("uiVariable", UiVariable),
        new CoreData("assignment", Assignment.Length == 0 ? "" : $"set PMLLIB={Assignment}"),
        Roots.Select(root => DescribeRoot("pmllib", root)), UiRoots.Select(root => DescribeRoot("pmlui", root)),
        PdmsUiRoots.Select(root => DescribeRoot("pdmsui", root)));

    private static CoreData DescribeRoot(string name, PmlSearchRoot root) => new(name,
        new CoreField("path", root.Path), new CoreField("state", root.State), new CoreField("legacy", root.Legacy));
}

internal sealed record PmlSearchRoot(string Path, string State, bool Legacy)
{
    public static PmlSearchRoot FromValue(string value, string folder)
    {
        var local = ResolvePath(value, folder);
        var state = local is null ? "unresolved" : Directory.Exists(local) ? "available" : "missing";
        return new PmlSearchRoot(local ?? value, state, WindowsEvarsIntegration.IsLegacyPath(value));
    }

    private static string? ResolvePath(string path, string folder)
    {
        if (path.IndexOfAny(['%', '$']) >= 0)
            return null;
        try { return System.IO.Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(System.IO.Path.GetFullPath(path, folder))); }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return null; }
    }
}
