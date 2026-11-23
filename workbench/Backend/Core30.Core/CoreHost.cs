using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Opens the workspace's enabled modules and runs their registered contracts.</summary>
public sealed class CoreHost(
    string name,
    IReadOnlyList<ToolRegistration> registrations,
    string? installedModules = null,
    IReadOnlyList<IRunLogProvider>? logProviders = null) : ICore30Backend
{
    private readonly IReadOnlyList<IRunLogProvider> _logs = logProviders
        ?? [new JsonLinesRunLogProvider(), new TextRunLogProvider()];

    internal string Name => name;

    public CoreToolbox Describe(string workspace) => Catalog(new FeatureContext(workspace)).Describe(Name);

    public IReadOnlyList<string> ReadAddins(string path) => AvevaAddinManifest.ReadEntries(path);

    public LoadConfigurationChange PreviewAddin(string path, string entry, bool enable) =>
        AvevaAddinManifest.Preview(path, entry, enable);

    public IReadOnlyList<CoreModuleManifest> ReadInstalledModules(string workspace)
    {
        var policy = ToolLoadPolicy.Load(new FeatureContext(workspace));
        return ReadUnambiguousModules()
            .Select(module => new CoreModuleManifest(module.Registration.Id, module.Registration.Title,
                module.ManifestPath, policy.IsEnabled(module.Registration))).ToArray();
    }

    public LoadConfigurationChange PreviewToolAvailability(string workspace, string id, bool enable)
    {
        var context = new FeatureContext(workspace);
        var builtIn = registrations.SingleOrDefault(tool => tool.Id == id);
        if (builtIn is not null)
            return ToolLoadPolicy.Preview(context, builtIn, enable, manifest: null);
        var module = ReadUnambiguousModules().SingleOrDefault(item => item.Registration.Id == id)
            ?? throw new InvalidDataException($"工具模块 {id} 未安装。");
        if (enable && !File.Exists(module.AssemblyPath))
            throw new InvalidDataException($"找不到 {module.Registration.Title} 的程序集。");
        return ToolLoadPolicy.Preview(context, module.Registration, enable,
            new(module.ManifestPath, module.ManifestSha256, enable ? module.AssemblyPath : null));
    }

    public void RestoreToolAvailability(string workspace, LoadConfigurationRevision revision) =>
        LoadConfigurationChange.Restore(new FeatureContext(workspace), ToolLoadPolicy.RelativePath, revision);

    public static string ToolAvailabilityPath(string workspace) =>
        new FeatureContext(workspace).FilePath(ToolLoadPolicy.RelativePath);

    private IEnumerable<InstalledModules.InstalledModule> ReadUnambiguousModules()
    {
        foreach (var module in InstalledModules.Manifests(installedModules))
        {
            if (registrations.Any(tool => tool.Id == module.Registration.Id))
                throw new InvalidDataException($"模块 {module.Registration.Id} 与内置工具同名。");
            yield return module;
        }
    }

    public IReadOnlyList<IFeature> Features(string workspace, string? module = null) =>
        OpenRunner(new FeatureContext(workspace), module).Features.ToArray();

    internal IEnumerable<RegisteredToolCommand> Commands(string workspace, string? module = null) =>
        Catalog(new FeatureContext(workspace)).Commands(module);

    public CoreReceipt<CoreData> Run(string workspace, CoreRequest request, RunInput? input = null)
    {
        var context = new FeatureContext(workspace) { Input = input ?? new RunInput() };
        return OpenRunner(context, request.Module.Length == 0 ? null : request.Module).Run(context, request);
    }

    public XElement RunXml(string feature, string workspace, Stream xml, string? module = null, RunInput? input = null)
    {
        var context = new FeatureContext(workspace) { Input = input };
        return OpenRunner(context, module).Run(feature, context, xml);
    }

    internal CoreRunner OpenRunner(FeatureContext context, string? module, Action<string>? diagnostics = null) =>
        new(Catalog(context).Features(module), _logs, diagnostics);

    private ToolCatalog Catalog(FeatureContext context) => new(ToolLoadPolicy.Load(context),
        registrations.Concat(ReadUnambiguousModules().Select(module => module.Registration)));
}

public sealed record CoreToolbox(string Name, string Configuration, IReadOnlyList<CoreTool> Tools);

public sealed record CoreModuleManifest(string Id, string Title, string Path, bool Enabled);

public sealed record CoreTool(string Id, string Title, string Category, bool Enabled, string? Standalone, ToolPage? Page)
{
    public string? ModuleManifestPath { get; init; }
}
