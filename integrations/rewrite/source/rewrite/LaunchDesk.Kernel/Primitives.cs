using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LaunchDesk.Kernel;

/// <summary>表示成功但没有额外返回数据的值。</summary>
public readonly record struct Unit;

/// <summary>配置工作空间的非负修订号，用于检测并发修改。</summary>
/// <param name="Value">用于并发核验的数值修订号。</param>
public readonly record struct Revision(long Value)
{
    /// <summary>计算下一修订号，溢出时拒绝继续递增。</summary>
    /// <returns>当前值加一后的修订号。</returns>
    public Revision Next()
    {
        return new Revision(checked(Value + 1));
    }

    /// <summary>返回采用固定区域格式的修订号文本。</summary>
    /// <returns>十进制修订号，不随系统区域设置改变。</returns>
    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>持久操作的稳定标识；重试与结果核验沿用标识。</summary>
/// <param name="Value">原操作的稳定 GUID。</param>
public readonly record struct OperationId(Guid Value)
{
    /// <summary>创建新的稳定号；同一次未确定操作的重试不得再调用此方法换号。</summary>
    /// <returns>基于新 GUID 的操作标识；原操作后续核验应保留此值。</returns>
    public static OperationId New()
    {
        return new OperationId(Guid.NewGuid());
    }

    /// <summary>返回不含连字符的操作号。</summary>
    /// <returns>32 位 GUID 十六进制文本。</returns>
    public override string ToString()
    {
        return Value.ToString("N");
    }
}

/// <summary>直接配置提交的稳定标识，用于去重和核验未确定结果。</summary>
/// <param name="Value">原配置提交的稳定 GUID。</param>
public readonly record struct CommitId(Guid Value)
{
    /// <summary>创建新的稳定号；同一次未确定操作的重试不得再调用此方法换号。</summary>
    /// <returns>基于新 GUID 的配置提交标识；原提交后续核验应保留此值。</returns>
    public static CommitId New()
    {
        return new CommitId(Guid.NewGuid());
    }

    /// <summary>返回不含连字符的配置提交号。</summary>
    /// <returns>32 位 GUID 十六进制文本。</returns>
    public override string ToString()
    {
        return Value.ToString("N");
    }
}

/// <summary>可向用户展示的脱敏错误，含原因码及重试提示。</summary>
/// <param name="Code">脱敏错误原因码</param>
/// <param name="SafeMessage">可展示且无秘密的说明</param>
/// <param name="RetryHint">是否提示按原编号核验或重试</param>
public sealed record Error(string Code, string SafeMessage, bool RetryHint = false);

/// <summary>封装成功值或脱敏错误；失败结果不提供可读取的值。</summary>
/// <include file="../Comments.xml" path="doc/T/*"/>
public sealed class Result<T>
{
    private readonly T? value;
    /// <summary>是否含成功结果。</summary>
    public bool IsSuccess { get; }
    /// <summary>失败时的脱敏错误。</summary>
    public Error? Error { get; }
    /// <summary>成功值；失败时读取抛InvalidOperationException。</summary>
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");

    private Result(T value)
    {
        this.value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
    }

    /// <summary>构造含成功值的结果。</summary>
    /// <include file="../Comments.xml" path="doc/Value/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    public static Result<T> Ok(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>构造仅携带脱敏错误的失败结果。</summary>
    /// <param name="code">稳定且不含秘密的错误原因码。</param>
    /// <param name="message">用户可见且无秘密的提示。</param>
    /// <param name="retry">是否附加按原编号继续核验的提示。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    public static Result<T> Fail(string code, string message, bool retry = false)
    {
        return new Result<T>(new Error(code, message, retry));
    }

    /// <summary>构造仅携带脱敏错误的失败结果。</summary>
    /// <param name="error">失败时的脱敏错误</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    public static Result<T> Fail(Error error)
    {
        return new Result<T>(error);
    }

    /// <summary>仅转换成功值；失败时不调用转换器，原样传递错误及重试提示；转换异常继续向上传播。</summary>
    /// <typeparam name="TNext">转换后的成功值类型。</typeparam>
    /// <param name="map">成功值转换器。</param>
    /// <returns>转换结果或原错误。</returns>
    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        IsSuccess ? Result<TNext>.Ok(map(Value)) : Result<TNext>.Fail(Error!);

    /// <summary>仅成功时继续可能失败的转换；失败不调用转换器，原样传递错误；转换异常向上传播。</summary>
    /// <typeparam name="TNext">后续操作的成功值类型。</typeparam>
    /// <param name="bind">返回下一结果的转换器。</param>
    /// <returns>后续结果或原错误。</returns>
    public Result<TNext> Bind<TNext>(Func<T, Result<TNext>> bind) =>
        IsSuccess ? bind(Value) : Result<TNext>.Fail(Error!);
}

/// <summary>对规范化 JSON、字节或文本计算 SHA-256 摘要。</summary>
public static class CanonicalDigest
{
    /// <summary>对规范化 JSON 字段顺序计算 SHA-256 内容摘要。</summary>
    /// <include file="../Comments.xml" path="doc/T/*"/>
    /// <include file="../Comments.xml" path="doc/Value/*"/>
    /// <returns>规范化 JSON 内容的 64 位小写十六进制 SHA-256 摘要。</returns>
    public static string Of<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, element);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    /// <summary>对传入字节计算 SHA-256 摘要。</summary>
    /// <include file="../Comments.xml" path="doc/Value/*"/>
    /// <returns>字节内容的 64 位小写十六进制 SHA-256 摘要。</returns>
    public static string Bytes(ReadOnlySpan<byte> value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(value));
    }

    /// <summary>对 UTF-8 文本计算稳定摘要。</summary>
    /// <include file="../Comments.xml" path="doc/Value/*"/>
    /// <returns>UTF-8 编码文本的 64 位小写十六进制 SHA-256 摘要。</returns>
    public static string Text(string value)
    {
        return Bytes(Encoding.UTF8.GetBytes(value));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            default: element.WriteTo(writer); break;
        }
    }
}

/// <summary>协调器支持的有限操作类别。</summary>
public enum OperationKind
{
    /// <summary>启动应用。</summary>
    Launch,
    /// <summary>交付工具文件。</summary>
    Deploy,
    /// <summary>明确应用持久变量。</summary>
    ApplyVariables,
    /// <summary>迁移旧版工作空间。</summary>
    MigrateConfiguration
}

/// <summary>已持久记录的操作阶段，区分窗口观察、会话确认和未确定结果。</summary>
public enum RunStage
{
    /// <summary>已受理，未确认执行结果。</summary>
    Accepted,
    /// <summary>执行者已取得准入。</summary>
    Executing,
    /// <summary>正在执行前置或连通性检查。</summary>
    Checking,
    /// <summary>入口已创建，继续观察模块交接。</summary>
    HandedOff,
    /// <summary>已观察到窗口，未确认项目登录。</summary>
    WindowObserved,
    /// <summary>已取得独立的项目会话确认事实。</summary>
    SessionConfirmed,
    /// <summary>当前有限操作已有确定完成结果。</summary>
    Completed,
    /// <summary>当前有限操作确定失败。</summary>
    Failed,
    /// <summary>已持久记录停止请求。</summary>
    CancelRequested,
    /// <summary>操作在受支持边界内取消。</summary>
    Cancelled,
    /// <summary>观察结束，结果未独立确认。</summary>
    Unconfirmed,
    /// <summary>已观察到目标会话退出。</summary>
    Exited,
    /// <summary>结果不确定，需核验原操作。</summary>
    NeedsVerification
}

/// <summary>按端点身份、证据时效及连续探测轮次归约的连接状态。</summary>
public enum ResourceState
{
    /// <summary>尚无当前身份的肯定证据。</summary>
    Unknown,
    /// <summary>正在执行前置或连通性检查。</summary>
    Checking,
    /// <summary>本轮失败，仍在连续复查窗口内。</summary>
    Rechecking,
    /// <summary>当前端点取得仍有效的就绪连通证据。</summary>
    Reachable,
    /// <summary>连续失败达到当前策略阈值。</summary>
    Unreachable,
    /// <summary>资源已明确暂停监测。</summary>
    Disabled
}

/// <summary>探测或进程观察事实的来源类别。</summary>
public enum EvidenceKind
{
    /// <summary>虚拟机状态证据。</summary>
    VirtualMachine,
    /// <summary>本机服务状态证据。</summary>
    LocalService,
    /// <summary>端口连接证据。</summary>
    Tcp,
    /// <summary>已创建入口进程。</summary>
    ProcessCreated,
    /// <summary>入口进程退出，不等于后代会话退出。</summary>
    EntryExited,
    /// <summary>已观察到窗口，未确认项目登录。</summary>
    WindowObserved,
    /// <summary>已取得独立的项目会话确认事实。</summary>
    SessionConfirmed,
    /// <summary>已观察到目标会话退出。</summary>
    SessionExited,
    /// <summary>按原请求取得的核验事实。</summary>
    Verification
}

/// <summary>应用入口的参数构造方式。</summary>
public enum EntryKind
{
    /// <summary>mon.exe 的 PROD 与 INIT 入口。</summary>
    Monitor,
    /// <summary>Marine BAT 或 CMD 的受限参数入口。</summary>
    MarineBatch,
    /// <summary>通用 EXE 及明确参数模板。</summary>
    Executable
}

/// <summary>工具环境变量的作用范围。</summary>
public enum VariableScope
{
    /// <summary>仅注入此次启动的子进程。</summary>
    Process,
    /// <summary>持久写入当前 Windows 用户范围。</summary>
    User,
    /// <summary>持久写入系统范围，按需明确提升。</summary>
    Machine
}

/// <summary>工具来源的读取方式。</summary>
public enum PackageKind
{
    /// <summary>ZIP 压缩包。</summary>
    Zip,
    /// <summary>通过受信任辅助程序读取 7z 压缩包。</summary>
    SevenZip,
    /// <summary>完整普通文件夹。</summary>
    Folder,
    /// <summary>一个或多个普通文件及目标相对路径映射。</summary>
    Files
}

/// <summary>部署计划执行的动作。</summary>
public enum DeploymentAction
{
    /// <summary>按冻结内容交付工具文件。</summary>
    Install,
    /// <summary>基于原回执与当前差异恢复未完成写入。</summary>
    Recover,
    /// <summary>基于原回执与备份生成明确回滚。</summary>
    Rollback
}

/// <summary>原配置提交的可核验状态。</summary>
public enum CommitState
{
    /// <summary>没有找到原提交历史。</summary>
    NotFound,
    /// <summary>原提交已准备，最终切换未确认。</summary>
    Pending,
    /// <summary>原提交已确定写入。</summary>
    Committed,
    /// <summary>已确定原提交未生效。</summary>
    NotCommitted,
    /// <summary>原提交结果仍不确定。</summary>
    Indeterminate
}

/// <summary>保存登录配置时对本机凭据关联采用的更新方式。</summary>
public enum SecretUpdateKind
{
    /// <summary>保留旧凭据关联。</summary>
    Keep,
    /// <summary>解除旧凭据关联，保守保留仍可能被引用的秘密。</summary>
    Clear,
    /// <summary>为本次输入创建新凭据关联。</summary>
    Replace
}

/// <summary>刷新候选来源时保留选择或明确选择最新项的意图。</summary>
public enum RefreshIntent
{
    /// <summary>保留已有人工来源选择。</summary>
    PreserveSelection,
    /// <summary>刷新候选列表而不隐式改用最新来源。</summary>
    RefreshSources,
    /// <summary>明确把选择改为最新修改的候选。</summary>
    SelectLatest
}
