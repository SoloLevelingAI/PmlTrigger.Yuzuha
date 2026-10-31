# 五条业务流程

[按功能查用途、入口及路径](~/artifacts/docfx/navigation/source-index/features.md)

| 流程 | 协议／区别 |
| --- | --- |
| [启动](launch.md) | 受理≠进程≠窗口≠会话确认。 |
| [交付](delivery.md) | 定义、来源、确认计划、写入分开。 |
| [变量](variables.md) | 子进程环境≠持久变量。 |
| [分享／迁移](sharing-migration.md) | 分享用 CommitId，迁移用 OperationId。 |
| [监测](monitoring.md) | 独立持续服务，不进后台日志。 |

后台回路：提交→日志受理／一次执行准入→处理器→上下文校验事实／日志递增序号→查询订阅→Dispatcher。配置走 CommitIntent／CommitReceipt；观测走内存缓存／订阅。

取消等待不撤销副作用；停止观察不强杀应用、不自动回滚。未知结果沿原编号核验，不盲目重放。阅读路线用 `npm run test:rewrite` 验证。[术语](../glossary.md) · [分层](../architecture.md) · [全部模块及入口](~/artifacts/docfx/navigation/modules/index.md)
