# C# 发现、可验证记录与依赖评审

状态：变更草案。用户约束已记录，DEP-01—05 已获批准，具体实施任务尚未派发；没有创建应用工程、执行发现、安装依赖或派发 Agent。本目录采用 OpenSpec 变更组织形式，未运行 CLI 初始化或 validate，不能宣称已通过工具校验。

批准更新：用户已批准 DEP-01—05，并指定 ArchUnitNET 和 FigCraft；此批准更新取代最初提案的待评审状态。具体执行任务仍未派发，安装与验证仍未发生。客户表达保持八股，架构共性面向后续 Feature 复用；见[客户简介](../../../customer-brief.md)与[共用架构及界面规划](../../../architecture-and-design.md)。

## Why

新项目要先能发现开发环境的应用、允许编辑预填值，并用 Record 与日志核验过程；不应为相同产品能力并存 C# 与 PowerShell 实现。成熟库可减少重复工作，但采用权属于用户。

## What Changes

- 产品发现改用专用 C# 实现，桌面与本机验证入口调用同一套逻辑，不要求不同产品发现器强行抽象复用。
- 第一条可运行流程即包含不可变快照、List<Record> 与落盘 Log，失败也能复核。
- 产品中新语言/专用工具以及新增依赖须明确评审；过程中的 PowerShell 不成为产品依赖。
- NLog、CommunityToolkit 仅列候选；具体包版本和验证方案提交用户确认。

## Dependency Review

2026-10-27 已完成官方资料调研：[选型报告](../../../dependency-review.md)集中维护精确版本、许可证、替代方案与验证步骤；[决策状态](design.md)关联 DEP-01—05，已获用户批准但未安装验证。调研完成不表示已安装、已通过运行验证或已批准。

## Impact

本次只更新独立设计目录。八股设计与伪代码见[Feature 方案](../../../launch-by-profile.md)，选型记录见[依赖评审](../../../dependency-review.md)，[增量要求](specs/discovery-and-evidence/spec.md)定义可验收约束；[任务](tasks.md)记录状态。
