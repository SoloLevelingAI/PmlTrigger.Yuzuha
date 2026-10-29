# OpenSpec 与 Codex

本仓库已用 OpenSpec 1.15.0 初始化 Codex，生成的项目技能位于 `.agents/skills/openspec-*`。

当前先审查 [既有契约提取稿](changes/extract-existing-contracts/contract-review.md)，本次不修改生产行为。

## 规范归属

- `docs/specification/contract.json` 继续是唯一生效的结构化规范源；既有 R/B/T/S 标识和状态不变。
- `openspec/changes/` 保存待审提案、设计、任务和固定基线证据。生成完毕不等于批准。
- 本轮是工具接入和文档提取，使用 `skip_specs: true`，不写入 `openspec/specs/`，不执行 apply / sync / archive。
- 以后若需改变行为或迁移规范源，先建立对应变更，并在审查时明确如何同步既有规范和生成链。

## 使用

安装 OpenSpec 后，在本仓库根目录执行：

```powershell
openspec list
openspec status --change extract-existing-contracts
openspec validate extract-existing-contracts --strict
```

在 Codex 中可使用 `$openspec-explore` 分析问题、`$openspec-propose` 起草变更。先审查提案和契约；明确要求实施后再使用 `$openspec-apply-change`。完成后的归档也需要符合本项目的规范归属约定。

Codex CLI / IDE 可用技能选择器或 `$技能名`；桌面版可在 Skills 中选择。Codex 会检测本地技能变化，若尚未出现，重新打开项目或重启 Codex。

OpenSpec 更新后，运行 `openspec update` 刷新生成技能，并审查差异。不要在生成技能中手工维护项目约定；项目约定写在 `openspec/config.yaml`。

## 审查入口

| 文件 | 内容 |
| --- | --- |
| [contract-review.md](changes/extract-existing-contracts/contract-review.md) | 原需求、公共不变量、操作矩阵和待确认问题 |
| [operation-inventory.json](changes/extract-existing-contracts/operation-inventory.json) | 当前基线原生 CLI 导出的完整输入 Schema / 授权标志 |
| [evidence.json](changes/extract-existing-contracts/evidence.json) | 基线、需求展开、符号和测试定位、源文件哈希 |
| [verification.md](changes/extract-existing-contracts/verification.md) | 本轮验证结果及适用边界 |
| [tasks.md](changes/extract-existing-contracts/tasks.md) | 已完成的取证工作和仍待人工确认的事项 |

参考：[OpenSpec 的 Codex 接入](https://openspec.dev/docs/supported-tools#codex)、[Codex 本地技能](https://learn.chatgpt.com/docs/build-skills)。
