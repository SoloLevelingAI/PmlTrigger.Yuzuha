using System.Collections.Immutable;
using System.Diagnostics;
using LaunchDesk.Kernel;
using LaunchDesk.OperationContracts;
using LaunchDesk.UseCases;
using PlanEntryKind = LaunchDesk.Kernel.EntryKind;

namespace LaunchDesk.Core;

/// <summary>保留现有桌面调用契约，将登录字段交给共享的核心启动规则。</summary>
/// <remarks>不改变 View、ViewModel 绑定、配置格式或凭据存储；本适配层只接入参数构造，不代表整套操作协调器已经迁移。</remarks>
public static class LaunchService
{
    /// <summary>显示所选入口的参数顺序；只显示本机凭据占位，不读取密码。</summary>
    public static string Preview(LoginProfile p, AppEntry a, LicenseEntry l)
    {
        var command = a.Kind == EntryKind.Monitor
            ? $"PROD {a.ProductCode} init \"{a.InitializationPath}\"\n{p.Project} {p.Username}/{{本机凭据}} {NormalizeMdb(p.Mdb)} {p.Module}"
            : $"{p.Module} noconsole {p.Project} {p.Username}/{{本机凭据}} {NormalizeMdb(p.Mdb)}";
        return $"许可（仅本次进程）\nAVEVA_LICENSE_SERVER_LIST={l.Address}\n\n\"{a.ExecutablePath}\"\n{command}";
    }

    /// <summary>把 MDB 规范为一个前导斜杠，保留名称大小写。</summary>
    public static string NormalizeMdb(string mdb) => "/" + mdb.Trim().TrimStart('/');

    /// <summary>核对本机路径、许可字段和共享的产品参数规则，不创建目标进程。</summary>
    public static void Validate(LoginProfile p, AppEntry a, LicenseEntry l)
    {
        WorkspaceStore.ValidateLicense(l);
        if (!File.Exists(a.ExecutablePath))
            throw new FileNotFoundException("未找到程序，请先修复应用路径。", a.ExecutablePath);
        if (!Directory.Exists(a.WorkingDirectory))
            throw new DirectoryNotFoundException("工作目录不存在，请在“应用与版本”中指定有效目录。");
        if (a.Kind == EntryKind.Monitor && !File.Exists(a.InitializationPath))
            throw new FileNotFoundException("初始化文件不存在，请选择现有初始化文件。", a.InitializationPath);
        _ = BuildArguments(p, a);
        if (a.Kind == EntryKind.MarineBatch && a.ExecutablePath.Any(c => c is '%' or '"' || char.IsControl(c)))
            throw new InvalidDataException("Marine 批处理路径不能包含百分号、双引号或控制字符。");
    }

    /// <summary>沿用现有进程启动边界，使用共享规则生成 Monitor 或 Marine 参数。</summary>
    /// <remarks>
    /// Monitor 使用原生 ArgumentList；Marine 只给入口路径加引号，避免厂商脚本的 %1 模块比较失效。
    /// 密码仅在本机进程边界填入秘密插槽，不放入预览或持久配置；Marine 不能原样传递的词元在启动前拒绝。
    /// </remarks>
    public static ProcessStartInfo Build(LoginProfile p, AppEntry a, LicenseEntry l, string password)
    {
        Validate(p, a, l);
        if (password.Any(c => c is '\0' or '\r' or '\n'))
            throw new InvalidDataException("密码不能包含换行或空字符。");
        var arguments = BuildArguments(p, a)
            .Select(argument => argument.SecretSlot ? argument.SecretPrefix + password : argument.Value).ToArray();
        try
        {
            if (a.Kind == EntryKind.MarineBatch && !arguments.All(MarineBatchPolicy.IsLiteralToken))
                throw new InvalidDataException("Marine 登录字段或密码包含批处理无法原样传递的字符。");
            var start = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = a.WorkingDirectory };
            start.Environment["AVEVA_LICENSE_SERVER_LIST"] = l.Address;
            if (a.Kind == EntryKind.Monitor)
            {
                start.FileName = a.ExecutablePath;
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
            }
            else
            {
                start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                start.CreateNoWindow = true;
                for (var index = 0; index < arguments.Length; index++)
                    start.Environment[$"LAUNCHDESK_ARG_{index}"] = arguments[index];
                var tokens = string.Join(' ', Enumerable.Range(0, arguments.Length).Select(index => $"%LAUNCHDESK_ARG_{index}%"));
                start.Arguments = $"/d /v:off /s /c \"\"{a.ExecutablePath}\" {tokens}\"";
            }
            return start;
        }
        finally { Array.Clear(arguments); }
    }

    // 仅投影纯参数规则所需字段，不迁移配置、标识或凭据引用。
    private static ImmutableArray<LaunchArgument> BuildArguments(LoginProfile p, AppEntry a)
    {
        var kind = a.Kind switch
        {
            EntryKind.Monitor => PlanEntryKind.Monitor,
            EntryKind.MarineBatch => PlanEntryKind.MarineBatch,
            _ => throw new InvalidDataException("不支持此程序入口类型。")
        };
        var application = new ApplicationDefinition(Guid.Empty, a.Name, a.Version, kind,
            a.ExecutablePath, a.InitializationPath, a.WorkingDirectory, a.ProductCode);
        var profile = new ProfileDraft(null, p.Name, Guid.Empty, p.VersionGroup,
            p.Project, p.Username, p.Mdb, p.Module, null, []);
        var result = kind == PlanEntryKind.Monitor
            ? LaunchPlanBuilder.BuildMonitorArguments(application, profile)
            : LaunchPlanBuilder.BuildMarineArguments(application, profile);
        if (!result.IsSuccess) throw new InvalidDataException(result.Error!.SafeMessage);
        return result.Value;
    }
}
