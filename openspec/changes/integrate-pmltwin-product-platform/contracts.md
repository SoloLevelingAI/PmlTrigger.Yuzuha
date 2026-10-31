# 契约草案 v0.1

状态：planned，供实施细化；下列名称不是已经注册的 CLI operation ID。既有 46/52 项操作目录保持不变。本文件不修改 canonical contract.json。

| 契约 | 最小字段/责任 | 失败与兼容要求 |
|---|---|---|
| FeatureDescriptor | id、version、contractVersion、owner、requires、operations、entryKind、enabled、implementationStatus | 重复 ID/版本不兼容拒绝；注册、启用、授权分别表示 |
| OperationDescriptor | id、inputSchema、outputSchema、effect、requiredCapabilities、timeout、cancellation、retryPolicy | 旧 ID/schema/退出码由适配器保留；新动作必须在目录显式注册 |
| DiscoveryRequest | roots、product/version filters、time/depth/file budgets、refresh | 只读；不执行 BAT、不读取秘密、不默认全盘无限扫描 |
| DiscoveryResult | candidates、evidenceSource、observedAt、expiresAt、coverage、diagnostics | unavailable/ambiguous/incomplete 可区分；静态与运行时证据分开 |
| ResolvedField | value、origin、userOverride、validation、alternatives、needsInput | 人工修改优先；无交互调用不弹窗 |
| OperationPlan | operationId、featureId、targetIdentity、expectedRevision、inputDigest、source/target hashes、effects、grants、expiresAt | 过期/变更拒绝；预览没有副作用；秘密不入摘要素材或日志 |
| OperationReceipt | operationId、attemptId、requestId、state、phase、code、message、evidence、recoveryActions、rollbackStatus | success/failed/cancelled/unknown 区分；未知动作不可盲重放 |
| DatabaseSession | scope、schemaVersion、readOnly、transaction、expectedRevision | 一致快照、并发冲突、只读降级；不泄漏数据库连接给 UI |
| DataScope | product、version、workspace、kind、identity | 知识与用户数据分层；凭据只存引用，不能按同名自动授权 |
| MigrationPlan | sourceIdentity、formatVersion、manifest/hash、mapping、backup、validation、cutover | 旧数据保留；缺项逐条诊断；不激活导入配置 |
| DesignHandoff | featureId、nativeFile、frameIds、tokenVersion、operations、states、evidence/status | 按钮到契约一一映射；设计不等于运行证据 |

## 操作族与所有者

| 操作族（候选逻辑名称） | 所有者 | 首期处理 |
|---|---|---|
| feature describe/list | Host composition | 统一 Addin/worker 描述，具体 CAF 延后适配 |
| environment discover/resolve/check | Environment service | 汇聚现有扫描器，提供来源与 needsInput |
| launch plan/execute/observe | Launch use case | 融合 rewrite 协调与 Next 发现，保留 Yuzuha CLI 兼容 |
| configuration edit/share/import/migrate | Configuration use case | 接 rewrite 契约；秘密剔除，机器绑定重新验证 |
| tools preview/apply/recover | Deployment use case | 复用现有部署事务、计划和受控 Worker |
| environment variables plan/apply | Environment use case | 区分用户/机器变量和厂商 EVARS，冻结作用域 |
| health inspect/watch | Resource use case | 只读监测不触发自修复，后台预算可控 |
| ribbon document/history/validate/export | Ribbon use case | 保留 HTTP v1、定义/引用与增量撤销 |
| uic preview/file-apply/registration-apply | UIC deployment | 分离内容与加载清单，保留摘要与备份 |
| function register/list/permission/invoke | Communication adapter + local registry | 管理 CLI 统一入口仍待补；本地注册不证明 Host 已加载 |
| knowledge import/check/search | Knowledge use case | 复用既有产品/版本隔离，两套分别验收 |
| operation observe/recover | Application coordinator | 按原编号核对；不可跨 Feature 任意回滚 |

## 非秘密验证配置

优先样例项目标识：`APS`，来源为用户 2026-10-31 指示。先扫描产品/版本/入口/模块和可访问性，再使用本机受控凭据渠道登录。用户名、口令、凭据引用不得进入作品集；口令不得写入本提案、命令行参数、日志或截图。若运行依赖尚未满足，返回 blocked/unavailable 及具体原因，不标实机验证通过。
