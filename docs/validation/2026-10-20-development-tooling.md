# CLI / MCP、完整文档与架构验证 · 2026-10-20

目标基线：`SoloLevelingAI/PmlTrigger.Yuzuha` 的 `04617bf572a637d63509762041108a011ecdc9f3`。参考 `SoloLevelingAI/LaunchDesk` 的 `8f1f71dd07c2981683c8fb88a311abb6504b6029`，适配独立 DevHost、DocFX 主题、完整项目/模块浏览与架构守卫。基线由 GitHub 源码归档取得；本地生成补丁所用提交不是上游提交。

## 同程序双入口

两个服务均默认启动 MCP stdio，也接受显式 `--mcp`。终端入口 `--cli list / call / batch` 使用同一可执行文件的一个 MCP 子进程，通过真实工具注册与参数结构复用业务逻辑。batch 共用一条连接，首次业务、协议或传输失败即停止，保留先前结果；取消或超时不会重试。

执行服务 14 个工具、知识服务 8 个工具。新增 `markdb`、`endmarkdb`、`inspect_registered_pmllib`、`get_ai_runtime_status`、`get_recent_tool_calls`。原生 PML 执行使用相关开发任务的连续授权，不逐条重复审批。Native AOT smoke 对比 CLI、无参数 MCP、显式 MCP 的完整工具与 JSON Schema，验证 JSON stdout、批处理失败停止、无 SQLite 的内置查询、未选目标时不执行 MARKDB，以及持久化失败日志。原有 smoke 另验证内置优先级、隔离知识源和检索数量边界。

## 文档与注释

- 十个生产项目全部纳入项目/模块浏览和架构清单，包含新增 Workspace 与 Windows WPF Desktop。
- 六个 .NET 10 项目生成 DocFX API；WPF 桌面独立编译和截图检查，手写源码纳入索引。Host 和安装器明确标记为源码解析，未使用假 SDK 或编译桩。
- 十个功能模块；类型、成员与页面数量由覆盖报告计算。现有中文/英文使用、安装、维护指南及全部 36 个 PML/PMLUI 文件纳入站点，历史文件明确标记。
- 生产项目公开 XML 文档启用严格编译诊断；Roslyn 检查类型和公开、内部、受保护成员摘要，缺失数为零。私有成员可浏览，未声称所有私有步骤均有说明。
- 与基线比较 Roslyn token、预处理指令及禁用分支文本；行为变更限定于服务入口、原生执行策略说明、执行工具描述和会话日志目标读取，其余既有 C# 变更为注释。新项目和工具单独列出。
- HTTP OpenAPI 包含四个真实路由、具名 DTO、XML 摘要与输入错误响应。导出服务器地址使用 `/`。两个服务链接同一内置指南源文件，DevHost 的 OpenAPI XML 输入保留其中一份，避免重复文档 ID；两个服务的完整 XML 仍用于 DocFX。
- 文档构建检查项目集合、类型归属、相对链接、页面 ID 与类型目标。主题支持系统字体、颜色、密度、全站搜索、折叠成员深链接、分页与手机导航，资源从本机提供。

## 验证命令

```powershell
npm ci
npm test
npm run docs:build
npm run docs:serve
./scripts/Build-NativeServers.ps1 -OutputRoot artifacts/native
python -X utf8 tests/cli_smoke.py artifacts/native
python -X utf8 tests/v03/builtin_smoke.py artifacts/native
```

测试包含 60 项行为、HTTP、工作区和 CLI 集成测试，以及 11 项 ArchUnitNET / xUnit 架构测试，共 71 项通过。新增目录预算、部分扫描、开发产物跳过、路径越界、ZIP 展开、复制前并发修改、覆盖前备份、启动参数、秘密脱敏、日志轮转、进程退出待核验、日志失败及新工具实际调用检查。架构覆盖准确项目引用、无循环、六个 .NET 10 程序集依赖方向、开发预览无业务写入依赖和真实 IL 负例。CI 执行测试、文档、Native AOT smoke 及 Windows 工作台发布。

## 原生工作台与本机目录实查

WPF 工作台整合启动配置、插件复制、目录提醒与工具状态。普通启动、复制与观察不需要 Python；现有测试与维护脚本保留。窗口关闭时等待复制事务结束，避免中断恢复；不会自动启动工程、复制插件或执行 REHASH。

从 E3D 2.1 的 EVAR 读取到注册目录 `%LOCALAPPDATA%/YuzuhaToolkit/PmlTrigger.Yuzuha/PMLLIB`，只读扫描完成：31 个文件、27 个 PML 文件、82,317 字节，未达到当前提醒预算。扫描日志与截图使用隔离的本机状态目录；未更改该注册目录或 EVAR。

交付包含 TRX、文档完整性报告、原生构建/smoke 日志与浏览器结果。补丁实际应用到原始归档后逐文件核对字节；未推送 GitHub。

## 验证边界

本次编译并测试两个 Windows x64 Native AOT 服务与自包含 WPF 工作台；未执行真实工程启动、AVEVA 模型操作、MARKDB/ENDMARKDB 实机联测、安装升级或专有 SDK 下的 Host 构建。NET35、NET48 和安装器纳入项目引用与源码检查，未纳入 .NET 10 IL 规则。当前已安装客户端和 Host 未被覆盖；使用新 MCP 程序需要切换实际客户端入口并重启。原有各版本实机验证记录保持各自范围。

原仓库八个 PML 文件与历史 Semantic 指纹不一致：`PMLLIB/pml.index`、`Addins/YuzuhaAddin.pmlobj`、`Addins/YuzuhaRpc.pmlobj`、`Examples/YuzuhaBoxExample.pmlobj`、`Examples/YuzuhaExcuter.pmlobj`、`Obsolete/YuzuhaRpcCommand.pmlcmd.obsolote`、`Obsolete/YuzuhaTriggerCommand.pmlcmd.obsolete`、`Traversal/YuzuhaObjectWalker.pmlobj`。这些文件与原始归档逐字节一致，保留原有差异；仅更新本次已审阅修改文件对应的指纹。

文档入口：`http://127.0.0.1:5276/docs/index.html`；实时 Scalar：`http://127.0.0.1:5276/scalar/v1`。开发宿主提供指南和计算预览；工程执行仍走 MCP 会话选择与身份核验。
