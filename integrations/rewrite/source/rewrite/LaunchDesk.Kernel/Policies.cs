using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace LaunchDesk.Kernel;

/// <summary>生成每次启动独立的子进程环境。</summary>
public sealed class ProcessEnvironmentPolicy
{
    /// <summary>按Windows大小写无关规则复制父环境，先移除全部保留许可变量，再注入本次许可。EnvironmentOverlay兼含移除项和设置项；工具变量不得覆盖保留名，输入不变。</summary>
    /// <param name="parent">捕获的父环境或前一状态，不直接修改父进程环境。</param>
    /// <param name="toolVariables">已展开并检查冲突的工具变量。</param>
    /// <param name="licenseAddress">本次子进程使用的独立许可地址；空值表示不注入。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    public Result<ImmutableDictionary<string, string>> Build(IReadOnlyDictionary<string, string> parent,
        IReadOnlyDictionary<string, string> toolVariables, string? licenseAddress)
    {
        var builder = parent.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase).ToBuilder();
        foreach (var pair in toolVariables)
        {
            if (!ValidVariableName(pair.Key) || pair.Value.Contains('\0'))
                return Result<ImmutableDictionary<string, string>>.Fail("InvalidVariable", "工具变量名称或值无效。");
            if (Reserved(pair.Key))
                return Result<ImmutableDictionary<string, string>>.Fail("ReservedVariable", "工具变量不能覆盖许可或启动器专用变量。");
            builder[pair.Key] = pair.Value;
        }

        // 所选资源决定子进程许可变量，覆盖父进程的不同值。
        if (licenseAddress is not null) builder["AVEVA_LICENSE_SERVER_LIST"] = licenseAddress;
        return Result<ImmutableDictionary<string, string>>.Ok(builder.ToImmutable());
    }

    /// <summary>判断环境变量名称是否满足受支持的字符规则。</summary>
    /// <param name="name">显示名或配置名</param>
    /// <returns>是否满足所述条件。</returns>
    public static bool ValidVariableName(string name)
    {
        return Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant);
    }

    /// <summary>判断变量是否属于许可或进程保留范围，避免工具覆盖专用配置。</summary>
    /// <param name="name">显示名或配置名</param>
    /// <returns>是否满足所述条件。</returns>
    public static bool Reserved(string name)
    {
        return name.Equals("AVEVA_LICENSE_SERVER_LIST", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("LAUNCHDESK_", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>校验公共生命周期、操作身份、序号和终结约束。</summary>
public sealed class OperationStatePolicy
{
    /// <summary>计算下一修订号，溢出时拒绝继续递增。</summary>
    /// <param name="current">刷新前的人工来源选择或当前状态。</param>
    /// <param name="requested">待合并的有限更新意图。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    public Result<RunStage> Next(RunStage current, RunStage requested)
    {
        if (current == requested) return Result<RunStage>.Ok(current);
        if (current is RunStage.Failed or RunStage.Cancelled or RunStage.Exited or RunStage.Completed
            or RunStage.Unconfirmed)
            return Result<RunStage>.Fail("TerminalOperation", "操作已经结束，不能重新执行。");
        if (requested == RunStage.Accepted || (requested == RunStage.Executing && current != RunStage.Accepted))
            return Result<RunStage>.Fail("InvalidTransition", "不能重新取得或重放该操作的执行权。");
        if (current == RunStage.NeedsVerification &&
            requested is not (RunStage.Unconfirmed or RunStage.Failed or RunStage.Cancelled))
            return Result<RunStage>.Fail("VerificationRequired", "该操作必须先核验，不能自动继续。");
        return Result<RunStage>.Ok(requested);
    }
}