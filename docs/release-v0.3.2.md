# v0.3.2 — 连续界面记录 / Session UI recording

本次新增 C# 截图/日志实现、自动化测试与实测报告由 OpenAI Codex 按维护者要求编写；原有 PML 控件监控原型由维护者提供。本版本延续预发布状态，并保留 v0.3.1 的 Native AOT、内置指南、可选 SQLite 和通用客户端注册功能。

- 原 NET35/NET48 Host 增加 BeginLog / EndLog / Log：完整 PML Array 与操作前后截图写入同一个会话 JSONL。
- JPEG 覆盖 AVEVA 主界面及同进程浮动窗体；连续相同截图复用。默认位置为 `%LOCALAPPDATA%\YuzuhaToolkit\Records\<session>\session.jsonl`，图片位于同目录 images。
- Semantic 元数据集中在 docs/semantic，一一对应源文件，记录 UTC 修改时间和备用 SHA256。
- E3D 2.1 实机会话记录、NET35/NET48 自动化测试已通过。详见[实测报告](validation/2026-09-19-e3d.md)。维护者确认两条直接查询正常；Codex 的 MCP 调用失败不代表 PML 函数有缺陷。

包版本及 Host DLL 版本为 0.3.2 / 0.3.2.0。NET10 服务器原样沿用已发布 v0.3.1 Native AOT 二进制及其原始版本和 SHA256 清单，没有本地重新编译服务器。五个标准 Host Profile 从本次源码构建；构建通过不等同于全部 AVEVA 版本实机验证。

升级前完全退出 AVEVA，从解压包运行原有更新脚本；更新后重启 AVEVA 和 AI 客户端。本次发布不自动覆盖正在运行的本机安装。不含 AVEVA 专有引用 DLL、模型数据、原始截图或日志。

English: Codex authored the new recorder, tests and report; the maintainer supplied the existing PML monitoring prototype. This prerelease adds paired full-UI JPEG capture and append-only session JSONL to NET35/NET48 hosts, with centralized UTC/SHA256 metadata. E3D 2.1 live recording and both framework smoke tests passed; see the report for exact coverage and maintainer-confirmed query results. Unchanged v0.3.1 Native AOT server binaries retain their original manifest/version. Close AVEVA before updating and restart AVEVA and the AI client afterwards.
