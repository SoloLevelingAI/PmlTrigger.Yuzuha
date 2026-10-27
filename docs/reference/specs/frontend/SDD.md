# 前端 SDD · 职责

| 边界 | 责任 |
| --- | --- |
| MainWindow（启动） | 草稿/密码输入、确认、状态、回执；不读凭据或创建工程进程 |
| ViewModels | 界面状态、筛选、结果投影；无需 MVVM 基类 |
| 后端 | 共用校验、凭据、进程、历史；返回数据/警告 |
| 任务上下文 | 目标、关联编号、资料/记录侧栏；不共享筛选状态 |

Desktop 按 Views/ViewModels/Presentation/Testing 收纳；[源码导读](../../source-reading.md)。现有导航保留，连续任务重组待定。[B索引](../../../../artifacts/docfx/navigation/specifications/bindings.md)仅定位。
