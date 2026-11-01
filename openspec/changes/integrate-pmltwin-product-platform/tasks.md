# Tasks

本清单仅跟踪新增整体集成。既有管理器实现与未完成任务继续归 `separate-cli-manager-deployment`；不能因旧测试或本提案写完而勾选以下实施项。各组开始前将超出单次工作范围的任务拆成小切片，保留原编号映射。

## 1. 来源与契约基线

- [x] 1.1 固定原 Archon/rewrite、Next、Ribbon 和 PMLTWIN 的文件级来源清单，保留工作区修改；以哈希清单及差异审阅验证，不能从副本覆盖原仓。证据见 baseline-handoff.md 与 baseline/source-manifest.json。
- [x] 1.2 将 Archon catalog 原条目与 Next 需求 ID、Yuzuha R/B/T/S 逐条映射到 Feature，交付无重复所有者、无静默丢项的映射检查报告。191个原ID逐条保留，见 baseline/id-feature-map.json。
- [ ] 1.3 将 contracts.md 细化为版本化 schema 与兼容样例，验证旧 CLI/HTTP 样例可映射，新逻辑名称不冒充已注册操作。
- [ ] 1.4 记录并评审独立 Database、操作协调者与 CAF adapter 决策；以评审记录和依赖图确认责任，无跨运行时直载假设。

## 2. Feature 与操作生命周期

- [x] 2.1 增加 Feature 描述与唯一注册目录，复用现有 Addin/worker；测试重复 ID、禁用、不支持版本及无副作用装配，补调用说明。已接实际Manager装配入口，见 features/functions-composition-handoff.md；目录不冒称所有平台Feature实现完成。
- [ ] 2.2 实现 operationId/requestId/attemptId 映射和唯一受理者；测试相同摘要复用、不同摘要冲突、持久受理失败不执行，补状态图。
- [ ] 2.3 接执行租约、页面关闭后的观察及中断恢复；用进程夹具验证 unknown 不重放、取消等待不伪装撤销，记录恢复操作契约。
- [ ] 2.4 保留 Worker 同用户/双向PID/启动时间/nonce/摘要边界；用替换进程和篡改请求夹具证明写前拒绝，明确与 CAF 授权的区别。

## 3. 后端易用性与首个完整任务

- [ ] 3.1 汇聚目录、注册表和初始化扫描结果；用多安装/权限错误/超时夹具验证来源、覆盖和预算，文档列明静态求值限制。
- [ ] 3.2 实现字段解析、偏好复验及人工覆盖保护；测试唯一候选零强制手填、多候选 needsInput、无交互不弹窗，记录提示触发规则。
- [ ] 3.3 对比 rewrite、Next 与现有 Manager 的启动语义，选定唯一实现并接兼容适配；用 Monitor/Marine/Direct 支持矩阵验证不误转换，补迁移说明。
- [ ] 3.4 完成发现→编辑→冻结→启动→观察纵向切片；GUI/CLI/脚本夹具验证同一输入同一决定和回执，不以设计原型代替接线。
- [ ] 3.5 只读定位本机 APS、产品/版本/模块/许可前置和 CAF 程序集元数据，交付脱敏环境报告；缺失项标 unavailable，禁止把探测当登录成功。

## 4. Database 与数据迁移

- [x] 4.1 建立 Database.Contracts 与独立 Database 程序集最小接口；架构测试证明 UI 不依赖存储实现、DLL 不含 AVEVA/WPF/部署副作用。独立DLL与依赖图已验证，现有工作区尚未切换该实现，见 features/database-handoff.md。
- [x] 4.2 完成配置与文档修订存储切片；测试并发修订、事务失败、只读/版本不兼容及重启恢复，文档声明唯一写者。14项独立测试通过；不替代4.4/4.5数据迁移与切换。
- [ ] 4.3 完成操作事实与大型资产索引切片；验证受理记录、哈希、受限路径和中断阶段，不宣称文件/数据库全局原子。
- [x] 4.4 为 rewrite/Next JSON 和 Ribbon 工作区实现只读导入适配与迁移预览；夹具验证字段/历史保留、秘密剔除、Direct诊断和不自动激活。已按实际格式矩阵实现且14项测试通过，见 features/migration-preview-continuation.md。独立复核确认本项只读边界已完成；活动Database写入/切换仍归4.3/4.5，不再混入本项。
- [ ] 4.5 实现显式切换与恢复；测试损坏备份、磁盘不足、并发写者、WAL一致备份和降级拒写，交付可执行恢复手册。
- [x] 4.6 保留两套知识库产品/版本隔离；按现有导入与完整性接口验证数量、身份、FTS和来源检查，禁止把旧报告当新的新鲜度证据。2026-10-31重新只读核验AM/E3D已导入库，见 docs/validation/platform-2026-10-31/knowledge-checks.json。

## 5. rewrite 扩展任务与 Ribbon

- [ ] 5.1 将 rewrite 配置分享/迁移接统一契约；以跨机绑定和秘密不迁移测试验证，补导入/撤回操作说明。
- [ ] 5.2 融合工具部署来源选择与恢复语义，复用原管理器事务；测试来源刷新不暗改选择、摘要变更拒绝及恢复冲突，避免新建第二套复制引擎。
- [ ] 5.3 接入环境变量与资源健康 Feature；测试作用域/提权、只读监测预算及过期状态，文档区分变量与 EVARS。
- [ ] 5.4 提供 Ribbon HTTP v1 适配，保留规范化身份/确定导出/输入限制；验证旧API夹具、错误码和UI不直接存储，补接口映射。
- [ ] 5.5 接入 UIC 编辑/历史及 Database adapter；测试增量撤销、未知XML/快捷键保留和历史恢复不写源，避免旧后端和新库双写。
- [x] 5.6 接入 UIC 预览/覆盖/登记；用模拟安装验证摘要、备份、编码和未选XML字节不变，保留原始绑定与失败诊断。v2与Web/桌面UI接线、53项回归、真实Web DOM及原生桌面预览通过，见 features/ribbon-ui-continuation.md；完整v1路由、Database和CAF各归5.4/5.5/5.7。
- [ ] 5.7 细化 CAF SDK/运行时适配清单并实现首个已确认宿主入口；以真实接口编译与装配测试验证，未支持版本明确拒绝。
- [x] 5.8 完成函数注册/列表/权限/调用的管理 CLI 接口缺口，与原管理器任务关联；测试源改变、权限撤销、Host未加载及unknown，保持既有通信ID。既有函数/契约9项与真实通信CLI无目标拒绝烟测11项通过；真实Host业务执行和GUI仍独立验收，见 features/functions-composition-handoff.md。

## 6. 独立设计、界面与作品集

- [ ] 6.1 从各原稿按任务拆出独立 Feature FigCraft 与来源索引，共用token；串行编辑并验证原生组件/变量/原型及原稿保留。
- [ ] 6.2 为每个 Feature 编写按钮到schema/权限/状态/恢复的映射，验证空态/歧义/冲突/超时/unknown任务链，补短交接。
- [ ] 6.3 接 WPF/CAF 界面并验证与 CLI 行为等价；实际截图检查窄屏、DPI、键盘焦点和可读性，不以 D2C 导出成功代替业务验收。
- [ ] 6.4 整合 rewrite/Next/Ribbon 作品集叙事及文档入口，交付来源/版本/状态索引和同尺寸对照；分别记录独立审阅与用户视觉认可。

## 7. 产品整体验收

- [ ] 7.1 验证基础产品包不带 VM/DevHost/Aspire，移除扩展不影响普通启动；检查共享后端哈希、安装升级和回退报告。
- [ ] 7.2 在实际发现的 APS 上按用户授权验证登录及会话核验，分别记录每个产品版本的非秘密证据；无环境或失败如实标记。
- [ ] 7.3 对选定产品执行明确范围的 UIC 加载验收，检查图标/尺寸/Tooltip及获准动作，保留恢复记录；实机部署沿用既有具体预览边界。
- [ ] 7.4 并行验收整体用户任务、架构边界、数据恢复、兼容与作品集；逐缺陷关闭并交付可复现报告，不用历史测试总数替代集成通过。
- [ ] 7.5 评审 canonical contract 的新增/映射与状态回填，确认原ID保留且无候选冒充生效要求后提交最终规范变更。

## 8. 首期启动切片（用户 2026-10-31 指定，保留上层任务映射）

本组是 1.3 / 2.2–2.3 / 3.1–3.5 / 6.1–6.2 / 7.2–7.4 的具体实施切片，不替代其余平台、CAF、Database 和全产品验收。证据入口为 [Feature 交接](features/launch-workflow-handoff.md)。

- [x] 8.1 注册 environment.discover/resolve，复用现有发现和模块目录；证明原样草稿可预览保存，记录覆盖/预算和人工覆盖。
- [x] 8.2 实现 launch.plan/execute/observe 的冻结、先受理、唯一尝试、恢复及 EVARS 变化拒绝，完成隔离测试。
- [x] 8.3 接通 HTML 与管理 CLI 同一执行器，保存配置复用、显式启动、按编号恢复观察，验证秘密输入及本机 HTTP 边界。
- [x] 8.4 用实际 APS 发起一次启动，记录 Monitor→Design 交接及持久回执；入口退出不推定工程退出，其他版本不沿用该结论。
- [x] 8.5 补中文 C# XML/DocFX、源码调试映射和一条命令启动脚本，验证实际构建及启动入口。
- [x] 8.6 交付独立启动 FigCraft、浅深色变量、HTML 四尺寸滚动实测，以及原生变量回写 CSS 的可执行检查。
- [x] 8.7 汇总并行审查、修复和测试证据，导出五个已注册操作输入 schema、文件哈希及精简交接，不推送或归档未完成平台。
- [x] 8.8 完成严格 Host/PML 实机验收：仅更新实装 resolver/Rpc，PID8408 expectedModel=DESIGN 严格 TargetVerified=true，两次内置读取 OK；协议能力仍0。原始证据及回退见 docs/validation/host-pml-verification-2026-10-31.md，旧 mismatch 历史保留。

- [x] 8.9 修复管理 CLI 启动产品后输出捕获等待 EOF：复用唯一启动后端，不注入标准设备并脱离调用方控制台；26项回归及 APS 实机374ms返回通过，父终端退出后工程仍可严格核验，不改变幂等及 unknown 语义。
- [x] 8.10 处理实装 Host 查询协议为 0 的兼容问题：备份及哈希复验后仅更新E3D2.1 Host DLL；APS PID25624严格DESIGN身份、真实查询协议1分页/预算拒绝/无回调副作用通过，旧Host门禁不放宽。证据及交接见 docs/validation/cli-host-compatibility-2026-10-31.md。

## Workflow follow-up

## 9. 单仓迁移交付（用户2026-10-31追加）

本组验收三份来源的必要代码、测试、设计和作品集在单仓完整可用，不能仅交付邻仓路径或HTTP适配器。保留原平台业务融合任务；资产集成不自动完成WPF/CAF实机或数据库切换。全部9.x验收完成后才推送本批，不以阶段提交提前推送。

- [x] 9.1 纳入 rewrite 的源码依赖闭包、离线测试、原生设计及作品集资料，保留来源哈希/原ID，原仓只读。
- [x] 9.2 纳入 Next 的源码依赖闭包、测试、可编辑设计与完整作品集本地资产，清除邻仓依赖。
- [x] 9.3 纳入 Ribbon/UIC 原后端、契约、UI、测试、设计与作品集，管理器连接本仓可构建启动的后端。
- [x] 9.4 提供统一构建/启动/资料入口与依赖检查；开发脚本不依赖本机用户名、相邻仓或硬编码源根，外部AVEVA环境清楚分离。
- [x] 9.5 在全新独立克隆中还原依赖、构建必要项目、运行离线测试并检查设计/作品集资产；克隆父目录无来源项目，验证报告绑定提交与命令。
- [x] 9.6 修复并行审阅问题，记录来源、改动、测试、已知限制和新电脑接手步骤，严格校验OpenSpec后推送并复核远端SHA。

## Workflow follow-up (continued)

## 10. 云端验收补丁复核（2026-10-31）

本组只复核独立补丁，不自动合并或升级原平台完成状态。详见 features/pr13-acceptance-ci-review.md 与其证据摘要。

- [x] 10.1 获取 fix/pr13-acceptance-ci 精确提交 f89f2fa6，与 bc8c778 对比归属、运行语义和校验门槛。
- [x] 10.2 在独立干净克隆执行 Windows .NET、文档、CLI、Native AOT、WPF 及相关生命周期回归，记录跳过与环境失败。
- [x] 10.3 核对精确提交的 GitHub push 三工作流，保留本机、云端、实机证据的区别。
- [x] 10.4 将结论、既有问题、复现方法与日志哈希写回 OpenSpec，严格校验本变更。
- [x] 10.5 单独升级既有 yaml 2.8.1 开发依赖并复验文档解析，消除 GHSA-48c2-rrv3-qjmp；不得关闭审计。
- [x] 10.6 按最终实现范围更新 PR #13 标题和描述，明确单仓来源集成与未完成平台任务。

## Workflow follow-up (continued after review)

- 用户2026-10-31追加授权：继续实施、做好后推送并留文档。8.7中的当时不推送边界由本授权更新；允许推送已验收切片，不将未完成平台归档或称作整体完成。
- 本次基础切片交接：features/platform-foundation-handoff.md。Database修订、CAF编译及Ribbon兼容入口均保留各自集成/实机缺口，不因DLL存在而勾选整组。

- 评审和实施完成前不 sync 或 archive 本候选变更。
- 按独立 Feature 继续细化；每次只读共享契约与当前 Feature 交接，避免把所有设计载入同一上下文。

## 11. 补丁合并与精简首页（2026-10-31）

本组为用户明确授权的后续实施，不改写第10组当时的只读复核范围。

- [x] 11.1 合并 fix/pr13-acceptance-ci@f89f2fa6，保留已有提交与严格校验。
- [x] 11.2 WPF 五工作区导航、Dashboard 默认首页、摘要折叠，保留全部12功能视图及草稿。
- [x] 11.3 FigCraft 新修订压缩为一个画布页、五工作区及窄窗变体，导出主题变量与预览；历史设计不覆盖。
- [x] 11.4 合并后开发、CLI、WPF、文档和安全审计回归，记录可选跳过及非实机边界。
- [x] 11.5 严格校验 OpenSpec、提交推送并复核远端 SHA，留下证据和交接。

交接：features/pr13-merge-dashboard-handoff.md。本组完成不等于平台所有任务完成。

## 12. CI视口与整体契约讲解

- [x] 12.1 下载精确失败提交日志/截图，确认请求视口与实际视口失配。
- [x] 12.2 修复受限宿主渲染，逐图核验尺寸与完整性，保留所有原校验。
- [x] 12.3 制作整体/Feature/Script契约HTML演示，独立事实审阅，标注未实现和未实机边界。
- [x] 12.4 完成最终本机回归与HTML视觉检查，推送后跟踪精确提交CI并记录结果。

交接：features/ci-viewport-contract-handoff.md；证据：features/ci-viewport-acceptance.json。

## 13. Sol 并行续作：先契约后实现

契约基线：[continuation-contracts.md](features/continuation-contracts.md)。映射原任务而不替代整组；人工评审、实际产品加载及活动数据库切换保持各自验收边界。

- [x] 13.1 核对最新源码、列明 WPF/Ribbon/迁移责任边界，先落盘续作契约与拒绝/恢复/验证标准。
- [x] 13.2 WPF 启动复用持久 launch.plan/execute/observe，显示原编号与恢复观察；验证冻结变更、重复与 unknown 不重放，保留秘密边界（3.4 / 管理器6.5）。56项桌面测试、构建与隔离正式控件验证通过，见 features/wpf-launch-continuation.md；不代表实机登录通过。
- [x] 13.3 Ribbon 新增严格版本化冻结预览/应用入口并保留 v1；验证计划及历史/目标变化拒绝、备份/编码和 pending-reload 不冒充实际加载（1.3 / 5.4 / 5.6）。本批44项Ribbon测试及Manager适配测试通过；新选安装原生UIC保护缺口已独立审阅、复现、修复。见 features/ribbon-v2-continuation.md；当时UI仍用v1，后续接线见14.2。
- [x] 13.4 为明确支持的 rewrite/Next JSON 与 Ribbon 工作区提供只读迁移预览，验证未知字段/历史、秘密剔除、Direct诊断及不激活（4.4）。迁移14项及实际CLI不完整输入拒绝通过，见 features/migration-preview-continuation.md；不代表活动Database迁移完成。
- [x] 13.5 整合各切片，运行对应真实测试与构建，记录新环境限制及未完成项；不得以历史 CI 或其他产品实机结果替代本轮证据。Manager224通过/2可选跳过、Desktop56通过、Ribbon44通过；WPF与CLI构建、隔离控件及操作注册验证通过。见 features/sol-continuation-handoff.md。

## 14. Ribbon UI 与重启加载续作

用户继续授权 Sol 并行实施及 APS 实机验证；按契约、细化、实现顺序推进。此组映射 5.4/5.6/6.2/6.3/7.2/7.3，不替代 Database、CAF 和全产品验收。

- [x] 14.1 细化 Ribbon UI 冻结计划与重启验收契约；明确预览、确认、unknown 恢复及磁盘部署和运行时加载证据的区别。见 features/ribbon-ui-continuation.md、features/ribbon-runtime-verification.md。
- [x] 14.2 将 Ribbon 编辑 UI 的文件更新/模块登记接入 v2；验证只提交计划身份、变更失效、重复点击与不确定结果禁止自动重试，显示待重启状态。写前pending、跨窗口/进程锁及同锁人工核对通过；首次新建仍明确标识v1。
- [x] 14.3 验证实际 UI 与后端调用链，记录构建、测试和独立审阅；更新集成来源哈希，不将模拟安装当实机证据。53/53回归、9/9客户端专项、Web实际DOM、WPF真实预览与布局恢复通过；桌面Apply另以HTTP测试覆盖，见 docs/validation/ribbon-ui-2026-10-31.evidence。
- [x] 14.4 在本机 APS 对具体 UIC 预览、部署、重启和运行时加载取证，记录目标身份、备份/恢复边界及图标、尺寸、Tooltip。E3D2.1 APS/ALL/DESIGN重启前后严格身份、32/16图标和两Tooltip通过；测试登记已按哈希恢复，七清单全match，未点击宏。见 docs/validation/ribbon-restart-2026-10-31.md；不替代全产品7.3。
- [x] 14.5 按总体契约→Feature→内部 Script→总结交付可追溯说明，区分规范、实现及验收状态，汇总本轮证据与剩余任务。见 docs/reference/project-contract-overview.zh-CN.md；包含13个既有Feature映射、14类Script七字段，明确MDB候选发现尚未实现。

## 15. 契约元数据与可复用图表组件

用户2026-11-01追加，映射6.2/6.4的展示切片；不替代其余业务实现或全部Feature设计。元数据契约见 features/contract-atlas.md。

- [x] 15.1 冻结元数据schema、来源权威、三轴状态、公开投影和组件输入契约，保留canonical及原稿。见 features/contract-atlas.md 与 docs/contracts/schema.json。
- [x] 15.2 实现稳定提取、公开白名单和漂移/引用检查，保留规范ID/状态及14类Script七字段；验证恶意内容与公开泄漏拒绝。18Feature/14Script/24原要求/191源映射/3流程，8项测试与标准JSON Schema独立校验通过。
- [x] 15.3 实现可复用Web组件与help/portfolio图谱，验证层级/搜索/筛选/JSON导入/纯SVG导出及移动端、键盘。真实浏览器18布局检查及交互通过；五个独立组件可复用，整页控制器为单实例。见 docs/presentation/contract-atlas/README.md。
- [x] 15.4 同元数据生成真实FigCraft组件/实例/变量与新文件，结构检查和渲染目检；提供Figma Plugin并明确真实执行边界。4原生组件/50实例/8变量，3项设计测试含40步布局通过；Figma离线mock通过，真实编辑器运行not-run。见 docs/design/contract-atlas/README.md。
- [x] 15.5 独立复核来源、状态、交互、安全预算及展示效果，交付一键生成/预览/复用说明与本轮证据。来源变化需build/check，未自动发布；证据见 docs/validation/contract-atlas-2026-11-01.json，保留全部原业务未完成任务。

## 16. 主题与阅读深度修订

用户指出明暗与阅读视图耦合、实现深度不足。按features/contract-atlas.md主题修订实现，保留元数据及原业务边界。

- [x] 16.1 独立light/dark/system偏好、首屏应用、系统响应、持久化和跨标签同步；存储不可用仍可使用。修复explicit→system动态监听状态分歧，原失败序列复验通过。
- [x] 16.2 生成同源语义主题token，Web组件/重要图元/SVG响应实际主题，核验文字与图形对比度。tokens v2/CSS漂移检查、Shadow/SVG实值及文字4.5/重要图元3对比检查通过。
- [x] 16.3 让指南/作品集具有明确阅读层次，提供所有登记流程的选择并保持实体/筛选/流程状态。指南完整展开、作品集分层阅读；3条流程均可显式选择，主题不重绘已展开细节。
- [x] 16.4 FigCraft/Figma采用独立Light/Dark模式，验证两阅读视图×两主题样本与文本事实一致。4组合真实FigCraft渲染及结构通过，10共享变量；Figma离线mock通过，真实运行not-run。
- [x] 16.5 真实浏览器验证四组合、系统/持久化/跨标签/失败恢复及视觉效果，更新说明、漂移与生成证据。12矩阵及完整专项通过；见 docs/validation/contract-atlas-theme-2026-11-01.json，未升级原业务完成状态。

## 17. 元数据到组件的共同自动生成链

用户明确完善生成深度。按features/contract-generation-pipeline.md先细化再实现；不能只做换色、手动build或浏览器内存替换。

- [x] 17.1 冻结可编辑作者源、共享组件模型、修订发布、自动更新及失败恢复契约。证据ledger独立只读；保存受理与共同发布分开核对。
- [x] 17.2 实现白名单候选编辑和共享render model，Web/原生消费同一事实投影，组件视图配置实际生效。20项数据/作者/共享模型测试通过，含证据篡改与实际字节/展开预算反例。
- [x] 17.3 实现单写者watch协调、隔离生成、同摘要共同发布、去抖/过期结果拒绝及lastgood恢复。10项协调器/HTTP/竞态测试通过，包含作者字节/依赖哈希自洽、最终复验、缓存/同版本重启门禁及停机等待保存。
- [x] 17.4 接入Web修订订阅、草稿预览/显式保存与并发冲突，自动更新保留阅读状态并准确标记原生生成状态。13项真实浏览器消费/恢复检查通过（原生使用明确测试替身），另18布局及12主题矩阵回归通过；真实原生共同生成独立验收。
- [x] 17.5 以隔离源真实修改Feature/七字段/连接和视图属性，验证Web自动刷新与FigCraft同版本属性/结构、失败保留及重连，交付可复现说明和证据。真实FigCraft CLI+Chromium完成5个共同修订，原生矢量连接及双摘要核对通过；预览后依赖变化仍能完成保存，脚本/HTTP错误为0。33项专项全通过无跳过，见 docs/validation/contract-generation-2026-11-01.json；Figma真实运行仍not-run，保留原业务未完成项。

## 18. 契约关联DocFX/Roslyn方法预览

用户追加方法预览；按features/contract-method-preview.md先契约再实施，保留规范/实现/运行验证边界。

- [x] 18.1 冻结精确符号绑定、源码/文档/静态关系、同修订与公开边界契约。独立internal目录与公开图分开，编译receipt与双端摘要同修订核验。
- [x] 18.2 复用真实编译输入完善Roslyn方法字段，验证重载/泛型/partial/async及真实方法快照，不增加业务执行行为。17项语义测试通过；真实13080节点/59229关系/747输入摘要复验通过，未配置Host SDK明确排除。
- [x] 18.3 实现有界方法目录、真实DocFX成员引用和独立快照，验证缺失/过期/越界/错误绑定及源码原字节身份。202方法/512静态边/200DocFX资源，6项目录专项通过；目录外入/出边均保留范围说明，API metadata/build零警告零错误。
- [x] 18.4 接入共同修订与Web方法预览，保留契约选择/草稿/阅读状态，验证源码纯文本、静态关系和恢复。15项方法浏览器检查、既有13项实时消费及18布局/12主题回归通过；显式方法入口、纯文本、源码展开/焦点及草稿隔离保留。
- [x] 18.5 以真实Roslyn+DocFX+浏览器验收契约到方法、精确成员链接与源码更新，交付总分总说明和证据，不升级原业务未完成状态。真实3修订ready→stale→重新捕获ready、精确API锚点、隔离源码更新与原仓不变通过，见 docs/validation/contract-method-preview-2026-11-01.json；总分总说明见 docs/reference/contract-method-preview.zh-CN.md。

## 19. 完整内容与摘要/详情阅读

用户追加作品集直观性、完整契约/实现入口及一键披露；按features/contract-reading-disclosure.md先契约再实现。

- [x] 19.1 冻结完整目录、卡内摘要/详情、全局披露、状态保持及事实边界契约。阅读视图、外观和全局程度独立；见features/contract-reading-disclosure.md。
- [x] 19.2 补共享规范索引与覆盖投影、完整来源入口和明确方法绑定范围。10节352行/191映射原文对照通过；全部32项方法范围明确，当前精确18/不可用8/不适用6，不捏造实现或候选状态。
- [x] 19.3 实现卡片/选中契约/方法/规范附录的摘要与原生折叠。32卡188原字段、14组七字段完整；组件六组布局及附录逐单元格核对通过，关键限制与排除项持续显示。
- [x] 19.4 接入跨Shadow组件的统一展开/折叠与路由状态，覆盖导航、更新、混合状态和独立草稿。9项整页阅读及实时消费者状态/焦点验收通过，修正方法方向切换丢焦点；48项Node专项全通过。
- [x] 19.5 完成完整性、压缩量、手机/键盘/焦点和真实服务/DocFX回归。18布局/12主题/17方法消费者及真实3修订回归通过；共同服务ready347方法/1364静态边，真实FigCraft7组件107实例；见docs/validation/contract-reading-2026-11-01.json及docs/reference/contract-reading.zh-CN.md。保留原业务未完成项。

## 20. 文档布局、页内目录与共享区块样式

用户追加常规文档导航、区块颜色提示及FigCraft/CSS同源固化；按features/contract-document-navigation.md先契约再实现。

- [x] 20.1 冻结完整左目录、当前正文、本页标题、定位/滚动/更新恢复及共享设计token契约。补独立作品集视觉条款，不以分色/功能通过代替美观。
- [x] 20.2 实现文档布局与完整层级目录、当前契约优先、桌面sticky与窄屏折叠可达。12组合核对完整32实体、左右独立滚动、手机选择/Esc/resize；作品集目标/交付优先、低频工具收起。
- [x] 20.3 实现稳定本页目录、跨Shadow必要展开、URL/历史、scrollspy与更新/删除恢复。独立模块及整页验证同名/hidden/代次清理、章节相对位置与焦点、来源隔离；全局折叠父锚点刷新仍折叠。
- [x] 20.4 同源区块颜色/排版token生成CSS及真实FigCraft参考板，保留原文与事实边界。六角色/两主题、40变量、真实双文档板32目录/七字段/可编辑文字通过；原生/Figma mock/token5项0skip，Figma宿主not-run。
- [x] 20.5 完成12布局组合、10组交互、独立outline及真实共同服务/ExecuteAsync/DocFX/原生验收。50项Node0失败0跳过；既有18布局/12主题/9阅读门槛保留。总分总说明见docs/reference/contract-document-navigation.zh-CN.md，证据见docs/validation/contract-document-2026-11-01.json；保留原平台27项未完成业务任务。
- [x] 20.6 独立完成作品集视觉复核：实际Web桌面/移动及FigCraft浅深渲染目检，按任务焦点/层级/对齐留白/色彩/叙事复核；原生大描边框改中性正文细分隔和紧凑图例。状态agent-reviewed、human-not-reviewed，不以功能/分色替代美观，不代称用户认可。

## 21. Dashboard会话可用性与摘要小标题

用户要求实际打开桌面、默认Dashboard及打开E3D自动灯态；先细化features/dashboard-session-health.md，不以展示完成代称产品全部完成。

- [x] 21.1 冻结只读监测、绿灯身份/新鲜度、失联清理、目标选择及摘要小标题契约。可用性与操作目标分开，不以展示完成代称全部产品完成。
- [x] 21.2 修复状态核验异常/选择失败保留旧绿与旧结果，epoch阻止忙操作旧回执回填；24项先成功后失效/竞态测试通过，输入和unknown语义保持。
- [x] 21.3 实现独立持续发现/只读Host核验及Dashboard首屏灯态，14项覆盖超时/过期/PID启动时间/管道模块/取消等；最终38项集中0失败0跳过，WPF构建0警告0错误。
- [x] 21.4 文档摘要加中性18px小标题和目录，七字段/来源/旧URL保留；12布局/10交互、6组件矩阵及32实体原文保真通过。FigCraft对应标题、原文/间距及真实CLI3项通过，Figma宿主仍not-run。
- [x] 21.5 实际桌面默认Dashboard；无会话灰灯→真实WPF已有APS /ALL DESIGN启动一次→Host可用绿灯、未自动选择目标。精确PID/start/管道/协议回执核对；WPF合成118截图及绑定0错、方法387/1477 ready独立复核。见docs/validation/dashboard-session-health-2026-11-01.json和docs/reference/dashboard-session-health.zh-CN.md。真实断线未强制测试，原27业务项保留。

## 22. 任务地图与从主干到分支的动线

用户指出必须滚过无关信息才找到答案。按features/task-map-navigation.md先细约后实施，重组阅读和任务入口，不以展示地图代替未实现业务。

- [x] 22.1 冻结主干/分支、共享taskMap、旧路由兼容、桌面状态入口及视觉/事实边界契约。见features/task-map-navigation.md，严格校验通过；不改变原规范或产品完成状态。
- [x] 22.2 生成同源稳定任务地图及真实FigCraft可编辑参考板，覆盖全Feature和真实流程，不猜无流程步骤。50节点/71类型关系/5阅读组、18Feature/14Script/12原步骤/3流程；14项专项0跳过，真实浅深地图板769可编辑文字/40变量，见artifacts/task-map-native-validation.json。
- [x] 22.3 实现Web地图优先、任务/步骤上下文与分支切换，完整内容/旧深链/实时更新可往返。12首屏与6步骤布局、10交互组通过，手机完整任务卡/三尺寸完整路线与前后步均首屏可见；步骤删除显式回标题焦点，旧文档12+10回归通过，见artifacts/contract-atlas-task-map/results.json。
- [x] 22.4 实现Desktop情境主入口、明确核验后任务选择、分支导航和就近结果，保留输入/授权/unknown语义。45项模型回归0跳过，WPF零警告/错误、118合成图与绑定0错；真实既有APS目标明确核验后四任务往返、Dashboard主CTA变化通过，无业务查询/PML，见artifacts/task-map-desktop/final-real-desktop.json。
- [x] 22.5 完成真实Web/原生/桌面动线和视觉复核、方法新鲜度、共同发布与总分总交接，原业务未完成项保持。51项Node/45项桌面模型0失败0跳过，真实五共同修订、当前契约→Plan/ExecuteAsync精确DocFX/新增Decide源码及SHA通过；754源码输入fresh、393方法/1485静态边ready。证据见docs/validation/task-map-navigation-2026-11-01.json，总分总说明见docs/reference/task-map-navigation.zh-CN.md；视觉agent-reviewed/human-not-reviewed，原27项不变。

## 23. 独立构图与三种视觉融合候选

按features/independent-visual-directions.md保留相同项目内容，独立构建ION/PRISM/BENCH三套网页。由用户后续选择FigCraft深化区域，不沿用旧三栏结构换色。

- [x] 23.1 冻结三套融合规则、共用内容接口、独立构图与本地预览/评审边界。见features/independent-visual-directions.md与各rules.json；严格校验通过，FigCraft等待用户指定区域。
- [x] 23.2 实现只读共用内容/完整阅读、比较入口与本地服务，支持同内容切版和深链。独立19440服务、公共投影/双摘要验证、完整七字段/目录/规范、四比较场景与区域上下文入口已接通；无作者写API。
- [x] 23.3 完成三套独立DOM/CSS及真实内容的项目、流程、契约与来源互动。ION海报轨道、PRISM层叠透镜、BENCH纸张/金属档案三版冻结；18Feature/14Script/三流程保真，未覆盖旧Atlas/Desktop或原生稿。
- [x] 23.4 实际浏览器核对覆盖/互动/键盘/三尺寸及视觉截图，交付比较链接、融合规则和评审区域索引。真实37布局检查/9组七字段原文与焦点/15区域深链、32目录/191映射及只读边界通过，12实拍比较预览与三版规则可用；证据见docs/validation/contract-style-lab-2026-11-01.json，入口19440。agent-reviewed/human-not-reviewed，FigCraft深化未开始，原27项不变。

## 24. 页内方法预览与ION主题色

用户倾向ION并要求方法不跳转、增加多套配色。按features/inline-methods-and-ion-palettes.md先细化契约，再实现；内部源码与公共图谱仍隔离。

- [x] 24.1 冻结页内方法/受限桥接、六主题与状态保持契约，记录ION偏好及视觉边界。细约见inline-methods-and-ion-palettes.md及desktop-developer-reading.md；OpenSpec严格校验通过。
- [x] 24.2 实现只读方法桥接和同页方法/源码/关系/DocFX预览，保留原任务与关闭焦点。14项桥接/原方法测试及13组组件回归通过；真实393方法/1485关系、源行/SHA和精确DocFX成员已验证，源码仍不进入公共投影。
- [x] 24.3 实现ION六主题同源JSON/CSS、首屏/URL/偏好、完整图形与阅读器配色，切换不丢内容状态。六主题生成漂移检查、偏好/URL/首屏及真实方法状态矩阵通过，正文72项对比度不低于4.5。
- [x] 24.4 完成真实源及无跳转/失败恢复、六主题桌面双尺寸/状态/视觉回归，更新交接与证据，原业务任务保持。真实8组页内/深链/焦点验证、12张1920/1440主题实拍通过，1024紧凑桌面复核；手机基本可达，独立布局后续设计。见docs/validation/contract-style-inline-2026-11-01.json；agent-reviewed/human-not-reviewed，原27项业务任务保留。
- [x] 24.5 按desktop-developer-reading.md落实桌面优先：ION流程步骤常驻，面包屑为主页/Feature/Script/Script Method；契约/方法/验证在当前工作区切换，长内容内部滚动，验证主题/直接返回/导航/焦点保持，手机后续独立排版。三个桌面尺寸通过；分析详情折叠、方法签名/源码首屏优先及完整名称提示已实拍目检，Script关联Feature证据不冒称独立验证。

## 25. 面包屑内直接切换同级对象

按features/desktop-developer-reading.md先细化候选范围、上下文、读目录门禁与键盘交互，再实施；保持原业务未完成项。

- [x] 25.1 冻结Feature/Script/精确方法同级集合、名称/下拉分工、栏目/主题/历史和失效恢复契约，并严格校验。详见desktop-developer-reading.md同级切换补充及feature-design-evidence规范场景。
- [x] 25.2 增加同摘要ready的受限方法根读取/选择接口，复验Script归属并拒绝邻域/旧摘要/旧owner；隔离组件验证通过。getter/selector及候选失效通知已接通，原13组及新增跨Script/未绑定/邻域/旧身份/关闭反例通过，不改变公共投影。
- [x] 25.3 实现ION同级搜索选择面板、直接切分支及键盘/焦点；保持栏目/主题，清旧步骤/方法，重选不重建；修复同Script换步后关闭方法的有效owner回调。名称返回和独立箭头分工、空/单项、原ID/签名提示及不同reader的历史恢复已真实验证。
- [x] 25.4 完成真实源码与桌面三尺寸、候选/状态/历史/刷新/空态/可达性及视觉复核，交付使用说明和证据。真实8组/16截图、18Feature/所属Script/精确3方法根、源行与SHA及6主题菜单对比度通过；原页内8组回归通过。见docs/validation/contract-breadcrumb-peers-2026-11-01.json；agent-reviewed/human-not-reviewed，FigCraft未深化，原27项业务任务保持。

## 26. PR审查修复、证据闭环与持续验证

用户明确授权按审查顺序修复、验证、更新文档与PR并推送。本组只跟踪本轮修复，不替代原27项平台业务任务，不重新执行AVEVA、不升级历史报告的日期或产品范围。

- [x] 26.1 修复当前CI的39项源码注释缺失，保留检查门槛；重新构建检查器及文档，验证无语义变更。
- [x] 26.2 将启动、会话的历史离线/UI/实机报告接入只读证据账本；保留失败/中间状态/排除项，提供可核对SHA的完整报告入口。
- [x] 26.3 将生成一致性、契约/桥接专项和真实浏览器检查纳入独立CI任务；保留失败日志、截图、环境/提交及明确的可选工具缺失记录。
- [x] 26.4 收紧桌面契约字段与来源披露，手机任务入口前置；验证原文/来源、证据范围、导航、无横向溢出及三尺寸截图。
- [x] 26.5 完成本机与精确推送提交的云端复验，更新交接和PR，保留平台未完成项及未执行的实机/人工视觉验收。
