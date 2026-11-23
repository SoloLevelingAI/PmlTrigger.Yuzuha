using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Core30.Core;
using Core30.Generated;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private static CoreData RunExternalLaunch(FeatureContext context, CoreData request)
    {
        var mode = Read(request, "mode");
        ValidateExternalMode(mode, (bool?)request.Field("confirm"));
        var plan = BuildVendorPlan(context, request);
        var result = VendorPreview(plan);
        if (mode == "launch")
            result.Add(SubmitVendorPlan(context, plan));
        return result;
    }

    private static void ValidateExternalMode(string mode, bool? confirm)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Monitor / Marine 启动需要 Windows。");
        if (mode == "launch" && confirm != true)
            throw new InvalidDataException("启动真实程序须在 XML 中显式设置 mode=\"launch\" 与 confirm=\"true\"。");
    }

    private static VendorPlan BuildVendorPlan(FeatureContext context, CoreData request)
    {
        var selection = request.Children().Single();
        var protocol = VendorProtocols.Require(selection.Name.LocalName);
        var (executable, workingDirectory) = ReadVendorPaths(request, protocol);
        var fields = VendorLaunchFields.ReadValidated(request);
        var license = ReadLicense(fields);
        var login = ReadLogin(fields);
        var (arguments, uicPath) = protocol.BuildArguments(context, selection, executable, login);
        return new VendorPlan(executable, workingDirectory, license, protocol, arguments, uicPath);
    }

    private static (string Executable, string WorkingDirectory) ReadVendorPaths(CoreData request, IVendorLaunchProtocol protocol)
    {
        var executable = ReadLocalPath(request, "executable");
        var workingDirectory = ReadLocalPath(request, "workingDirectory");
        protocol.ValidateEntry(executable);
        ValidateEntryFile(executable);
        ValidateDirectory(workingDirectory);
        return (executable, workingDirectory);
    }

    private static string? ReadLicense(VendorLaunchFields fields)
    {
        if (fields.LicenseHost is null && fields.LicensePort is null)
            return null;
        if (fields.LicenseHost is not { } host || fields.LicensePort is not { } port)
            throw new InvalidDataException("指定许可地址时，须同时填写服务器和端口。");
        ValidateLicenseHost(host);
        return $"{port}@{host}";
    }

    private static void ValidateLicenseHost(string host)
    {
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.IndexOfAny(['[', ']', '%']) >= 0)
            throw new InvalidDataException("许可地址须为主机名或 IP 地址。请单独填写端口。");
    }

    private static VendorLogin ReadLogin(VendorLaunchFields fields)
    {
        var login = new VendorLogin(fields.Project, fields.User, fields.Mdb.Trim().TrimStart('/'), fields.Module);
        ValidateTokens(login.Project, login.User, login.Mdb, login.Module);
        ValidateUser(login.User);
        return login;
    }

    private static CoreData VendorPreview(VendorPlan plan) =>
        new("result", new CoreField("mode", plan.Protocol.DisplayName),
            new CoreField("executable", plan.Executable), new CoreField("workingDirectory", plan.WorkingDirectory),
            new CoreData("environment", EnvironmentPreview("AVEVA_LICENSE_SERVER_LIST", plan.LicenseAddress),
                EnvironmentPreview("CAF_UIC_PATH", plan.CafUicPath)),
            new CoreData("preview", plan.Arguments.Select(argument => new CoreData("argument",
                new CoreField("secretSlot", argument.SecretSlot), argument.Mask()))));

    private static CoreData? EnvironmentPreview(string name, string? value) =>
        value is null ? null : new CoreData("variable", new CoreField("name", name), value);

    private static CoreData SubmitVendorPlan(FeatureContext context, VendorPlan plan)
    {
        var password = ReadLaunchPassword(context);
        var start = CreateVendorStart(plan, password);
        var (pid, startTicks) = SubmitVendorProcess(start, plan.Protocol);
        return new CoreData("process", new CoreField("status", "Submitted"), new CoreField("pid", pid),
            startTicks > 0 ? new CoreField("startTicks", startTicks) : null);
    }

    private static ProcessStartInfo CreateVendorStart(VendorPlan plan, string password)
    {
        var arguments = plan.Arguments.Select(argument => argument.Fill(password)).ToArray();
        var start = new ProcessStartInfo(plan.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = plan.WorkingDirectory
        };
        SetVendorEnvironment(start, plan);
        try
        {
            plan.Protocol.ConfigureArguments(start, arguments);
            return start;
        }
        catch
        {
            plan.Protocol.ClearArguments(start);
            throw;
        }
        finally
        {
            Array.Clear(arguments);
        }
    }

    private static void SetVendorEnvironment(ProcessStartInfo start, VendorPlan plan)
    {
        if (plan.LicenseAddress is { } address)
            start.Environment["AVEVA_LICENSE_SERVER_LIST"] = address;
        if (plan.CafUicPath is { } customization)
            start.Environment["CAF_UIC_PATH"] = customization;
    }

    private static (int Pid, long StartTicks) SubmitVendorProcess(ProcessStartInfo start, IVendorLaunchProtocol protocol)
    {
        try
        {
            return StartVendorProcess(start);
        }
        finally
        {
            protocol.ClearArguments(start);
        }
    }

    private static (int Pid, long StartTicks) StartVendorProcess(ProcessStartInfo start)
    {
        try
        {
            return WindowsDetachedProcess.StartWithIdentity(start);
        }
        catch (Win32Exception error)
        {
            throw new IOException($"Windows 无法创建启动进程（{error.NativeErrorCode}）。{error.Message}", error);
        }
    }

    private static string ReadLaunchPassword(FeatureContext context)
    {
        var password = context.Input is { } input
            ? input.ReadPassword?.Invoke()
            : Console.IsInputRedirected ? ReadPipedPassword() : ReadHiddenPassword();
        ValidatePassword(password);
        return password!;
    }

    private static string? ReadPipedPassword()
    {
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(true, true),
            detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        try
        {
            return input.ReadLine();
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("从标准输入传入的 AVEVA 密码须为 UTF-8 单行文本。");
        }
    }

    private static string ReadHiddenPassword()
    {
        Console.Error.Write("AVEVA password: ");
        var password = new StringBuilder();
        for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
            ApplyPasswordKey(password, key);
        Console.Error.WriteLine();
        return password.ToString();
    }

    private static void ApplyPasswordKey(StringBuilder password, ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
                password.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            ValidatePasswordLength(password.Length);
        }
    }

    private static void ValidatePassword(string? password)
    {
        if (password is null || password.Any(character => character is '\0' or '\r' or '\n'))
            throw new InvalidDataException("需要从控制台输入一行 AVEVA 密码。");
        ValidatePasswordLength(password.Length);
    }

    private static void ValidatePasswordLength(int length)
    {
        if (length > 1024)
            throw new InvalidDataException("密码超过允许长度。");
    }

    private static string ReadLocalPath(CoreData input, string attribute)
    {
        var path = Read(input, attribute);
        ValidateLocalPath(path, attribute);
        return Path.GetFullPath(path);
    }

    private static void ValidateLocalPath(string path, string attribute)
    {
        if (path.Length < 4 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('\\' or '/')
            || path[3..].Contains(':') || path.Any(character => char.IsControl(character) || character == '"'))
            throw new InvalidDataException($"{attribute} 须为本地驱动器下的完整文件路径。");
    }

    private static void ValidateEntryExtension(string path, params string[] supported)
    {
        if (!supported.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Monitor 入口须为 EXE，Marine 入口须为 BAT 或 CMD。");
    }

    private static void ValidateEntryFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("未找到配置中指定的启动入口。", path);
    }

    private static void ValidateDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"未找到工作目录 {path}。");
    }

    private static void ValidateTokens(params string[] values)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(character =>
            char.IsWhiteSpace(character) || char.IsControl(character) || character == '"')))
            throw new InvalidDataException("项目、用户、MDB、模块或产品代码须为无空白和引号的非空词元。");
    }

    private static void ValidateUser(string user)
    {
        if (user.Contains('/'))
            throw new InvalidDataException("登录用户不可包含凭据分隔符 /。");
    }

    private sealed record VendorLogin(string Project, string User, string Mdb, string Module)
    {
        public VendorArgument UserToken => new(User + "/", SecretSlot: true);
        public VendorArgument MdbToken => new("/" + Mdb);
    }

    private sealed record VendorPlan(string Executable, string WorkingDirectory, string? LicenseAddress,
        IVendorLaunchProtocol Protocol, VendorArgument[] Arguments, string? CafUicPath);

    private sealed record VendorArgument(string Value, bool SecretSlot = false)
    {
        public string Mask() => Fill("******");
        public string Fill(string password) => SecretSlot ? Value + password : Value;
    }
}
