# 变量：从改名预览到写后核验

<div class="flow-sequence" aria-label="变量应用流程">

新旧名称／值／作用域 → 预览 → 确认 → 受理准入 → 应用／检查点 → 写后核验

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 预览 | <xref:LaunchDesk.Desktop.ToolPageViewModel> 形成 VariableRenameRequest；<xref:LaunchDesk.UseCases.VariablePlanner> 校验名称、保留名和值，读取新旧名称现值，冻结 before/target 与 PlanDigest。 |
| 应用 | 确认持久作用域／管理员选项后，以 ApplyVariables 提交 <xref:LaunchDesk.UseCases.OperationCoordinator>。<xref:LaunchDesk.UseCases.VariableOperationHandler> 选择 <xref:LaunchDesk.Windows.WindowsEnvironmentStore> 或 <xref:LaunchDesk.Windows.WindowsPrivilegeBroker>；对照原值、保存改名检查点并回读实际结果，阶段按[公共回路](index.md)返回页面。 |

外部改值后重读重预览；部分改名保留检查点和原编号，失败不代表没有写入。取消管理员授权不能报成功。实值属于 Windows 作用域，检查点归适配器，事实归日志。Process 作用域只随子进程注入；保存工具定义不代表已应用变量。统一测试覆盖改名、外改、管理员和页面禁写注册表。
