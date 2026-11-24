using System.Diagnostics;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private interface IVendorLaunchProtocol
    {
        string DisplayName { get; }
        void ValidateEntry(string executable);
        (VendorArgument[] Arguments, string? CafUicPath) BuildArguments(FeatureContext context, CoreData selection,
            string executable, VendorLogin login);
        void ConfigureArguments(ProcessStartInfo start, string[] arguments);
        void ClearArguments(ProcessStartInfo start);
    }

    private sealed class MonitorLaunchProtocol : IVendorLaunchProtocol
    {
        public string DisplayName => "Monitor";

        public void ValidateEntry(string executable) => ValidateEntryExtension(executable, ".exe");

        public (VendorArgument[] Arguments, string? CafUicPath) BuildArguments(FeatureContext context, CoreData selection,
            string executable, VendorLogin login)
        {
            var (product, init) = ReadMonitorFields(selection);
            VendorArgument[] arguments = [new("PROD"), new(product), new("init"), new(init), new(login.Project),
                login.UserToken, login.MdbToken, new(login.Module)];
            return (arguments, ReadCustomizationPath(context, selection, executable));
        }

        public void ConfigureArguments(ProcessStartInfo start, string[] arguments)
        {
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
        }

        public void ClearArguments(ProcessStartInfo start) => start.ArgumentList.Clear();

        private static (string Product, string Init) ReadMonitorFields(CoreData selection)
        {
            var product = Read(selection, "product");
            var init = ReadLocalPath(selection, "init");
            ValidateTokens(product);
            ValidateInitializationFile(init);
            return (product, init);
        }

        private static void ValidateInitializationFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("未找到 Monitor 初始化文件。", path);
        }

        private static string? ReadCustomizationPath(FeatureContext context, CoreData selection, string executable)
        {
            var workspaceFirst = (bool?)selection.Field("uicFirst") == true;
            if ((string?)selection.Field("uicDirectory") is not { } relative)
                return workspaceFirst ? throw new InvalidDataException("UIC 优先目录需要设置 uicDirectory。") : null;
            var directory = context.FilePath(relative);
            var installation = Path.GetDirectoryName(executable)!;
            ValidateCustomizationPath(directory, installation);
            return BuildCustomizationPath(directory, installation, workspaceFirst);
        }

        private static string BuildCustomizationPath(string directory, string installation, bool workspaceFirst)
        {
            var inherited = Environment.GetEnvironmentVariable("CAF_UIC_PATH")?.Split(Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            var paths = workspaceFirst
                ? new[] { directory, installation }.Concat(inherited)
                : new[] { installation }.Concat(inherited).Append(directory);
            return string.Join(Path.PathSeparator, paths.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static void ValidateCustomizationPath(string directory, string installation)
        {
            ValidateDirectory(directory);
            if (directory.Contains(Path.PathSeparator) || installation.Contains(Path.PathSeparator))
                throw new InvalidDataException("UIC 目录和安装路径须可作为独立的 CAF_UIC_PATH 路径项。");
        }
    }

    private sealed class MarineLaunchProtocol : IVendorLaunchProtocol
    {
        private const string ArgumentPrefix = "CORE30_F01_ARG_";

        public string DisplayName => "Marine";

        public void ValidateEntry(string executable)
        {
            ValidateEntryExtension(executable, ".bat", ".cmd");
            if (executable.Contains('%'))
                throw new InvalidDataException("Marine 批处理路径不可包含百分号。");
        }

        public (VendorArgument[] Arguments, string? CafUicPath) BuildArguments(FeatureContext context, CoreData selection,
            string executable, VendorLogin login)
        {
            VendorArgument[] arguments = [new(login.Module), new("noconsole"), new(login.Project), login.UserToken, login.MdbToken];
            ValidateMarineTokens(arguments.Select(argument => argument.Value));
            return (arguments, null);
        }

        // The batch path is quoted for CMD. Marine checks the unquoted module with %1.
        public void ConfigureArguments(ProcessStartInfo start, string[] arguments)
        {
            ValidateMarineTokens(arguments);
            var batch = start.FileName;
            start.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var variables = new string[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
            {
                var name = $"{ArgumentPrefix}{index}";
                start.Environment[name] = arguments[index];
                variables[index] = $"%{name}%";
            }
            start.Arguments = $"/d /v:off /s /c \"\"{batch}\" {string.Join(' ', variables)}\"";
        }

        public void ClearArguments(ProcessStartInfo start)
        {
            foreach (var name in start.Environment.Keys.Where(name => name.StartsWith(ArgumentPrefix, StringComparison.Ordinal)).ToArray())
                start.Environment.Remove(name);
        }

        private static void ValidateMarineTokens(IEnumerable<string> arguments)
        {
            const string cmdOperators = "\"%!^&|<>() ,;=";
            if (arguments.Any(token => token.Length is 0 or > 1024
                || token.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || cmdOperators.Contains(character))))
                throw new InvalidDataException("Marine 登录字段或密码包含批处理无法原样传递的字符。");
        }
    }
}
