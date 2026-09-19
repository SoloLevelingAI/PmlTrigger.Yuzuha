# PmlTrigger.Yuzuha v0.3.1

## 中文

本版让内置 PmlTrigger 能力在没有 SQLite 的情况下可用，并修复其他 AI 客户端注册与安装资料不一致的问题。

- 两个 NET10 MCP 服务均为 Native AOT；目标机器无需安装 .NET 10 SDK/Runtime。NET35/NET48 AVEVA Host 保持原样。
- 新增 `get_builtin_usage`，返回完整的内置说明。“尝试在 PDMS 中查询当前元素”优先定位 `YuzuhaReadCurrentElement`。除非用户明确选择替代方法，补充索引不能覆盖内置方法。
- SQLite 为可选的机械切分/全文检索模块。安装和升级不自动建库，已有官方、自定义、经验与信任数据保留。
- 通用 `-McpJsonPath` 注册支持两个 stdio 服务，不要求 Codex。完整预检后才提交配置；预览不写文件，冲突不覆盖，EVAR 失败恢复配置。无 Codex 时先通过 `-SkipMcpRegistration` 部署，再注册到实际客户端。
- 通过 `Get-ItemProperty` 或 `reg query` 查询安装信息。E3D 使用有效的 `Evar.INIT`；PDMS/AM 确认没有 INIT 时才回退 BAT。批处理语法的 INIT 可传给 `-EvarBat`，原文件自动备份，托管块置于初始化之后；E3D/net48 使用 `pmlui`，AM/PDMS/net35 使用 `pdmsui`。权限不足时报告，不修改其他环境。
- 安装包、源码包与内嵌使用指南同步生成；包内逐文件校验和、AOT 程序哈希清单及附件 SHA256 一致。

验证涵盖发布后的 MCP 工具调用、空 SQLite 下内置指南、补充索引隔离、安装/更新回滚、通用注册及合成 NET35/NET48 协议交互。**本版仍为待真实 AVEVA 环境验收的预发布**，不宣称已通过所有产品版本的模型操作验收。

升级请从新解压的安装包运行 `scripts/Update-YuzuhaAgent.ps1`，提前关闭相关 MCP/AVEVA 进程。默认本机 MCP 注册不修改 EVAR。

**安装或升级后必须完全退出并重启 AI 客户端，仅新建聊天不够。**

## English

Built-in PmlTrigger usage now works without SQLite. Both NET10 stdio servers are Native AOT; existing NET35/NET48 AVEVA Hosts are retained. `get_builtin_usage` returns complete release-maintained guides, with built-in methods taking precedence unless the user explicitly chooses a replacement. Optional FTS5 references no longer block installation or trigger automatic indexing.

Generic `-McpJsonPath` registration plans both servers before writing, preserves unrelated configuration, honors preview, rejects conflicting entries and restores configuration if EVAR modification fails. Codex is not required: deploy with `-SkipMcpRegistration`, then register the installed executables in the actual client's configuration.

Discover AVEVA installations with `Get-ItemProperty` or `reg query`. Prefer the active INIT file (E3D uses INIT); only fall back to BAT for PDMS/AM when INIT is absent. Batch-style INIT is supported by the batch writer with backup and a trailing managed block. net48 uses `pmlui`; net35 uses `pdmsui`. Report permission failures rather than editing another environment.

Published-tool, optional-index, lifecycle, generic-registration and synthetic NET35/NET48 RPC tests passed. **Prerelease pending live AVEVA acceptance.** Package/source/checksums are synchronized. No local databases are distributed. Default local MCP setup does not modify EVAR.

**Fully exit and restart the AI client after installation or upgrade.**
