using System.Text.RegularExpressions;

namespace LaunchDesk.UseCases;

/// <summary>核心启动规则：把冻结配置转换为产品参数、秘密插槽和独立子进程环境。</summary>
/// <remarks>仅计算计划，不访问文件/凭据、不创建进程。Monitor/Marine分别用BuildMonitorArguments/BuildMarineArguments；产品代码及初始化文件取自所选应用，不按版本/模块猜测。</remarks>
/// <param name="environmentPolicy">生成独立子进程环境的纯规则。</param>
public sealed class LaunchPlanBuilder(ProcessEnvironmentPolicy environmentPolicy)
{
    /// <summary>按冻结入口选择参数协议，合并工具环境，生成不含明文秘密的启动计划。</summary>
    /// <remarks>协调器检查修订和机器绑定，进程网关检查路径存在性；Marine运行时秘密须在建进程前通过批处理字面词元校验。</remarks>
    /// <param name="snapshot">同一修订下冻结的应用、登录、资源和关联工具。</param>
    /// <param name="parent">捕获的父环境；构造过程不修改它。</param>
    /// <returns>含有序参数和独立环境的计划，或不含秘密的失败原因。</returns>
    public Result<LaunchPlan> Build(LaunchConfiguration snapshot, EnvironmentSnapshot parent)
    {
        var app = snapshot.Application;
        if (string.IsNullOrWhiteSpace(app.ExecutablePath) || string.IsNullOrWhiteSpace(app.WorkingDirectory))
            return Result<LaunchPlan>.Fail("MissingBinding", "请先指定有效的程序入口和工作目录。");
        var arguments = app.Kind switch
        {
            EntryKind.Monitor => BuildMonitorArguments(app, snapshot.Profile.Fields),
            EntryKind.MarineBatch => BuildMarineArguments(app, snapshot.Profile.Fields),
            EntryKind.Executable => BuildExecutableArguments(app, snapshot.Profile.Fields),
            _ => Result<ImmutableArray<LaunchArgument>>.Fail("UnsupportedEntrypoint", "不支持此程序入口类型。")
        };
        if (!arguments.IsSuccess) return Result<LaunchPlan>.Fail(arguments.Error!);
        var environment = BuildEnvironment(snapshot, parent);
        if (!environment.IsSuccess) return Result<LaunchPlan>.Fail(environment.Error!);
        var plan = new LaunchPlan(Guid.NewGuid(), snapshot.WorkspaceRevision, app.ExecutablePath, app.WorkingDirectory,
            app.Kind, arguments.Value, environment.Value, snapshot.Resource, snapshot.Profile.Credential, "");
        return Result<LaunchPlan>.Ok(plan with { PlanDigest = CanonicalDigest.Of(plan) });
    }

    private Result<ImmutableDictionary<string, string>> BuildEnvironment(LaunchConfiguration snapshot,
        EnvironmentSnapshot parent)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in snapshot.Tools)
        foreach (var variable in tool.Variables)
        {
            var value = variable.Value.Replace("{Target}", tool.TargetDirectory, StringComparison.OrdinalIgnoreCase);
            if (variables.TryGetValue(variable.Name, out var previous) && previous != value)
                return Result<ImmutableDictionary<string, string>>.Fail("VariableConflict", "关联工具对同一环境变量设置了不同内容。");
            variables[variable.Name] = value;
        }

        var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Result<string> Expand(string key, HashSet<string> visiting)
        {
            if (expanded.TryGetValue(key, out var found)) return Result<string>.Ok(found);
            if (!visiting.Add(key)) return Result<string>.Fail("VariableCycle", "工具环境变量存在循环引用。");
            Error? error = null;
            var value = Regex.Replace(variables[key], "%([^%]+)%", match =>
            {
                var name = match.Groups[1].Value;
                if (!variables.ContainsKey(name)) return parent.Values.GetValueOrDefault(name) ?? match.Value;
                var result = Expand(name, visiting);
                if (!result.IsSuccess)
                {
                    error = result.Error;
                    return "";
                }

                return result.Value;
            });
            visiting.Remove(key);
            if (error is not null) return Result<string>.Fail(error);
            if (value.Length > 32760) return Result<string>.Fail("VariableTooLong", "展开后的工具变量过长。");
            expanded[key] = value;
            return Result<string>.Ok(value);
        }

        foreach (var name in variables.Keys)
        {
            var result = Expand(name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (!result.IsSuccess) return Result<ImmutableDictionary<string, string>>.Fail(result.Error!);
        }

        return environmentPolicy.Build(parent.Values, expanded,
            snapshot.Resource is null ? null : $"{snapshot.Resource.RmsPort}@{snapshot.Resource.Host}");
    }

    /// <summary>从冻结配置构造 Monitor 原生 EXE 启动参数。</summary>
    /// <remarks>
    /// 顺序固定为 <c>PROD &lt;product&gt; init &lt;init&gt; &lt;project&gt; &lt;user&gt;/&lt;password&gt; /&lt;mdb&gt; &lt;module&gt;</c>。
    /// Design 常用 E3D 与 launch.init；E3D 2.1 Paragon 的已确认入口使用 CATALOGUE 与 catalogue.init，均由应用配置明确指定。
    /// 初始化路径保留完整词元，由原生 ArgumentList 处理空格，不提前把双引号写入参数值。密码只表示为 SecretSlot。
    /// </remarks>
    /// <example><code>mon.exe PROD CATALOGUE init "C:\Program Files (x86)\AVEVA\Everything3D2.10\catalogue.init" SEA SYSTEM/****** /ALL paragon</code></example>
    /// <param name="application">含 EXE、产品代码与初始化文件的 Monitor 应用定义。</param>
    /// <param name="profile">冻结的项目、用户、MDB 与模块，不含明文密码。</param>
    /// <returns>八个有序参数，其中第六个是带用户前缀的秘密插槽；无效输入返回失败。</returns>
    public static Result<ImmutableArray<LaunchArgument>> BuildMonitorArguments(ApplicationDefinition application,
        ProfileDraft profile)
    {
        if (application.Kind != EntryKind.Monitor || !Extension(application.ExecutablePath, ".exe"))
            return Result<ImmutableArray<LaunchArgument>>.Fail("UnsupportedEntrypoint", "Monitor 入口需要 EXE 程序。");
        var mdb = NormalizeMdb(profile.Mdb);
        if (!Tokens(profile.Project, profile.User, mdb.TrimStart('/'), profile.Module, application.ProductCode) ||
            string.IsNullOrWhiteSpace(application.InitializationPath) ||
            application.InitializationPath.Any(character => char.IsControl(character) || character == '"'))
            return Result<ImmutableArray<LaunchArgument>>.Fail("InvalidArguments", "项目、用户、MDB、模块、产品代码或初始化文件无效。");
        return Result<ImmutableArray<LaunchArgument>>.Ok([
            new("PROD"), new(application.ProductCode), new("init"), new(application.InitializationPath),
            new(profile.Project), new("", true, profile.User + "/"), new(mdb), new(profile.Module)
        ]);
    }

    /// <summary>从冻结配置构造 Marine 批处理启动参数。</summary>
    /// <remarks>
    /// Marine 入口只接受 .bat/.cmd，顺序固定为
    /// <c>&lt;module&gt; noconsole &lt;project&gt; &lt;user&gt;/&lt;password&gt; /&lt;mdb&gt;</c>。
    /// 模块不能含字面双引号，否则厂商脚本使用 %1 比较时无法识别 Paragon 等模块。
    /// 密码只以 SecretSlot 存在于计划，进程边界才临时渲染；处理器在创建进程后立即释放秘密租约，LaunchDesk 不将秘密写入计划或日志。
    /// 多层 cmd 无法保持原值的字符由 MarineBatchPolicy 明确拒绝，不修改厂商脚本。
    /// </remarks>
    /// <example><code>marine.bat Paragon noconsole SEA SYSTEM/****** /ALL</code></example>
    /// <param name="application">已选定的 Marine BAT 或 CMD 应用定义。</param>
    /// <param name="profile">冻结的模块、项目、用户与 MDB，不含明文密码。</param>
    /// <returns>五个有序参数，其中第四个是带用户前缀的秘密插槽；不安全词元返回失败。</returns>
    public static Result<ImmutableArray<LaunchArgument>> BuildMarineArguments(ApplicationDefinition application,
        ProfileDraft profile)
    {
        if (application.Kind != EntryKind.MarineBatch ||
            !(Extension(application.ExecutablePath, ".bat") || Extension(application.ExecutablePath, ".cmd")))
            return Result<ImmutableArray<LaunchArgument>>.Fail("UnsupportedEntrypoint", "Marine 入口需要 BAT 或 CMD 文件。");
        var mdb = NormalizeMdb(profile.Mdb);
        if (!Tokens(profile.Project, profile.User, mdb.TrimStart('/'), profile.Module) ||
            !new[] { profile.Project, profile.User, mdb, profile.Module }.All(MarineBatchPolicy.IsLiteralToken))
            return Result<ImmutableArray<LaunchArgument>>.Fail("UnsafeBatchArguments", "Marine 登录字段包含批处理无法原样传递的字符。");
        return Result<ImmutableArray<LaunchArgument>>.Ok([
            new(profile.Module), new("noconsole"), new(profile.Project),
            new("", true, profile.User + "/"), new(mdb)
        ]);
    }

    private static Result<ImmutableArray<LaunchArgument>> BuildExecutableArguments(ApplicationDefinition app,
        ProfileDraft profile)
    {
        if (!Extension(app.ExecutablePath, ".exe"))
            return Result<ImmutableArray<LaunchArgument>>.Fail("UnsupportedEntrypoint", "原生入口需要 EXE 程序。");
        var arguments = ImmutableArray.CreateBuilder<LaunchArgument>();
        foreach (var token in app.ArgumentTemplate.IsDefault ? [] : app.ArgumentTemplate)
            if (token.Equals("{password}", StringComparison.OrdinalIgnoreCase))
                arguments.Add(new LaunchArgument("", true));
            else
                arguments.Add(new LaunchArgument(token
                    .Replace("{project}", profile.Project, StringComparison.OrdinalIgnoreCase)
                    .Replace("{user}", profile.User, StringComparison.OrdinalIgnoreCase)
                    .Replace("{mdb}", profile.Mdb, StringComparison.OrdinalIgnoreCase)
                    .Replace("{module}", profile.Module, StringComparison.OrdinalIgnoreCase)));
        return Result<ImmutableArray<LaunchArgument>>.Ok(arguments.ToImmutable());
    }

    private static string NormalizeMdb(string value) => "/" + value.Trim().TrimStart('/');
    private static bool Extension(string path, string extension) =>
        Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static bool Tokens(params string[] values)
    {
        return values.All(value =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= 300 &&
            !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"'));
    }
}
