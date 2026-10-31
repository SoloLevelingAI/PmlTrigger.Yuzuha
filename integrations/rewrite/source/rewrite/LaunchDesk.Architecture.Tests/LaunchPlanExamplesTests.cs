using System.Collections.Immutable;
using System.Xml.Linq;
using LaunchDesk.Kernel;
using LaunchDesk.OperationContracts;
using LaunchDesk.UseCases;
using LaunchDesk.Windows;

namespace LaunchDesk.Architecture.Tests;

/// <summary>核心启动协议的可执行文档；XML example 与真实参数断言共用同一份期望命令。</summary>
public sealed class LaunchPlanExamplesTests
{
    /// <summary>Marine 示例须是五个有序参数，秘密保持插槽，不能混入 Monitor 参数。</summary>
    [Fact]
    public void MarineExampleIsAnExecutableContract()
    {
        var app = Application(EntryKind.MarineBatch, @"C:\AVEVA\Marine\OH12.1.SP4\marine.bat", "", "");
        var profile = Profile(app, "Paragon");
        var arguments = Success(LaunchPlanBuilder.BuildMarineArguments(app, profile));
        Assert.Equal(["Paragon", "noconsole", "SEA", "", "/ALL"], arguments.Select(argument => argument.Value));
        Assert.Equal("SYSTEM/", arguments[3].SecretPrefix);
        Assert.Equal(3, Array.FindIndex(arguments.ToArray(), argument => argument.SecretSlot));
        Assert.Single(arguments.Where(argument => argument.SecretSlot));
        Assert.Equal(ReadExample(nameof(LaunchPlanBuilder.BuildMarineArguments)), "marine.bat " + Masked(arguments));
        var plan = Success(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(Configuration(app, profile), Parent()));
        Assert.Equal(arguments.ToArray(), plan.Arguments.ToArray());
    }

    /// <summary>E3D 2.1 Paragon 的文档示例与 CATALOGUE／catalogue.init 的真实参数须完全一致。</summary>
    [Fact]
    public void CatalogueExampleIsAnExecutableContract()
    {
        var app = Application(EntryKind.Monitor, @"C:\Program Files (x86)\AVEVA\Everything3D2.10\mon.exe",
            @"C:\Program Files (x86)\AVEVA\Everything3D2.10\catalogue.init", "CATALOGUE");
        var arguments = Success(LaunchPlanBuilder.BuildMonitorArguments(app, Profile(app, "paragon")));
        Assert.Equal(["PROD", "CATALOGUE", "init", app.InitializationPath, "SEA", "", "/ALL", "paragon"],
            arguments.Select(argument => argument.Value));
        Assert.Equal("SYSTEM/", arguments[5].SecretPrefix);
        Assert.Single(arguments.Where(argument => argument.SecretSlot));
        Assert.Equal(ReadExample(nameof(LaunchPlanBuilder.BuildMonitorArguments)), "mon.exe " + Masked(arguments));
    }

    /// <summary>Monitor 不硬编码版本、产品或 init 文件，Design 与 Catalogue 均采用明确应用配置。</summary>
    /// <param name="version">产品显示版本。</param>
    /// <param name="product">明确的 Monitor 产品代码。</param>
    /// <param name="initialization">明确的初始化文件名。</param>
    /// <param name="module">登录模块名。</param>
    [Theory]
    [InlineData("2.1", "E3D", "launch.init", "Design")]
    [InlineData("3.1", "E3D", "launch.init", "design")]
    [InlineData("2.1", "CATALOGUE", "catalogue.init", "paragon")]
    public void MonitorHonorsExplicitProductAndInitialization(string version, string product, string initialization,
        string module)
    {
        var app = Application(EntryKind.Monitor, @"C:\Installed Product\mon.exe", @"C:\Installed Product\" + initialization, product) with { Version = version };
        var profile = Profile(app, module);
        var plan = Success(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(Configuration(app, profile), Parent()));
        Assert.Equal(product, plan.Arguments[1].Value);
        Assert.Equal(app.InitializationPath, plan.Arguments[3].Value);
        Assert.DoesNotContain('"', plan.Arguments[3].Value);
        Assert.Equal(module, plan.Arguments[7].Value);
        Assert.Equal("", plan.Arguments[5].Value);
    }

    /// <summary>MDB 前导斜杠规范化不改两个产品的参数位置。</summary>
    /// <param name="mdb">用户输入的 MDB 名称。</param>
    [Theory]
    [InlineData("ALL")]
    [InlineData("/ALL")]
    [InlineData("//ALL")]
    public void MdbUsesExactlyOneLeadingSlash(string mdb)
    {
        var app = Application(EntryKind.MarineBatch, "marine.cmd", "", "");
        Assert.Equal("/ALL", Success(LaunchPlanBuilder.BuildMarineArguments(app, Profile(app, "Paragon") with { Mdb = mdb }))[4].Value);
        app = Application(EntryKind.Monitor, "mon.exe", "launch.init", "E3D");
        Assert.Equal("/ALL", Success(LaunchPlanBuilder.BuildMonitorArguments(app, Profile(app, "Design") with { Mdb = mdb }))[6].Value);
    }

    /// <summary>Marine 拒绝错入口、空 MDB 和 shell 控制字符，不能把错误计划交给进程层。</summary>
    [Fact]
    public void MarineRejectsUnsupportedEntrypointAndUnsafeFields()
    {
        var app = Application(EntryKind.MarineBatch, "marine.exe", "", "");
        Assert.Equal("UnsupportedEntrypoint", LaunchPlanBuilder.BuildMarineArguments(app, Profile(app, "Paragon")).Error!.Code);
        app = app with { ExecutablePath = "marine.bat" };
        foreach (var profile in new[] { Profile(app, "Paragon&whoami"), Profile(app, "Paragon") with { Mdb = "/" } })
            Assert.False(LaunchPlanBuilder.BuildMarineArguments(app, profile).IsSuccess);
    }

    /// <summary>Monitor 拒绝缺失或带字面引号的初始化值，原生路径只在进程边界编码。</summary>
    [Fact]
    public void MonitorRejectsMissingOrPrequotedInitialization()
    {
        foreach (var initialization in new[] { "", "\"C:\\Program Files\\launch.init\"" })
        {
            var app = Application(EntryKind.Monitor, "mon.exe", initialization, "E3D");
            Assert.Equal("InvalidArguments", LaunchPlanBuilder.BuildMonitorArguments(app, Profile(app, "Design")).Error!.Code);
        }
    }

    /// <summary>合法五参数计划在注入不可原样传递的秘密后须于创建批处理前失败。</summary>
    /// <param name="password">只用于隔离断言的虚构不支持输入。</param>
    [Theory]
    [InlineData("p\"quote")]
    [InlineData("p%PATH%")]
    [InlineData("p&echo")]
    [InlineData("p!expand")]
    [InlineData("has space")]
    [InlineData("line\r\nbreak")]
    public async Task UnsupportedMarineSecretsFailBeforeCreatingAProcess(string password)
    {
        var root = Path.GetFullPath(Path.Combine(ArchitectureBoundaryTests.Repository(), "artifacts", "launch-fixtures", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "marine.cmd");
            var marker = Path.Combine(root, "executed.txt");
            await File.WriteAllTextAsync(file, "@echo unexpected>\"%~dp0executed.txt\"\r\n");
            var app = Application(EntryKind.MarineBatch, file, "", "") with { WorkingDirectory = root };
            var plan = Success(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(Configuration(app, Profile(app, "Paragon")), Parent()));
            using var secret = new SecretLease(new TransientSecret(password));
            var result = await new WindowsProcessGateway().StartAsync(plan, secret, default);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Error!.Code, new[] { "UnsafeBatchArguments", "InvalidArguments" });
            Assert.False(File.Exists(marker));
            Assert.DoesNotContain(password, result.Error.SafeMessage);
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.Combine(ArchitectureBoundaryTests.Repository(), "artifacts", "launch-fixtures")) + Path.DirectorySeparatorChar, root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static ApplicationDefinition Application(EntryKind kind, string executable, string initialization, string product) =>
        new(Guid.NewGuid(), "Fixture", "2.1", kind, executable, initialization, @"C:\Working Directory", product);
    private static ProfileDraft Profile(ApplicationDefinition app, string module) =>
        new(Guid.NewGuid(), "Fixture", app.Id, "2.1", "SEA", "SYSTEM", "/ALL", module, null, []);
    private static LaunchConfiguration Configuration(ApplicationDefinition app, ProfileDraft profile) =>
        new(new Revision(1), new ProfileConfiguration(profile.ProfileId!.Value, profile, new CredentialRef("fixture")), app, null, []);
    private static EnvironmentSnapshot Parent() => new(ImmutableDictionary<string, string>.Empty);
    private static T Success<T>(Result<T> result) { Assert.True(result.IsSuccess, result.Error?.Code); return result.Value; }
    private static string ReadExample(string method) => XDocument.Load(Path.ChangeExtension(typeof(LaunchPlanBuilder).Assembly.Location, ".xml"))
        .Descendants("member").Single(member => member.Attribute("name")?.Value.StartsWith("M:LaunchDesk.UseCases.LaunchPlanBuilder." + method + "(", StringComparison.Ordinal) == true)
        .Element("example")!.Element("code")!.Value.Trim();
    private static string Masked(ImmutableArray<LaunchArgument> arguments) => string.Join(" ", arguments.Select(argument =>
        argument.SecretSlot ? argument.SecretPrefix + "******" : argument.Value.Contains(' ') ? "\"" + argument.Value + "\"" : argument.Value));
}
