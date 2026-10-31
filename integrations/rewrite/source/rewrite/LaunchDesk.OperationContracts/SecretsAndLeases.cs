using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("LaunchDesk.Windows")]
[assembly: InternalsVisibleTo("LaunchDesk.Rewrite.Tests")]

namespace LaunchDesk.OperationContracts;

/// <summary>仅在本机凭据库中解析的稳定引用；分享投影不会含此引用。</summary>
/// <param name="Value">Windows 凭据库内定位秘密的本机引用键，不是密码。</param>
public sealed record CredentialRef(string Value)
{
    /// <summary>返回固定遮蔽标记，不含秘密字符或凭据引用。</summary>
    /// <returns>不含秘密的固定类型标记。</returns>
    public override string ToString()
    {
        return "[本机凭据引用]";
    }
}

/// <summary>拥有可清理字符缓冲区的临时秘密；禁止 JSON 序列化，释放后不可再次借用。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class TransientSecret : IDisposable
{
    private char[]? characters;

    /// <summary>复制输入字符到独占缓冲区，由本对象负责清理。</summary>
    /// <include file="../Comments.xml" path="doc/Value/*"/>
    public TransientSecret(ReadOnlySpan<char> value)
    {
        characters = value.ToArray();
    }

    /// <summary>借用仍存活秘密的只读字符视图；调用方不得延长秘密生命周期或持久记录内容。</summary>
    /// <returns>本对象仍持有的字符视图；缓冲区已释放则抛ObjectDisposedException。</returns>
    public ReadOnlySpan<char> Borrow()
    {
        return characters is null ? throw new ObjectDisposedException(nameof(TransientSecret)) : characters;
    }

    /// <summary>秘密缓冲区是否已释放。</summary>
    public bool IsDisposed => characters is null;

    /// <summary>清零并释放临时字符缓冲区；可重复调用。</summary>
    public void Dispose()
    {
        var data = Interlocked.Exchange(ref characters, null);
        if (data is not null) Array.Clear(data);
    }

    /// <summary>返回固定遮蔽标记，不含秘密字符或凭据引用。</summary>
    /// <returns>不含秘密的固定类型标记。</returns>
    public override string ToString()
    {
        return "[临时秘密]";
    }
}

/// <summary>限定已解析秘密的运行时所有权；释放租约会清理所拥有的临时字符。</summary>
/// <param name="secret"><include file="../Comments.xml" path="doc/Secret/param[@name='secret']/*"/></param>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class SecretLease(TransientSecret secret) : IDisposable
{
    /// <summary>借用仍存活秘密的只读字符视图；调用方不得延长秘密生命周期或持久记录内容。</summary>
    /// <returns>租约拥有的临时字符视图，不延长租约生命周期。</returns>
    public ReadOnlySpan<char> Borrow()
    {
        return secret.Borrow();
    }

    /// <summary>释放并清理本租约拥有的临时秘密。</summary>
    public void Dispose()
    {
        secret.Dispose();
    }

    /// <summary>返回固定遮蔽标记，不含秘密字符或凭据引用。</summary>
    /// <returns>不含秘密的固定类型标记。</returns>
    public override string ToString()
    {
        return "[秘密租约]";
    }
}

/// <summary>一次保存对旧凭据关联的保留、清除或替换意图；秘密只在运行时移交。</summary>
/// <param name="Kind">保存时保留、清除或替换秘密关联的意图类别。</param>
/// <param name="InputId">本次临时秘密更新输入的稳定标识</param>
/// <param name="Secret">仅在运行时移交和释放的临时秘密</param>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed record SecretUpdate(SecretUpdateKind Kind, Guid InputId, TransientSecret? Secret = null) : IDisposable
{
    /// <summary>创建保留当前本机凭据关联的保存意图。</summary>
    /// <returns>不携带临时秘密的 Keep 意图。</returns>
    public static SecretUpdate Keep()
    {
        return new SecretUpdate(SecretUpdateKind.Keep, Guid.Empty);
    }

    /// <summary>创建解除当前凭据关联的保存意图，不自动删除仍可能被引用的秘密。</summary>
    /// <returns>不携带临时秘密的 Clear 意图。</returns>
    public static SecretUpdate Clear()
    {
        return new SecretUpdate(SecretUpdateKind.Clear, Guid.Empty);
    }

    /// <summary>创建拥有独立输入身份的新秘密替换意图。</summary>
    /// <include file="../Comments.xml" path="doc/Secret/*"/>
    /// <returns>带新输入 GUID 并拥有传入临时秘密的 Replace 意图。</returns>
    public static SecretUpdate Replace(TransientSecret secret)
    {
        return new SecretUpdate(SecretUpdateKind.Replace, Guid.NewGuid(), secret);
    }

    /// <summary>清理Replace临时秘密；Keep/Clear无秘密可清理。</summary>
    public void Dispose()
    {
        Secret?.Dispose();
    }
}

/// <summary>拒绝序列化秘密、执行权和运行时租约，防止能力或秘密进入 JSON。</summary>
public sealed class RejectSecretSerialization : JsonConverterFactory
{
    /// <summary>声明该转换器工厂拦截其标注类型的序列化。</summary>
    /// <param name="typeToConvert">正在检查的运行时类型。</param>
    /// <returns>始终返回 true。</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return true;
    }

    /// <summary>为指定类型创建拒绝读写 JSON 的转换器。</summary>
    /// <param name="typeToConvert">需禁止 JSON 读写的运行时类型。</param>
    /// <param name="options">调用方提供的 JSON 序列化选项。</param>
    /// <returns>读取或写入时均抛出 NotSupportedException 的类型化转换器。</returns>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(typeof(Deny<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class Deny<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Runtime capabilities and secrets cannot be serialized.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Runtime capabilities and secrets cannot be serialized.");
        }
    }
}

/// <summary>配置准备结果含提交号、摘要、稳定ID映射及已有回执；仅首次成功准备授予原进程绑定的写入能力，重复结果只读。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class WorkspaceCommitSession : IAsyncDisposable
{
    /// <summary>当前提交会话冻结的意图。</summary>
    public CommitIntent Intent { get; }
    /// <summary>原操作或提交的持久回执。</summary>
    public CommitReceipt Receipt { get; }
    /// <summary>当前不可变状态快照。</summary>
    public WorkspaceSnapshotInternal Snapshot { get; }
    /// <summary>是否拥有首次提交的可写会话。</summary>
    public bool IsFirst { get; }
    internal object? Capability { get; }
    private Func<ValueTask>? release;

    internal WorkspaceCommitSession(CommitIntent intent, CommitReceipt receipt, WorkspaceSnapshotInternal snapshot,
        bool first, object? capability, Func<ValueTask>? release = null)
    {
        Intent = intent;
        Receipt = receipt;
        Snapshot = snapshot;
        IsFirst = first;
        Capability = capability;
        this.release = release;
    }

    /// <summary>释放首次提交会话所拥有的跨进程锁；释放回调最多调用一次。</summary>
    /// <returns>表示会话释放完成的异步结果。</returns>
    public async ValueTask DisposeAsync()
    {
        var action = Interlocked.Exchange(ref release, null);
        if (action is not null) await action();
    }
}

/// <summary>操作是首次受理还是复用已存在的受理结果。</summary>
public enum AcceptanceDisposition
{
    /// <summary>首次持久受理，允许尝试取得唯一执行权。</summary>
    Created,
    /// <summary>返回原已受理身份，不创建第二次副作用。</summary>
    Existing
}

/// <summary>不可变受理结果含操作号、请求摘要、快照和受理分类；仅Created携带进程绑定、一次性、不可序列化的准入凭据。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class OperationAcceptance
{
    /// <summary>当前不可变状态快照。</summary>
    public OperationRequestSnapshot Snapshot { get; }
    /// <summary>首次或已存在请求的准入结果。</summary>
    public AcceptanceDisposition Disposition { get; }
    internal object? Capability { get; }

    internal OperationAcceptance(OperationRequestSnapshot snapshot, AcceptanceDisposition disposition,
        object? capability)
    {
        Snapshot = snapshot;
        Disposition = disposition;
        Capability = capability;
    }
}

/// <summary>可靠持久的唯一执行权，绑定操作号、请求摘要、执行代次、机器、进程创建身份及随机实例标识。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class OperationExecutionLease : IAsyncDisposable
{
    /// <summary>稳定标识。</summary>
    public OperationId Id { get; }
    internal object Capability { get; }
    private Func<ValueTask>? release;

    internal OperationExecutionLease(OperationId id, object capability, Func<ValueTask> release)
    {
        Id = id;
        Capability = capability;
        this.release = release;
    }

    /// <summary>释放当前执行准入的所有权；释放回调最多调用一次。</summary>
    /// <returns>表示执行租约释放完成的异步结果。</returns>
    public async ValueTask DisposeAsync()
    {
        var action = Interlocked.Exchange(ref release, null);
        if (action is not null) await action();
    }
}

/// <summary>持有经核验的部署影响范围与控制根登记之间的保护准入。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class ControlDataLease : IAsyncDisposable
{
    /// <summary>当前保护准入覆盖的规范化文件系统范围。</summary>
    public IReadOnlyList<CanonicalPath> Scopes { get; }
    internal object Capability { get; }
    private Func<ValueTask>? release;

    internal ControlDataLease(IReadOnlyList<CanonicalPath> scopes, object capability, Func<ValueTask> release)
    {
        Scopes = scopes;
        Capability = capability;
        this.release = release;
    }

    /// <summary>释放当前控制资料保护范围的准入；释放回调最多调用一次。</summary>
    /// <returns>表示保护准入释放完成的异步结果。</returns>
    public async ValueTask DisposeAsync()
    {
        var action = Interlocked.Exchange(ref release, null);
        if (action is not null) await action();
    }
}

/// <summary>限定当前执行者持有的规范化目标互斥范围；释放后不可继续写入。</summary>
[JsonConverter(typeof(RejectSecretSerialization))]
public sealed class TargetLease : IAsyncDisposable
{
    /// <summary>秘密缓冲区是否已释放。</summary>
    public bool IsDisposed => release is null;
    /// <summary>本次租约持有的目标范围。</summary>
    public IReadOnlyList<CanonicalPath> Targets { get; }
    internal object Capability { get; }
    private Func<ValueTask>? release;

    internal TargetLease(IReadOnlyList<CanonicalPath> targets, object capability, Func<ValueTask> release)
    {
        Targets = targets;
        Capability = capability;
        this.release = release;
    }

    /// <summary>释放当前目标互斥租约；释放回调最多调用一次。</summary>
    /// <returns>表示目标租约释放完成的异步结果。</returns>
    public async ValueTask DisposeAsync()
    {
        var action = Interlocked.Exchange(ref release, null);
        if (action is not null) await action();
    }
}

/// <summary>限定单次请求处理器的所有权，异步释放处理器所拥有的资源。</summary>
/// <param name="handler">当前请求独占的处理器。</param>
/// <param name="release">租约结束时仅执行一次的释放回调。</param>
public sealed class HandlerLease(IOperationHandler handler, Func<ValueTask>? release = null) : IAsyncDisposable
{
    /// <summary>当前有限请求使用的处理器。</summary>
    public IOperationHandler Handler { get; } = handler;

    /// <summary>调用处理器租约提供的释放回调；没有回调时直接完成。</summary>
    /// <returns>表示处理器自有资源释放完成的异步结果。</returns>
    public ValueTask DisposeAsync()
    {
        return release?.Invoke() ?? ValueTask.CompletedTask;
    }
}

/// <summary>拥有一次已创建目标的跟踪句柄；释放观察资源不强制结束目标应用。</summary>
/// <param name="rootId">启动入口根进程的 PID。</param>
/// <param name="handle">当前调用拥有的本机句柄。</param>
/// <param name="release">由调用方控制调用次数的跟踪句柄释放回调。</param>
public sealed class TrackedProcess(int rootId, object handle, Func<ValueTask> release) : IAsyncDisposable
{
    /// <summary>启动入口根进程的 PID。</summary>
    public int RootId { get; } = rootId;
    /// <summary>受本对象生命周期管理的运行时跟踪句柄。</summary>
    public object Handle { get; } = handle;

    /// <summary>调用跟踪资源释放回调，不请求终止目标进程。</summary>
    /// <returns>表示跟踪资源释放完成的异步结果。</returns>
    public ValueTask DisposeAsync()
    {
        return release();
    }
}