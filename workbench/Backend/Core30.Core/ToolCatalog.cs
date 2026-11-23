namespace Core30.Core;

/// <summary>Registers an independently developed tool and its versioned Feature handlers.</summary>
public interface IToolModule
{
    IEnumerable<IFeature> GetFeatures();
}

public sealed record ToolRegistration(string Id, string Title, string Category, Func<IEnumerable<IFeature>> LoadFeatures,
    bool EnabledByDefault = true, string? StandalonePath = null)
{
    public ToolPage? Page { get; init; }
    public string? ModuleManifestPath { get; init; }
    public Func<IReadOnlyList<ToolCommand>>? LoadCommands { get; init; }
}

public sealed record ToolPage(string Title, string Feature, string Mode);

internal sealed class ToolCatalog(ToolLoadPolicy policy, IEnumerable<ToolRegistration> registrations)
{
    private readonly Dictionary<string, ToolRegistration> _tools = registrations.ToDictionary(tool => tool.Id, StringComparer.Ordinal);

    public IEnumerable<IFeature> Features(string? module) =>
        SelectEnabled(module).SelectMany(tool => tool.LoadFeatures());

    public IEnumerable<RegisteredToolCommand> Commands(string? module) => _tools.Values
        .Where(tool => (module is null || tool.Id == module) && policy.IsEnabled(tool))
        .SelectMany(ReadCommands);

    public CoreToolbox Describe(string name) => new(name, policy.Source, _tools.Values.Select(tool =>
        new CoreTool(tool.Id, tool.Title, tool.Category, policy.IsEnabled(tool), tool.StandalonePath, tool.Page)
        { ModuleManifestPath = tool.ModuleManifestPath }).ToArray());

    private IEnumerable<ToolRegistration> SelectEnabled(string? module)
    {
        if (module is null && _tools.Count != 1)
            return _tools.Values.Where(policy.IsEnabled);
        var selected = module is null ? _tools.Values.Single() : Find(module);
        RequireEnabled(selected);
        return [selected];
    }

    private ToolRegistration Find(string id) => _tools.TryGetValue(id, out var tool)
        ? tool : throw new InvalidDataException($"模块 {id} 未安装。");

    private void RequireEnabled(ToolRegistration tool)
    {
        if (!policy.IsEnabled(tool))
            throw new InvalidDataException($"模块 {tool.Id} 已关闭。请检查 {ToolLoadPolicy.RelativePath}。");
    }

    private static IEnumerable<RegisteredToolCommand> ReadCommands(ToolRegistration tool)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in tool.LoadCommands?.Invoke() ?? [])
        {
            if (!IsIdentifier(command.Name) || !names.Add(command.Name))
                throw new InvalidDataException($"模块 {tool.Id} 的命令名无效或重复。");
            var options = new HashSet<string>(StringComparer.Ordinal) { "help", "workspace" };
            if (command.Options.Any(option => !IsIdentifier(option.Name) || !options.Add(option.Name)))
                throw new InvalidDataException($"命令 {tool.Id} {command.Name} 的参数名无效、重复或由宿主保留。");
            yield return new RegisteredToolCommand(tool.Id, command);
        }
    }

    private static bool IsIdentifier(string value) => value.Length != 0 && char.IsAsciiLetterLower(value[0]) &&
        value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
}
