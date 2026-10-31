> 实施状态更新：用户于 2026-10-19 批准按本架构重构，`rewrite/archon` 已完成独立候选版。见 [实施说明](IMPLEMENTATION.md) 与 [运行验证](../verification/3.0.0-archon.md)。下文是保留的架构阶段材料；原生记录不回写审批历史。

# Archon 独立重写方案

**原生流程已到人工审阅点，尚未批准业务实现。** 分支：`architecture/archon`。采用用户指定的 [coleam00/Archon v0.12.1](https://github.com/coleam00/Archon/tree/v0.12.1)。

可先打开 [Archon → LikeC4 可视化审阅入口](visualization/README.md)，按总览、模块、类型、场景和审阅发现逐层查看。类型与依赖从当前原生契约生成；不修改独立 LikeC4 / OpenSpec 方案。

文字材料从 [当前审阅入口](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/CURRENT-REVIEW.md) 开始，再看 [问题、修订及复核](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/FINDINGS-AND-REVISIONS.md) 和 [当前类与接口](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/CURRENT-CLASSES.md)。当前提案包含 46 个类、26 个接口、10 个值对象，覆盖 R01–R14 和 27 个场景。

原生 Codex 节点完成了需求 → 候选比较 → 类契约 → 反例审阅 → 修订 → 独立复核。建议 A2：配置 CRUD、发现与导入走直接用例；有副作用和恢复需要的操作共用有限协调层。初次发现的 1 项 blocking、4 项 major 均获契约层复核接受。新增 F06 接口说明残留已单独勘误，明确标记未再调用模型复核。

## 真实执行与证据

[原生报告](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/REVIEW-PACKET.md)、[状态及接续链](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/native-run.json)、[节点记录](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/native-events.jsonl)、[输入与版本](runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d/input-manifest.json) 均已保存。最终为 paused / human-review / approval，未批准。

保存脚本曾失败，使用官方 --adopt 接续，失败记录没有隐藏；最后的归档工作流没有 AI 节点。当前契约为 reviewed-contracts.json，原模型合并结果 revised-contracts.json 保持原样，F06 对齐另记 editorial-corrections.json。模型标识在提供者记录中 unavailable，未虚构模型或费用。详见 [检查范围](CHECKS.md)。

## 文件与复现

不只有 Markdown：`.archon/workflows/launchdesk/` 包含五个工作流、结构化输出 schema、命令和 TypeScript 脚本；`tools/archon/` 包含隔离运行器、接续运行器、版本来源和只读补丁初始化；`runs/` 包含 JSON / JSONL、来源清单、哈希及阅读版报告。

```powershell
npm ci --ignore-scripts
npm run review
npm run evidence:check
node scripts/test-architecture-artifacts.mjs '<Bun 1.4.2 的 bun.exe>'
```

模型运行和接续方法见 [运行器说明](../../tools/archon/README.md)。使用隔离 SQLite 和现有 Codex 登录；向 OpenAI/Codex 发送本批私有架构资料已由用户明确授权。唯一提供者补丁是将模型节点收紧至只读，认证检查保留。普通应用不依赖这些开发工具或后台服务。

## 与初稿的关系

ARCHITECTURE.md、CLASS-DESIGN.md、CONTRACTS.md、CALL-FLOWS.md 及顶层两个契约 JSON 保留为原生审阅前的初稿和静态检查基线，不能混当成当前修订。`npm run review` 检查这份基线；本次修订的模型审阅、引用合并与证据检查另有记录，尚未进行程序集或应用行为测试。

LikeC4 和 OpenSpec 保持各自分支原值。模型输入未包含 FigCraft 图稿，不代表文件丢失：[既有图稿](../design/README.md) 仍在仓库，实施前须加入尺寸、DPI 和视觉验收输入。应用实现及行为验收仍在 [TASKS](TASKS.md) 等待批准。
