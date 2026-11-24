using System.Diagnostics;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record ProjectReaderApi(string StandaloneAssembly, string StandaloneType, string DatabaseAssembly, string ProjectType)
{
    public IEnumerable<string> AssemblyNames => [StandaloneAssembly + ".dll", DatabaseAssembly + ".dll"];
}

internal sealed record ProjectReaderEnvironment(string Installation, string Project, string Scratch, string Database)
{
    public string Binary => Path.TrimEndingDirectorySeparator(Installation) + "\\;";
    public string Work => Scratch + "\\";
}

internal sealed record ProjectReaderSession(ProjectReaderEnvironment Environment,
    Func<ProcessStartInfo> CreateSdkCommand, Func<ProcessStartInfo, XElement> RunWorker);

/// <summary>Supplies the native API and process environment for one compatibility flow.</summary>
internal interface IProjectReaderFlow
{
    string Id { get; }
    ProjectReaderApi Api { get; }
    IEnumerable<string> MissingDependencies(ProjectReadRequest request) => [];
    void Configure(IDictionary<string, string> variables, ProjectReaderEnvironment environment);
    XElement Read(ProjectReaderSession session) => session.RunWorker(session.CreateSdkCommand());
}

internal sealed class MarineProjectReaderFlow : IProjectReaderFlow
{
    public string Id => "marine-sdk";
    public ProjectReaderApi Api { get; } = new("Aveva.Pdms.Standalone", "Aveva.Pdms.Standalone.PdmsStandalone",
        "Aveva.Pdms.Database", "Aveva.Pdms.Database.Project");

    public void Configure(IDictionary<string, string> variables, ProjectReaderEnvironment environment)
    {
        variables["AVEVA_PRODUCT"] = "Marine";
        variables["PDMS_INSTALLED_DIR"] = Path.TrimEndingDirectorySeparator(environment.Installation) + "\\";
        variables["PDMS_NOCONSOLE"] = "1";
        variables["PDMSUI"] = Path.Combine(environment.Installation, "pdmsui") + "\\";
        variables[environment.Project + "000ID"] = environment.Project;
        // Project.Open checks this before login. Keep the directory inside the reader snapshot.
        variables[environment.Project + "MAC"] = Directory.CreateDirectory(Path.Combine(environment.Scratch, "macros")).FullName + "\\";
    }
}

internal sealed class E3dProjectReaderFlow : IProjectReaderFlow
{
    public string Id => "e3d-sdk";
    public ProjectReaderApi Api { get; } = new("Aveva.Core3D.Standalone", "Aveva.Core3D.Standalone.Standalone",
        "Aveva.Core.Database", "Aveva.Core.Database.Project");

    public void Configure(IDictionary<string, string> variables, ProjectReaderEnvironment environment)
    {
        variables["AVEVA_DESIGN_INSTALLED_DIR"] = variables["AVEVA_DESIGN_EXE"] = environment.Binary;
        variables["AVEVA_DESIGN_USER"] = variables["AVEVA_DESIGN_WORK"] = environment.Work;
        variables["PMLUI"] = variables["PDMSUI"] = Path.Combine(environment.Installation, "pmlui") + "\\";
    }
}

internal sealed record ProjectReaderBinding(AvevaProduct Product, IProjectReaderFlow Flow, string? Release = null);

/// <summary>Selects one binding before dependency checks. A failed read never changes the selected flow.</summary>
internal sealed class ProjectReaderRegistry(IEnumerable<ProjectReaderBinding> bindings)
{
    private readonly ImplementationCatalog<AvevaProduct, IProjectReaderFlow> _flows = new(bindings.Select(binding =>
        new ImplementationRegistration<AvevaProduct, IProjectReaderFlow>(binding.Product, binding.Flow, binding.Release)));

    public IProjectReaderFlow? Resolve(LaunchProjectSdk sdk) => _flows.Find(sdk.Product, sdk.Release);
}
