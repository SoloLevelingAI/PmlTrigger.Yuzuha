# 来源、状态与需求映射

核对日期：2026-10-31，Asia/Shanghai。三名 `gpt-6.1-sol` 子代理并行只读调查，主代理整合；未调用 Astra 或 Claude Code。测试数量引用旧报告，不是本轮重跑。相邻仓库未写入。

## 原仓身份

| 来源 | 分支 / HEAD | 当前证据边界 |
|---|---|---|
| PmlTrigger.Yuzuha（PMLTWIN） | codex/openspec-contract-review-20261027 / 57e6e8d51a0271b2fae5f2a60363af51a5a32da3 | 大量已有未提交实现；原 contract.json 生效；管理器 change 35/48 为此前状态 |
| LaunchDesk.Archon | codex/rewrite-semantic-compression / 9c9c8d8fe0bde8ba15cc04ba67e237b5d55c5dc5 | 原仓限定只读实查；一个未跟踪规格草稿不作为已提交证据；无 OpenSpec 根 |
| LaunchDesk.Next | codex/remote-portable-design-review / 78b8dc48b4b1746d67e5cc40b1012fecd38bef41 | 含既有工作区文档修改，状态按来源页分别读取 |
| UicRibbonDesigner | master，无可解析 HEAD | 本机工作区源码证据，不虚构提交 ID；README 顶部0.7与后续状态不同步 |
| FigCraft.D2C | master，无可解析 HEAD | 本机源码/报告证据，未以 Git 提交证明发行状态 |

Archon 权威根为 `C:/Users/26648/Documents/Codex/2026-10-15/ni/LaunchDesk.Archon`。工作区 `semantic-compression/rewrite`、`semantic-compression-round2/rewrite`、`file-index-edit/rewrite` 是其不同阶段作业副本。原仓 Git 历史已确认 e6a0d0d → 6185aba → 288d731 → 4f14ae0 → 42eb10d → cccfbd0 → 9c9c8d8；实施取原仓当前固定基线，不执行副本的 apply/commit 脚本。

## 代码与已有验证来源

以下 `Archon/` 指上列权威根；`Next/` 指同级 LaunchDesk.Next；`Ribbon/` 指同级 UicRibbonDesigner。路径用于定位事实，不声明已迁移。

| 边界 | 来源符号/文件 | 现状及验收来源 |
|---|---|---|
| rewrite 分层 | Archon/rewrite/architecture.json、README.md、Comments.xml | Kernel → OperationContracts → UseCases/Windows → Bootstrap；Desktop 只依赖契约；DevHost/AppHost 开发专用；Architecture.Tests |
| 操作受理与执行 | Archon/rewrite/LaunchDesk.UseCases/Operations/OperationCoordinator.cs | OperationId、ExpectedRevision、PlanDigest、持久受理和租约；Rewrite.Tests，未本轮复跑 |
| 部署与恢复 | Archon/rewrite/LaunchDesk.UseCases/Delivery/Planning.cs、Handlers.cs | 来源选择保持、预览、备份、恢复、变量/迁移；Worker 是有限提权边界 |
| 配置/资源 | Archon/rewrite/LaunchDesk.UseCases/Catalog、Resources | 分享、迁移、资源健康；Windows/Storage 为 JSON 实现，不能冒充 Database DLL 已有 |
| 产品共存 | Archon/docs/rewrite/IMPLEMENTATION.md | 原 App 为正式 UI，rewrite 为独立验证壳，完整 ViewModel/协调器接线未完 |
| 登录发现与冻结 | Next/src/LaunchDesk.Next.Application/Features/LoginLaunch/Discovery.cs、LaunchModel.cs、LaunchWorkspace.cs、Scripts/FindInstalledAveva.cs、FindAvevaProjectsAndModules.cs | 目录/初始化静态扫描、人工覆盖、冻结快照；Next/tests 单元、集成与架构测试；不把静态推断称运行事实 |
| Addin 装配 | Next/src/LaunchDesk.Next.Bootstrap/Addins/AddinCatalog.cs、WorkspaceHost.cs | XML 启动期装配已编译工厂；Next/tests/LaunchDesk.Next.ArchitectureTests/AddinTests.cs；CAF API 未接入 |
| Yuzuha 集成基础 | src/YuzuhaToolkit.Manager.Core、Workspace、Knowledge.Core、Communication.Contracts | 既有 Manager52/通信46目录、部署/知识/worker；此前401离线测试见 docs/validation/portfolio-review-2026-10-30；不是此次集成验收 |
| 历史导入 | src/YuzuhaToolkit.Manager.Core/LaunchDeskImport.cs、LaunchDeskImportOperations.cs | tests/YuzuhaToolkit.Manager.Tests/LaunchDeskImportTests.cs；净化导入不激活，Direct 跳过并警告；不是完整功能融合 |
| Ribbon API/数据 | Ribbon/docs/api.md、packages/contracts-csharp、apps/backend/Features/Workspace/WorkspaceFiles.cs | C# net48 独立后端，HTTP v1；文件工作区非缓存；独立 Database DLL 未实现 |
| Ribbon 部署/发现 | Ribbon/apps/backend/Features/Deployment/Manifests.cs、DeploymentFeature.cs、Assets/discover-installations.ps1 | 注册表/目录/绑定扫描；file-apply 与 registration-apply 分开；docs/verification.md 实机加载仍待验收 |
| Ribbon 架构 | Ribbon/scripts/check-features.mjs、openspec/changes/command-first-authoring/tasks.md | UI 不直接访问 Backend/WorkspaceFiles；旧报告34测试非此次重跑；WPF 状态与视觉设计分别记账 |
| D2C | FigCraft.D2C/src/automation/plugin-api.ts、VISUAL_REVIEW_HANDOFF.md | FigCraft → WPF/Godot 布局；不是 UIC 生成器；历史53项含受限/未测，不能归为全通过 |

## 原需求与统一能力映射

| 原规范来源/ID | 对应本变更 | 继承方式 |
|---|---|---|
| PMLTWIN contract.json G1/G2/G3 及现有 R/B/T/S | 所有能力 | 原 ID/状态保留；新增映射在评审后回填，当前不改生效源 |
| Archon docs/reference/specifications/catalog.json，BRD/PRD/SRS/FRD/SDD/SDS 家族；BRD-001/002/003/004 | composition、environment、database | 保留编号、scope 和“待确认”；原文为反向整理初稿，测试关联不等于需求完成；逐条导入是实施任务 |
| Next PM-01…05、MD-01 | environment、composition | 继承跨版本项目复用、来源/覆盖/人工修订、模型冻结，不回退到按安装版本盲改项目路径 |
| Next RP-01…03、PF-01、CR-01…07 | environment、database、composition | 远程可访问性、可读配置、秘密引用及授权仍有待实现部分；Tomlyn 选型不等于已安装 |
| Next FM-01、AD-01、DV-01…03、VM-01/02、LC-01…06 | composition | 普通远程不依赖 VM 开关；CAF/许可诊断/实际签出分别验证，历史实验不扩大为当前矩阵 |
| Next DG-01 | environment / 操作回执 | 不允许空 Error；旧版故障根因因证据缺失尚未确定，不伪造修复结论 |
| Next UX-01…04、DOC-01…03 | feature-design-evidence | 设计、DocFX、作品集来源与视觉认可分别保留 |
| Ribbon add-ribbon-uic-generator、import-manage-uic-history、optimize-icons-and-deploy-uic、command-first-authoring、portfolio-polish-workspace | ribbon-uic-integration、design-evidence | 引用原 change，不重置勾选；实际加载与新 WPF 集成不能由0.12画稿证明 |
| 现有 separate-cli-manager-deployment 六项能力 | 上层集成约束 | 原变更继续负责部署/通信/知识/启动/插件及未完 GUI 验收；不复制任务完成度 |

## 原生设计与作品集

| 原资产 | 用途与融合位置 |
|---|---|
| Archon/docs/design/LaunchDesk-fusion-v2.figcraft 与01–17导出 | 产品/验证壳原稿；包含工作区、部署、历史、配置、许可、迁移、应用、分享、工具、变量等；按任务另存 Feature 稿 |
| LaunchDesk.Next.Design/ | Next 创建前的历史设计与分工说明；不能当当前实现状态 |
| Next/docs/portfolio/design/launchdesk-portfolio.figcraft、index.html、compare.html | 八幕作品集；HTML↔FigCraft来源见 artifacts/portfolio-roundtrip/provenance.json，视觉认可仍待评审 |
| Next/docs/design/docfx-reading-theme.md | 文档主题与作品集同源，非产品 WPF 皮肤 |
| PMLTWIN/docs/design/pmltwin-v08 与 yuzuha-manager-features | 历史整体稿与当前00＋9独立草稿；完整任务链与生产 WPF 未通过 |
| Ribbon/design/figcraft/ribbon-studio-portfolio-0.12.figcraft | 当前设计原稿及 portfolio-0.12/ 证据；静态样例未接后端，实施时拆分编排与部署 Feature |
| FigCraft.D2C/artifacts/office-ribbon-0.8.0-state-verified/source/office-ribbon.figcraft | 转换工具验证资产；examples/work-orders 是工单样例，与用户所指 rewrite 无关 |

## 现场验证基线

用户 2026-10-31 指定 `APS` 为优先候选，并允许登录验证。先实际发现安装/版本/项目/模块，再验证；不保证每套安装都有 APS。本轮仅完成规划，没有执行登录或宣称产品通过。秘密不进入本文件或作品集，后续凭据只走本机受控渠道。
