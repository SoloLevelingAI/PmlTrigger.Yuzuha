using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Workspace;

/// <summary>配置独立快照、原始文件修订及真实来源。</summary>
public sealed record ConfigSnapshot<T>(T Value, string Revision, int SchemaVersion, string FilePath, bool Exists);
/// <summary>JSON Pointer 对应字段的修改前后值，值是 JSON 文本。</summary>
public sealed record ConfigChange(string Path, string? Before, string? After);
/// <summary>提交结果和何时影响后续操作。</summary>
public sealed record ConfigCommit<T>(ConfigSnapshot<T> Snapshot, ConfigChange[] Changes, string Effect);
/// <summary>可回溯的非秘密修改原因。</summary>
public sealed record ConfigurationChangeReason(string Reason, string? ProfileId = null, string? RestoredFrom = null);
/// <summary>已提交修订的历史元数据；快照与父修订由内容哈希校验。</summary>
public sealed record ConfigHistoryEntry(string Revision, string ParentRevision, DateTimeOffset SavedUtc,
    string Reason, string? ProfileId, string? RestoredFrom, string Source);
/// <summary>可替换配置存储，只管理快照与提交一致性，不执行业务操作。</summary>
public interface IConfigurationStore<T>
{
    /// <summary>读取与编辑对象独立的快照。</summary>
    ConfigSnapshot<T> Read();
    /// <summary>在写锁内核验预期修订并提交一个新版本。</summary>
    ConfigCommit<T> Commit(T candidate, string expectedRevision, ConfigurationChangeReason reason);
    /// <summary>读取已提交历史，最多返回指定数量。</summary>
    ConfigHistoryEntry[] History(int limit = 100);
    /// <summary>读取历史版本，不改变当前文件。</summary>
    ConfigSnapshot<T> ReadRevision(string revision);
}
/// <summary>修改基于过期文件；保留实际修订供客户端重新读取。</summary>
public sealed class ConfigurationConflictException(string actualRevision)
    : IOException("配置已被其他窗口、CLI 或外部编辑器修改，请重新读取并比较后再保存。")
{
    /// <summary>检测到的当前文件内容修订。</summary>
    public string ActualRevision { get; } = actualRevision;
}
/// <summary>安装实例身份，产品模块目录不会跨产品套用。</summary>
public sealed record InstalledProductIdentity(string Executable, string Product, string Version = "", string Kind = "Monitor");
/// <summary>原生命令值及界面标签；Verified 只表示已找到安装元数据。</summary>
public sealed record ProductModuleOption(string Value, string Label, bool Verified);
/// <summary>所选安装的模块选项与来源，不表示登录或 Host 已经验证。</summary>
public sealed record ProductModuleSet(string Product, string Version, string Source, ProductModuleOption[] Options, string Warning);
/// <summary>产品与版本模块目录的可替换边界。</summary>
public interface IProductModuleCatalog
{
    /// <summary>读取指定安装实例的模块列表。</summary>
    ProductModuleSet Get(InstalledProductIdentity installation);
}
/// <summary>可定位到字段的业务校验问题。</summary>
public sealed record LaunchValidationIssue(string Field, string Code, string Message);
/// <summary>配置校验与本次使用的模块目录。</summary>
public sealed record LaunchValidation(bool IsValid, LaunchValidationIssue[] Issues, ProductModuleSet Modules);
/// <summary>配置读取结果，携带保存所需的修订。</summary>
public sealed record LaunchProfileRead(LaunchProfile Profile, string Revision, string FilePath, string Effect);
/// <summary>编辑或恢复预览，不写文件或启动进程。</summary>
public sealed record LaunchProfilePreview(LaunchProfile Profile, string ExpectedRevision,
    ConfigChange[] Changes, LaunchValidation Validation, string Effect);
/// <summary>业务校验失败，调用方可展示字段问题而不解析错误文本。</summary>
public sealed class LaunchProfileValidationException(LaunchValidation validation)
    : ArgumentException(string.Join("；", validation.Issues.Select(i => i.Message)))
{
    /// <summary>完整字段校验结果。</summary>
    public LaunchValidation Validation { get; } = validation;
}
/// <summary>启动生命周期和配置历史的 Native AOT 序列化元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConfigSnapshot<WorkspaceSettings>))]
[JsonSerializable(typeof(ConfigCommit<WorkspaceSettings>))]
[JsonSerializable(typeof(ConfigHistoryEntry))]
[JsonSerializable(typeof(ConfigHistoryEntry[]))]
[JsonSerializable(typeof(ConfigChange[]))]
[JsonSerializable(typeof(LaunchProfile))]
[JsonSerializable(typeof(LaunchProfileRead))]
[JsonSerializable(typeof(LaunchProfileRead[]))]
[JsonSerializable(typeof(LaunchProfilePreview))]
[JsonSerializable(typeof(LaunchValidation))]
[JsonSerializable(typeof(ProductModuleSet))]
[JsonSerializable(typeof(InstalledProductIdentity))]
public partial class LaunchLifecycleJsonContext : JsonSerializerContext;
