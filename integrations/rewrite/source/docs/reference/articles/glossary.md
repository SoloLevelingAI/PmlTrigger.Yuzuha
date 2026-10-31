# 核心术语表

| 术语 | 含义与边界 |
| --- | --- |
| <xref:LaunchDesk.Kernel.CommitId> | 直接配置提交的稳定编号；未知结果核验原回执，不换号重写。 |
| <xref:LaunchDesk.Kernel.OperationId> | 后台请求及事实的稳定编号，本身不赋予执行权。 |
| Revision | 配置／资源修订；预期值防并发覆盖，不是操作编号。 |
| CommitReceipt | 原提交修订、实体及历史结果，不能用当前目录替代。 |
| OperationAcceptance | 先持久受理；Created 才可能带首次准入，Existing 不再派发。 |
| Disposition | Created／Existing 的受理关系，不是运行阶段或权限。 |
| <xref:LaunchDesk.OperationContracts.OperationExecutionLease> | 原进程一次准入后的运行时权利；不可序列化／分享，释放后不能报告事实。 |
| SecretLease | 短时秘密所有权，用毕释放；配置仅存引用，日志／分享不存秘密。 |
| TargetLease | 写入时持有目标互斥和保护；路径文字不是目标身份或执行权。 |
| ResourceDescriptor | 资源 ID、修订、机器、端点、服务／VM 选择和策略。 |
| DescriptorDigest / IdentityKey | 内容摘要及上述身份＋策略版本；不同身份的证据不混用。 |
| ProbePolicy | 重试、超时、失败／恢复轮次与新鲜度规则。 |
| ProbeSnapshot | 一轮归约状态与分项证据，不能证明业务许可。 |
| RunSnapshot | 原操作的阶段、序号、原因码和脱敏消息，只展示已记录事实。 |
| OperationFact | 处理器报告，经上下文校验阶段，由日志分配递增序号。 |
| PlanDigest | 预览冻结来源、目标、修订及摘要；变化后重预览、重确认。 |
| Dispatcher 投影 | Bootstrap 订阅事实，在 WPF 线程更新状态，不另建业务状态机。 |

[三种回路](flows/index.md) · [完整模型定义](~/artifacts/docfx/navigation/models/index.md)
