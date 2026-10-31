---
document_kind: index
implementation_status: navigation
reviewed_on: 2026-10-29
---
# OpenSpec行为场景

[行为规范](../openspec/changes/launch-by-profile/specs/launch-by-profile/spec.md)、[实施清单](../openspec/changes/launch-by-profile/tasks.md)和[变更动机](../openspec/changes/launch-by-profile/proposal.md)从项目根openspec目录直接映射构建，不复制维护正文。功能解释见[八股正文](features/launch-by-profile.md)。

使用已批准OpenSpec1.15.0，在项目根执行 `openspec validate launch-by-profile --strict`。本机借用既有只读CLI安装；不写入旧工程。实际结果保存于artifacts/documentation/openspec-validation.txt。

当前重构：[具体功能脚本规范](../openspec/changes/explicit-feature-scripts/specs/feature-source-readability/spec.md)、[设计](../openspec/changes/explicit-feature-scripts/design.md)、[实施清单](../openspec/changes/explicit-feature-scripts/tasks.md)。在项目根执行 openspec validate explicit-feature-scripts --strict。


[逐条需求台账](../openspec/requirements.md)区分实现与提案。[Project/Module发现规格](../openspec/changes/project-module-discovery/specs/project-module-discovery/spec.md)已实施于fee897f；[远程项目、TOML与本机凭据授权](../openspec/changes/portable-profiles-and-credential-grants/specs/portable-profiles-and-credential-grants/spec.md)现已补齐设计，尚未实施。评审请从[八节方案](reviews/remote-portable.md)进入，结合[FigCraft](design/remote-portable-review.md)、[TOML调研](decisions/toml-selection.md)和[VM计划](reviews/remote-project-lab.md)。[任务清单](../openspec/changes/portable-profiles-and-credential-grants/tasks.md)明确已交付设计与未完成批准/实现/执行。
