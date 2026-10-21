# PMLTWIN v0.4 第一阶段验证记录

日期：2026-10-21（Asia/Shanghai）。开发基线：`codex/yuzuha-native-workspace` / `5d73e1dc5d1dc648f57cef16771938830a87671b`。工作分支：`codex/pmltwin-v04-phase1`。

本机结果针对本次修改的工作区执行，执行时 `dirty=true`，不能冒充基线提交已包含或已验证这些新增功能。提交后的 CI 会在独立报告中记录检出的提交 SHA、环境、SDK、时间和运行地址；本记录不预报 CI 状态。

环境：Windows build 26200 x64，.NET SDK `10.0.400-preview.0.26322.102` / Runtime `10.0.9`，PowerShell 7，Node.js `24.18.1`。Native AOT 使用 MSVC `14.51.36231`、Windows SDK `10.0.26100.0`；Python 使用本机提供的实际解释器，通过 `YUZUHA_TEST_PYTHON` 指定。

| 本次检查 | 结果 | 范围与证据 |
| --- | --- | --- |
| xUnit 产品与真实 MCP / CLI 回归 | 79 passed，0 failed，0 skipped | `artifacts/tests/development_*.trx`，`run-metadata.json`；包括新增注册权限、数组预算和真实 MCP 拒绝顺序 |
| ArchUnitNET 与架构守卫 | 11 passed，0 failed，0 skipped | 同目录 TRX；开发 HTTP 处理器不得依赖新的注册与执行入口 |
| 文档关联与 Roslyn 自动发现 | 7 passed | Node 测试；失效关联、歧义方法、重复 ID、共享前缀、真实源码范围和无效 C# |
| TRX 身份与结果检查 | 1 个有效夹具接受，4 个错误夹具拒绝 | 正数量、准确方法、全部 Passed、没有 skipped；零执行不能通过 |
| 按 ID 执行真实 Theory | 2 数据行 passed | `xunit:YuzuhaToolkit.Tests.BackendBehaviorTests.PmlNumbersUseInvariantCulture` |
| Native AOT CLI / MCP | passed | `smoke:native-cli`；两个发布后的实际可执行文件，19 / 8 个工具，CLI 与默认/显式 MCP 的工具与 schema 一致；注册权限、源码改变和数组入口拒绝行为 |
| Native AOT 无数据库内置指南 | passed | `smoke:native-builtin`；内置优先级、中文路由、隔离的合成检索资料 |
| NET35 与 NET48 RPC 兼容 | 两套 passed | `smoke:rpc-net35` / `smoke:rpc-net48`；Native AOT 客户端与复用生产 DTO 的 Framework 夹具，包含窗口与续读元数据和旧 Host 拒绝 |
| NET35 与 NET48 记录器 | 两套 passed | `smoke:formrecord-net35` / `smoke:formrecord-net48`；CLR 2.0.50727.9188 / 4.0.30319.42000，合成数组、配对、测试窗口像素和本机文件 |
| NET35 Host 编译 | passed，0 warning / error | 显式 AM profile，使用已安装 Marine 12.1.SP4 引用；没有部署到活动 AVEVA |
| NET48 Host 编译 | passed | 显式 E3D2.1 profile，使用本机 E3D 2.1 引用；没有部署到活动 AVEVA |

完整 DocFX 构建通过 `scripts/Build-Documentation.ps1` 验证 OpenAPI、LikeC4、源码摘要、真实测试声明、API/PML 目标和生成页面链接；具体统计写入本机 `artifacts/docfx/integrity.json`，最终结果同时列在 PR 验证摘要。生成目录不入 Git。

每个独立测试 ID 的证据保存在执行器打印的 `artifacts/dt/<ID哈希>/<运行编号>/`。`documented-run.json` 包含提交、dirty、源码哈希、环境、UTC 时间、执行数与相对证据路径。记录器截图、活动日志、知识库、用户工程和安装路径清单均不上传。CI 仅上传明确列出的离线结果文件及静态文档。

## 未运行的实机验收

`manual:formuse-get-gadget`、`manual:formuse-get-gadget-type`、`manual:formuse-decorated-call`、`manual:formuse-gadget-decorated`、`manual:formuse-choose-form`、`manual:aveva-bounded-array` 全部保持 **NotRun / 未实测**。按 `tests/manual/formuse.md` 在明确的产品、版本、Host 和测试工程中执行。离线算法、Framework 协议及引用编译不能证明真实 AVEVA 行为；9 月历史报告不能成为本次通过。

## 基线指纹差异

额外执行 `scripts/Verify-Semantic.ps1 -Mode Hash`，发现 17 项既有文件与历史指纹不匹配：12 项安装器文件和 5 项 PML 对象。Git 确认这些文件本次均无内容改动；可能涉及既有内容修订或检出字节差异，未进一步归因。本次只更新实际修改的两个 Host 项目指纹，并保留历史证据边界；没有通过重写原生文件或历史指纹掩盖差异。此额外检查仍报告失败，留待单独维护。
