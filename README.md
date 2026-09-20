# PmlTrigger.Yuzuha

面向 AVEVA E3D、PDMS 和 AM 的本机 AI/PML 工具：连接工程会话、查询与执行 PML、检索本地资料，并记录窗体操作过程。

A local AI/PML toolkit for AVEVA E3D, PDMS and AM: connect to engineering sessions, query and execute PML, retrieve local references, and record form interactions.

[最新正式版 v0.3.2 / Latest stable](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/releases/tag/v0.3.2) · [中文](#中文) · [English](#english)

## 中文

### 核心能力

| 能力 | 已提供的功能 |
| --- | --- |
| 会话连接 | 发现多个 AVEVA 会话，显式选择 PID，核验进程启动时间、专用管道和 Host 身份 |
| PML 查询与执行 | 生成带类型参数的调用；查询当前元素、指定 DBREF 和全局对象；在 AVEVA 主线程执行授权命令 |
| 内置使用指南 | 通过 `get_builtin_usage` 获取随版本维护的说明；无需先建立 SQLite 知识库 |
| 本地资料检索 | 可选的 PMLLIB/PMLUI/WebHelp 切分与全文检索；项目、官方资料和本地经验分库管理 |
| 经验与信任管理 | 追加用户确认的经验；维护函数信任记录，避免把传输失败当成函数缺陷 |
| Host 操作记录 | 完整 PML Array、操作前后截图、连续会话 JSONL、相同截图复用 |
| 安装与升级 | 通用 MCP 客户端注册、冲突预检与失败回滚；升级保留知识库、信任记录和自定义 Profile |
| 源文件校验 | `docs/semantic` 集中保存一一对应的 UTC 修改时间及 SHA256 元数据 |

### 从 0.3 开始更新了什么

- **0.3.0：本地知识与可靠升级。** 增加知识库服务、资料切分、分层检索、经验持久化和函数信任管理；改进升级数据保留、部分注册失败回滚及自定义 Framework 选择。
- **0.3.1：部署与调用入口。** 两个 NET10 MCP 服务统一 Native AOT，目标机器无需安装 .NET 10 Runtime；增加内置指南，SQLite 改为可选；完善非 Codex 客户端注册、INIT/BAT 配置和协议测试。
- **0.3.2：Host 新功能。** 增加连续会话日志、主界面与浮动窗体截图、操作前后配对、截图复用和 Semantic 校验，并完成 E3D 2.1 操作记录实测。

详细记录：[0.3.0](docs/release-v0.3.0.md) · [0.3.1](docs/release-v0.3.1.md) · [0.3.2](docs/release-v0.3.2.md)。

### 快速开始

1. 下载 Release 的 `agent-win-x64.zip` 并解压；安装目录名保留 `PmlTrigger`。
2. 按[安装说明](AGENT-INSTALL.zh-CN.md)部署并注册 MCP。支持通用 `mcpServers` JSON 配置，不要求使用 Codex。
3. 按[环境配置](docs/aveva-discovery.md)选择 AVEVA Profile；默认仅注册本机 MCP 不修改 EVAR。
4. 完全退出并重启 AI 客户端；修改 Host 或 AVEVA 环境后，也要完全重启 AVEVA。
5. 让 AI 获取内置指南、列出会话，再明确选择目标 PID，确认身份后执行查询。

可从这些请求开始：

- “列出 AVEVA 会话，先不要执行 PML。”
- “读取当前元素，先用 30 项、深度 2。”
- “检索我已经注册的本机 PML 资料。”

安装、更新和卸载是三个独立操作，请按需选择，不要依次全部执行：

```powershell
.\scripts\Install-YuzuhaAgent.ps1
.\scripts\Update-YuzuhaAgent.ps1
.\scripts\Uninstall-YuzuhaAgent.ps1
```

升级前关闭相关 MCP/AVEVA 进程，从新解压的包执行更新。PML 文件或路径变更后，在重新启动的 AVEVA 中执行 `PML REHASH ALL`。

### Host 日志与截图

NET35/NET48 Host 提供：

- `BeginLog(!record)`：保存操作前 Array 和截图，返回记录 ID。
- `EndLog(!record, !recordId)`：保存操作后记录及截图，与同一 ID 配对。
- `Log(!record)`：保存一次 Array 与截图。

日志连续写入 `%LOCALAPPDATA%\YuzuhaToolkit\Records\<session>\session.jsonl`，JPEG 位于同目录 `images`。截图覆盖可见的 AVEVA 主界面及同进程浮动窗体，不是只截当前控件。连续相同截图复用。

PML 监控代码需要调用这些接口；并非所有 AI 调用或按钮都会自动被记录。数组支持范围、返回值检查和错误处理见[记录接口说明](docs/session-recording.md)。

### AI 调用日志：当前边界

**Host 操作记录不等于 AI 工具调用审计。**

- NET10 MCP 目前提供 stderr 诊断输出，调用响应中可包含 RequestId、状态和错误信息；尚未实现覆盖每次工具调用、参数、耗时和结果的统一持久化会话日志。
- 当前仓库未提供独立的通用 PML 执行 CLI。知识服务提供 `--refresh-project` 维护入口，另有安装/注册脚本；这些入口没有统一的调用审计日志。
- 外部 AI 客户端或其 CLI 自行保存的历史不属于 Yuzuha 的日志保证。
- Host 日志可用于后续 AI 分析，但尚未提供自动关联 MCP 请求、截图与分析结论的完整流程。

### 支持与验证

预置配置：AM、PDMS 使用 NET35；E3D 2.1、3.1.0、3.1.6 使用 NET48。自定义 Profile 明确设置 `Yuzuha` 和 `YuzuhaFramework`。

E3D 2.1 的会话操作记录已实测；NET35/NET48 独立记录测试及五个标准 Host 配置构建已通过。**构建通过不代表所有产品版本均已实机验证**；AM/PDMS、其他 E3D 版本及多屏混合 DPI 的覆盖范围见[实测报告](docs/validation/2026-09-19-e3d.md)。

执行工具可以修改活动模型，只应执行明确授权的命令。超时后不要自动重试：第一次调用可能已经执行。查询失败也不能直接判定 PML 函数有缺陷。

### 数据与开发

知识库在本机按需建立，不随发行包分发；官方资料建库需要明确指定来源。截图和日志可能包含工程信息，分享前请检查内容。AVEVA 专有 SDK 不随仓库或 Release 分发。

```text
PMLLIB/   PML 启动、查询、命令和窗体监控
PMLUI/    AVEVA Addin 注册
src/      NET10 MCP 与 NET35/NET48 Host 源码
scripts/  构建、安装、更新、注册和校验
skill/    AI 使用规范与工具参考
docs/     功能、部署、验证及 semantic 元数据
tests/    自动化测试与实测宏
```

开发构建参见[构建配置](docs/build-configuration.md)；缺少适配配置时参见[Host 本地构建](skill/references/local-build.zh-CN.md)。

## English

### Capabilities

- Discover AVEVA sessions and explicitly select a PID; verify process identity and the PID-bound Host.
- Generate typed PML calls, read the current element, named DBREFs and global objects, and execute authorized commands on the AVEVA main thread.
- Retrieve release-maintained built-in guides without creating a SQLite database.
- Optionally index local PMLLIB/PMLUI/WebHelp; keep project, official and experience sources separate.
- Persist user-confirmed experience and function trust records.
- Record complete PML arrays and paired main-UI/floating-window screenshots through NET35/NET48 Host APIs.
- Register with generic MCP clients, detect conflicts, roll back supported failures and preserve local state during upgrades.
- Verify source metadata centrally stored under `docs/semantic` using UTC modification times and SHA256.

### The 0.3 series

**0.3.0** added local knowledge infrastructure, layered retrieval, persistent experience, function trust management and recoverable deployment. **0.3.1** unified both NET10 services on Native AOT, added built-in guides, made SQLite optional and improved generic client registration and AVEVA environment configuration. **0.3.2** added Host session logging, paired UI capture, image reuse and semantic verification, with live E3D 2.1 recording checks.

See [0.3.0](docs/release-v0.3.0.md), [0.3.1](docs/release-v0.3.1.md) and [0.3.2](docs/release-v0.3.2.md) release notes.

### Getting started

Download and extract the release agent archive. Follow [installation instructions](AGENT-INSTALL.md), [client setup](docs/ai-client-setup.md) and [environment configuration](docs/aveva-discovery.md). Retain `PmlTrigger` in the installation directory name. Codex is not required; generic MCP JSON registration is supported. MCP-only setup does not modify EVAR by default.

Fully restart the AI client after installation/update and AVEVA after Host/environment changes. Start with built-in guides, list sessions, explicitly select a target, then request a small read. Close affected processes before updating from a freshly extracted archive. After PML changes, restart AVEVA and execute `PML REHASH ALL`.

### Recording and audit boundaries

Host `BeginLog` / `EndLog` pair before/after records; `Log` captures a single record. Session JSONL and JPEGs are stored under `%LOCALAPPDATA%\YuzuhaToolkit\Records\<session>`. Visible main and same-process floating windows are included; consecutive identical images are reused. PML monitoring must invoke the APIs—recording is not automatically enabled for every button or AI call. See the [API guide](docs/session-recording.md).

**Persistent AI tool-call auditing is not implemented.** NET10 MCP has stderr diagnostics and response metadata, not a unified on-disk history of every tool's arguments, duration and outcome. There is no standalone general-purpose PML execution CLI in this repository; the Knowledge service's `--refresh-project` maintenance entry and deployment scripts do not supply a unified audit log. External AI-client/CLI history is outside Yuzuha's logging guarantees. Automatic correlation of MCP requests, screenshots and AI analysis is not yet provided.

### Compatibility and safety

Standard profiles target AM/PDMS NET35 and E3D 2.1/3.1.0/3.1.6 NET48. E3D 2.1 recording was live-tested; independent NET35/NET48 recorder tests and five standard Host builds passed. This does not establish live compatibility with every product version or multi-monitor/DPI configuration. See the [validation report](docs/validation/2026-09-19-e3d.md).

Execute only explicitly authorized commands. Do not automatically retry timeouts or mistake transport errors for defective PML functions. Local reference databases and proprietary AVEVA SDKs are not distributed. Review screenshots/logs for engineering information before sharing.

## License / 许可证

Project source is licensed under [Apache-2.0](LICENSE); bundled dependencies retain their own licenses. See [THIRD-PARTY.md](THIRD-PARTY.md).

项目源码采用 Apache-2.0；随附依赖保留各自许可证。本次 Host 记录实现、测试和实测报告由 OpenAI Codex 按维护者要求编写；PML 控件监控原型由维护者提供。
