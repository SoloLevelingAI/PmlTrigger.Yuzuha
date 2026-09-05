# PmlTrigger.Yuzuha

[中文](#中文) | [English](#english)

## 中文

### v0.3.0

项目/官方/本地经验三库隔离；双 MCP 注册失败回滚；升级保留本地数据；自定义 Legacy Profile 明确选择 NET35。
详见 [0.3 中文说明](docs/v0.3.zh-CN.md)。本候选包仍待 AVEVA 实机验收。


PmlTrigger.Yuzuha 是面向 AVEVA 系列工程应用的本机 Agent/PML 桥接工具。
自包含的 .NET 10 MCP 进程通过按 AVEVA PID 隔离的 Named Pipe，与运行在
AVEVA 主线程内的 NET35 或 NET48 PMLNet Host 通信。

> 当前候选版本为 v0.3.0。执行型工具可以直接修改活动模型，仅应由可信本机用户在
> 明确授权后使用。执行超时后禁止自动重试，因为第一次调用可能已经成功。

### v0.2 重点更新

- 支持多个同时运行的 AVEVA 会话，可发现并区分 Design、Paragon 等模块窗口。
- 支持 AM、PDMS、E3D 2.1、E3D 3.1.0 和 E3D 3.1.6 对应的 NET35/NET48 Host。
- 通过 EVAR 自定义变量 `Yuzuha` 选择运行时 Profile。
- 会话发现接受所有标题包含 `AVEVA` 的可见窗口，不再维护逐产品标题白名单。
- 真正连接前仍校验 PID 专用管道、进程启动时间和 Host 身份。
- 提供带管理标记、冲突检查和失败回滚的安装、更新及卸载脚本。

### v0.2.3 新增

- 安装目录名保护：安装目录必须包含 `PmlTrigger`（Win11 8.3 短名
  `PMLTRI~1` 仍可匹配）；确需自定义目录名时，安装器改写引导函数中的
  目录 token 并明确提示风险。
- 新增独立知识库服务器 `YuzuhaToolkitKnowledge`（.NET 10 Native AOT +
  SQLite/FTS5）：从本机 PMLLIB/PMLUI 与 WebHelp 语法切片建库、确定性
  检索；数据库仅在本机生成，绝不随包分发。
- 执行失败分诊与函数信任列表：传输失败不再被误判为函数不可用；经用户
  确认的错误答案才写入不可信列表，修复/删除按用户明确指示处理。
- `scripts/Build-LocalHost.ps1`：AVEVA 版本无预置 Profile 时，仅本地
  编译 NET48/NET35 Host（Net10 服务器永不本地重编译）。
- 全部文档提供中英双语（`.zh-CN.md` 为中文版，供作者审阅；英文版用于
  国际化）。

### 功能

- 生成带类型参数的 PML 全局方法调用文本，不连接 AVEVA。
- 发现本机可见的 AVEVA 会话，并由用户显式选择返回的 PID。
- 在 AVEVA 主线程执行一条经过明确授权的 PML 命令。
- 将 CE、DBREF 或 PML 全局对象图读取为结构化 JSON。
- 通过无副作用的连接状态检查验证 PID、启动时间、管道和模块。

### 目录

```text
PMLLIB/   PML 启动、遍历、命令和示例定义
PMLUI/    AVEVA 模块 Addin 注册
src/      .NET 10 MCP、NET35 与 NET48 Host 源码
scripts/  Profile 构建及 Agent 安装/更新/卸载脚本
skill/    Codex Skill 和工具参考资料
docs/     构建、部署和 PML API 文档
```

### 构建

```powershell
.\scripts\Build-AvevaProfiles.ps1 -ProfileRoot 'D:\AVEVA\AvevaProfile'
```

AVEVA SDK 不随仓库或 Release 分发。NET35/NET48 Host 必须使用用户已经安装并
获得许可的本机 AVEVA SDK 构建。

### 安装、更新与卸载

从 Release 解压安装包后运行：

```powershell
.\scripts\Install-YuzuhaAgent.ps1
.\scripts\Update-YuzuhaAgent.ps1
.\scripts\Uninstall-YuzuhaAgent.ps1
```

更新或修改 PML 路径后，完全退出 AVEVA，重新启动并执行：

```pml
PML REHASH ALL
```

详细规则见 [AGENT-INSTALL.zh-CN.md](AGENT-INSTALL.zh-CN.md)（中文）/
[AGENT-INSTALL.md](AGENT-INSTALL.md)（英文）和
[部署文档 deployment.zh-CN.md](skill/references/deployment.zh-CN.md)（中文）/
[deployment guide](skill/references/deployment.md)（英文）。

## English

### v0.3.0

Independent project/official/experience databases, rollback for dual MCP registration, preserved local state, and explicit Legacy framework selection.
See [0.3 release notes](docs/v0.3.en.md). Live AVEVA acceptance is pending.


PmlTrigger.Yuzuha is a local Agent-to-PML bridge for AVEVA engineering
applications. A self-contained .NET 10 MCP process communicates over a
PID-bound named pipe with a NET35 or NET48 PMLNet host running on the AVEVA
main thread.

> The current candidate version is v0.3.0. Execution tools can directly modify the active
> model. Use them only after an explicit request from a trusted local user.
> Never automatically retry a timed-out execution because the first call may
> already have completed.

### What is new in v0.2

- Discover and select among multiple simultaneous AVEVA sessions, including
  module windows such as Design and Paragon.
- Support profile-specific NET35/NET48 hosts for AM, PDMS, E3D 2.1,
  E3D 3.1.0, and E3D 3.1.6.
- Select the runtime profile through the custom `Yuzuha` EVAR variable.
- Discover any visible window whose title contains `AVEVA`, without a
  product-by-product title allowlist.
- Continue to verify the PID-bound pipe, process start time, and host identity
  before establishing a real connection.
- Provide managed install, update, and uninstall scripts with markers,
  conflict detection, and rollback on failed updates.

### What is new in v0.2.3

- Install folder name protection: the installation folder must contain
  `PmlTrigger` (Windows 8.3 short names such as `PMLTRI~1` still match);
  with an explicitly required custom folder name the installer rewrites the
  bootstrap folder token and prints a risk warning.
- A separate knowledge server `YuzuhaToolkitKnowledge` (.NET 10 Native AOT +
  SQLite/FTS5): builds a local knowledge base from this machine's
  PMLLIB/PMLUI and WebHelp with syntax-aware chunking and deterministic
  retrieval; the database exists only on the machine that builds it and
  never ships.
- Execution failure triage and a function trust list: transport failures are
  no longer mistaken for a broken function; only user-confirmed wrong
  answers enter the untrusted list, and fixes or removals follow explicit
  user instruction.
- `scripts/Build-LocalHost.ps1`: when an AVEVA version has no prebuilt
  profile, only the NET48/NET35 host is compiled locally (the Net10 servers
  are never rebuilt locally).
- All documentation is bilingual (`.zh-CN.md` files are the Chinese versions
  reviewed by the author; the English files serve international readers).

### Features

- Build typed PML global-method calls without connecting to AVEVA.
- Discover visible local AVEVA sessions and explicitly select a returned PID.
- Execute one explicitly authorized PML command on the AVEVA main thread.
- Read CE, DBREF, and PML global-object graphs as structured JSON.
- Verify PID, process start time, pipe, and module through a side-effect-free
  connection-status check.

### Repository layout

```text
PMLLIB/   PML bootstrap, traversal, command, and example definitions
PMLUI/    Addin registration for AVEVA modules
src/      .NET 10 MCP plus NET35 and NET48 host source
scripts/  Profile build and managed install/update/uninstall scripts
skill/    Codex skill and tool reference
docs/     Build, deployment, and PML API documentation
```

### Build

```powershell
.\scripts\Build-AvevaProfiles.ps1 -ProfileRoot 'D:\AVEVA\AvevaProfile'
```

The AVEVA SDK is not distributed in this repository or its releases. Build
the NET35/NET48 hosts against the user's licensed local AVEVA installation.

### Install, update, and uninstall

Extract a Release archive and run:

```powershell
.\scripts\Install-YuzuhaAgent.ps1
.\scripts\Update-YuzuhaAgent.ps1
.\scripts\Uninstall-YuzuhaAgent.ps1
```

After an update or PML-path change, fully restart AVEVA and run:

```pml
PML REHASH ALL
```

See [AGENT-INSTALL.md](AGENT-INSTALL.md) and the
[deployment guide](skill/references/deployment.md) for details
(Chinese versions: [AGENT-INSTALL.zh-CN.md](AGENT-INSTALL.zh-CN.md),
[deployment.zh-CN.md](skill/references/deployment.zh-CN.md)).

## License / 许可证

Project source is licensed under the [Apache License 2.0](LICENSE). Bundled
dependencies retain their own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md).

项目源码采用 Apache-2.0 许可证；随附依赖保留各自许可证，详见
[THIRD-PARTY.md](THIRD-PARTY.md)。
