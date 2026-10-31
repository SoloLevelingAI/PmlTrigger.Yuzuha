# 分享与迁移：两条不同的提交路径

<div class="flow-sequence" aria-label="分享导入流程">

选择非秘密定义 → 导出／读取 → 只读预览 → 本机确认 → CommitId → 原回执／刷新

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 分享 | <xref:LaunchDesk.Desktop.SharingPageViewModel> 经 <xref:LaunchDesk.UseCases.ConfigurationShareService> 导出明确子集；<xref:LaunchDesk.OperationContracts.IConfigurationDocuments> 读写 ShareBundle，不执行文件内容。`PreviewImportAsync` 只预览，确认后 `CommitAsync` 冻结 ImportProposal 与预期修订。配置存储保留回执，未知结果查原 CommitId，确定后再读目录。入口／资源须本机核验，密码和凭据引用不随文件照搬；导入不等于可立即启动。 |

<div class="flow-sequence" aria-label="旧数据迁移流程">

旧来源／摘要预览 → 确认 → OperationId 受理准入 → 独立副本准备／校验／激活 → 事实／刷新

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 迁移 | <xref:LaunchDesk.Desktop.SettingsPageViewModel> 经 IMigrationPlanningService 冻结 SourceSha256；<xref:LaunchDesk.UseCases.WorkspaceMigrationService> 调 <xref:LaunchDesk.Windows.JsonMigrationStore> 的 Prepare／Validate／Activate，按预期修订启用副本、保留原文件。迁移资料归该适配器，事实归日志；IMigrationQueries 按原 OperationId 核验，不重执行。来源摘要或目标修订变化须重预览。 |

场景：AR19、AR22、F05。[公共协议](index.md)
