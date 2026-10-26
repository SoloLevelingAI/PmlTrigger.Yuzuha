# 前端 SDD · 职责

| 边界 | 责任 |
| --- | --- |
| MainWindow / SessionWorkbench | 任务导航、草稿、确认、状态呈现 |
| LaunchProfileService | 与 CLI 共用校验/修订/历史/恢复 |
| 任务上下文 | 目标、关联编号、资料/记录侧栏；不共享筛选状态 |

当前按工程、会话、应用、知识、运行、设置组织；连续任务重组见待定。业务规则留在服务；有复用/测试压力才提取纯状态。无独立变化点不新增接口或 Command。现有 WPF 接点由 [B 索引](../../../../artifacts/docfx/navigation/specifications/bindings.md)定位；目标交互不能据此标成已实现。
