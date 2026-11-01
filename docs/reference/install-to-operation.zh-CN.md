# 从安装到工程操作

<!-- Generated from ManagerContractCatalog and the actual operation registry; edit the C# contract, then regenerate. -->

安装应用后，由管理器明确选择环境、接手旧所有权、冻结部署与EVARS、核对回执，再启动与严格核验。阅读位置不代表执行进度。

## 全面契约

用户从一次应用安装开始，在同一管理器选择环境、核对来源、预览EVARS和部署、明确应用并读取回执，之后启动和核验工程。WPF、CLI和只读API共享下列阶段契约与操作目录。原canonical需求状态保持，文档导航和静态源码不代表本轮实机通过。

Inno新交互默认将环境准备交给管理器；安装应用不自动写EVARS或启动AVEVA。旧显式aveva/none计划仍按自身范围执行。管理器copy不覆盖Inno应用根，旧所有权先预览再显式移交；未知格式、漂移、权限或并发冲突明确拒绝。

## 任务地图

- [安装交接](#install)：读取交接身份、包清单及旧所有权状态。
- [选择产品环境](#environment)：汇聚目录/注册表/初始化候选并解释来源；复用模块和启动规则。
- [核对来源与旧所有权](#ownership)：只读核对旧安装所有者和迁移来源；显式授权后移交旧卸载记录。
- [预览部署与EVARS](#preview)：RuntimeDeploymentService与EvarsEditor生成同源冻结预览，保存非秘密计划缓存。
- [明确应用](#apply)：唯一Manager后端复验计划并写入，分别记录逐文件变化、备份及EVARS结果。
- [核对回执与恢复](#recover)：比较当前摘要与受管记录，按条件恢复文件和环境段。
- [配置与启动](#launch)：冻结launch.plan；先持久受理，再经唯一启动边界执行，按原编号观察。
- [严格会话核验](#verify)：经独立通信CLI核对目标身份和协议；不执行PML业务命令。

## 安装器如何兑现契约

- [installer/Yuzuha.iss](~/artifacts/docfx/navigation/scripts/sources/15be6c2cf13e5845.md)：Inno入口在应用安装成功后，以原交互用户打开准备工作区；静默模式不自动打开。启动参数只携带待核对的载荷根。
- [installer/NativePages.iss](~/artifacts/docfx/navigation/scripts/sources/5e7478040d7f9a06.md)：原生安装页面默认选择manager接手模式；旧显式aveva/none计划保持其声明范围。
- [installer/build.py](~/artifacts/docfx/navigation/scripts/sources/63a876a5dec01c36.md)：要求新Manager与Desktop入口，生成逐文件载荷清单；文档、通信CLI、四Host与管理CLI分别交付。

可选CLI Skill已有手工安装时，默认仍拒绝直接覆盖。只有明确选择adoptSkill并批准同一计划后，才接管完整冻结范围；文件/目录成员及SHA、当前安装标记和首次原文均复验，额外本地文件保留。安装应用和Skill更新不等于接入AVEVA。

- [installer/SetupBridge/PlanEngine.cs](~/artifacts/docfx/navigation/scripts/sources/1912c15edd2fbf0c.md)：同一安装事务绑定包清单、全局marker/priorState、批准哈希、备份与恢复，不从缺失原文推断允许删除。
- [installer/SetupBridge/SkillAdoption.cs](~/artifacts/docfx/navigation/scripts/sources/c201b9d80c2cc158.md)：冻结明确Skill目标的完整有界快照，复验链接/成员/内容变化；审阅文字与实际字节一致，正常升级及显式接管共用原文恢复规则。

[显式接管正反例的完整测试源码](~/installer/tests/SkillAdoptionTests.cs)。复刻离线安装契约：`./scripts/run-documented-test.ps1 -TestId smoke:installer-contracts`；使用真实临时文件和ReparsePoint夹具，不执行产品安装器或修改当前Skill。

<a id="install"></a>

## 1. 安装交接

| 契约字段 | 约定 |
|---|---|
| 输入 | 应用工具包根与安装交接标记。 |
| 前置 | Inno已交付应用、清单与管理器入口；没有据此接入AVEVA。 |
| 算法与调用 | 读取交接身份、包清单及旧所有权状态。 |
| 作用 | 只读；不修改厂商EVARS或启动工程。 |
| 校验 | 校验标记格式、应用根、载荷身份和路径边界。 |
| 回执 | 工具包/安装所有权快照；文件就绪不表示Host已加载。 |
| 恢复 | 缺失或不支持格式显式说明，返回安装入口核对，不猜接入成功。 |

### 操作与脚本解释

- `setup.handoff.inspect`：Read-only installed package/ownership verification; does not connect AVEVA.；授权：无业务写入授权。

- **YuzuhaToolkit.Manager.SetupHandoffService.Inspect**：解释安装交接和载荷身份；不接管运行环境。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.SetupHandoffService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/984d7e67dee08a47.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/SetupHandoffService.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="environment"></a>

## 2. 选择产品环境

| 契约字段 | 约定 |
|---|---|
| 输入 | 明确扫描根、预算及产品/版本候选。 |
| 前置 | 有可访问的产品目录；多候选需用户选择。 |
| 算法与调用 | 汇聚目录/注册表/初始化候选并解释来源；复用模块和启动规则。 |
| 作用 | 只读发现；不执行厂商BAT、不登录或默认授权。 |
| 校验 | 预算、候选来源、产品/模块组合和人工覆盖有效性。 |
| 回执 | 发现及解释结果；安装可见与登录成功分开。 |
| 恢复 | 缺失、超时、歧义明确展示，保留人工输入。 |

### 操作与脚本解释

- `installation.discover`：Read-only bounded installation and static project/module discovery ported from LaunchDesk.Next.；授权：无业务写入授权。
- `installation.projects`：Read-only bounded installation and static project/module discovery ported from LaunchDesk.Next.；授权：无业务写入授权。
- `environment.discover`：Read-only bounded environment discovery and explicit override resolution; returns needsInput without prompting or launching.；授权：无业务写入授权。
- `environment.resolve`：Read-only bounded environment discovery and explicit override resolution; returns needsInput without prompting or launching.；授权：无业务写入授权。

- **YuzuhaToolkit.Manager.ManagerInstallationDiscovery.Scan**：有预算地定位真实安装。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.ManagerInstallationDiscovery) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/0c6c9d21708afaca.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/InstallationDiscovery.cs`。
- **YuzuhaToolkit.Manager.LaunchDiscoveryService.Discover**：候选与来源解释，不把静态发现当登录。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.LaunchDiscoveryService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/5b7e863a0a328784.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/LaunchDiscovery.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="ownership"></a>

## 3. 核对来源与旧所有权

| 契约字段 | 约定 |
|---|---|
| 输入 | 所选工具包、EVARS目标及旧安装记录；可选旧项目来源。 |
| 前置 | 目标明确，旧所有权格式和实际摘要可核对。 |
| 算法与调用 | 只读核对旧安装所有者和迁移来源；显式授权后移交旧卸载记录。 |
| 作用 | 移交只修改受支持的旧所有权记录；migration.preview不激活或切换Database。 |
| 校验 | 格式、所有者、当前摘要、冻结引用和写入授权；拒绝外部漂移。 |
| 回执 | 迁移预览或移交回执、原记录备份；不表示数据库已切换。 |
| 恢复 | 不支持格式或漂移保持原样；依据备份和回执核对，不删除标记伪造接管。 |

### 操作与脚本解释

- `setup.legacy.preview`：Freeze one legacy v2 environment ownership transfer without changing EVARS or the installer marker.；授权：无业务写入授权。
- `setup.legacy.transfer`：Explicitly transfer frozen legacy ownership; retain old record/receipt and never edit EVARS.；授权：写入 。
- `migration.preview`：Read bounded source material without activation or storage switching.；授权：无业务写入授权。

- **YuzuhaToolkit.Manager.SetupHandoffService.PreviewLegacy**：区分旧安装器所有权和管理器部署所有权。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.SetupHandoffService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/984d7e67dee08a47.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/SetupHandoffService.cs`。
- **YuzuhaToolkit.Manager.MigrationPreview.Preview**：解释旧来源的只读映射与敏感字段剔除。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.MigrationPreview) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/eec5ec8dbba41349.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/MigrationPreview.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="preview"></a>

## 4. 预览部署与EVARS

| 契约字段 | 约定 |
|---|---|
| 输入 | 工具包、所选安装/INIT或BAT、profile/framework、独立运行根和copy/reference模式。 |
| 前置 | 来源身份可核对；旧Inno环境所有权已明确移交；copy目标不能覆盖Inno应用根。 |
| 算法与调用 | RuntimeDeploymentService与EvarsEditor生成同源冻结预览，保存非秘密计划缓存。 |
| 作用 | 不写业务目标；展示受管行、前后摘要、文件差异和恢复方式。 |
| 校验 | 来源/目标范围、包清单、安装身份、产品与Host映射、旧环境所有者、编码、摘要及bootstrap适配。 |
| 回执 | planId/sha256和明确变化摘要；预览不是部署完成。 |
| 恢复 | 输入改变清除可执行预览并保留草稿；拒绝无效或过期计划。 |

### 操作与脚本解释

- `deployment.preview`：Preview runtime copy or in-place reference and its selected AVEVA environment.；授权：无业务写入授权。
- `evars.preview`：Preview the selected installation's effective INIT/BAT and retain a local frozen plan.；授权：无业务写入授权。

- **YuzuhaToolkit.Manager.RuntimeDeploymentService.Preview**：冻结载荷、运行根与环境文件的变化。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.RuntimeDeploymentService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/58e57c6993990aec.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentService.cs`。
- **YuzuhaToolkit.Manager.EvarsEditor.Preview**：保留原编码和非受管行，解释将写入的受管段。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.EvarsEditor) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/686b975f61ea2208.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/EvarsEditor.cs`。
- **YuzuhaToolkit.Manager.ManagerHostProfiles.Framework**：用同一精确映射拒绝产品与Host配置错配。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.ManagerHostProfiles) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/11394cf64bb07f25.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/ManagerHostProfiles.cs`。
- **YuzuhaToolkit.Manager.SetupEnvironmentOwnershipGuard.EnsureAvailable**：依据明确来源与已登记安装的当前记录阻止绕过旧环境所有权；未登记且未提供的旧根不声称已全机排除。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.SetupEnvironmentOwnershipGuard) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/9559cd35663286a7.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/SetupEnvironmentOwnershipGuard.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="apply"></a>

## 5. 明确应用

| 契约字段 | 约定 |
|---|---|
| 输入 | 已审阅planId/sha256及本次明确写入授权。 |
| 前置 | 冻结计划、源文件、目标和安装身份仍一致。 |
| 算法与调用 | 唯一Manager后端复验计划并写入，分别记录逐文件变化、备份及EVARS结果。 |
| 作用 | 仅实施当前冻结范围；不启动AVEVA、不推断运行时加载成功。 |
| 校验 | 写入授权先于处理；再次核验摘要、所有权和实际文件。 |
| 回执 | 部署或EVARS回执、备份/恢复引用；部分恢复或unknown仍可定位。 |
| 恢复 | 错误显示在当前动作附近；未知结果不自动重放，按原回执核对。 |

### 操作与脚本解释

- `deployment.apply`：Apply the exact reviewed runtime/environment plan; retain backups and recovery evidence.；授权：写入 。
- `evars.apply`：Apply a frozen environment plan after checking its fingerprint; retain the original backup.；授权：写入 。

- **YuzuhaToolkit.Manager.RuntimeDeploymentService.Apply**：执行当前计划并保留跨文件失败诊断。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.RuntimeDeploymentService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/58e57c6993990aec.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentService.cs`。
- **YuzuhaToolkit.Manager.EvarsEditor.Apply**：仅写入冻结的环境修改并保留原件。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.EvarsEditor) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/686b975f61ea2208.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/EvarsEditor.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="recover"></a>

## 6. 核对回执与恢复

| 契约字段 | 约定 |
|---|---|
| 输入 | 原操作回执及恢复引用。 |
| 前置 | 只恢复本次拥有的变化；目标等于本次写入结果时恢复，已等于原始指纹时只确认。 |
| 算法与调用 | 比较当前摘要与受管记录，按条件恢复文件和环境段。 |
| 作用 | 不覆盖外部后续修改；应用卸载与运行部署恢复分别处理。 |
| 校验 | 当前目标、备份完整性、摘要和恢复授权；冲突逐项保留。 |
| 回执 | 恢复结果、未恢复项及原因；不能把部分恢复称为全部成功。 |
| 恢复 | 保留备份与冲突证据，人工解决冲突后按同回执续行；丢回包先核对，不无条件回滚或清理。 |

### 操作与脚本解释

- `deployment.restore`：Conditionally restore the recorded runtime and environment transaction.；授权：写入 。
- `evars.restore`：Restore a recorded environment change only if the target still equals the applied content.；授权：写入 。

- **YuzuhaToolkit.Manager.RuntimeDeploymentService.Restore**：逐项核对受管文件，保留冲突并允许按原回执继续未完成恢复。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.RuntimeDeploymentService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/58e57c6993990aec.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentService.cs`。
- **YuzuhaToolkit.Manager.EvarsEditor.Restore**：只恢复已应用内容；原始指纹已恢复时确认，不重写文件。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.EvarsEditor) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/686b975f61ea2208.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/EvarsEditor.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="launch"></a>

## 7. 配置与启动

| 契约字段 | 约定 |
|---|---|
| 输入 | 产品入口、配置修订、明确工程/模块及本次内存凭据。 |
| 前置 | 环境准备范围已核对；密码经stdin/本机文件传入，不能放argv。 |
| 算法与调用 | 冻结launch.plan；先持久受理，再经唯一启动边界执行，按原编号观察。 |
| 作用 | 创建入口进程；进程创建与登录、Host身份分别判断。 |
| 校验 | 修订/EVARS/入口摘要、计划有效期、显式启动授权和唯一尝试。 |
| 回执 | operationId/attemptId与入口/交接观察；密码不入计划、日志或文档。 |
| 恢复 | unknown不重放；取消等待不声称撤销已启动工程，使用launch.observe。 |

### 操作与脚本解释

- `launch.plan`：只读目标并缓存非秘密冻结计划，不改变启动配置或厂商文件。；授权：无业务写入授权。
- `launch.execute`：先持久受理再启动；重复操作返回已有事实，未知结果禁止重放。；授权：启动 。
- `launch.observe`：观察持久启动回执；入口进程存在不代表已登录。；授权：无业务写入授权。

- **YuzuhaToolkit.Manager.LaunchWorkflowService.Plan**：冻结当前配置与实际入口文件。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.LaunchWorkflowService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/8c52649c1ad69d59.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs`。
- **YuzuhaToolkit.Manager.LaunchWorkflowService.ExecuteAsync**：先受理、唯一启动与未知结果边界。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.LaunchWorkflowService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/8c52649c1ad69d59.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs`。
- **YuzuhaToolkit.Manager.LaunchWorkflowService.Observe**：按原编号读取持久事实和进程身份。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.LaunchWorkflowService) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/8c52649c1ad69d59.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

<a id="verify"></a>

## 8. 严格会话核验

| 契约字段 | 约定 |
|---|---|
| 输入 | 显式选定PID、十进制字符串启动ticks、模块、管道及关联配置。 |
| 前置 | 发现候选后明确选择操作目标；可用灯态不自动选目标。 |
| 算法与调用 | 经独立通信CLI核对目标身份和协议；不执行PML业务命令。 |
| 作用 | 只读核验；不授予执行权限，也不证明所有业务通过。 |
| 校验 | PID、完整64位启动身份、模块、管道、协议和回执新鲜度。 |
| 回执 | 严格连接证据或明确失败；与历史验收日期、产品范围分开。 |
| 恢复 | 失联/身份变化清旧结果，重新发现和明确核验；不能沿用旧绿灯。 |

### 操作与脚本解释

- `connection.sessions`：Call the independent communication CLI; no PML is executed.；授权：无业务写入授权。
- `connection.select`：Call the independent communication CLI; no PML is executed.；授权：明确目标身份 。
- `connection.status`：Call the independent communication CLI; no PML is executed.；授权：明确目标身份 。
- `connection.test`：Call the independent communication CLI; no PML is executed.；授权：明确目标身份 。

- **YuzuhaToolkit.Manager.CommunicationCliClient.InvokeAsync**：保持独立进程边界，不拼造进程内RPC调用轨迹。
  [DocFX类型与成员](xref:YuzuhaToolkit.Manager.CommunicationCliClient) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/ce32ac38a9088010.md) · 源文件：`src/YuzuhaToolkit.Manager.Core/CommunicationCliClient.cs`。

**验收范围：** 本阶段说明实现契约与读取路径；不代称本轮实机通过。历史实机仅按原报告的E3D2.1/DESIGN范围，跨产品、真实安装/移交、Database切换及用户视觉认可分别验收。

## CLI、OpenAPI和TypeScript

```powershell
Yuzuha.Manager.Cli.exe op invoke manager.contracts --input-stdin
# stdin: {}
Yuzuha.Manager.Cli.exe op describe deployment.preview
```

CLI复用同一注册目录和stdin/文件输入，写入、启动、执行分别显式授权。配置保存、冻结计划、写入与Host核验是不同动作。进程启动ticks经十进制字符串传递，禁止经过JavaScript number舍入；失败或unknown不自动重试。

[契约JSON](../contracts/manager-journey.json) · [只读OpenAPI](../contracts/manager-journey.openapi.json) · [TS客户端](../contracts/manager-journey-client.ts) · [可执行JS客户端](../contracts/manager-journey-client.mjs)。OpenAPI只描述实际只读HTTP目录；业务调用由CLI执行，不据此宣称存在HTTP写入路由。

OperationResult.data remains unknown JSON; no dedicated output schema is promised. CLI process start ticks are decimal strings; do not round through JavaScript numbers. Static script links are not execution evidence.

## 验收与接手

安装与EVARS/部署移交使用可审查计划、备份和恢复回执；真实安装、旧所有权移交和厂商文件修改按具体预览取得批准。临时目录测试、WPF合成截图、历史E3D2.1/DESIGN报告、当前实机及人工视觉认可分别记录。

[首轮验证与排除项](../validation/install-to-operation-2026-11-01.json) · [视觉与Skill追加验证](../validation/visual-priority-skill-adoption-2026-11-02.json) · [本轮实施契约](~/openspec/changes/integrate-pmltwin-product-platform/features/install-to-operation-journey.md) · [准备界面与共享设计](../design/install-to-operation/README.md) · [开发与维护](documentation.md)。
