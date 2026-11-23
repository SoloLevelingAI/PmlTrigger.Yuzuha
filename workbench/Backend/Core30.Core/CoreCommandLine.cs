using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Shared headless command adapter for the aggregate and standalone entry points.</summary>
public static partial class CoreCommandLine
{
    public const int AccessDeniedReceiptExitCode = 5;

    public static int Run(string[] args, ToolRegistration[] registrations, bool hasLaunchFixture = false,
        string name = "Core30 · C# Core", string? installedModules = null)
        => Run(args, registrations, [new JsonLinesRunLogProvider(), new TextRunLogProvider()],
            hasLaunchFixture, name, installedModules);

    public static int Run(string[] args, ToolRegistration[] registrations, IReadOnlyList<IRunLogProvider> logProviders,
        bool hasLaunchFixture = false, string name = "Core30 · C# Core", string? installedModules = null)
        => Run(args, new CoreHost(name, registrations, installedModules, logProviders), hasLaunchFixture);

    public static int Run(string[] args, CoreHost host, bool hasLaunchFixture = false)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            return args switch
            {
                [] or ["help"] or ["--help"] => Help(host, host.Name, Directory.GetCurrentDirectory()),
                ["help" or "--help", "--workspace", var workspace] => Help(host, host.Name, workspace),
                ["tools"] => Describe(host, Directory.GetCurrentDirectory()),
                ["tools", var workspace] => Describe(host, workspace),
                ["list"] => List(host, Directory.GetCurrentDirectory()),
                ["list", var workspace] => List(host, workspace),
                ["list", var workspace, var module] => List(host, workspace, module),
                ["run", var feature, var workspace, var request] => Execute(host, feature, workspace, request),
                ["run", var feature, var workspace, var request, var module] => Execute(host, feature, workspace, request, module),
                ["run-receipt", var feature, var workspace, var request, var module, var receipt] =>
                    ExecuteWithReceipt(host, feature, workspace, request, module, receipt),
                ["__receive-launch", .. var received] when hasLaunchFixture => ReceiveLaunch(received),
                _ => RunRegisteredCommand(host, args)
            };
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static int Describe(CoreHost host, string workspace)
    {
        var toolbox = host.Describe(workspace);
        Console.WriteLine(new XElement("toolbox", new XAttribute("name", toolbox.Name),
            new XAttribute("configuration", toolbox.Configuration), toolbox.Tools.Select(DescribeTool)));
        return 0;
    }

    private static XElement DescribeTool(CoreTool tool) =>
        new("module", new XAttribute("id", tool.Id), new XAttribute("title", tool.Title),
            new XAttribute("category", tool.Category), new XAttribute("enabled", tool.Enabled),
            tool.Standalone is { } path ? new XAttribute("standalone", path) : null,
            tool.Page is { } page ? new XElement("page", new XAttribute("title", page.Title),
                new XAttribute("feature", page.Feature), new XAttribute("mode", page.Mode)) : null);

    private static int List(CoreHost host, string workspace, string? module = null)
    {
        var runner = host.OpenRunner(new FeatureContext(workspace), module, Console.Error.WriteLine);
        foreach (var feature in runner.Features.GroupBy(contract => contract.Id))
        {
            var versions = string.Join(",", feature.Select(contract => contract.Version));
            Console.WriteLine($"{feature.Key}\t{versions}\t{feature.First().Title}");
        }
        return 0;
    }

    private static int Execute(CoreHost host, string feature, string workspace, string request, string? module = null)
    {
        var context = new FeatureContext(workspace);
        Console.WriteLine(RunFeature(host, context, feature, request, module));
        return 0;
    }

    private static XElement RunFeature(CoreHost host, FeatureContext context, string feature, string request, string? module)
    {
        var runner = host.OpenRunner(context, module, Console.Error.WriteLine);
        using var input = File.OpenRead(request);
        return runner.Run(feature, context, input);
    }

    /// <summary>Uses a workspace receipt when the caller cannot inherit the foreground process streams.</summary>
    private static int ExecuteWithReceipt(CoreHost host, string feature, string workspace, string request, string module, string receipt)
    {
        var context = new FeatureContext(workspace);
        using var output = File.Open(context.FilePath(receipt), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var (result, exitCode) = RunForReceipt(host, context, feature, request, module);
        result.Save(output);
        return exitCode;
    }

    private static (XElement Result, int ExitCode) RunForReceipt(CoreHost host, FeatureContext context,
        string feature, string request, string module)
    {
        try { return (RunFeature(host, context, feature, request, module), 0); }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            var exitCode = error is UnauthorizedAccessException ? AccessDeniedReceiptExitCode : 1;
            return (new XElement("error", error.Message), exitCode);
        }
    }

    private static int Help(CoreHost host, string name, string workspace)
    {
        Console.WriteLine($"{name}\n  tools [WORKSPACE]\n  list [WORKSPACE [MODULE]]\n  run FEATURE WORKSPACE REQUEST_XML [MODULE]\n  run-receipt FEATURE WORKSPACE REQUEST_XML MODULE RECEIPT_RELATIVE\n  help [--workspace WORKSPACE]");
        WriteRegisteredHelp(host, workspace);
        return 0;
    }

    private static int ReceiveLaunch(string[] received)
    {
        Console.WriteLine(JsonSerializer.Serialize(received));
        return 0;
    }

    private static int UsageError(string? detail = null)
    {
        if (detail is not null)
            Console.Error.WriteLine(detail);
        Console.Error.WriteLine("命令无效。使用 help 查看用法。");
        return 2;
    }

}
