# Proposal

## Why

PMLTWIN 的目标是让用户更方便地开发 PML、使用三维系统并持续维护工具，而不是增加一组需要分别配置的程序。LaunchDesk 的发现与启动、Yuzuha 的部署与通信、Ribbon/UIC 的界面编排，以及设计、开发实验、文档和作品集需要围绕同一任务、同一环境证据和同一后端契约协作。

取证日期：2026-10-31（Asia/Shanghai）。PMLTWIN 当前源码仓库为 PmlTrigger.Yuzuha，分支 `codex/openspec-contract-review-20261027`，HEAD `57e6e8d51a0271b2fae5f2a60363af51a5a32da3`，包含已有未提交实现；HEAD 不是工作区全部内容的版本。相邻仓库来源与状态见 [source-inventory.md](source-inventory.md)。

## What Changes

- 建立 PMLTWIN 整体产品任务图：发现环境 → 选择工程 → 部署工具/界面 → 启动 → 核验通信 → 调用功能 → 回看证据。每个模块说明用户收益、自动化责任、失败恢复和验收依据。
- 将 LaunchDesk.Archon/rewrite、Next 登录切片、可选许可/VM实验、开发工具、文档、作品集与 Ribbon/UIC 纳入统一能力目录。用户已明确“拆单”指 rewrite；以核实后的原仓为依据，工作副本和历史快照仅用于追溯，不再维护多套后端。
- 将易用性实现为后端可脚本调用的环境发现、候选解释、预填、校验、预览、执行与回执；界面、CLI、脚本不各写规则。可可靠提取的信息不重复询问用户，歧义与授权不能由探测猜测。
- 定义 CAF 风格 Feature 装配契约与具体 CAF 适配层隔离；数据库统一由独立 `PmlTwin.Database.dll` 管理应用数据，保留 AVEVA 通信进程和 NET35/NET48 Host 边界。
- 纳入 Ribbon 的共享命令定义/布局引用、UIC 无损导入、版本历史和预览部署契约。保持 UIC 内容文件与加载 XML 清单的区别，配置持久化不等于实际部署。
- 每个 Feature 独立 FigCraft 与契约映射；作品集展示任务价值、调用链和真实证据，保留“已实现/部分/拟议/历史”状态。
- 按契约、后端、适配、界面、安装与实机证据逐步细化；保留现有 CLI ID 和兼容入口。未来删除旧入口或改变持久格式属于 **BREAKING**，必须另附迁移验收。

## Capabilities

### New Capabilities

- `product-feature-composition`：产品任务、Feature 身份、CAF 装配及可移除扩展。
- `environment-assisted-workflows`：同源环境探测、最少提问和可复现操作计划。
- `application-database-boundary`：独立数据库程序集、版本隔离、迁移和恢复。
- `ribbon-uic-integration`：界面编排与 UIC 导入、生成、部署和版本管理。
- `feature-design-evidence`：Feature 原生设计、文档、作品集及证据追溯。

### Modified Capabilities

无：当前已同步的 `openspec/specs/` 为空。已有 `separate-cli-manager-deployment` 六项能力继续由原变更负责，本变更只补上层集成约束，不复制其验收任务。`docs/specification/contract.json` 仍是生效规范，保留 G/R/B/T/S 等原 ID；本提案不是规范迁移批准。

## Impact

涉及 Manager、Workspace、Knowledge、Desktop、发布与架构测试，以及 LaunchDesk.Archon/rewrite、LaunchDesk.Next、UicRibbonDesigner、FigCraft.D2C 的明确适配边界。相邻项目本轮只读检索；不搬运二进制、凭据或私有知识库，不修改现有运行目录，不实施 CAF SDK 接入或数据库迁移。本轮交付是可评审的整体契约和实施顺序，未完成项一律不标为实现通过。
