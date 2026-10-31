# 部署：从来源选择到可核验回执

<div class="flow-sequence" aria-label="工具部署流程">

来源／预检 → 私有暂存与差异计划 → 明确确认 → 受理准入 → 写入／备份／回执 → 历史

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 来源 | <xref:LaunchDesk.Desktop.ToolPageViewModel> 刷新保留人工选择，仅 SelectLatest 换来源；<xref:LaunchDesk.UseCases.ToolSourceCatalog> 发现候选，<xref:LaunchDesk.Windows.PackageReader> 检查内容而不执行。 |
| 交付 | <xref:LaunchDesk.UseCases.DeploymentPlanner> 复查来源摘要、目标身份和控制根保护，暂存并保存差异及 PlanDigest。确认后提交 Deploy、修订和 ConfirmedDigest；<xref:LaunchDesk.UseCases.DeliveryOperationHandler> 再验证摘要及暂存，普通分支持目标租约调用 <xref:LaunchDesk.Windows.FileDeliveryPort>，管理员分支交 IPrivilegeBroker。 |

Worker 验同用户、双向 PID、启动时间、nonce、原请求和有效执行权；写前事实须父进程持久确认。来源／目标／修订变化及保护冲突需重预览。部分写入先读原回执，再 `PreviewRecoveryAsync` 产生新恢复／回滚计划并确认；取消授权独立记结果。

暂存存来源／计划，交付存检查点／备份／回执，日志存运行事实；完成、失败或需核验均刷新历史，二者分开展示。场景：AR12、AR13–17、AR14、AR18。[公共协议](index.md)
