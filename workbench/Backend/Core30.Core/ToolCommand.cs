using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Optional command declarations for modules hosted by the common CLI.</summary>
public interface IToolCommandProvider
{
    IReadOnlyList<ToolCommand> GetCommands();
}

public sealed record ToolCommand(string Name, string Feature, string Description, IReadOnlyList<ToolCommandOption> Options,
    Func<IReadOnlyDictionary<string, string>, XElement> CreateRequest);

public sealed record ToolCommandOption(string Name, string ValueName, string Description, bool Required = false);

internal sealed record RegisteredToolCommand(string Module, ToolCommand Command)
{
    public string Name => $"{Module} {Command.Name}";
}
