using System.Reflection;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
namespace LaunchDesk.Next.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly string[] Product = { "LaunchDesk.Next.Domain", "LaunchDesk.Next.Application", "LaunchDesk.Next.Platform", "LaunchDesk.Next.Desktop", "LaunchDesk.Next.Bootstrap" };
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(Product.Select(Assembly.Load).Concat(new[] { typeof(System.Windows.Window).Assembly, typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject).Assembly, typeof(System.IO.File).Assembly, typeof(System.ComponentModel.INotifyPropertyChanged).Assembly, typeof(System.Windows.Input.ICommand).Assembly }).ToArray()).Build();
    private static readonly ArchUnitNET.Domain.Architecture FixtureArchitecture = new ArchLoader().LoadAssemblies(Product.Select(Assembly.Load).Concat(new[] { typeof(ArchitectureTests).Assembly, typeof(System.Windows.Window).Assembly, typeof(System.ComponentModel.INotifyPropertyChanged).Assembly, typeof(System.Windows.Input.ICommand).Assembly }).ToArray()).Build();
    [Test] public void LoadedAssembliesAreNonEmpty() { foreach (var assembly in Product) Assert.That(Assembly.Load(assembly).GetTypes().Length, Is.GreaterThan(0), assembly); Assert.That(Architecture.Types.Count(), Is.GreaterThan(20)); }
    private static IArchRule Rule(string source, string forbidden, string id) => Types().That().HaveFullNameContaining(source).Should().NotDependOnAny(Types().That().HaveFullNameContaining(forbidden)).Because(id);
    [TestCase("LaunchDesk.Next.Application.Infrastructure.Contracts")]
    [TestCase("LaunchDesk.Next.Platform")]
    public void LogisticsCannotKnowFeatureTypes(string source)
    {
        Assert.That(Product.Select(Assembly.Load).SelectMany(assembly => assembly.GetTypes())
            .Any(type => (type.FullName ?? "").Contains(source)), Is.True);
        Assert.That(Rule(source, "LaunchDesk.Next.Application.Features", "ARC-07 neutral logistics")
            .HasNoViolations(Architecture), Is.True);
    }

    [TestCase("Fixtures.Infrastructure")]
    [TestCase("Fixtures.PlatformFeature")]
    public void LogisticsFeatureDependencyIsDetected(string source) =>
        Assert.That(Rule(source, "LaunchDesk.Next.Application.Features", "ARC-07 neutral logistics")
            .HasNoViolations(FixtureArchitecture), Is.False);
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "LaunchDesk.Next.Application")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "LaunchDesk.Next.Platform")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "System.IO")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "System.Diagnostics.Process")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "System.Windows")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "NLog")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "CommunityToolkit")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "Microsoft.Win32")]
    [TestCase("ARC-01", "LaunchDesk.Next.Domain", "System.Net")]
    [TestCase("ARC-02", "LaunchDesk.Next.Application", "LaunchDesk.Next.Platform")]
    [TestCase("ARC-02", "LaunchDesk.Next.Application", "LaunchDesk.Next.Desktop")]
    [TestCase("ARC-02", "LaunchDesk.Next.Application", "CommunityToolkit")]
    [TestCase("ARC-02", "LaunchDesk.Next.Application", "System.Windows")]
    [TestCase("ARC-03", "LaunchViewModel", "LoginLaunchCoordinator")]
    [TestCase("ARC-03", "LaunchDesk.Next.Desktop.Features.Launch.LaunchViewModel", "ILaunchService")]
    [TestCase("ARC-03", "LaunchViewModel", "LaunchDesk.Next.Platform")]
    [TestCase("ARC-03", "LaunchViewModel", "System.IO")]
    [TestCase("ARC-03", "LaunchViewModel", "System.Diagnostics.Process")]
    [TestCase("ARC-03", "LaunchViewModel", "Microsoft.Win32")]
    [TestCase("ARC-04", "LaunchDesk.Next.Application.Features.LoginLaunch.LaunchInput", "System.Windows")]
    [TestCase("ARC-04", "LaunchDesk.Next.Application.Features.LoginLaunch.LaunchInput", "CommunityToolkit")]
    [TestCase("ARC-04", "LaunchDesk.Next.Application.Features.LoginLaunch.LaunchInput", "ViewModel")]
    [TestCase("ARC-04", "LaunchRequest", "System.Windows")]
    [TestCase("ARC-04", "LaunchRequest", "CommunityToolkit")]
    [TestCase("ARC-04", "LaunchRequest", "ViewModel")]
    [TestCase("ARC-04", "LaunchModel", "System.Windows")]
    [TestCase("ARC-04", "LaunchModel", "CommunityToolkit")]
    [TestCase("ARC-03", "LaunchInteraction", "SavedProfiles")]
    [TestCase("ARC-03", "LaunchInteraction", "Features.LoginLaunch.Scripts")]

    [TestCase("ARC-06", "LaunchDesk.Next.Platform", "LaunchDesk.Next.Desktop")]
    [TestCase("ARC-06", "LaunchDesk.Next.Domain", "LaunchDesk.Next.Desktop")]
    public void ProductBoundaries(string id, string source, string forbidden) { Assert.That(Product.Select(Assembly.Load).SelectMany(a => a.GetTypes()).Any(t => (t.FullName ?? "").Contains(source, StringComparison.Ordinal)), Is.True, id + " source selector must be nonempty"); Assert.That(Rule(source, forbidden, id).HasNoViolations(Architecture), Is.True, $"{id}: {source} must not depend on {forbidden}"); }
    [TestCase("ARC-01", "Fixtures.Domain", "LaunchDesk.Next.Platform")]
    [TestCase("ARC-02", "Fixtures.Application", "LaunchDesk.Next.Platform")]
    [TestCase("ARC-03", "Fixtures.Desktop.LaunchViewModel", "ILaunchService")]
    [TestCase("ARC-04", "Fixtures.Dto.LaunchInput", "System.Windows")]

    [TestCase("ARC-06", "Fixtures.Platform", "LaunchDesk.Next.Desktop")]
    public void NegativeFixturesAreDetected(string id, string source, string target) { Assert.That(Rule(source, target, id).HasNoViolations(FixtureArchitecture), Is.False, $"{id}: {source} -> {target} fixture missed"); }
    private static IArchRule DesktopPlatformRule() => Types().That().HaveFullNameContaining("LaunchDesk.Next.Desktop.").And().DoNotHaveFullNameContaining("LaunchDesk.Next.Desktop.Bootstrap.").Should().NotDependOnAny(Types().That().HaveFullNameContaining("LaunchDesk.Next.Platform.")).Because("ARC-05 only explicit Bootstrap namespace may reference Platform");
    [Test] public void AllDesktopExceptBootstrapCannotReferencePlatform() => Assert.That(DesktopPlatformRule().HasNoViolations(Architecture), Is.True);
    [Test] public void DesktopServicesViolationUsesSameProductionPredicate() => Assert.That(DesktopPlatformRule().HasNoViolations(FixtureArchitecture), Is.False, "ARC-05 Desktop.Services violation missed");
    [TestCase("LaunchInput", "INotifyPropertyChanged")]
    [TestCase("LaunchInput", "ICommand")]
    [TestCase("LaunchRequest", "INotifyPropertyChanged")]
    [TestCase("LaunchRequest", "ICommand")]
    public void InformationTypesCannotDependOnNotificationOrCommandInterfaces(string source, string forbidden) => Assert.That(Rule("LaunchDesk.Next.Application.Features.LoginLaunch." + source, forbidden, "ARC-04").HasNoViolations(Architecture), Is.True);
    [TestCase("INotifyPropertyChanged")]
    [TestCase("ICommand")]
    public void InformationInterfaceNegativeFixturesAreDetected(string forbidden) => Assert.That(Rule("Fixtures.Dto.InterfaceInput", forbidden, "ARC-04").HasNoViolations(FixtureArchitecture), Is.False); [Test]
    public void ModuleGraphIsAcyclicAndNegativeCycleIsDetected()
    {
        var graph = Product.ToDictionary(x => x, x => Assembly.Load(x).GetReferencedAssemblies().Select(a => a.Name!).Where(Product.Contains).ToArray());
        Assert.That(HasCycle(graph), Is.False, "ARC-06 product assembly cycle");
        Assert.That(HasCycle(new Dictionary<string, string[]> { { "CycleA", ["CycleB"] }, { "CycleB", ["CycleA"] } }), Is.True, "ARC-06 cycle fixture missed");
        Assert.That(Rule("Fixtures.CycleA", "Fixtures.CycleB", "ARC-06 cycle edge").HasNoViolations(FixtureArchitecture), Is.False);
        Assert.That(Rule("Fixtures.CycleB", "Fixtures.CycleA", "ARC-06 cycle edge").HasNoViolations(FixtureArchitecture), Is.False);
    }
    private static bool HasCycle(Dictionary<string, string[]> graph) { var active = new HashSet<string>(); var done = new HashSet<string>(); bool Visit(string n) { if (active.Contains(n)) return true; if (!done.Add(n)) return false; active.Add(n); foreach (var next in graph[n]) if (Visit(next)) return true; active.Remove(n); return false; } return graph.Keys.Any(Visit); }
    [Test] public void ProjectGraphAndSharedSourcesRemainIndependent() { var root = FindRoot(); var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories); foreach (var project in projects) { var xml = System.Xml.Linq.XDocument.Load(project); foreach (var reference in xml.Descendants("ProjectReference")) { var value = reference.Attribute("Include")!.Value; Assert.That(value, Does.Not.Contain("Test").And.Not.Contain("Archon")); Assert.That(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, value)).StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True); } Assert.That(xml.Descendants("Compile").Any(e => e.Attribute("Include") != null), Is.False, "Shared compile sources forbidden"); } }
    [Test]
    public void ProductCannotContainOrLoadTheDevelopmentLab()
    {
        foreach (var name in Product)
        {
            var assembly = Assembly.Load(name);
            Assert.That(assembly.GetTypes().Any(type => (type.Namespace ?? "").StartsWith("LaunchDesk.Development", StringComparison.Ordinal)), Is.False, name);
            Assert.That(assembly.GetReferencedAssemblies().Any(reference => (reference.Name ?? "").Contains("RemoteProjectLab", StringComparison.Ordinal)), Is.False, name);
            var nativeImports = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Select(method => method.GetCustomAttribute<System.Runtime.InteropServices.DllImportAttribute>()?.Value ?? "");
            Assert.That(nativeImports.Any(path => path.Contains("vix", StringComparison.OrdinalIgnoreCase) || path.Contains("vmware", StringComparison.OrdinalIgnoreCase)), Is.False, name);
        }
        var root = FindRoot();
        var solution = System.Xml.Linq.XDocument.Load(Path.Combine(root, "LaunchDesk.Next.slnx"));
        Assert.That(solution.Descendants("Project").Any(project => (project.Attribute("Path")?.Value ?? "").Replace('\\', '/').StartsWith("dev/", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    private static string FindRoot() { var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); while (d != null && !File.Exists(Path.Combine(d.FullName, "LaunchDesk.Next.slnx"))) d = d.Parent; return d!.FullName; }
}
