using System.Diagnostics;

namespace YuzuhaToolkit.Workspace;

/// <summary>参照 LaunchDesk 的原生 Monitor 与 Marine 启动参数，隔离本次进程环境。</summary>
public static class LaunchService
{
    /// <summary>生成无密码启动预览；不创建进程，也不读凭据。</summary>
    /// <param name="profile">用户选定的产品和登录字段。</param>
    /// <param name="modules">可替换的产品模块目录；未提供时使用本机产品目录。</param>
    public static string Preview(LaunchProfile profile, IProductModuleCatalog? modules = null) => string.Join(" ", Arguments(profile, "{本机凭据或本次输入}", modules).Select(argument => argument.Contains(' ') ? '"' + argument + '"' : argument));

    /// <summary>生成实际启动描述，密码只在进程边界注入；不会保存到工作区或日志。</summary>
    /// <param name="profile">启动配置。</param>
    /// <param name="password">本次输入密码。</param>
    /// <param name="modules">可替换的产品模块目录，保持调用服务与进程参数使用同一模块规则。</param>
    public static ProcessStartInfo Build(LaunchProfile profile, string password, IProductModuleCatalog? modules = null)
    {
        if (!File.Exists(profile.Executable)) throw new FileNotFoundException("程序入口不存在。", profile.Executable);
        if (!Directory.Exists(profile.WorkingDirectory)) throw new DirectoryNotFoundException("工作目录不存在。");
        if (password.Any(char.IsControl)) throw new ArgumentException("密码不能包含控制字符。");
        var arguments = Arguments(profile, password, modules);
        var start = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = profile.WorkingDirectory };
        if (profile.Kind == "Monitor")
        {
            if (!File.Exists(profile.Initialization)) throw new FileNotFoundException("初始化文件不存在。", profile.Initialization);
            start.FileName = profile.Executable;
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
        }
        else
        {
            if (!arguments.All(BatchToken) || profile.Executable.Any(c => c is '"' or '%' || char.IsControl(c))) throw new ArgumentException("Marine 启动字段包含批处理无法原样传递的字符。");
            start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"); start.CreateNoWindow = true;
            for (var index = 0; index < arguments.Length; index++) start.Environment[$"YUZUHA_LAUNCH_ARG_{index}"] = arguments[index];
            start.Arguments = "/d /v:off /s /c \"\"" + profile.Executable + "\" " + string.Join(' ', Enumerable.Range(0, arguments.Length).Select(index => $"%YUZUHA_LAUNCH_ARG_{index}%")) + "\"";
        }
        if (!string.IsNullOrWhiteSpace(profile.License)) start.Environment["AVEVA_LICENSE_SERVER_LIST"] = profile.License;
        foreach (var variable in profile.Environment)
        {
            if (variable.Key.Any(c => c is '=' or '\0') || variable.Value.Contains('\0')) throw new ArgumentException("环境变量无效。");
            start.Environment[variable.Key] = variable.Value;
        }
        StartupPolicy.Prepare(start);
        return start;
    }

    /// <summary>启动产品入口并返回入口 PID；不注入标准设备或继承调用方句柄，保持原生 GUI 启动合同并释放 CLI 输出管道。</summary>
    /// <param name="profile">已验证的启动配置。</param>
    /// <param name="password">仅在进程边界注入的本次密码。</param>
    /// <param name="modules">产品模块目录。</param>
    public static int Start(LaunchProfile profile, string password, IProductModuleCatalog? modules = null) => Start(Build(profile, password, modules));

    /// <summary>启动已准备的产品入口描述，保留本次启动策略的关联信息。</summary>
    /// <param name="start">由 Build 生成的启动描述。</param>
    public static int Start(ProcessStartInfo start) => DetachedProcess.Start(start);

    private static string[] Arguments(LaunchProfile profile, string password, IProductModuleCatalog? modules)
    {
        var catalog = (modules ?? new ProductModuleCatalog()).Get(new(profile.Executable, profile.Product, profile.Version, profile.Kind));
        if (!catalog.Options.Any(m => m.Value.Equals(profile.Module, StringComparison.OrdinalIgnoreCase)) ||
            (catalog.Product == "AM" && profile.Kind != "MarineBatch") || (catalog.Product == "E3D" && profile.Kind != "Monitor"))
            throw new ArgumentException("启动产品、入口类型与模块不匹配，请重新选择该产品的模块。");
        var mdb = "/" + profile.Mdb.Trim().TrimStart('/');
        if (new[] { profile.Project, profile.User, mdb.TrimStart('/'), profile.Module }.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"')))
            throw new ArgumentException("请填写不含空白或控制字符的项目、用户、MDB 与模块。");
        if (profile.Kind == "Monitor")
        {
            if (!Path.GetExtension(profile.Executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(profile.Product) || profile.Product.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(profile.Initialization) || profile.Initialization.Any(c => char.IsControl(c) || c == '"'))
                throw new ArgumentException("Monitor 需要 EXE、产品代码和初始化文件。");
            return ["PROD", profile.Product, "init", profile.Initialization, profile.Project, profile.User + "/" + password, mdb, profile.Module];
        }
        if (profile.Kind != "MarineBatch" || !new[] { ".bat", ".cmd" }.Contains(Path.GetExtension(profile.Executable), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("启动类型或入口扩展名无效。");
        return [profile.Module, "noconsole", profile.Project, profile.User + "/" + password, mdb];
    }
    private static bool BatchToken(string value) => !string.IsNullOrEmpty(value) && value.Length <= 1024 && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '%' or '!' or '^' or '&' or '|' or '<' or '>' or '(' or ')' or ',' or ';' or '=');
}
