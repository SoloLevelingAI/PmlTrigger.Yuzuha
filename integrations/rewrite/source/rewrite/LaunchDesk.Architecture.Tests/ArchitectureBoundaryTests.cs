using System.Reflection;
using System.Xml.Linq;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;
using Assembly = System.Reflection.Assembly;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace LaunchDesk.Architecture.Tests;

/// <summary>针对真实编译程序集和项目文件检查已批准的分层边界，不启动桌面或外部软件。</summary>
public sealed class ArchitectureBoundaryTests
{
    private static readonly IReadOnlyDictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>
    {
        ["LaunchDesk.Kernel"] = Assembly.Load("LaunchDesk.Kernel"),
        ["LaunchDesk.OperationContracts"] = Assembly.Load("LaunchDesk.OperationContracts"),
        ["LaunchDesk.UseCases"] = Assembly.Load("LaunchDesk.UseCases"),
        ["LaunchDesk.Windows"] = Assembly.Load("LaunchDesk.Windows"),
        ["LaunchDesk.Desktop"] = Assembly.Load("LaunchDesk.Desktop"),
        ["LaunchDesk.Bootstrap"] = Assembly.Load("LaunchDesk.Archon"),
        ["LaunchDesk.Worker"] = Assembly.Load("LaunchDesk.Worker"),
        ["LaunchDesk.DevHost"] = Assembly.Load("LaunchDesk.DevHost")
    };
    // 显式登记目标，即使生产类型未引用Registry/Socket仍能执行规则；
    // 否则ArchUnitNET无法解析这些目标。
    private static readonly ArchitectureModel Compiled = new ArchLoader().LoadAssemblies(Assemblies.Values
        .Concat([typeof(Microsoft.Win32.Registry).Assembly, typeof(System.Net.Sockets.TcpClient).Assembly]).Distinct().ToArray()).Build();
    private static readonly Dictionary<string, string[]> Approved = ReadApprovedGraph();

    /// <summary>验证每层只能使用批准的项目依赖闭包；含签名、字段和方法体中的类型依赖。</summary>
    /// <param name="project">架构白名单中的项目名称。</param>
    [Theory]
    [InlineData("LaunchDesk.Kernel")]
    [InlineData("LaunchDesk.OperationContracts")]
    [InlineData("LaunchDesk.UseCases")]
    [InlineData("LaunchDesk.Windows")]
    [InlineData("LaunchDesk.Desktop")]
    [InlineData("LaunchDesk.Bootstrap")]
    [InlineData("LaunchDesk.Worker")]
    [InlineData("LaunchDesk.DevHost")]
    public void CompiledDependenciesFollowApprovedLayers(string project)
    {
        var allowed = new HashSet<string> { project };
        void Include(string current)
        {
            foreach (var target in Approved[current]) if (allowed.Add(target)) Include(target);
        }
        Include(project);
        foreach (var forbidden in Assemblies.Keys.Except(allowed))
            Types().That().ResideInAssembly(Assemblies[project].FullName!)
                .Should().NotDependOnAny(Types().That().ResideInAssembly(Assemblies[forbidden].FullName!))
                .Because($"{project} 只能通过批准的依赖方向使用 {forbidden}").Check(Compiled);
    }

    /// <summary>ViewModel 及其编译生成的协作类型归 Desktop 交互层。</summary>
    [Fact]
    public void ViewModelsBelongToDesktopInteraction()
    {
        Classes().That().HaveFullNameContaining("ViewModel").Should()
            .Be(Types().That().ResideInAssembly(Assemblies["LaunchDesk.Desktop"].FullName!)).Check(Compiled);
    }

    /// <summary>交互层与用例层经端口请求副作用，不直接访问文件、注册表、进程或网络连接。</summary>
    /// <param name="project">需保持副作用隔离的项目。</param>
    [Theory]
    [InlineData("LaunchDesk.Desktop")]
    [InlineData("LaunchDesk.UseCases")]
    public void InteractionAndUseCasesDoNotBypassSideEffectPorts(string project)
    {
        foreach (var forbidden in new[] { typeof(File), typeof(Directory), typeof(FileStream), typeof(FileInfo),
                     typeof(Process), typeof(Microsoft.Win32.Registry), typeof(System.Net.Sockets.TcpClient), typeof(System.Net.Sockets.Socket) })
            Types().That().ResideInAssembly(Assemblies[project].FullName!).Should()
                .NotDependOnAny(forbidden)
                .Because("实际副作用由 Windows 适配器提供，Path 等纯路径运算允许在页面使用").Check(Compiled);
    }

    /// <summary>常规桌面和诊断程序集不引用可选 Aspire 包。</summary>
    [Fact]
    public void AspireRemainsOptional()
    {
        foreach (var assembly in Assemblies.Values)
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
                reference.Name?.StartsWith("Aspire", StringComparison.Ordinal) == true);
    }

    /// <summary>校验所有实际项目的精确引用清单，拒绝遗漏项目、额外依赖和循环。</summary>
    [Fact]
    public void ProjectGraphMatchesApprovedEdgesAndHasNoCycles()
    {
        var actual = Directory.GetFiles(Path.Combine(Repository(), "rewrite"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, path => XDocument.Load(path).Descendants("ProjectReference")
                .Select(node => Path.GetFileNameWithoutExtension(node.Attribute("Include")!.Value.Replace('\\', '/'))!).Order().ToArray());
        Assert.Equal(Approved.Keys.Order(), actual.Keys.Order());
        foreach (var project in Approved) Assert.Equal(project.Value.Order(), actual[project.Key]);
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

    /// <summary>反例证明守卫能发现方法体中的文件访问；不会调用反例方法。</summary>
    [Fact]
    public void SideEffectGuardRejectsARealCompiledViolation()
    {
        var fixture = new ArchLoader().LoadAssemblies(typeof(InvalidIoFixture).Assembly).Build();
        var rule = Classes().That().HaveFullNameContaining(nameof(InvalidIoFixture)).Should()
            .NotDependOnAny(typeof(File));
        var failure = Assert.ThrowsAny<Exception>(() => rule.Check(fixture));
        Assert.Contains(nameof(InvalidIoFixture), failure.Message);
        Assert.Contains("File", failure.Message);
    }

    internal static string Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "rewrite", "architecture.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Architecture tests require a repository checkout.");
    }

    private static Dictionary<string, string[]> ReadApprovedGraph()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository(), "rewrite", "architecture.json")));
        return json.RootElement.GetProperty("projectReferences").EnumerateObject().ToDictionary(
            property => property.Name, property => property.Value.EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    private static class InvalidIoFixture
    {
        public static string Read(string path) => File.ReadAllText(path);
    }
}
