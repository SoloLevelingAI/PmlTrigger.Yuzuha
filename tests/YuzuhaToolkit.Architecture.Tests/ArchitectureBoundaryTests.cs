using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace YuzuhaToolkit.Architecture.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly Dictionary<string, Assembly> Assemblies = new[]
    {
        "PlantHost.Rpc.Net10", "YuzuhaToolkit.Mcp", "YuzuhaToolkit.Knowledge", "YuzuhaToolkit.DevHost", "YuzuhaToolkit.CommandLine", "YuzuhaToolkit.Workspace"
    }.ToDictionary(name => name, Assembly.Load);
    private static readonly ArchUnitNET.Domain.Architecture Compiled = new ArchLoader()
        .LoadAssemblies(Assemblies.Values.ToArray()).Build();

    [Theory]
    [InlineData("PlantHost.Rpc.Net10")]
    [InlineData("YuzuhaToolkit.Mcp")]
    [InlineData("YuzuhaToolkit.Knowledge")]
    [InlineData("YuzuhaToolkit.DevHost")]
    [InlineData("YuzuhaToolkit.CommandLine")]
    [InlineData("YuzuhaToolkit.Workspace")]
    public void CompiledDependenciesFollowApprovedDirection(string project)
    {
        var graph = ReadGraph();
        var allowed = new HashSet<string> { project };
        void Include(string current)
        {
            foreach (var target in graph[current]) if (allowed.Add(target)) Include(target);
        }
        Include(project);
        foreach (var forbidden in Assemblies.Keys.Except(allowed))
        {
            Assert.DoesNotContain(Assemblies[project].GetReferencedAssemblies(), reference => reference.Name == forbidden);
            // Both MCP executables compile the same linked Builtins source. ArchUnitNET
            // identifies those duplicate types by full name. The assembly check above
            // still rejects any real reference, including one to a linked Builtins type.
            Types().That().ResideInAssembly(Assemblies[project].FullName!).Should()
                .NotDependOnAny(Types().That().ResideInAssembly(Assemblies[forbidden].FullName!)
                    .And().DoNotResideInNamespace("YuzuhaToolkit.Builtins"))
                .Because("依赖方向由 docs/architecture/dependencies.json 定义").Check(Compiled);
        }
    }

    [Fact]
    public void AllSourceProjectsMatchApprovedGraphAndAreAcyclic()
    {
        var approved = ReadGraph();
        var actual = Directory.GetFiles(Path.Combine(Repository(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Concat([Path.Combine(Repository(), "installer/SetupBridge/SetupBridge.csproj")])
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => XDocument.Load(p).Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/'))).Order().ToArray());
        Assert.Equal(approved.Keys.Order(), actual.Keys.Order());
        foreach (var project in approved) Assert.Equal(project.Value.Order(), actual[project.Key]);
        var visited = new HashSet<string>();
        var active = new HashSet<string>();
        void Visit(string project)
        {
            Assert.DoesNotContain(project, active);
            if (!visited.Add(project)) return;
            active.Add(project);
            foreach (var target in actual[project]) Visit(target);
            active.Remove(project);
        }
        foreach (var project in actual.Keys) Visit(project);
    }

    [Fact]
    public void DevelopmentHandlersCannotExecuteOrPersist()
    {
        foreach (var forbidden in new[] { typeof(YuzuhaToolkit.Mcp.YuzuhaRpcBridge),
                     typeof(YuzuhaToolkit.Mcp.PmlFunctionTrustStore), typeof(YuzuhaToolkit.Knowledge.KnowledgeRepository),
                     typeof(YuzuhaToolkit.Mcp.PmlCallTools), typeof(YuzuhaToolkit.Mcp.AvevaSessionDiscovery),
                     typeof(PlantHost.Rpc.RpcClient), typeof(YuzuhaToolkit.Knowledge.KnowledgeTools),
                     typeof(YuzuhaToolkit.Knowledge.KnowledgeLayerTools), typeof(YuzuhaToolkit.Mcp.PmlFunctionTrustTools),
                     typeof(YuzuhaToolkit.Mcp.PmlFunctionRegistry), typeof(YuzuhaToolkit.Mcp.PmlFunctionTools),
                     typeof(YuzuhaToolkit.Mcp.BoundedArrayRequest) })
            Classes().That().HaveFullNameContaining("YuzuhaToolkit.DevHost.DevelopmentEndpoints").Should()
                .NotDependOnAny(forbidden).Because("HTTP 开发接口只读内置内容并计算预览").Check(Compiled);
    }

    [Fact]
    public void KnowledgeAlgorithmsRemainIndependentOfStorageAndTransport()
    {
        Types().That().ResideInNamespace("YuzuhaToolkit.Knowledge.Search").Or()
            .ResideInNamespace("YuzuhaToolkit.Knowledge.Chunking").Should()
            .NotDependOnAny(typeof(YuzuhaToolkit.Knowledge.KnowledgeRepository)).Check(Compiled);
    }

    [Fact]
    public void ProductionServersDoNotReferenceDevelopmentPackages()
    {
        foreach (var name in new[] { "YuzuhaToolkit.Mcp", "YuzuhaToolkit.Knowledge", "PlantHost.Rpc.Net10", "YuzuhaToolkit.Workspace" })
            Assert.DoesNotContain(Assemblies[name].GetReferencedAssemblies(), reference =>
                reference.Name?.StartsWith("Scalar", StringComparison.Ordinal) == true ||
                reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true ||
                reference.Name?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true ||
                reference.Name?.Contains("ArchUnit", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void GuardRejectsACompiledMethodBodyViolation()
    {
        var fixture = new ArchLoader().LoadAssemblies(typeof(InvalidIoFixture).Assembly, typeof(File).Assembly).Build();
        var rule = Classes().That().HaveFullNameContaining(nameof(InvalidIoFixture)).Should().NotDependOnAny(typeof(File));
        var failure = Assert.ThrowsAny<Exception>(() => rule.Check(fixture));
        Assert.Contains(nameof(InvalidIoFixture), failure.Message);
        Assert.Contains("File", failure.Message);
    }

    private static Dictionary<string, string[]> ReadGraph()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository(), "docs/architecture/dependencies.json")));
        return document.RootElement.GetProperty("projects").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.GetProperty("references").EnumerateArray().Select(r => r.GetString()!).ToArray());
    }

    private static string Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "YuzuhaToolkit.Development.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate Yuzuha repository.");
    }
}

internal static class InvalidIoFixture
{
    // Only the compiled dependency is inspected; this method is never executed.
    internal static string Read(string path) => File.ReadAllText(path);
}
