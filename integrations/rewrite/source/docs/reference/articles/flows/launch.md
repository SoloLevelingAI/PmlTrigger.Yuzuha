# 启动：从点击到运行事实

<div class="flow-sequence" aria-label="启动流程">

选择登录 → 冻结请求／受理准入 → 资源与计划 → 秘密／进程 → 事实 → Dispatcher

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 受理 | <xref:LaunchDesk.Desktop.ProfilesPageViewModel> 的 `LaunchSelectedAsync` 提交 ProfileId、OperationId、预期修订；<xref:LaunchDesk.UseCases.OperationCoordinator> 核对摘要及本机绑定，冻结 LaunchConfiguration；<xref:LaunchDesk.Windows.JsonRunJournal> 保存请求、所有者和阶段事实于 ControlRoot/operations。 |
| 执行 | <xref:LaunchDesk.UseCases.LaunchOperationHandler> 用 `RequireFreshAsync` 复查冻结资源，交给 <xref:LaunchDesk.UseCases.LaunchPlanBuilder> 构造独立子环境；需密码时解析 SecretLease，先记启动意图，<xref:LaunchDesk.Windows.WindowsProcessGateway> 创建一次进程后及时释放秘密并观察。参数顺序及 Marine 交接限制见[核心启动规则](../core-launch.md)。 |

Checking→HandedOff→WindowObserved 按公共回路回页面。只有 SessionConfirmed 才确认会话，否则观察期满为 Unconfirmed。修订冲突或本机应用／资源未绑定，执行前拒绝；启动后记录失败进入 StartNeedsVerification，不再开一次。秘密不进日志。

核验场景：AR05、F01、F04、AR11；编号、取消与返回回路见[公共协议](index.md)。
