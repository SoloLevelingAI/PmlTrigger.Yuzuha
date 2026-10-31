namespace PmlTwin.Database.Contracts;

/// <summary>应用数据隔离范围；各字段按原样、区分大小写匹配，不代表工程访问授权。</summary>
/// <param name="Product">产品身份。</param><param name="Version">产品版本。</param>
/// <param name="Workspace">工作区身份。</param><param name="Kind">数据用途，例如 configuration 或 document。</param>
public sealed record DataScope(string Product, string Version, string Workspace, string Kind);

/// <summary>不可变修订快照；内容只允许非秘密应用数据。</summary>
/// <param name="Identity">范围内对象身份。</param><param name="Revision">单调递增修订，从 1 开始。</param>
/// <param name="Content">完整 UTF-8 文本内容。</param><param name="Sha256">内容的 SHA-256 十六进制摘要。</param>
/// <param name="SavedUtc">提交时间。</param><param name="Reason">提交原因。</param>
public sealed record RevisionSnapshot(string Identity, long Revision, string Content, string Sha256, DateTimeOffset SavedUtc, string Reason);

/// <summary>比较并交换写入；ExpectedRevision 为 0 表示对象必须不存在。</summary>
/// <param name="Identity">范围内对象身份。</param><param name="ExpectedRevision">预期当前修订。</param>
/// <param name="Content">完整新内容。</param><param name="Reason">提交原因。</param>
public sealed record RevisionWrite(string Identity, long ExpectedRevision, string Content, string Reason);

/// <summary>事务提交结果；发生冲突时全批次不写入，并返回发生冲突对象的当前快照。</summary>
/// <param name="Committed">是否已提交整个批次。</param><param name="Snapshots">成功时的新快照；冲突时为空。</param>
/// <param name="ConflictIdentity">冲突对象身份。</param><param name="Current">冲突时当前快照；对象不存在时为空。</param>
public sealed record RevisionCommit(bool Committed, IReadOnlyList<RevisionSnapshot> Snapshots, string? ConflictIdentity = null, RevisionSnapshot? Current = null);

/// <summary>固定范围的应用修订会话；不公开供应商连接，不执行部署或 AVEVA 动作。</summary>
public interface IRevisionSession
{
    /// <summary>取得固定隔离范围。</summary>
    DataScope Scope { get; }
    /// <summary>取得数据库格式版本。</summary>
    int SchemaVersion { get; }
    /// <summary>指示当前会话是否禁止写入。</summary>
    bool ReadOnly { get; }
    /// <summary>读取当前修订；对象不存在时返回空。</summary>
    RevisionSnapshot? Read(string identity);
    /// <summary>读取指定历史修订；不存在时返回空。</summary>
    RevisionSnapshot? ReadRevision(string identity, long revision);
    /// <summary>原子提交至多 100 个不同对象；任一比较失败时全部回滚。</summary>
    RevisionCommit Commit(IReadOnlyList<RevisionWrite> writes);
}

/// <summary>数据库格式不受当前程序集支持；不尝试创建、升级或降级写入。</summary>
public sealed class DatabaseVersionException : InvalidOperationException
{
    /// <summary>构造版本不兼容错误。</summary>
    public DatabaseVersionException(int actual, int supported) : base($"数据库格式 {actual} 不兼容，当前支持 {supported}。") { }
}
