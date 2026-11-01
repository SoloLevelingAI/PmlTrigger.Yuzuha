# PMLTWIN 项目契约：总体、Feature 与内部 Script

本文按“总体边界 → Feature 分工 → 内部执行契约 → 总体交付判断”介绍项目，面向接手、开发与验收。核对日期：2026-10-31。这里的 Script 包括 PowerShell/PML 脚本及承担明确算法或编排责任的 C# 方法，不意味着所有实现都是脚本语言。

## 一、总体契约：项目承诺什么

PMLTWIN / Phayton / Yuzuha 将本地资料检索、配置维护、工程启动、会话查询和受控开发操作放在可追溯的工作台中。用户应能先看到目标、输入来源、预期变化和失败出口，再明确决定是否写配置、复制文件、启动程序或执行 PML。三个业务目标来自 [canonical contract](../specification/contract.json)：G1 开发更便捷、G2 辅助三维使用、G3 可持续接手。

### 1.1 三类事实不能混用

| 层次 | 权威来源 | 本文如何使用 |
|---|---|---|
| 既有生效要求 | [contract.json](../specification/contract.json)，R/B/T/S 原 ID | 保留原状态与验收含义；当前为24条要求，15 verified、5 partial、4 planned |
| 平台候选设计与任务 | [proposal](../../openspec/changes/integrate-pmltwin-product-platform/proposal.md)、[contracts](../../openspec/changes/integrate-pmltwin-product-platform/contracts.md)、[design](../../openspec/changes/integrate-pmltwin-product-platform/design.md)、[tasks](../../openspec/changes/integrate-pmltwin-product-platform/tasks.md) | 描述要融合的责任与尚未闭合的要求；候选逻辑名称不能冒充已注册 operation |
| 实际行为与本次证据 | 当前源码、测试、各 Feature 交接 | 单独说明“已实现”“离线验收”“实机未验收”，不凭任务大类或历史测试总数升级状态 |

`verified` 也有适用环境与时点，不代表全部产品、版本、界面或安装通过。[既有契约演示](../presentation/contracts/README.md)是只读快照；本说明补充当前续作，不覆盖旧快照。业务 Feature 名来自 [capabilities.json](capabilities.json)，Manager 的 `feature.list` 则按实际操作族装配，两者粒度不同。

### 1.2 不变量与依赖方向

1. **同一业务决定只有一个所有者。** GUI、管理 CLI、HTML 是入口；配置校验、冻结、执行与观察归现有用例。不得在页面重新实现启动、部署或历史写者。
2. **发现、启动、登录、目标核验、运行时加载分别证明。** 找到安装不表示已登录；进程创建不表示 Host 可用；文件写入不表示 Ribbon 或 PML 已加载。
3. **授权按效果分开。** 写配置、启动与执行各自授权；输入 JSON 不能自授权；Feature 已注册、已启用、已兼容也不等于获准执行。
4. **目标身份有作用域。** 通信调用绑定 PID、启动时间、模块及协议。DBREF 是当前工程/会话中的身份，不能跨工程永久复用。
5. **计划绑定事实。** 已采用冻结的入口绑定修订、摘要、目标及期限；变化应拒绝。旧兼容接口不能被宣传成具有新版本全部保证。
6. **unknown 必须保留。** 可能已经执行却没有可靠回执时，按原编号观察或人工核对，不自动换编号重试。取消等待、关闭页面不表示撤销。
7. **秘密与用户资料受保护。** 密码不进入计划、命令行、证据或作品集；迁移导出剔除可携带授权的凭据引用。用户数据不属于安装缓存。
8. **不承诺跨资源全局原子。** SQLite 事务不能覆盖外部文件、进程和宿主动作；用阶段、备份、回读与有条件恢复说明结果。

当前结构是 WPF/管理 CLI/HTML → Manager 用例 → 配置、发现、部署、知识及独立通信适配；Ribbon 保持独立 net48 HTTP 服务。Database.Contracts 与独立 net10 Database DLL 已有最小修订存储，活动业务尚未整体迁入。CAF net48 薄入口不加载现代 Database/Manager DLL；NET35/NET48 Host 经匹配通信边界访问。DevHost、Aspire、VM 不是基础运行前提。实际装配见 [ManagerOperationCatalog](../../src/YuzuhaToolkit.Manager.Core/ManagerOperationCatalog.cs)、[FeatureRegistry](../../src/YuzuhaToolkit.Manager.Core/FeatureRegistry.cs)。

## 二、Feature 契约：谁负责什么

以下按业务任务组织。每个 Feature 都列责任、输入输出、状态、副作用、失败出口和验收入口；表内“对应 R”是可追溯映射，不新增 canonical ID。

业务目录的13个既有Feature完整映射如下；平台新能力在后续小节独立补充，不挤占这些ID。

| capabilities ID | 原目录状态 | 本文责任位置 |
|---|---|---|
| launch | implemented | 2.1 环境/配置/启动；不表示全部平台启动语义已融合 |
| deployment | partial | 2.2 文件/运行目录部署 |
| sessions | implemented | 2.3 显式目标与身份核验 |
| objects | implemented | 2.3 对象/DBREF/全局读取 |
| functions | implemented | 2.4 登记/许可/调用 |
| forms | implemented | 2.3 原生Form/Gadget |
| arrays | implemented | 2.3与3.6 有界原生数组 |
| formuse | planned | 2.9 跟踪原型限制 |
| knowledge | partial | 2.5 分层检索/导入 |
| activity | implemented | 2.9 本机运行记录 |
| review | planned | 2.9 AI草稿评审与迁移GUI缺口 |
| tests | planned | 2.9 桌面测试中心GUI缺口 |
| web | partial | 2.9 本机HTML应用管理，远程分发未完成 |

### 2.1 环境发现、配置与启动

**责任与输入输出。** `environment.discover/resolve` 汇聚安装与模块候选；`launch.profile.*` 管理草稿、差异、修订与历史；`launch.plan/execute/observe` 绑定一次启动。输入包括明确选择的产品/入口、工程及模块、配置修订和操作编号；输出包含候选来源/歧义、冻结文件摘要和关联回执。入口目录与模块规则来自既有服务，人工覆盖不能被后台刷新静默改掉。

**状态与效果。** 配置保存不启动；计划可写非秘密本机缓存，但不创建产品进程。执行先持久受理，再调用唯一启动边界；success 仅说明入口进程创建。`accepted/unknown`、入口交接观察与 `HostVerification=unverified` 分开。WPF 续作已接同一协调后端，unknown 时保留原编号并禁止当前窗口新启动；观察按钮不会再启动。

**MDB实际缺口。** 当前页面只有MDB文本输入和保存，没有从所选已登录工程搜集可用MDB列表，也没有带来源/有效期的候选选择。用户本次明确指定`/ALL`不能算自动发现通过；后续应由匹配会话/产品的查询返回候选、保留人工覆盖、在无权限/未登录/多候选时解释，不能猜默认MDB。该项是[环境辅助流程规范](../../openspec/changes/integrate-pmltwin-product-platform/specs/environment-assisted-workflows/spec.md)中的planned契约，本轮没有补造实现或操作ID。

**失败与验收。** 修订/EVARS/入口变化、过期、授权不足在边界前拒绝；受理后不确定结果按原编号恢复。Monitor、Marine 与 Direct 不能相互猜测转换。对应 R02–R06、R13–R16、R23；完整跨产品纵向验收仍未完成。源码：[LaunchDiscovery](../../src/YuzuhaToolkit.Manager.Core/LaunchDiscovery.cs)、[LaunchWorkflow](../../src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs)、[WPF 适配](../../src/YuzuhaToolkit.Desktop/MainWindow.LaunchWorkflow.xaml.cs)。测试：[LaunchWorkflowTests](../../tests/YuzuhaToolkit.Manager.Tests/LaunchWorkflowTests.cs)、[DesktopLaunchWorkflowTests](../../tests/YuzuhaToolkit.Tests/DesktopLaunchWorkflowTests.cs)。本轮界面及限制见 [WPF 交接](../../openspec/changes/integrate-pmltwin-product-platform/features/wpf-launch-continuation.md)。

### 2.2 文件部署、运行目录与 EVARS

**责任与输入输出。** 复制服务接源目录/文件/ZIP、注册根、目标及冻结计划；运行目录部署另绑定产品安装身份、载荷清单和生效环境文件。输出为逐文件变化、旧/新摘要、备份与恢复状态。EVARS 专用编辑与 Windows 用户/机器环境变量是不同业务，不能互相替代。

**状态与效果。** preview 不改业务目标；apply 明确写目标文件及托管环境段，恢复仅处理本操作拥有且未被外部改变的文件。复制成功不表示 DLL/PML 已加载。7z 支持需要明确的外部格式依赖，不能静默假装已解压。发布/安装全链和 GUI 差异接线仍有缺口。

**失败与验收。** 源/目标摘要变化、链接、越界、占用和恢复冲突必须可辨；恢复不覆盖外部新改动。对应 R10、R19、R23。源码：[PluginDeployment](../../src/YuzuhaToolkit.Workspace/PluginDeployment.cs)、[RuntimeDeploymentService](../../src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentService.cs)、[EvarsEditor](../../src/YuzuhaToolkit.Manager.Core/EvarsEditor.cs)。测试：[CopyOperationTests](../../tests/YuzuhaToolkit.Manager.Tests/CopyOperationTests.cs)、[RuntimeDeploymentTests](../../tests/YuzuhaToolkit.Manager.Tests/RuntimeDeploymentTests.cs)、[EvarsEditorTests](../../tests/YuzuhaToolkit.Manager.Tests/EvarsEditorTests.cs)。

### 2.3 会话、对象、Form/Gadget 与数组查询

**责任与输入输出。** 会话发现只提供候选，调用必须显式选定目标。对象 Feature 将 CE、指定元素/DBREF、全局对象和成员路径转成已有内置读取；Form/Gadget 使用既有原生元数据；数组按原生索引窗口读取，保留 unset 与续页位置。输出保留原始结果、协议/目标证据、截断和预算。

**状态与效果。** 这些能力已有实现，但 UI 全任务、产品矩阵和每种原生对象仍不能泛称全验收。查询可能创建请求临时变量，DBREF 包装函数会临时切换 CE，因此“读取”不等于完全没有宿主状态操作；不得触发业务回调或推断执行授权。

**失败与验收。** PID 重用、模块变化、旧协议在分派前拒绝；页条件改变必须清旧续页。空数组不能单独证明查询成功：原 PML 包装函数也以空数组表达部分错误。对应 R08、R09、R17、R18。源码：[CommunicationCliClient](../../src/YuzuhaToolkit.Manager.Core/CommunicationCliClient.cs)、[DesktopObjectQuery](../../src/YuzuhaToolkit.Desktop/DesktopObjectQuery.cs)、[PmlFormQuery](../../src/Shared/PmlFormQuery.cs)、[PmlArrayWindow](../../src/Shared/PmlArrayWindow.cs)、[YuzuhaReadDbref](../../PMLLIB/Traversal/YuzuhaReadDbref.pmlfnc)。验收入口：[Form/Gadget 指南](form-gadget-query.md)、[PmlFormQueryTests](../../tests/YuzuhaToolkit.Tests/PmlFormQueryTests.cs)、[通信夹具](../../tests/YuzuhaToolkit.Manager.Tests/CommunicationProcessTests.cs)。

### 2.4 函数发现、注册、许可与调用

**责任与输入输出。** 注册源码路径、函数身份、标量签名及来源摘要；列表/许可/调用分别处理。调用通过独立通信 CLI，保持原 ID 和目标校验。原生 CLI 能力枚举与 PML 自动发现是两件事。

**状态与效果。** 管理 CLI 缺口已补，但自动发现范围、GUI 和真实 Host 业务执行仍分别验收。注册和授予本机许可不表示目标 Host 已加载对应源码；真正调用可能产生宿主副作用。

**失败与验收。** 源变化、许可撤销、Host 未加载、协议不足及 unknown 均须明确；不绕过签名限制回退任意命令。对应 R08、R11、R16、R17。源码：[PmlFunctionRegistry](../../src/YuzuhaToolkit.Mcp.Core/PmlFunctionRegistry.cs)；入口与9项契约/通信烟测边界见 [函数交接](../../openspec/changes/integrate-pmltwin-product-platform/features/functions-composition-handoff.md)。

### 2.5 知识、用户数据与经验

**责任与输入输出。** 按 product/version/role 分层维护资料源、已有 SQLite、正文与个人笔记。输入为明确选定来源或数据库及导入目标；输出为导入预览、来源映射、新鲜度、库身份、chunk ID 与完整性报告。搜索不创建或自动重建知识库。

**状态与效果。** 搜索/正文/管理及双路导入已有实现；canonical R07/R20 的“官方默认接入”仍为 planned，不能因本机已有两套已校验库便改写。导入发布新库/登记，个人/工程/经验数据保持分层，不因安装更新被覆盖。

**失败与验收。** 同名冲突、格式不兼容、损坏、FTS 失配、活动写入及来源变化拒绝启用；新鲜度证据与历史 manifest 分开。对应 R07、R20；GUI 与 CLI 同库报告逐项等价仍需完成。源码：[KnowledgeImportService](../../src/YuzuhaToolkit.Manager.Core/KnowledgeImportService.cs)、[知识测试](../../tests/YuzuhaToolkit.Manager.Tests/KnowledgeImportTests.cs)，说明见 [manager-knowledge](manager-knowledge.md)。

### 2.6 Database 与只读迁移预览

**责任与输入输出。** `IRevisionSession` 接固定 DataScope、身份、ExpectedRevision 与文本，返回历史/CAS 提交或冲突。`migration.preview` 另接 format=rewrite/next/ribbon 及本机绝对来源，输出来源格式、原字节 SHA256、净化 JSON、诊断和 `activate=false`。

**状态与效果。** 独立 DLL 与修订存储已实现；UI/CLI 仍保留原权威写者，尚未整体注入 Database。只读预览已支持真实来源最小结构，保留未知非秘密字段/历史并剔除秘密及凭据引用；不写来源、不创建迁移目标、不激活。旧 `launchdesk.import.*` 的净化快照路径仍是独立兼容能力，不能当作新库切换。

**失败与验收。** 未来格式、损坏、重复键、哈希不符、预算/链接拒绝；不完整返回 `MIGRATION_PREVIEW_INCOMPLETE`，保留审查材料。Direct原值保留并提示，不转Monitor。只读导入适配/迁移预览所需三格式、字段/历史保留、秘密剔除、Direct诊断及不激活已由14项专测验证；这对应平台4.4，不应因4.3事实/资产索引或4.5写入切换/恢复尚未实施而漏计。完整数据写入迁移、WAL一致备份及切换恢复仍未完成。源码：[RevisionContracts](../../src/PmlTwin.Database.Contracts/RevisionContracts.cs)、[SqliteRevisionStore](../../src/PmlTwin.Database/SqliteRevisionStore.cs)、[MigrationPreview](../../src/YuzuhaToolkit.Manager.Core/MigrationPreview.cs)。证据：[Database 交接](../../openspec/changes/integrate-pmltwin-product-platform/features/database-handoff.md)、[迁移交接](../../openspec/changes/integrate-pmltwin-product-platform/features/migration-preview-continuation.md)。

### 2.7 Ribbon 编辑、历史、UIC 部署与运行时生效

**责任与输入输出。** 编辑 Feature 维护命令定义/布局引用、稳定 ID、未知 XML、图标与历史；恢复历史只恢复编辑工作区。部署 Feature 分开处理 `file-update`（只写 UIC）与 `registration-add`（只写选定加载 XML）。v2 preview 接已有 documentId 的 ProjectV1 及必要模块选择；apply 只接服务端 planId/planDigest，不能重新传项目或路径。

**状态与效果。** 原独立Ribbon UI/后端已纳入本仓；Manager有部分v1路由及四个v2冻结部署入口。**本轮Web及WPF部署入口的已有文件更新/登记已接v2，Ribbon离线全量53/53通过、零跳过，其中客户端专项9项通过。** 新文件首次生成保留明确标记的v1兼容路径，不宣传为v2冻结。完整v1路由映射、统一Database adapter和更广产品矩阵仍未完成。v2的规范化项目、历史版本、导出摘要、目标字节及登记名由服务端冻结；preview不新增编辑历史或写目标，仅缓存受限计划。

| 阶段 | 当前能证明 | 仍须另行证明 |
|---|---|---|
| saved | 编辑工作区有修订/历史 | 安装中的文件是否改变 |
| deployed / unchanged | 写后回读匹配计划，或目标无需写入 | 正在运行的产品是否读取了该 UIC/XML |
| reloadRequired=true、runtimeVerified=false | 明确还需要加载核验 | 具体产品/模块的图标、尺寸、Tooltip 与获准动作 |
| 后端重启 | 服务重新启动；内存未执行计划失效 | AVEVA 已加载；之前不确定写入未发生 |
| AVEVA 重启 | 新进程出现 | 正确项目/模块加载了正确文件，且实际 UI 与动作符合预期 |

接口不自动重启AVEVA，不自动执行宏。是否需要重新打开模块、刷新或重启，必须由目标产品版本的实际加载机制确定；不能用“重启即可生效”代替验收。真实加载应先确认具体预览范围与回退，再记录文件身份、目标进程/模块、可见UI及动作范围；离线夹具不提供这些证据。

**本轮独立运行时事实。** 本机E3D2.1的APS、`/ALL`、DESIGN已经严格核验登录/会话；正常退出旧工程并重新启动后，新Design会话再次核验，实际页签、分组、Large/Small图标与两项Tooltip通过。UIC内两张PNG IHDR分别为32×32和16×16。未点击Macro，不能声明按钮业务执行通过；这项实机证据不等于新UI入口最终回归通过，也不外推AM、PDMS、E3D3或CAF。部署时的`runtimeVerified=false`回执保持原样，后续加载证据独立追加。[正常重启验收报告](../validation/ribbon-restart-2026-10-31.md)记录新旧会话、截图、哈希、备份及正常退出/恢复唯一Design测试登记/停止本次后端的收尾；恢复后加载未再次验收。

本次真实文件部署和登记由主代理**直接调用HTTP v2**完成，没有通过最终编辑UI点击提交。UI接线/交互烟测与该实机结果分别取证，不能组合叙述成“UI全程实机验收”。平台14.4记录这一明确实机切片，整体验收7.3仍未勾选。

**失败与验收。** 过期/篡改/跨种类计划、历史或目标变化先于 Save/Bind/目标写入拒绝；原生保护取服务端权威来源及绑定，preferredBinding 不能授予托管权。执行边界后不确定返回 unknown；同计划在存活缓存内只返回原回执，服务重启/过期后不能据缓存不存在判定未执行。保留 v1 403/409/423 兼容，不把 v1 旧目标摘要视作完整 v2 冻结。

源码：[WorkspaceFiles](../../integrations/ribbon/source/apps/backend/Features/Workspace/WorkspaceFiles.cs)、[FrozenDeploymentFeature](../../integrations/ribbon/source/apps/backend/Features/Deployment/FrozenDeploymentFeature.cs)、[Manager 适配](../../src/YuzuhaToolkit.Manager.Core/RibbonHttpAdapter.cs)、[现有 UI 部署入口](../../integrations/ribbon/source/apps/web/src/deployment.ts)。验证：[冻结部署测试](../../integrations/ribbon/source/tests/frozen-deployment.test.ts)、[HTTP 适配测试](../../tests/YuzuhaToolkit.Manager.Tests/RibbonHttpAdapterTests.cs)、[v2交接及原生保护修复](../../openspec/changes/integrate-pmltwin-product-platform/features/ribbon-v2-continuation.md)。

UI薄客户端负责发送前持久pending、确认后只发原引用、禁重复点击及回执身份核查；本地门禁不替代服务端受理协调者。具体实现及当前边界见[UI接线契约](../../openspec/changes/integrate-pmltwin-product-platform/features/ribbon-ui-continuation.md)和下文3.13，测试数字以最终交接为准。

本轮已核对的证据分层为：原生WPF烟测只证明实际部署工具/登记的v2 Preview控件、路径与返回流程，Apply由桌面C#薄客户端HTTP夹具验证，并覆盖发送前持久/跨进程恢复与v1不确定性；Chromium DOM与隔离安装已完整执行Web文件/登记apply，证明仅发计划引用、页面关闭后pending保留、刷新/新tab门禁和跨tab竞争拒绝，不是AVEVA实机。日志分别为[离线全量](../validation/ribbon-ui-2026-10-31.evidence/full-tests-final.log)、[客户端专项](../validation/ribbon-ui-2026-10-31.evidence/client-tests-final.log)、[原生界面烟测](../validation/ribbon-ui-2026-10-31.evidence/desktop-smoke-final.log)及[Web DOM报告](../validation/ribbon-ui-2026-10-31.evidence/web-results.json)；后者passed=true、crossTabConcurrentWriteRejected=true。不能把HTTP夹具说成原生WPF确认按钮全部真实写入已验收，也不能把Web DOM模拟安装与直接HTTP实机合并成UI实机全流程。

### 2.8 CAF、可选许可/VM 与宿主装配

**责任与输入输出。** Feature 描述记录唯一 owner、版本、操作和支持 profile；基础管理器显式装配固定工厂/worker。CAF adapter 只接真实宿主服务上下文并验证 CAF 程序集身份，返回只读描述；许可诊断、VM 任务通过独立可选包处理。

**状态与效果。** CAF 三个已确认 SDK profile 有编译和隔离测试，Start/Stop 仅改变内存生命周期；没有真实注册、UI 挂载或自动启动管理器。许可探测不等于 checkout；VM 查看计划不启动机器，明确执行才可产生 VM 效果。基础包移除扩展后仍应可启动，完整发行/GUI 验收仍待完成。

**失败与验收。** 未支持 SDK/运行时、缺包、停用、崩溃和未知结果不能静默回退；CAF 注册不授予 Worker 提权或 PML 执行许可。源码：[PmlTwinAddin](../../src/PmlTwin.Caf.Adapter/PmlTwinAddin.cs)、[PluginOperations](../../src/YuzuhaToolkit.Manager.Core/PluginOperations.cs)、[PluginRegistry](../../src/YuzuhaToolkit.Manager.Core/PluginRegistry.cs)，边界见 [CAF交接](../../openspec/changes/integrate-pmltwin-product-platform/features/caf-adapter-handoff.md)、[PluginBoundaryTests](../../tests/YuzuhaToolkit.Manager.Tests/PluginBoundaryTests.cs)。

### 2.9 记录、HTML 应用、设计与待实现工具

**记录 Feature** 对应 R09/R21：输入本机关联编号及筛选条件，输出保留范围内的状态/操作/耗时与证据；记录失败和业务失败分别表达，未结束操作归 unknown。[RuntimeJournal](../../src/YuzuhaToolkit.Workspace/RuntimeJournal.cs) 不是所有平台动作统一协调者。

**HTML 应用 Feature** 将 Form 分类、按钮与参数转换为既有 IPC 调用；打开独立管理器不自动执行按钮。目录编辑有写入效果，按钮效果取决于明确绑定的原操作，远程分发尚未完成。源码与指南见 [HTML应用管理](html-app-manager.md)。

**设计/作品集 Feature** 对应 R01/R12/R24 及平台6.x：输入独立原稿、共享 token、操作 schema 和状态，交付可编辑文件、按钮映射、运行截图及来源。Dashboard 五工作区与启动续作有局部证据；D2C 输出布局不能证明业务接线、CAF或 UIC 已实现。实际范围见 [设计规范](../../openspec/changes/integrate-pmltwin-product-platform/specs/feature-design-evidence/spec.md)。

**FormUse、AI草稿评审迁移、桌面测试中心** 在业务目录中仍分别为 planned 或原型，不能因存在跟踪 PML、测试执行器或独立历史服务就描述成完整 GUI 产品。入口与限制见 [planned-tools](features/planned-tools.md)。

### 2.10 配置分享、环境变量与资源健康：候选平台Feature

**责任与输入输出。** 配置分享接所选配置/来源与目标机器绑定信息，输出剔除秘密后的分享材料及重绑定诊断；环境变量接明确用户/机器作用域和预期旧值，输出差异/所需权限；资源健康接目标与监测预算，输出观测时点、有效期与异常证据。这些是候选责任，操作名必须由实际目录确认，不能把contracts.md中的逻辑名称直接当CLI ID。

**状态与效果。** rewrite相关来源和测试已纳入单仓，但平台5.1/5.3尚未闭环。分享不迁移秘密/凭据授权；导入不自动激活；变量变更与EVARS分开，并说明只影响后续进程还是目标运行态；健康监测是只读，不自动修复、提权或签出许可。

**失败与验收。** 跨机缺路径/绑定、过期偏好、作用域权限不足、监测超预算或证据过期须明确；保留人工覆盖，不能静默选第一候选。后续验收需要跨机重绑定、秘密剔除、提权边界和过期状态夹具。来源与分工见 [来源清单](../../openspec/changes/integrate-pmltwin-product-platform/source-inventory.md)、[候选契约](../../openspec/changes/integrate-pmltwin-product-platform/contracts.md)和平台5.1/5.3任务；本说明不声称已完成产品接线。

## 三、内部 Script 细约：七个字段逐项说明

七字段统一为：**输入、前置、算法/调用、作用、校验、回执、恢复**。字段用于审查真实责任，不将某个 C# 类的保证泛化到 PowerShell/PML 包装层。[原演示脚本契约](../presentation/contracts/README.md)保留历史快照，本节以当前源码为准。

### 3.1 启动工作台与本地服务编排

来源：[Start-LaunchWorkbench.ps1](../../scripts/Start-LaunchWorkbench.ps1)、[Start-IntegratedWorkbench.ps1](../../scripts/Start-IntegratedWorkbench.ps1)。

- **输入：** NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。
- **前置：** 匹配仓库 SDK；所需 CLI/Ribbon 后端存在或允许本次构建；本机端口可用。
- **算法/调用：** 增量构建 → 启动本机 Ribbon（整合入口）→ 健康核对 → 调用 Manager workbench；不复制发现/启动算法。
- **作用：** 可构建、启动本机服务/浏览器、创建应用工作目录；此入口本身不登录或启动 AVEVA。
- **校验：** 构建退出码、文件存在、health 实现/版本/状态与自有进程仍存活。
- **回执：** 前台退出码、本机端点及下游操作回执；服务ready不是产品已连接。
- **恢复：** 失败不继续启动下游；finally 停自有服务并还原本进程环境。停止服务不撤销已经明确受理的产品动作。

### 3.2 启动计划、受理与观察

来源：[LaunchWorkflowService.Plan/ExecuteAsync/Observe](../../src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs)。

- **输入：** profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。
- **前置：** 配置有效、文件/修订/计划期限匹配；有权限；原编号没有冲突事实。
- **算法/调用：** Plan 冻结入口/INIT/EVARS → 缓存摘要 → 跨进程操作租约 → 同编号去重 → accepted持久化 → 唯一LaunchService.Start → 持久回执。
- **作用：** 计划写非秘密缓存；执行创建入口进程；观察读取/更新观察事实，不再次启动。
- **校验：** 同编号同摘要复用，异摘要冲突；受理持久失败不调用启动；退出入口保留交接候选，不能推定工程关闭。
- **回执：** operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。
- **恢复：** 中断accepted变unknown；写后回执失败保留不确定性；按原编号Observe，不能自动重放。

### 3.3 通信 CLI 子进程边界

来源：[CommunicationCliClient](../../src/YuzuhaToolkit.Manager.Core/CommunicationCliClient.cs)。

- **输入：** 已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。
- **前置：** 明确通信程序路径/身份、兼容协议；管理操作不能借此绕回通信边界。
- **算法/调用：** ArgumentList → 单份stdin JSON → 并行读取stdout/stderr → 时间/体积预算 → 核对关联回执。
- **作用：** 创建本次自有通信进程；下游操作可查询或改变宿主，必须按operation判断。
- **校验：** 程序指纹、版本、JSON、requestId、退出码与输出预算；PID身份校验不因GUI已选过而省略。
- **回执：** 原通信结果、分层evidence及mayHaveExecuted；stderr不是可替代业务结果的stdout。
- **恢复：** 终止/回收自有子进程不等于取消宿主动作；发出后无确定回执为unknown，不重发。

### 3.4 冻结复制与条件恢复

来源：[PluginDeployment.Preview/Apply/Restore](../../src/YuzuhaToolkit.Workspace/PluginDeployment.cs)。

- **输入：** 源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。
- **前置：** 路径边界、无链接、不互含、格式及大小预算满足；授权明确。
- **算法/调用：** 枚举冻结 → 全部写前复验 → 备份/暂存/写入 → 结果回读；不另建第二复制引擎。
- **作用：** 写目标和本操作备份；不自动删除无关文件、不加载Host。
- **校验：** 源/目标变化拒绝；检查实际复制与恢复未解决项。
- **回执：** DeploymentReceipt、应用异常中的部分结果、DeploymentRestoreReceipt。
- **恢复：** 仅恢复本次所有权内文件；外部改动不覆盖，RecoveryErrors/Unresolved单列，不承诺无条件回滚。

### 3.5 PML 部署、REHASH、重载与验证编排

来源：[Invoke-PmlDevelopment.ps1](../../scripts/Invoke-PmlDevelopment.ps1)、[PmlDevelopment.psm1](../../scripts/PmlDevelopment.psm1)。

- **输入：** CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。
- **前置：** 明确获准的部署/加载/验证任务；清单字段和期望字符串有效；不复用任意旧证据目录充当新结果。
- **算法/调用：** 固定发现身份 → 冻结复制 → REHASH ALL → Object/Form重载 → 验证宏 → 读取结果全局数组 → 精确比对expected。
- **作用：** 写部署文件与run.json/rpc.json；重载、宏和窗口可能改变宿主运行态。
- **校验：** 复制后对冻结源哈希回读；结果项数、UnparsedCount与每个键/字符串精确一致。该包装器不具有启动协调器的operationId持久去重；清单不是每步重新哈希冻结，不能补造此保证。
- **回执：** 每步执行前保存状态、原始调用与响应；失败或unknown停止，最后写rpc证据。
- **恢复：** 不自动重发/重跑全流程；先检查已复制文件、原始调用、加载状态及原备份，再明确选择下一动作。

### 3.6 数组窗口算法

来源：[PmlArrayWindow.Validate/Read](../../src/Shared/PmlArrayWindow.cs)。

- **输入：** 原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。
- **前置：** startIndex≥1，maxItems为1–1000，maxCharacters为1–262144，上界非负。
- **算法/调用：** 按原生索引顺序读取，不按已设置元素数压缩；字符预算不拆行。
- **作用：** 算法本身不创建或改写数组；委托仍受宿主协议约束。
- **校验：** 首行超预算明确失败；其余超预算保留下一索引；空、0、false和unset不相互转换。
- **回执：** Rows、StartIndex、NextIndex、Truncated、Characters及IndexUpperBound。
- **恢复：** 同目标/条件才能接续；换目标/筛选重置页，不拼旧结果。

### 3.7 DBREF对象读取与原生CE恢复

来源：[YuzuhaReadDbref.pmlfnc](../../PMLLIB/Traversal/YuzuhaReadDbref.pmlfnc)、[YuzuhaObjectWalker.pmlobj](../../PMLLIB/Traversal/YuzuhaObjectWalker.pmlobj)。

- **输入：** size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。
- **前置：** 目标PML已加载、当前会话/工程明确；参数与预算还需调用层约束。
- **算法/调用：** 保存CE → 解析目标 → 临时切CE → walker读取扁平数组 → 尝试还原CE。
- **作用：** 会临时改变CE；不应表述成纯无状态函数，也不自动获得写工程权限。
- **校验：** 原PML通过badref/handle处理部分失败；独立通信身份与预算不能省略。
- **回执：** 返回ARRAY；若目标无效或部分错误也可能是空数组，因此必须结合外层成功/错误证据。
- **恢复：** 原实现包含正常及读取异常后的CE恢复尝试，但不是通用finally保证；若原生恢复失败须人工核对CE，不能从空结果推断已恢复。

### 3.8 启动Host解析与REHASH策略

来源：[YuzuhaResolveRuntimePath](../../PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc)、[YuzuhaStartupPolicy](../../PMLLIB/Bootstrap/YuzuhaStartupPolicy.pmlobj)。

- **输入：** Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。
- **前置：** 对应profile的Host文件实际存在；安装目录token与安装预览一致。
- **算法/调用：** E3D2.1→net48/2.1、E3D3.1系列→net48/3.1.6、AM/PDMS→net35；策略仅none/空或rehash-all，未知值不当作PML执行。
- **作用：** 更新会话描述与解析结果；显式rehash-all可执行REHASH，默认关闭。
- **校验：** 不支持映射或缺DLL不得设为成功；策略执行前写开始日志，一旦开始即消耗会话防重试守卫。
- **回执：** 产品/版本/模块/profile/framework信息及策略报告；路径存在不是Host握手成功。
- **恢复：** 不自动降级加载错误运行时；改策略环境值通常由新进程读取，失败后不能借重试重复执行当前会话动作。

### 3.9 Ribbon v2冻结部署与Manager回执映射

来源：[FrozenDeploymentFeature.Preview/Apply](../../integrations/ribbon/source/apps/backend/Features/Deployment/FrozenDeploymentFeature.cs)、[RibbonV2Schemas](../../src/YuzuhaToolkit.Manager.Core/RibbonV2Schemas.cs)、[RibbonHttpAdapter](../../src/YuzuhaToolkit.Manager.Core/RibbonHttpAdapter.cs)。

- **输入：** preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。
- **前置：** 已有文档身份、权威来源/托管保护通过、计划期限/历史/目标一致；不能用伪造preferredBinding接管原生文件。
- **算法/调用：** 规范化与验证 → 缓存项目/目标/摘要 → apply写锁复验 → 消费计划 → 复用原文件/登记引擎 → 写后哈希回读。
- **作用：** file-update仅写UIC；registration-add仅写选定XML及相关工作区记录/绑定。未选XML及登记时UIC保持字节；不执行宏或重启。
- **校验：** 缓存最多64项、五分钟、单规范化输入4MiB、总缓存32MiB；HTTP及Manager另有20MiB输入/输出限制。Manager核对v2、请求编号/摘要、state及activation类型，旧v1错误保留。
- **回执：** workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。
- **恢复：** 存活缓存同计划返原回执；过期/后端重启没有持久执行事实，人工核对工作区/目标/备份，不自动重新执行；真实加载另验收。

### 3.10 只读迁移材料解析

来源：[MigrationPreview.Preview](../../src/YuzuhaToolkit.Manager.Core/MigrationPreview.cs)、[注册入口](../../src/YuzuhaToolkit.Manager.Core/MigrationPreviewOperations.cs)。

- **输入：** rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。
- **前置：** 明确支持结构；rewrite v3仅允许根内`.control/generations/<GUID-N>.json`及匹配SHA256。
- **算法/调用：** 受限枚举 → 原字节摘要 → UTF8/JSON/版本/重复键核查 → 递归净化 → 保留身份与历史材料。
- **作用：** 只读来源，不保存计划、不写Database、不激活；不完整数据也不发布为活动配置。
- **校验：** 256文件、单文件2MiB、总16MiB、深度64；拒绝链接/越界；敏感字段/凭据引用及可能夹带秘密的已知容器剔除，只报路径。未知普通文本保留不意味着能识别任意恶意隐写。
- **回执：** schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。
- **恢复：** 不改来源所以无需数据回退；修正格式/预算后另行预览。此报告不是可直接执行的Database切换计划。

### 3.11 知识双路导入

来源：[KnowledgeImportService](../../src/YuzuhaToolkit.Manager.Core/KnowledgeImportService.cs)。

- **输入：** product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。
- **前置：** 明确写授权、目标名无冲突、源未变、schema受支持；活动写占用/WAL等不能被不一致单文件复制绕过。
- **算法/调用：** 只读预览 → 私有暂存建库或一致备份 → 分层检查 → 重命名发布/登记。
- **作用：** 创建产品版本隔离库；不覆盖同名库，不重建个人/工程/经验层。
- **校验：** 完整性、schema、关系、FTS/MATCH、清单与来源指纹，canEnable决定能否启用。
- **回执：** 库身份、数据库SHA256、路径、检查项和来源证据状态；数据库身份与chunk ID共同定位正文。
- **恢复：** 失败清理本次暂存，保留旧库；已启用库变化要重检，不能把旧报告当新鲜证据。

### 3.12 SDK选择、构建与按ID验证

来源：[Use-YuzuhaDotnet.ps1](../../scripts/Use-YuzuhaDotnet.ps1)、[global.json](../../global.json)、[run-documented-test.ps1](../../scripts/run-documented-test.ps1)、[Test-Desktop.ps1](../../scripts/Test-Desktop.ps1)。

- **输入：** 仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。
- **前置：** helper要求host实际含10.0.302，只有10.0.400不能满足精确门禁；Windows/WPF/浏览器等按测试入口声明。
- **算法/调用：** 选择已有dotnet host并设置当前进程环境 → 核对测试ID/源码定位与摘要 → 新目录执行 → 解析原始结果/视口证据。
- **作用：** 构建、启动自有测试进程、写报告/截图；helper不安装SDK、不修改机器级环境。部分入口直接调用dotnet，不能假定全部自动dot-source helper。
- **校验：** 无匹配测试不能算通过；failed/skipped/not-run分别统计；截图需记录请求DIP、实际布局、PNG像素与DPI，图片存在不是验收。
- **回执：** commit/dirty、SDK/系统、执行数量、console/TRX/summary、源码身份及关联截图；历史结果不冒充本轮。
- **恢复：** 超时停止自有夹具并保留部分证据；环境缺失先修环境，重跑写新报告。首次CLI行为应在调用前设置首次运行/证书抑制环境，避免开发证书等非任务副作用。

### 3.13 UI发送前pending持久与恢复门禁

来源：[Web FrozenDeploymentClient](../../integrations/ribbon/source/apps/web/src/frozen-deployment.ts)、[WPF FrozenDeploymentClient](../../integrations/ribbon/source/apps/desktop/Features/Deployment/FrozenDeploymentClient.cs)。

- **输入：** 已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。
- **前置：** 没有未决部署记录；状态存储可写可回读；新建v1不冒充服务端冻结计划。
- **算法/调用：** 消费本地计划 → 发送前持久pending → 发一次apply → 核对回执身份/状态/activation → 可确认结束时清记录。Web使用同源navigator.locks覆盖整个写请求，锁内再次核对localStorage，无可靠锁支持则拒绝发送；WPF使用跨进程FileShare.None租约覆盖整个写请求，并在发送前CreateNew写pending、Flush(true)。
- **作用：** 写本机非秘密门禁记录；复用既有API，不建立第二个部署引擎。读到pending的新页面/进程先暂停写入，不因为页面关闭或服务重启自动解除。
- **校验：** 存储失败不得发送；v2回执必须匹配计划编号/摘要、版本、有效状态和activation类型。断连/超时/错身份/损坏回执保留unknown，阻止批次继续或自动重新预览。v1兼容入口发送后任何错误均保守unknown，不把旧引擎可能已保存历史的4xx当成无副作用拒绝；v2明确写前拒绝可以清理本地pending。跨页面竞争保护来自navigator锁而非localStorage自身原子性；人工解除必须核对原计划，不能视为服务器撤销。
- **回执：** UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。
- **恢复：** 重开读取记录后核对工作区历史、文件和备份；仅显式人工核对并二次确认才解除暂停。解除也获取同一Web锁/WPF跨进程租约，并比较提示时原编号与当前持久编号，旧A不能清除新B；当前持久记录优先于旧进程/标签内存编号。旧计划不重放、后续须新预览。上述客户端专项与离线全量已通过，原生预览、HTTP客户端与Web DOM证据仍各按其范围陈述。

### 3.14 独立运行时验收与收尾

来源：[本轮正常重启报告](../validation/ribbon-restart-2026-10-31.md)、[运行时验收契约](../../openspec/changes/integrate-pmltwin-product-platform/features/ribbon-runtime-verification.md)。此项是主代理明确授权的分阶段验收流程，不是假定存在一个能自动完成全部步骤的生产脚本。

- **输入：** 本次外部测试UIC、已确认的唯一Design登记、目标E3D2.1 APS/ALL/DESIGN、对应预览/哈希/备份与明确重启范围。
- **前置：** 启动和写入分别获准；原工程正常退出；新会话重新严格核验，不沿用旧灯态或旧PID。
- **算法/调用：** 主代理直接HTTP v2文件更新与单模块登记 → 保留部署时回执 → 正常退出/重新启动 → 独立通信身份核验 → 选中实际页签、悬停Tooltip → 对照UIC图像尺寸 → 正常退出并按after摘要条件恢复；不是最终UI点击提交的全流程测试。
- **作用：** 本次确实写独立UIC及Design测试登记、启动/退出真实产品；未覆盖原生业务UIC，未点击Macro。此流程不是一般“重启即生效”的承诺。
- **校验：** 旧Design PID115180与新PID127436及各自创建时刻分别核对；SessionSelected/Connected/TargetVerified均true；实际Tab/Group/Button与Tooltip截图、32/16 PNG IHDR和未选清单摘要共同限定证明范围。
- **回执：** 部署仍为deployed、runtimeVerified=false；后续运行时报告独立记录外观通过、Macro notRun及备份摘要，不能回写成部署时已加载。
- **恢复：** 新宿主正常退出后恢复唯一测试Design登记，七份清单与原摘要相符，本次后端停止，UIC/备份/回执保留。恢复后宿主加载及其他版本/模块不在本次通过范围。

## 四、回到总体：如何判断一次交付完成

一条完整任务链应能回答：**哪份要求 → 哪个Feature负责 → 哪个已注册operation → 哪份冻结输入 → 哪个内部Script/方法 → 哪些真实副作用 → 哪个回执 → 如何核验/恢复**。文档链接、设计导出、编译、离线测试、模拟安装和真实宿主加载是不同证据层，不能相互替代。

当前可以据源码和交接说明统一启动协调、部分独立数据库能力、只读迁移适配/预览、Ribbon冻结API/UI接线及现有通信/知识/部署边界；Ribbon离线全量53项通过，本机E3D2.1 APS/ALL/DESIGN已有一次正常重启后的具体Ribbon外观/Tooltip证据，未执行Macro。证据按原生预览、HTTP客户端、Web DOM和直接HTTP实机分别记账，不能称平台全部完成。剩余主要闭环是统一事实/资产存储与迁移切换、MDB候选搜集、各Feature GUI/CLI等价、Ribbon更广运行时/业务动作矩阵、CAF宿主集成、发行升级/回退及视觉认可。未完成任务分别以[平台清单](../../openspec/changes/integrate-pmltwin-product-platform/tasks.md)和[管理器清单](../../openspec/changes/separate-cli-manager-deployment/tasks.md)记账，不由本文改写。

对Ribbon尤其要保留三个答案：**编辑保存在哪里，磁盘部署到了哪里，目标运行时是否确实加载**。只有最后一项有具体产品/版本/模块、实际UI与获准动作证据时，才可声明相应范围生效；“已写文件”“服务已重启”“产品进程已重启”都不能单独给出这个结论。
