# 设计检查与实际运行记录

日期：2026-10-19；architecture/archon；implementationApproved=false。可进入人工架构审阅，不代表实现或行为验收通过。

## 原生运行

固定 Archon v0.12.1 源码、Bun 1.4.2、Codex CLI 0.163.0-alpha.10.2。使用隔离 SQLite、现有 Codex 登录和显式只读收紧补丁；认证检查未禁用，没有复制认证文件。版本和哈希见 tools/archon/runtime-provenance.json。

| 运行 | 实际结果 |
| --- | --- |
| ec42ac4a-2167-4fd2-91d4-085b7d6aef7c | 前五个模型节点完成；save-revision 因合并脚本缺陷失败 |
| 8b32f133-0d10-4e7c-acfd-ea34ed974513 | 原生 --adopt；修复合并，recheck 模型节点完成；save-recheck 误将新增 minor 判成不能审阅而失败 |
| 17f392ea-0cf9-4760-bc69-b8dc73a72c6d | 原生 --adopt；确定性归档，无新模型调用；最终 paused / human-review / approval，未批准 |

更早的 files 后端尝试被原生凭据预检拒绝，随后采用官方 SQLite。早期运行 66469b01-b59e-47f3-bebc-95244dfbf636 在 scope 完成后为修复脚本主动停止，原生 abandon 标记 cancelled，没有冒充完成。模型连接的 responseStreamDisconnected 重试警告保留。

原生状态、节点记录及三个来源清单在 runs/17f392ea-0cf9-4760-bc69-b8dc73a72c6d。提供者没有报告可用的实际模型标识，未把本机配置推断成已观测值，也未虚构费用。初次 1 项 blocking、4 项 major 的契约修订均获复核接受；新增 F06 minor 单独说明对齐，原模型结果保留，未声称再次模型复核。

## 已执行的检查

- 原生 CLI 对完整审阅工作流资源验证：0 errors / 0 warnings；原始 JSON 随证据保存。
- npm run review：六个工作流通过固定官方 schema 和本地资源、DAG、终点检查；七种工作流故意违规被拒绝，含可视化跳过证据和插入批准步骤。
- 初稿静态基线：14 项需求、27 场景、13 逻辑角色；五种依赖、覆盖和批准违规被拒绝。
- 初稿类级基线：39 类、22 接口、27 场景；七种接口、页面/平台、生命周期、环与批准违规被拒绝。这不是新增修订类型的程序集测试。
- 20 项本地脚本夹具检查通过：Windows 大 JSON、缺失或重复 ID、未知依赖、先新增后修订、值对象、显式 minor、虚假 readiness。结果明确 modelCalled=false。
- 只读补丁从官方原始 ZIP 文件到目标 SHA256 的转换、重复执行保持原状均已验证。
- npm run evidence:check：六个模型阶段完成、最终暂停、产物哈希和问题/修订/复核关联通过。导出时另核对四份输入未变。
- 当前审阅目录：46 类、26 接口、10 值对象；原始合并结果与 F06 后续勘误分开保存。

共同需求 SHA256：24eff27ae334871da78c9440d078fb0ef35efc10b9313c97cfe18ef8aaefc98b。LikeC4 aspire-like / aspire 保持 24b5d94 / 53a7ac2；OpenSpec 保持 c962a80；应用基线为 3ffd635。

## 新增 Archon → LikeC4 可视化检查

- 当前机器可读契约投影为 13 模块、82 类型、14 项需求、27 场景；19 个无模块字段的新增类型以显式展示建议归类，原始产物不变。
- 16 项投影正反例、LikeC4 语法/引用/布局检查和静态站点构建通过；依赖关系和场景间接调用分开处理。
- Playwright 实际检查 25 个非空嵌入图及 6 个分组下钻图、27 场景、14 需求、19 个资料链接，以及筛选、意见持久保存/导出和 1280 / 960 / 680 宽度无横向溢出。结果与截图见 visualization/previews。
- 原生可视化运行 4d4454ff-c281-48cb-b57b-0d178b3fb3ca：五个确定性 exec 节点完成，零模型调用；原架构审阅暂停状态不变。失败首次尝试、Bun 运行时发现警告及脏工作目录来源均保留在 visualization/evidence。

以上仅为审阅工具的检查。

## 尚未覆盖

没有生成或编译重写 C# / XAML，没有运行重写应用、程序集依赖、WPF 焦点/视觉、崩溃注入、真实许可或 VM 测试。既有图稿在 docs/design，但未进入本次模型输入。结构化修订是审阅产物，不能直接替换初稿 class-contracts.json 后声称已通过原有全部类级规则。应用实现及平台行为验收须在用户批准具体设计版本后开展。
