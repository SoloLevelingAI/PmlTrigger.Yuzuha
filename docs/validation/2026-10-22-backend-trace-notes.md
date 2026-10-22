# 后端溯源方向与迭代笔记验收

本批仅整理仓库文档。基线为 `7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3`，分支继续使用 `codex/pmltwin-v04-wpf-flows`，PR #6 保留草稿供人工审查。

## 修改范围

- 新增[迭代与意见记录](../reference/iteration-log.md)，保存 TRACE01 至 TRACE05 的原意、采纳决定、实现状态与验收条件；从 README、DocFX 首页及导航接入。
- 新增[后端调用链与排查](../reference/backend-tracing.md)，人工核对 Form 查询从业务入口、RPC 路由、NET35/NET48 Host、主线程到 FMSYS 的处理步骤，解释分页、预算、错误及回执编号。自动跨 Host 衔接明确保持待实现。
- 新增[理解与排查笔记](../reference/understanding-notes.md)，按问题选择输入输出表、算例、时序、决策表、决策记录等方法；5W2H、康奈尔笔记为可选示例，无固定优先级或强制模板。
- 更新 UI 溯源和文档维护说明：前端重点关注 Valid / Validate 所涉校验与状态，业务理解重点转向后端。不改生产接口、业务代码、原生 PML 或文档生成工具。

## 验证与本机证据

| 检查 | 结果与范围 | 本机证据 |
| --- | --- | --- |
| xUnit 与 ArchUnitNET | 142/142、11/11，无跳过；既有离线产品与架构断言 | `artifacts/iteration-notes-development.log`；`artifacts/tests/development_net10.0_20261022103728.trx`、`development_net10.0_20261022103847.trx` |
| 文档工具契约 | 既有 Node 检测 22/22；PowerShell 的精确身份、非零执行与 TRX 结果检测成功 | 同一开发验证日志；没有为静态文案增加镜像断言 |
| Native AOT Smoke | 生产源码未变，复用现有 Native AOT 二进制并重新运行 CLI/MCP、内置指南离线验证，成功；MCP 22、Knowledge 8 是工具目录数量 | `artifacts/iteration-notes-native-cli.log`、`artifacts/iteration-notes-native-builtin.log`；`artifacts/native-corrections/native-aot-manifest.json` |
| DocFX 完整构建 | 1277 页、22678 本地链接、129 测试入口，零警告或错误；全量构建及链接完整性检查通过 | `artifacts/iteration-notes-documentation.log`、`artifacts/docfx/integrity.json` |
| AVEVA 实机 | **NotRun**；本批不选择会话、执行 PML 或保存工程 | 人工复刻继续使用 `manual:aveva-form-query`，不由离线结果替代 |

测试结果来自提交前工作区，元数据记录基线提交、dirty=true、Windows 与 SDK 环境。不是当前提交的 CI 结果，也不作为后端完整链路或 AVEVA 实机已验收的凭证。原始输出留在本机 artifacts，不上传用户工程。
