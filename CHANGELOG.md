# Changelog / 更新日志

## 中文

### 0.1.0-preview.6 — 2026-08-30

- 将 .NET 10 MCP 切换到真正的 `PlantHost.Rpc.Net10` AOT 客户端，移除
  `DispatchProxy` 依赖，并通过 Native AOT 启动烟雾测试。
- 增加 2 秒心跳、连接状态、延迟、连续失败次数和最后错误报告。
- 将 PML 命令统一为 `!!YuzuhaTriggerCommand`，保留改名、属性修改和宏文件工作流。
- 增加 CE、DBREF、全局对象图读取以及 BOX、站点盘管演示资料。
- 更新中英双语 Agent 文档和 Skill，并修正 Skill 更新时的目录嵌套问题。
- 提供复制安装、旧版本备份、Release 校验及 SHA-256 清单。
- 统一源码、程序集、安装器、ZIP 与 GitHub 标签版本，并增加跨电脑安装模拟、ZIP
  解压复验和完整文件哈希校验。

## English

### 0.1.0-preview.6 — 2026-08-30

- Switch the .NET 10 MCP to the native-AOT-safe `PlantHost.Rpc.Net10` client,
  remove the `DispatchProxy` dependency, and verify the published executable
  with a startup smoke test.
- Add a two-second heartbeat with connection state, latency, consecutive
  failure count, and last-error reporting.
- Standardize PML workflows on `!!YuzuhaTriggerCommand` for rename, attribute
  mutation, and macro-file execution.
- Add CE, DBREF, and global-object traversal plus BOX and site spiral examples.
- Refresh the bilingual agent documentation and Skill, including safe Skill
  replacement without nested directories.
- Provide copy-mode installation, previous-version backup, release validation,
  and SHA-256 manifests.
- Align the source, assemblies, installer, ZIP, and GitHub tag versions, and
  add cross-machine installation simulation, extracted-ZIP verification, and
  complete file-hash validation.
