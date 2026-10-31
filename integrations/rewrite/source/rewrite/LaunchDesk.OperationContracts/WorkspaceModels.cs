using System.Collections.Immutable;

namespace LaunchDesk.OperationContracts;

/// <summary>应用入口登记，含可编辑路径、版本、初始化文件和发现规则。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="Version">版本名称或策略版本</param>
/// <param name="Kind"><include file="../Comments.xml" path="doc/Kind/*"/></param>
/// <param name="ExecutablePath">可执行程序或批处理入口的本机路径</param>
/// <param name="InitializationPath">mon.exe 使用的初始化文件路径</param>
/// <param name="WorkingDirectory">目标应用的工作目录</param>
/// <param name="ProductCode">Monitor 入口的产品代码</param>
/// <param name="ManualBinding">是否保留人工指定路径</param>
/// <param name="DiscoveryRoots">自动发现入口使用的根目录集合</param>
/// <param name="ArgumentTemplate">通用入口的非秘密参数模板</param>
/// <param name="DiscoveredPaths">自动发现取得的候选路径</param>
/// <param name="RequiresLocalBinding">是否需在本机核验并保存路径</param>
public sealed record ApplicationDefinition(
    Guid Id,
    string Name,
    string Version,
    EntryKind Kind,
    string ExecutablePath,
    string InitializationPath,
    string WorkingDirectory,
    string ProductCode,
    bool ManualBinding = true,
    ImmutableArray<string> DiscoveryRoots = default,
    ImmutableArray<string> ArgumentTemplate = default,
    ImmutableArray<string> DiscoveredPaths = default,
    bool RequiresLocalBinding = false)
{
    /// <summary>自动发现入口使用的根目录集合。</summary>
    public ImmutableArray<string> DiscoveryRoots { get; init; } = DiscoveryRoots.IsDefault ? [] : DiscoveryRoots;
    /// <summary>通用入口的非秘密参数模板。</summary>
    public ImmutableArray<string> ArgumentTemplate { get; init; } = ArgumentTemplate.IsDefault ? [] : ArgumentTemplate;
    /// <summary>自动发现取得的候选路径。</summary>
    public ImmutableArray<string> DiscoveredPaths { get; init; } = DiscoveredPaths.IsDefault ? [] : DiscoveredPaths;
}

/// <summary>许可连接的名称与独立资源描述。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
public sealed record LicenseDefinition(Guid Id, string Name, ResourceDescriptor Resource);

/// <summary>普通文件包中源文件与目标相对路径的映射。</summary>
/// <param name="SourcePath">明确选择的源文件路径</param>
/// <param name="RelativePath">相对目标根的路径</param>
public sealed record ToolFile(string SourcePath, string RelativePath);

/// <summary>关联工具注入的变量定义；持久作用域需单独预览和确认。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="Value">注入环境变量的非秘密文本值。</param>
/// <param name="Scope">变量作用域</param>
public sealed record ToolVariable(Guid Id, string Name, string Value, VariableScope Scope);

/// <summary>独立工具登记，含来源、部署目录、变量及备份选项。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="Group">工具分组或作者说明</param>
/// <param name="TargetDirectory">本次工具交付的目标目录</param>
/// <param name="Source">工具来源或来源快照</param>
/// <param name="SearchDirectory">刷新工具来源时扫描的目录</param>
/// <param name="Variables">工具变量定义或确认后的变量计划</param>
/// <param name="FlattenRoot">是否展开压缩包中唯一的顶层目录</param>
/// <param name="KeepBackup">是否保留交付前文件备份</param>
/// <param name="LegacyRecordsDirectory">旧版回执所在目录，只读兼容使用</param>
public sealed record ToolDefinition(
    Guid Id,
    string Name,
    string Group,
    string TargetDirectory,
    PackageSource Source,
    string SearchDirectory,
    ImmutableArray<ToolVariable> Variables,
    bool FlattenRoot = true,
    bool KeepBackup = true,
    string? LegacyRecordsDirectory = null);

/// <summary>可复用的非秘密参数模板定义。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="ArgumentTokens">模板含的非秘密参数词元</param>
public sealed record TemplateDefinition(Guid Id, string Name, ImmutableArray<string> ArgumentTokens);

/// <summary>可编辑登录字段及应用、许可和工具的稳定关联，不含明文密码。</summary>
/// <param name="ProfileId">登录配置的稳定标识</param>
/// <param name="Name">显示名或配置名</param>
/// <param name="ApplicationId">关联应用的稳定标识</param>
/// <param name="VersionGroup">登录配置的版本分组</param>
/// <param name="Project">目标应用项目名称</param>
/// <param name="User">目标应用用户名</param>
/// <param name="Mdb">目标应用 MDB 名称</param>
/// <param name="Module">待进入的模块名称</param>
/// <param name="LicenseId">关联许可的稳定标识；空值表示不注入许可地址</param>
/// <param name="ToolIds">明确关联或选择的工具标识集合</param>
public sealed record ProfileDraft(
    Guid? ProfileId,
    string Name,
    Guid ApplicationId,
    string VersionGroup,
    string Project,
    string User,
    string Mdb,
    string Module,
    Guid? LicenseId,
    ImmutableArray<Guid> ToolIds);

/// <summary>登录配置的公开不可变投影，含本机绑定与凭据状态文字。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Revision">当前配置修订号</param>
/// <param name="DisplayFields">供页面显示的非秘密字段</param>
/// <param name="BindingStatus">应用在本机的绑定状态说明</param>
/// <param name="CredentialStatus">可展示的本机凭据状态说明</param>
public sealed record ProfileSnapshot(
    Guid Id,
    Revision Revision,
    ProfileDraft DisplayFields,
    string BindingStatus,
    string CredentialStatus)
{
    /// <summary>显示名或配置名。</summary>
    public string Name => DisplayFields.Name;
}

/// <summary>工作空间内部登录记录，含凭据引用但不含明文秘密。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Fields">非秘密登录字段</param>
/// <param name="Credential">本机凭据引用；不是明文秘密</param>
public sealed record ProfileConfiguration(Guid Id, ProfileDraft Fields, CredentialRef? Credential);

/// <summary>供页面和分享逻辑使用的不可变公开配置快照。</summary>
/// <param name="WorkspaceId">工作空间的稳定身份</param>
/// <param name="Revision">当前配置修订号</param>
/// <param name="Applications">已登记应用集</param>
/// <param name="Profiles">登录配置集</param>
/// <param name="LicenseEndpoints">工作空间登记的许可连接集</param>
/// <param name="Tools">独立工具或关联工具集</param>
/// <param name="Templates">已登记的参数模板集</param>
public sealed record WorkspaceSnapshot(
    Guid WorkspaceId,
    Revision Revision,
    ImmutableArray<ApplicationDefinition> Applications,
    ImmutableArray<ProfileSnapshot> Profiles,
    ImmutableArray<LicenseDefinition> LicenseEndpoints,
    ImmutableArray<ToolDefinition> Tools,
    ImmutableArray<TemplateDefinition> Templates);

/// <summary>供受信任的配置存储及用例使用的内部快照。</summary>
/// <param name="WorkspaceId">工作空间的稳定身份</param>
/// <param name="Revision">当前配置修订号</param>
/// <param name="Applications">已登记应用集</param>
/// <param name="Profiles">登录配置集</param>
/// <param name="LicenseEndpoints">工作空间登记的许可连接集</param>
/// <param name="Tools">独立工具或关联工具集</param>
/// <param name="Templates">已登记的参数模板集</param>
public sealed record WorkspaceSnapshotInternal(
    Guid WorkspaceId,
    Revision Revision,
    ImmutableArray<ApplicationDefinition> Applications,
    ImmutableArray<ProfileConfiguration> Profiles,
    ImmutableArray<LicenseDefinition> LicenseEndpoints,
    ImmutableArray<ToolDefinition> Tools,
    ImmutableArray<TemplateDefinition> Templates)
{
    /// <summary>创建拥有稳定身份且不含管理条目的空工作空间。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <returns>具有指定或新工作空间身份、修订号为 0、所有登记集合为空的内部快照。</returns>
    public static WorkspaceSnapshotInternal Empty(Guid? id = null)
    {
        return new WorkspaceSnapshotInternal(id ?? Guid.NewGuid(), new Revision(0), [], [], [], [], []);
    }

    /// <summary>生成不含明文秘密与凭据引用的公开配置快照。</summary>
    /// <returns>移除本机凭据引用并保留可显示凭据状态的公开配置投影。</returns>
    public WorkspaceSnapshot PublicProjection()
    {
        return new WorkspaceSnapshot(WorkspaceId, Revision, Applications,
            Profiles.Select(p => new ProfileSnapshot(p.Id, Revision, p.Fields,
                Applications.Any(a =>
                    a.Id == p.Fields.ApplicationId && !a.RequiresLocalBinding &&
                    !string.IsNullOrWhiteSpace(a.ExecutablePath))
                    ? "本机入口已绑定"
                    : "需要核验本机入口",
                p.Credential is null ? "未配置密码" : "已保存本机凭据")).ToImmutableArray(), LicenseEndpoints, Tools, Templates);
    }
}

/// <summary>配置目录支持登记的条目类别。</summary>
public enum DefinitionKind
{
    /// <summary>应用入口登记。</summary>
    Application,
    /// <summary>独立许可连接登记。</summary>
    License,
    /// <summary>独立工具登记。</summary>
    Tool,
    /// <summary>非秘密参数模板登记。</summary>
    Template
}

/// <summary>保存或移除一种受支持配置定义的有限请求。</summary>
/// <param name="Kind">应用、许可、工具或模板定义类别。</param>
/// <param name="Application">应用入口定义</param>
/// <param name="License">关联许可连接</param>
/// <param name="Tool">独立工具定义</param>
/// <param name="Template">可复用的非秘密参数模板</param>
/// <param name="Delete">本条变更是否移除文件</param>
public sealed record DefinitionDraft(
    DefinitionKind Kind,
    ApplicationDefinition? Application = null,
    LicenseDefinition? License = null,
    ToolDefinition? Tool = null,
    TemplateDefinition? Template = null,
    bool Delete = false);

/// <summary>冻结一次直接配置提交的身份、修订、草稿及秘密更新意图。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Kind">定义、入口、来源或操作的有限类别</param>
/// <param name="ExpectedRevision">写入前须匹配的配置修订号</param>
/// <param name="RequestDigest">规范化操作请求的稳定摘要</param>
/// <param name="ProposedEntityId">直接提交准备采用的新条目标识</param>
public sealed record CommitIntent(
    CommitId Id,
    string Kind,
    Revision ExpectedRevision,
    string RequestDigest,
    Guid ProposedEntityId);

/// <summary>原提交的脱敏回执，用于重试去重和不确定结果核验。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="RequestDigest">规范化操作请求的稳定摘要</param>
/// <param name="State">当前持久回执、提交或资源状态。</param>
/// <param name="CommittedRevision">原提交确定写入后的修订号</param>
/// <param name="GeneratedEntityId">首次提交持久分配的新条目标识</param>
/// <param name="Profile">登录配置或内部记录</param>
/// <param name="Workspace">只供本机用例保存的内部快照，可能含本机凭据引用。</param>
/// <param name="ReasonCode">稳定且不含秘密的原因码</param>
public sealed record CommitReceipt(
    CommitId Id,
    string RequestDigest,
    CommitState State,
    Revision CommittedRevision,
    Guid GeneratedEntityId,
    ProfileSnapshot? Profile,
    WorkspaceSnapshot? Workspace,
    string ReasonCode);

/// <summary>提交会话中准备写入的新工作空间快照及结果关联。</summary>
/// <param name="Snapshot"><include file="../Comments.xml" path="doc/Snapshot/*"/></param>
/// <param name="Profile">登录配置或内部记录</param>
public sealed record WorkspaceChange(WorkspaceSnapshotInternal Snapshot, ProfileSnapshot? Profile = null);

/// <summary>同一工作空间的数据目录、配置文件、控制根与机器身份。</summary>
/// <param name="Directory">待解析或发现的目录路径</param>
/// <param name="ConfigurationFile">工作空间配置文件的绝对路径</param>
/// <param name="ControlRoot">日志、暂存、回执和执行权的保护根</param>
/// <param name="MachineId">拥有本机绑定或执行权的机器标识</param>
public sealed record WorkspaceLocation(string Directory, string ConfigurationFile, string ControlRoot, Guid MachineId);

/// <summary>从命令行取得的配置路径、数据目录和便携模式选项。</summary>
/// <param name="ConfigPath">命令行指定的配置文件路径</param>
/// <param name="DataDirectory">数据目录的绝对路径</param>
/// <param name="Portable">是否启用程序旁的便携工作空间</param>
public sealed record StartupOptions(string? ConfigPath = null, string? DataDirectory = null, bool Portable = false);

/// <summary>明确选择导出的登录及工具标识，不含秘密或运行时执行权。</summary>
/// <param name="ProfileIds">明确选择的登录配置标识集合</param>
/// <param name="IncludeTools">是否含所选登录关联的工具定义</param>
/// <param name="ToolIds">明确关联或选择的工具标识集合</param>
public sealed record ShareSelection(ImmutableArray<Guid> ProfileIds, bool IncludeTools = true,
    ImmutableArray<Guid> ToolIds = default)
{
    /// <summary>明确关联或选择的工具标识集合。</summary>
    public ImmutableArray<Guid> ToolIds { get; init; } = ToolIds.IsDefault ? [] : ToolIds;
}

/// <summary>分享文件中的非秘密登录字段。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Fields">非秘密登录字段</param>
public sealed record SharedProfile(Guid Id, ProfileDraft Fields);

/// <summary>可跨电脑分享的配置结构；接收方须复核本机绑定。</summary>
/// <param name="SchemaVersion">分享或协议的结构版本</param>
/// <param name="Applications">已登记应用集</param>
/// <param name="LicenseEndpoints">工作空间登记的许可连接集</param>
/// <param name="Profiles">登录配置集</param>
/// <param name="Tools">独立工具或关联工具集</param>
/// <param name="Templates">已登记的参数模板集</param>
public sealed record ShareBundle(
    int SchemaVersion,
    ImmutableArray<ApplicationDefinition> Applications,
    ImmutableArray<LicenseDefinition> LicenseEndpoints,
    ImmutableArray<SharedProfile> Profiles,
    ImmutableArray<ToolDefinition> Tools,
    ImmutableArray<TemplateDefinition> Templates);

/// <summary>导入预览、稳定来源摘要及本机绑定确认状态。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="BundleDigest">导入来源结构的稳定摘要</param>
/// <param name="Bundle">经过结构验证且不含秘密的分享内容。</param>
/// <param name="Notes">预览中需用户核对的说明</param>
/// <param name="LocalBindingsConfirmed">用户是否明确同意在本机核验绑定</param>
public sealed record ImportProposal(
    Guid Id,
    string BundleDigest,
    ShareBundle Bundle,
    ImmutableArray<string> Notes,
    bool LocalBindingsConfirmed = false);

/// <summary>捕获的父进程环境变量集合，供纯规则构造子进程环境。</summary>
/// <param name="Values">按名称索引的父进程环境变量集合</param>
public sealed record EnvironmentSnapshot(ImmutableDictionary<string, string> Values);

/// <summary>受理启动时冻结的应用、登录、许可与关联工具配置。</summary>
/// <param name="WorkspaceRevision">受理/预览冻结的配置修订</param>
/// <param name="Profile">登录配置或内部记录</param>
/// <param name="Application">应用入口定义</param>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
/// <param name="Tools">独立工具或关联工具集</param>
public sealed record LaunchConfiguration(
    Revision WorkspaceRevision,
    ProfileConfiguration Profile,
    ApplicationDefinition Application,
    ResourceDescriptor? Resource,
    ImmutableArray<ToolDefinition> Tools);

/// <summary>启动参数或运行时秘密插槽；秘密值不写入计划。</summary>
/// <param name="Value">非秘密参数文本；秘密插槽仅记录占位信息。</param>
/// <param name="SecretSlot">该参数是否是等待运行时注入的秘密插槽</param>
/// <param name="SecretPrefix">秘密参数前的不含秘密的固定前缀</param>
public sealed record LaunchArgument(string Value, bool SecretSlot = false, string SecretPrefix = "");

/// <summary>冻结的进程入口、参数和独立环境计划。</summary>
/// <param name="PlanId">计划的稳定标识</param>
/// <param name="WorkspaceRevision">受理/预览冻结的配置修订</param>
/// <param name="Executable">应用入口的本机路径</param>
/// <param name="WorkingDirectory">目标应用的工作目录</param>
/// <param name="Kind"><include file="../Comments.xml" path="doc/Kind/*"/></param>
/// <param name="Arguments">非秘密参数与运行时秘密插槽的有序集合。</param>
/// <param name="Environment">子进程环境或可替换的变量存储端口</param>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
/// <param name="Credential">本机凭据引用；不是明文秘密</param>
/// <param name="PlanDigest">冻结计划的规范化摘要</param>
public sealed record LaunchPlan(
    Guid PlanId,
    Revision WorkspaceRevision,
    string Executable,
    string WorkingDirectory,
    EntryKind Kind,
    ImmutableArray<LaunchArgument> Arguments,
    ImmutableDictionary<string, string> Environment,
    ResourceDescriptor? Resource,
    CredentialRef? Credential,
    string PlanDigest);
