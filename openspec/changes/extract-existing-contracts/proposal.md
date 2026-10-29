# Proposal

## Why

现有项目已用 `docs/specification/contract.json` 关联需求、操作、真实符号和测试，但缺少供 Codex 使用的 OpenSpec 工作流，以及将既有契约集中交给维护者审查的入口。本变更先接入工作流、提取既有事实和未闭合项，供审查后决定规范归属及后续修改。

取证基线：`SoloLevelingAI/PmlTrigger.Yuzuha`，分支 `codex/quality-baseline-20261026`，提交 `abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143`。

## What Changes

- 初始化 OpenSpec 1.15.0 的 Codex 项目技能和中文配置。
- 提供 `contract-review.md`：完整提取 24 条现有需求的原状态、规则、验收、操作和证据引用，并整理主要接口不变量及待审问题。
- 提供 `operation-inventory.json`：从该基线编译出的原生 CLI 的 `op list` 导出全部操作、版本、输入 Schema 和授权标志；不调用 AVEVA。
- 提供 `evidence.json` 和 `verification.md`：记录取证提交、源文件摘要、本轮离线验证与历史实机证据的边界。
- 保持 `contract.json` 为唯一生效规范源；提取物是固定基线的审查快照，不是另一份可独立编辑的规范。
- 不修改运行逻辑、公共 API、已有契约状态、旧证据或发布版本。

## Capabilities

### New Capabilities

无新增产品能力。这是对既有行为的文档提取和工具接入，`.openspec.yaml` 显式设置 `skip_specs: true`；审查材料不写入 `openspec/specs/`，也不构造虚假的 ADDED/MODIFIED 行为增量。

### Modified Capabilities

无。若审查决定改变某项要求或迁移规范源，应在明确批准后另建带实际 spec delta 的变更。

## Impact

新增文件仅为 `.agents/skills/openspec-*` 和 `openspec/` 下的工具配置、规划与证据材料。现有 .NET / WPF / NET35 / NET48 源码、46 项原生操作、`contract.json` 和现有生成链不变。验证只使用本机编译、隔离文件与合成 Host；不据此扩大 AVEVA 实机支持范围。
