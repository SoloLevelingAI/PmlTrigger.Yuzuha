using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>可核对的脚本身份、解释与随版本交付的DocFX类型入口；静态链接不表示已经执行。</summary>
public sealed record ManagerContractScript(string Path, string TypeName, string Member, string Explanation, string DocFxPath, string DocFxUid);

/// <summary>安装到工程操作的一个任务阶段，保留七字段、真实操作ID及独立证据边界。</summary>
public sealed record ManagerContractStage(string Id, string Title, string Input, string Precondition, string Method,
    string Effect, string Validation, string Receipt, string Recovery, string[] OperationIds,
    ManagerContractScript[] Scripts, string EvidenceBoundary);

/// <summary>多个入口共享的完整任务契约；读取此对象不扫描机器、不读用户数据、不执行操作。</summary>
public sealed record ManagerJourneyContract(string Schema, string Id, string Title, string Summary, ManagerContractStage[] Stages);

/// <summary>真实注册目录与完整任务契约的只读快照；操作结果Data仍是未知JSON，不承诺未登记输出schema。</summary>
public sealed record ManagerContractExport(ManagerJourneyContract Journey, OperationDescriptor[] Operations, string OutputBoundary);

/// <summary>安装、环境准备、部署与工程使用的共享契约目录；不成为第二份canonical要求。</summary>
public static class ManagerContractCatalog
{
    /// <summary>构造独立的七字段阶段快照，不接触安装或运行环境。</summary>
    public static ManagerJourneyContract Describe() => new("pmltwin.manager-journey/1", "install-to-operation",
        "从安装到工程操作", "安装应用后，由管理器明确选择环境、接手旧所有权、冻结部署与EVARS、核对回执，再启动与严格核验。阅读位置不代表执行进度。",
        [
            Stage("install", "安装交接", "应用工具包根与安装交接标记。", "Inno已交付应用、清单与管理器入口；没有据此接入AVEVA。",
                "读取交接身份、包清单及旧所有权状态。", "只读；不修改厂商EVARS或启动工程。", "校验标记格式、应用根、载荷身份和路径边界。",
                "工具包/安装所有权快照；文件就绪不表示Host已加载。", "缺失或不支持格式显式说明，返回安装入口核对，不猜接入成功。",
                ["setup.handoff.inspect"], [Script("SetupHandoffService", "Inspect", "解释安装交接和载荷身份；不接管运行环境。")]),
            Stage("environment", "选择产品环境", "明确扫描根、预算及产品/版本候选。", "有可访问的产品目录；多候选需用户选择。",
                "汇聚目录/注册表/初始化候选并解释来源；复用模块和启动规则。", "只读发现；不执行厂商BAT、不登录或默认授权。",
                "预算、候选来源、产品/模块组合和人工覆盖有效性。", "发现及解释结果；安装可见与登录成功分开。", "缺失、超时、歧义明确展示，保留人工输入。",
                ["installation.discover", "installation.projects", "environment.discover", "environment.resolve"],
                [Script("ManagerInstallationDiscovery", "Scan", "有预算地定位真实安装。"), Script("LaunchDiscoveryService", "Discover", "候选与来源解释，不把静态发现当登录。")]),
            Stage("ownership", "核对来源与旧所有权", "所选工具包、EVARS目标及旧安装记录；可选旧项目来源。", "目标明确，旧所有权格式和实际摘要可核对。",
                "只读核对旧安装所有者和迁移来源；显式授权后移交旧卸载记录。", "移交只修改受支持的旧所有权记录；migration.preview不激活或切换Database。",
                "格式、所有者、当前摘要、冻结引用和写入授权；拒绝外部漂移。", "迁移预览或移交回执、原记录备份；不表示数据库已切换。",
                "不支持格式或漂移保持原样；依据备份和回执核对，不删除标记伪造接管。",
                ["setup.legacy.preview", "setup.legacy.transfer", "migration.preview"],
                [Script("SetupHandoffService", "PreviewLegacy", "区分旧安装器所有权和管理器部署所有权。"), Script("MigrationPreview", "Preview", "解释旧来源的只读映射与敏感字段剔除。")]),
            Stage("preview", "预览部署与EVARS", "工具包、所选安装/INIT或BAT、profile/framework、独立运行根和copy/reference模式。", "来源身份可核对；旧Inno环境所有权已明确移交；copy目标不能覆盖Inno应用根。",
                "RuntimeDeploymentService与EvarsEditor生成同源冻结预览，保存非秘密计划缓存。", "不写业务目标；展示受管行、前后摘要、文件差异和恢复方式。",
                "来源/目标范围、包清单、安装身份、产品与Host映射、旧环境所有者、编码、摘要及bootstrap适配。", "planId/sha256和明确变化摘要；预览不是部署完成。",
                "输入改变清除可执行预览并保留草稿；拒绝无效或过期计划。",
                ["deployment.preview", "evars.preview"], [Script("RuntimeDeploymentService", "Preview", "冻结载荷、运行根与环境文件的变化。"), Script("EvarsEditor", "Preview", "保留原编码和非受管行，解释将写入的受管段。"),
                    Script("ManagerHostProfiles", "Framework", "用同一精确映射拒绝产品与Host配置错配。"), Script("SetupEnvironmentOwnershipGuard", "EnsureAvailable", "依据明确来源与已登记安装的当前记录阻止绕过旧环境所有权；未登记且未提供的旧根不声称已全机排除。")]),
            Stage("apply", "明确应用", "已审阅planId/sha256及本次明确写入授权。", "冻结计划、源文件、目标和安装身份仍一致。",
                "唯一Manager后端复验计划并写入，分别记录逐文件变化、备份及EVARS结果。", "仅实施当前冻结范围；不启动AVEVA、不推断运行时加载成功。",
                "写入授权先于处理；再次核验摘要、所有权和实际文件。", "部署或EVARS回执、备份/恢复引用；部分恢复或unknown仍可定位。",
                "错误显示在当前动作附近；未知结果不自动重放，按原回执核对。",
                ["deployment.apply", "evars.apply"], [Script("RuntimeDeploymentService", "Apply", "执行当前计划并保留跨文件失败诊断。"), Script("EvarsEditor", "Apply", "仅写入冻结的环境修改并保留原件。")]),
            Stage("recover", "核对回执与恢复", "原操作回执及恢复引用。", "只恢复本次拥有的变化；目标等于本次写入结果时恢复，已等于原始指纹时只确认。",
                "比较当前摘要与受管记录，按条件恢复文件和环境段。", "不覆盖外部后续修改；应用卸载与运行部署恢复分别处理。",
                "当前目标、备份完整性、摘要和恢复授权；冲突逐项保留。", "恢复结果、未恢复项及原因；不能把部分恢复称为全部成功。",
                "保留备份与冲突证据，人工解决冲突后按同回执续行；丢回包先核对，不无条件回滚或清理。",
                ["deployment.restore", "evars.restore"], [Script("RuntimeDeploymentService", "Restore", "逐项核对受管文件，保留冲突并允许按原回执继续未完成恢复。"), Script("EvarsEditor", "Restore", "只恢复已应用内容；原始指纹已恢复时确认，不重写文件。")]),
            Stage("launch", "配置与启动", "产品入口、配置修订、明确工程/模块及本次内存凭据。", "环境准备范围已核对；密码经stdin/本机文件传入，不能放argv。",
                "冻结launch.plan；先持久受理，再经唯一启动边界执行，按原编号观察。", "创建入口进程；进程创建与登录、Host身份分别判断。",
                "修订/EVARS/入口摘要、计划有效期、显式启动授权和唯一尝试。", "operationId/attemptId与入口/交接观察；密码不入计划、日志或文档。",
                "unknown不重放；取消等待不声称撤销已启动工程，使用launch.observe。",
                ["launch.plan", "launch.execute", "launch.observe"], [Script("LaunchWorkflowService", "Plan", "冻结当前配置与实际入口文件。"), Script("LaunchWorkflowService", "ExecuteAsync", "先受理、唯一启动与未知结果边界。"), Script("LaunchWorkflowService", "Observe", "按原编号读取持久事实和进程身份。")]),
            Stage("verify", "严格会话核验", "显式选定PID、十进制字符串启动ticks、模块、管道及关联配置。", "发现候选后明确选择操作目标；可用灯态不自动选目标。",
                "经独立通信CLI核对目标身份和协议；不执行PML业务命令。", "只读核验；不授予执行权限，也不证明所有业务通过。",
                "PID、完整64位启动身份、模块、管道、协议和回执新鲜度。", "严格连接证据或明确失败；与历史验收日期、产品范围分开。",
                "失联/身份变化清旧结果，重新发现和明确核验；不能沿用旧绿灯。",
                ["connection.sessions", "connection.select", "connection.status", "connection.test"], [Script("CommunicationCliClient", "InvokeAsync", "保持独立进程边界，不拼造进程内RPC调用轨迹。")])
        ]);

    /// <summary>以实际注册目录导出契约并拒绝悬空操作；不调用任何处理器。</summary>
    public static ManagerContractExport Export(OperationRegistry registry)
    {
        var journey = Describe();
        foreach (var id in journey.Stages.SelectMany(stage => stage.OperationIds))
            if (!registry.TryGet(id, out _)) throw new InvalidOperationException("Journey operation is not registered: " + id);
        return new(journey, registry.Descriptors.ToArray(), "OperationResult.data remains unknown JSON; no dedicated output schema is promised. CLI process start ticks are decimal strings; do not round through JavaScript numbers. Static script links are not execution evidence.");
    }

    /// <summary>注册无写入、无启动、无执行权限要求的目录读取；处理器只返回当前注册描述。</summary>
    public static OperationRegistration Registration(Func<OperationRegistry> registry)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");
        return new(new("manager.contracts", 1, "Read the complete installation-to-operation journey and actual registered operation schemas; no discovery or mutation.", schema.RootElement.Clone()),
            new DelegateOperationHandler((id, _, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(Export(registry()), ManagerContractJsonContext.Default.ManagerContractExport)));
            }));
    }

    private static ManagerContractStage Stage(string id, string title, string input, string precondition, string method,
        string effect, string validation, string receipt, string recovery, string[] operations, ManagerContractScript[] scripts) =>
        new(id, title, input, precondition, method, effect, validation, receipt, recovery, operations, scripts,
            "本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。");

    private static ManagerContractScript Script(string type, string member, string explanation) =>
        new("src/YuzuhaToolkit.Manager.Core/" + (type switch { "ManagerInstallationDiscovery" => "InstallationDiscovery", "LaunchDiscoveryService" => "LaunchDiscovery", "LaunchWorkflowService" => "LaunchWorkflow", _ => type }) + ".cs", "YuzuhaToolkit.Manager." + type, member, explanation,
            "api/manager/YuzuhaToolkit.Manager." + type + ".html", "YuzuhaToolkit.Manager." + type);
}

/// <summary>运行契约目录的Native AOT JSON元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ManagerContractExport))]
[JsonSerializable(typeof(ManagerJourneyContract))]
public partial class ManagerContractJsonContext : JsonSerializerContext;
