# PML 开发授权、自动 MARKDB 与 CLI 验证

本批对应 [PR #10](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/pull/10)；需求与长期状态见开发文档 `docs/reference/pml-development-plan.md`，云端继续工作约束见根目录 `AGENTS.md`。代码位于 `codex/pml-development-workflow`，以已经合入 main 的 #3–#9 为基线，不修改客户 PMLLIB 或用户工程。

## 最终行为

- Conservative 只登记和管理配置，不派发 PML；Standard 核验逐函数许可；Development 绑定 PID、启动 ticks、模块、源码根目录、原因和有效期。项目规则规避只能指定 `raw-command` 或 `function-permission`。
- 统一执行桥在派发前重新核验身份、授权与源码；Host 以一个主线程 RPC 回调执行 MARKDB、正文、ENDMARKDB。建立失败不进入正文，结束失败保留正文信息；源码/授权明确拒绝报告未派发，缺少正文回执或分组回执记录未知，绝不自动重试。
- 只读豁免限定为有类型边界的已有数组/Form 查询，或针对准确源码哈希和已加载版本进行人工审阅的注册函数。重新登记撤销许可和只读分类。数组赋值与清理仍分组。
- CLI `--cli dev` 复用 MCP 工具，保存开发身份、限定文件写入、比较哈希后覆盖、运行宏快照、读取证据和筛选/导出日志。测试的 Dispatched、Passed、Failed、Unknown 与人工 Pending 分开记录；测试结果全局变量先重置，不能沿用旧通过值。
- WPF 设置页接入授权，工具日志页支持检索、PID/结果筛选及 JSONL 导出。导出每行一个完整 JSON 对象，不覆盖已有证据。

## 验证范围与证据

验证环境：Windows x64，.NET SDK 10.0.400；工具输出与本机截图留在忽略的 `artifacts/` 中，Git 保留源码、测试、文档和可复刻命令。

| 检查 | 结果与证据 |
| --- | --- |
| 开发解决方案 | Release 编译通过，0 警告/错误 |
| xUnit 与架构 | 完整开发检查通过；最终批次结果保存在 `artifacts/development-complete-validation.log` 与 `artifacts/tests/*.trx` |
| 授权与未知回执 | 保守/标准/开发、撤销、源码改变、到期/启动身份错配、旧 Host、分组建立/结束失败、丢失回执、任意派发后异常、JSONL 格式及覆盖拒绝；`DevelopmentExecutionTests` |
| 文档自动化 | 41 项 Node 测试通过；`artifacts/development-documentation-tests.log` |
| Phayton | 40 项操作/桥接回归通过；`artifacts/phayton-final-tests.log` |
| Native AOT CLI/MCP | 两个执行入口与知识入口发布；真实子进程检查工具目录一致、JSON stdout、失败停止、开发写入/哈希/证据/导出；`artifacts/development-native-cli-smoke.log`、`artifacts/development-native-operations.log` |
| NET35 / NET48 | Native AOT 客户端调用仓库实际 `PmlCommandRpcService`；AVEVA 依赖为合成。两套 CLR 均通过自动分组、失败开始/结束、源码变化与测试结果重置；`tests/AotRpcSmoke` |
| WPF | 14 页 × 2 主题 × 3 视口，含同画布与错误状态截图共 116 张；授权设置、日志筛选/导出、原配置库和查询合成流程通过；`artifacts/tests/development-workflow-desktop/desktop-smoke.json` |
| 源码与 DocFX | 注释/语法检查、API/功能/测试关联、Roslyn UI 调用链与完整站点检查通过；`artifacts/development-workflow-docs-final.log` |

本机 shell 缺少 MSBuild 的 `OS` 属性时，Native AOT 发布显式传入 `-p:OS=Windows_NT`；实际运行系统及 SDK RID 均为 Windows x64，没有跳过平台检查。共享 checkout 的 Git 检查使用仅限本进程的 `safe.directory` 配置，不改用户全局 Git 设置。

复刻入口：`scripts/Test-Development.ps1`、`scripts/Test-Desktop.ps1`、`scripts/Build-Documentation.ps1`、`dotnet run --project tests/Phayton -c Release`、`tests/cli_smoke.py` 和 `tests/AotRpcSmoke/run.py`。Windows CI 同时覆盖 Native AOT、两个 Framework RPC 夹具、WPF 和文档。

## 合并与实际验收边界

#7、#6 按叠加关系合入 #5；#4 合入 main 后，#5 处理 Core、JSON 元数据和文档检查差异，当前 SHA 的三组 CI 通过后合并为 `f99d147`。#3 更新相同集成基线，保留启动 REHASH 修复与原现场调查，当前 SHA 的三组 CI 通过后合并为 `5efe62a`。本批功能以独立 PR #10 供后续审阅。

AVEVA 实机为 **NotRun**。合成依赖测试不证明真实 SDK、主线程调度、数据库权限、宏语法或工程断言已经通过。`tests/live/development/arithmetic.pmlmac` 是待人工检查的实机示例。实际执行需要更新匹配产品的 NET35/48 Host、重启 AVEVA 并确认自动分组协议版本 1；旧预编译安装包不会因源码合并自动升级。CLI 仍通过 AVEVA 所在 Windows 主机的本机 PID Named Pipe 执行，未增加公网桥，没有自动 SAVEWORK，也未把机器测试状态登记为人工审阅完成。
