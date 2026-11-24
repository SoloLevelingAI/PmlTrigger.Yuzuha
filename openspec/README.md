# OpenSpec

本仓库用 OpenSpec 记录待审变更。生成技能位于 `.agents/skills/openspec-*`。项目约定以 [config.yaml](config.yaml) 为准。

## 现在从哪里看

当前产品集成变更是 [integrate-pmltwin-product-platform](changes/integrate-pmltwin-product-platform/proposal.md)。契约说明在 [contracts.md](changes/integrate-pmltwin-product-platform/contracts.md)，版本化提案在 [contracts.proposal.json](changes/integrate-pmltwin-product-platform/contracts.proposal.json)。2026-11-02 的设计、契约与脚本追踪在 [报告](../docs/validation/design-contract-script-trace-2026-11-02.zh-CN.md)。

[extract-existing-contracts](changes/extract-existing-contracts/contract-review.md) 是更早的只读提取，保留作基线，不再是当前审查入口。

## 规范归属与同步政策

- `docs/specification/contract.json` 仍是唯一生效的结构化规范。OpenSpec 变更是审查提案，不是第二份已接受规范。
- `openspec/specs/` 只有 `.gitkeep`。在明确批准从 contract.json 迁移之前，不运行 sync，不运行 archive，不把 change 里的 delta spec 写入 `openspec/specs/`。
- `openspec/changes/` 保存提案、设计、任务和证据。生成或写完不等于批准。
- 配置里的 `skip` 与 “不归档、不同步” 继续有效。以后若要改变行为或迁移规范源，先新建变更，并在审查时写明如何同步。

## 使用

安装 OpenSpec 后，在仓库根目录执行：

```powershell
openspec list
openspec status --change integrate-pmltwin-product-platform
openspec validate --strict
```

先看提案和契约。明确要求实施后才使用 apply。归档同样要遵守上面的同步政策。

历史提取的审查材料仍在：

| 文件 | 内容 |
| --- | --- |
| [contract-review.md](changes/extract-existing-contracts/contract-review.md) | 原需求、公共不变量、操作矩阵和待确认问题 |
| [operation-inventory.json](changes/extract-existing-contracts/operation-inventory.json) | 提取时的输入 Schema / 授权标志 |
| [evidence.json](changes/extract-existing-contracts/evidence.json) | 基线、符号和源文件哈希 |
| [verification.md](changes/extract-existing-contracts/verification.md) | 该轮验证结果及边界 |
| [tasks.md](changes/extract-existing-contracts/tasks.md) | 已完成的取证和仍待人工确认的事项 |
