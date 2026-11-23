namespace Core30.Core;

/// <summary>The typed entry point for callers running in the same process as Core30.</summary>
public interface ICore30Backend
{
    CoreToolbox Describe(string workspace);
    CoreReceipt<CoreData> Run(string workspace, CoreRequest request, RunInput? input = null);
    LoadConfigurationChange PreviewToolAvailability(string workspace, string id, bool enable);
    IReadOnlyList<string> ReadAddins(string path);
    LoadConfigurationChange PreviewAddin(string path, string entry, bool enable);
}
