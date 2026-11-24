# 平台契约 v0.2

Schema：`pmltwin.platform-proposal/0.2`。状态：proposal。本文件不是运行时目录，也不修改 `docs/specification/contract.json`。

机器可读版本与兼容样例在 [contracts.proposal.json](contracts.proposal.json)。测试 `PlatformProposalContractTests` 对照实际 `OperationRegistry`：已列出的 operation ID 必须已注册，提案逻辑名称不是已注册操作。

## 运行时唯一来源

已注册操作、输入 schema、21 项旅程公开摘要和退出码对照只来自 `ManagerContractCatalog`，schema 为 `pmltwin.manager-journey/1`。阅读 `manager.contracts` 或 [安装到工程操作](../../../docs/reference/install-to-operation.zh-CN.md) 即可，不要在这里再抄一份 ID 清单。

`OperationResult.data` 仍是未知 JSON。旅程操作另外发布允许额外字段的公开摘要；`connection.sessions` 保持透传，没有稳定字段。安装器退出码 10（`INSTALL_APPLY_FAILED`）和管理器退出码 7 / `error.Code` 是不同进程，见导出的 `exitTaxonomy`。任何一行都不自动重试。

## 提案类型

下表的名称不是 CLI / HTTP operation ID。`FeatureDescriptor` 与 `OperationDescriptor` 已是运行时类型，分别由 `feature.list` 和 `manager.contracts` 返回。其余类型仍只存在于本提案。

| 名称 | 状态 | 说明 |
|---|---|---|
| FeatureDescriptor | 运行时类型 | 不是操作 ID。重复 ID、版本不兼容仍由 `FeatureRegistry` 拒绝。 |
| OperationDescriptor | 运行时类型 | 描述已注册操作的输入与授权。输出不在此封闭。 |
| OperationPlan | proposal | 统一计划形状尚未替代各操作自己的冻结计划。 |
| DatabaseSession | proposal | 不表示 Database 切换或会话已经实现。 |
| DesignHandoff | proposal | 设计文件不是运行证据。 |

旧逻辑族同样如此：`feature.list`、`environment.discover` / `environment.resolve`、`launch.plan` / `launch.execute` / `launch.observe`、`setup.handoff.inspect` 已注册。`feature.describe`、`environment.check`、`health.inspect`、`uic.preview` 不是已注册操作。配置编辑、知识库、UIC 和健康监测仍按各自已注册 ID 调用，不使用本文件里的候选句子当 ID。

## 兼容样例

| 样例 | 表面 | 标识 | 期望 |
|---|---|---|---|
| cli-handoff-inspect | CLI `op invoke` | `setup.handoff.inspect` | 已注册，无写入授权 |
| cli-and-http-contract-read | `GET /api/manager/contracts` | `manager.contracts` | 已注册，只读 |
| feature-list-not-feature-describe | CLI | `feature.list` | 已注册；`feature.describe` 不是 |
| proposal-logical-name | 提案 | `feature.describe` | 未注册 |
| proposal-environment-check | 提案 | `environment.check` | 未注册 |

延续契约见 [continuation-contracts.md](features/continuation-contracts.md)。追踪见 [设计、契约与脚本](../../../docs/validation/design-contract-script-trace-2026-11-02.zh-CN.md)。
