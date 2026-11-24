namespace Core30.Core;

public static partial class CoreCommandLine
{
    private static int RunRegisteredCommand(CoreHost host, string[] args)
    {
        if (args is not [var module, var name, ..] || module.StartsWith('-') || name.StartsWith('-') ||
            module is "help" or "tools" or "list" or "run" or "run-receipt" or "__receive-launch")
            return UsageError();

        try
        {
            var (values, showHelp) = ReadOptions(args.AsSpan(2));
            var workspace = values.Remove("workspace", out var path) ? path : Directory.GetCurrentDirectory();
            var registered = host.Commands(workspace, module).SingleOrDefault(item => item.Command.Name == name);
            if (registered is null)
                return UsageError($"命令 {module} {name} 未登记或所属模块未启用。");

            CheckOptions(registered.Command, values, showHelp);
            if (showHelp)
                return HelpForCommand(registered);

            var context = new FeatureContext(workspace);
            var runner = host.OpenRunner(context, registered.Module, Console.Error.WriteLine);
            using var request = new MemoryStream();
            registered.Command.CreateRequest(values).Save(request);
            request.Position = 0;
            Console.WriteLine(runner.Run(registered.Command.Feature, context, request));
            return 0;
        }
        catch (CommandUsageException error)
        {
            return UsageError(error.Message);
        }
    }

    private static (Dictionary<string, string> Values, bool ShowHelp) ReadOptions(ReadOnlySpan<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var showHelp = false;
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            if (option == "--help")
            {
                if (showHelp)
                    throw new CommandUsageException("参数 --help 重复。");
                showHelp = true;
                continue;
            }
            if (!option.StartsWith("--", StringComparison.Ordinal) || option.Length == 2)
                throw new CommandUsageException($"无法识别参数 {option}。请使用 --名称 值。");
            if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new CommandUsageException($"参数 {option} 缺少值。");
            if (!values.TryAdd(option[2..], args[++index]))
                throw new CommandUsageException($"参数 {option} 重复。");
        }
        return (values, showHelp);
    }

    private static void CheckOptions(ToolCommand command, Dictionary<string, string> values, bool showHelp)
    {
        var unknown = values.Keys.FirstOrDefault(name => command.Options.All(option => option.Name != name));
        if (unknown is not null)
            throw new CommandUsageException($"未知参数 --{unknown}。");
        var missing = command.Options.FirstOrDefault(option => option.Required && !values.ContainsKey(option.Name));
        if (!showHelp && missing is not null)
            throw new CommandUsageException($"缺少必填参数 --{missing.Name}。");
    }

    private static void WriteRegisteredHelp(CoreHost host, string workspace)
    {
        try
        {
            foreach (var entry in host.Commands(workspace).OrderBy(command => command.Name, StringComparer.Ordinal))
                Console.WriteLine($"  {Usage(entry)}  {entry.Command.Description}");
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            Console.Error.WriteLine(error.Message);
        }
    }

    private static int HelpForCommand(RegisteredToolCommand entry)
    {
        Console.WriteLine(Usage(entry));
        Console.WriteLine(entry.Command.Description);
        foreach (var option in entry.Command.Options)
            Console.WriteLine($"  --{option.Name} {option.ValueName}  {option.Description}");
        Console.WriteLine("  --workspace WORKSPACE  工作区路径，默认采用当前目录。");
        return 0;
    }

    private static string Usage(RegisteredToolCommand entry)
    {
        var options = entry.Command.Options.Select(option =>
        {
            var usage = $"--{option.Name} {option.ValueName}";
            return option.Required ? usage : $"[{usage}]";
        });
        return string.Join(' ', options.Prepend(entry.Name).Append("[--workspace WORKSPACE]"));
    }

    private sealed class CommandUsageException(string message) : Exception(message) { }
}
