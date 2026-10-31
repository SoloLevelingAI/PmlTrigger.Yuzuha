# PMLTWIN 整体集成方案

2026-10-31 · 候选规划，尚未实施整体融合。

目标：让开发 PML、启动和使用 AVEVA、维护工具与界面成为连续、可恢复的任务。自动能读出的环境信息由后端处理，用户只处理真正的选择。

- [提案与范围](proposal.md)：Archon/rewrite、Next、Yuzuha、Ribbon/UIC、设计与作品集的整体关系。
- [方案与分阶段迁移](design.md)：CAF、独立 Database DLL、唯一操作协调者、自动探测及运行时隔离。
- [契约草案](contracts.md)：Feature、操作、发现、计划、回执、存储、迁移和设计交接。
- [来源及原需求映射](source-inventory.md)：权威原仓、工作副本、实现状态和各自 FigCraft。
- [实施与验收任务](tasks.md)：保留旧变更进度，逐步细化后实施。

## 能力规格

- [Feature 装配与操作生命周期](specs/product-feature-composition/spec.md)
- [后端自动探测与易用性](specs/environment-assisted-workflows/spec.md)
- [独立 Database 与迁移](specs/application-database-boundary/spec.md)
- [Ribbon/UIC 集成](specs/ribbon-uic-integration/spec.md)
- [独立设计与作品集证据](specs/feature-design-evidence/spec.md)

## 本轮检查

三名 GPT-6.1 sol 子代理并行检索，平台和 Ribbon 分别复审提案。已补齐独立 DLL 实物交付、持久受理与去重、原生 UIC 只读保护/后端绑定三处约束。OpenSpec strict 校验通过，规划所需 proposal/design/specs/tasks 齐备；这只证明规划完整性，不代表代码、界面或实机验收完成。

本轮未修改产品代码、相邻仓库、已有 FigCraft 或生效 contract.json，未进行数据库迁移、登录、部署或新一轮产品测试。APS 已记为用户指定优先实机验证候选，实际路径与模块由后续探测决定，口令不落盘。

下一实施切片：固定逐文件来源和原需求映射 → schema/操作所有者 → 自动发现与启动纵向流程，再逐步纳入存储、rewrite其他能力、Ribbon和CAF。现有管理器 change 的未完事项仍保留。
