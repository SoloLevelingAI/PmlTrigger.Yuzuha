# Changelog / 更新日志

## 0.3.0 — 2026-09-05 (local candidate / 本地候选)

- Pin SQLitePCLRaw bundle 2.1.13 (native SQLite 3.53.3 on Windows x64) after CI flagged the old transitive dependency; retain NuGet security auditing. 修复 CI 发现的 SQLite 旧依赖问题并保持安全审计开启。

- Preflight both MCP registrations and EVAR; roll back new entries on failure, including nonzero exits after configuration writes. Restore failed installations/updates.
- Preserve knowledge, trust and custom Profiles across updates. Refresh only the package project DB; retain official, experience and legacy DBs.
- Add register_knowledge_source, search_knowledge_layers and append-only/idempotent record_local_experience. Build replacement databases in staging.
- Retain PDMS/AM 12.1 support with verified local SDK file baseline 12.1.4.0; use explicit YuzuhaFramework for custom Legacy profiles. Live AVEVA acceptance pending.
- 新增项目、官方、经验三库隔离与跨库检索；更新只刷新项目库；修复双 MCP 半安装和自定义 Legacy Profile 的框架选择。
- See docs/v0.3.zh-CN.md and docs/v0.3.en.md for scope, migration and verification.


## 中文

### 0.2.3 — 2026-09-04

- 安装目录名保护：lifecycle 安装/更新现在校验安装目录名必须包含
  `PmlTrigger`（引导函数 `!folderName = 'PMLTRI'` 匹配 PMLUI 路径；Win11
  8.3 短名 `PMLTRI~1` 仍可命中）。若用户坚持自定义目录名，安装器按
  `-BootstrapFolderToken`（默认取目录名前 6 位字母数字）改写
  `YuzuhaResolveRuntimePath` 中的 `!folderName`，写入管理标记并明确
  提示风险（误匹配其他 PMLUI 条目、8.3 截断、更新需复用同一 token）。
- `YuzuhaResolveRuntimePath` 增加自诊断：未命中任何 PMLUI 路径时打印
  token 与 PMLUI 值，并提示保留 `PmlTrigger` 目录名或修正 EVAR。
- 新增本地编译能力 `scripts/Build-LocalHost.ps1`：AVEVA 版本与预置
  Profile（AM/PDMS/E3D2.1/E3D3.1.0/E3D3.1.6）不匹配时，可依据本机
  `PMLNet.dll` 与 Utilities 程序集推导家族（E3D→net48、AM/PDMS→net35），
  编译任意命名的新 Profile 并输出到包内 `runtime\profiles`；仅编译
  NET48/NET35 Host（与 AVEVA 版本强相关），Net10 MCP/知识库服务器与
  AVEVA 版本无关、不做本地重编译；skill 新增
  `references/local-build.md`，要求先告知用户、征得同意并说明风险。
- 新增 `YuzuhaToolkit.Knowledge`（.NET 10 Native AOT + SQLite/FTS5 独立
  stdio MCP，注册名 `YuzuhaToolkitKnowledge`）：从本机 PMLLIB/PMLUI 与
  WebHelp 目录语法切片建库（`sources`/`semantic_chunks`/`call_refs`/
  `chunks_fts`，兼容 pml_knowledge_proto 的 Python 原型），提供
  `build/search/check/list/chunk` 五个工具；数据库仅在本机生成，安装
  复制阶段跳过 `knowledge` 目录，杜绝 AVEVA 衍生内容随包分发。skill
  新增 `references/knowledge-base.md`：无库或已过期时必须先询问用户
  （本机重建 / 从他人复制并校验 / 暂不建库），不同 `dbName` 隔离数据。
- 执行失败分诊与函数信任：`PML RPC failed:` 传输失败附加"不代表函数
  不可信"的分诊说明；`run_pml_command`/`run_pml_command_list` 对被标记
  函数返回 `FunctionTrustWarning`；新增 `list_pml_function_trust` 与
  `set_pml_function_trust`（untrusted 需用户确认错误答案后才写入，
  trusted/remove 仅按用户明确指示执行），状态持久化于
  `<install>\trust\pml-function-trust.json`。skill 明确：连接失败先查
  会话/主机并询问用户，方法未找到先怀疑"编辑中/未加载"，禁止凭本地
  记忆替换可能只存在于开发环境的函数。
- `Register-YuzuhaMcp.ps1` 重构为可复用的注册检查，同一轮内按相同冲突
  规则注册两个 MCP；卸载时一并移除知识库 MCP。

### 0.2.1 — 2026-09-02

- 将 D 盘发布源中的完整 PMLUI 同步回公共源码树。
- 更新 Design、Catalog、Draft 和 Isodraft 的 Yuzuha Add-in 定义，补充
  `object`、`directory` 与统一的启动入口。
- 补充 PMLUI `version.dat`，确保源码包与安装包内容一致。
- 完整同步 D 盘 PMLLIB：使用 `!!YuzuhaRpc` 和 `!!YuzuhaExecuter` 对象，
  将旧 `.pmlcmd` 命令移入 `Obsolete`，并更新 `pml.index`。

### 0.2.0 — 2026-09-02

- 增加 AVEVA 多会话和多模块支持，可发现并区分 Design、Paragon 等窗口。
- 增加 AM、PDMS、E3D 2.1、E3D 3.1.0 和 E3D 3.1.6 Profile，按产品选择
  NET35 或 NET48 PMLNet Host。
- 通过 EVAR 自定义变量 `Yuzuha` 选择运行时 Profile。
- 放宽会话发现：所有标题包含 `AVEVA` 的可见窗口都可成为候选，不再逐个硬编码
  原厂产品标题；选择时继续验证 PID 专用管道、进程启动时间和 Host 身份。
- 使用按 PID 隔离的 Named Pipe，支持同时运行多个 AVEVA 会话。
- 增加安全的 Agent 安装、更新和卸载流程，包含管理标记、MCP 冲突检查和失败回滚。
- 更新后应完全重启 AVEVA，并执行 `PML REHASH ALL`。

### 0.1.0-preview.4 — 2026-08-26

- 将可选 BOX 示例定义恢复到 `PMLLIB/Examples` 并加入 `pml.index`，使 AVEVA
  能发现这些定义，但 Addin 启动时不会自动执行。

### 0.1.0-preview.3 — 2026-08-26

- 发布经过清理的 Net10 MCP 与 Net48 PMLNet Host 源码。
- 提供 CE、DBREF 和 PML 全局对象图读取。
- 提供明确授权后的 PML 命令和宏文件执行。
- 增加结构化数组规范化与汇总输出。
- 从公共源码树移除构建输出、本机配置、备份和 AVEVA 专有程序集。
- 增加本机可信边界说明和可复现 Release 打包。

## English

### 0.2.3 — 2026-09-04

- Install folder name protection: install/update now requires the install
  folder to contain `PmlTrigger` (the bootstrap matches `!folderName =
  'PMLTRI'` against the PMLUI path; Windows 8.3 short names such as
  `PMLTRI~1` still match). If the user insists on a custom folder name, the
  installer rewrites `!folderName` in `YuzuhaResolveRuntimePath` from
  `-BootstrapFolderToken` (by default the first six letters/digits of the
  folder name), records the token in the management marker, and prints an
  explicit risk warning (wrong-path collisions, 8.3 truncation, and token
  reuse on update).
- `YuzuhaResolveRuntimePath` gains self-diagnostics: when no PMLUI path
  matches, it prints the token and the PMLUI value instead of failing
  silently.
- Local build capability `scripts/Build-LocalHost.ps1`: when an AVEVA version
  has no prebuilt profile (AM/PDMS/E3D2.1/E3D3.1.0/E3D3.1.6), derive the
  family from the local `PMLNet.dll` and utilities assembly
  (E3D→net48, AM/PDMS→net35) and compile an arbitrarily named profile into
  the package `runtime\profiles`. Only the NET48/NET35 host is built locally
  (it is the only AVEVA-version-dependent component); the Net10 MCP and
  knowledge servers are never rebuilt. The skill adds
  `references/local-build.md`: inform the user, ask for consent, and state
  the risks first.
- New `YuzuhaToolkit.Knowledge` (.NET 10 Native AOT + SQLite/FTS5 stdio MCP
  server, registered as `YuzuhaToolkitKnowledge`): builds a knowledge base
  locally from the machine's PMLLIB/PMLUI and WebHelp directories with
  syntax-aware chunking (`sources`/`semantic_chunks`/`call_refs`/
  `chunks_fts`, compatible with the pml_knowledge_proto Python prototype)
  and exposes five tools (`build`/`search`/`check`/`list`/`chunk`). The
  database exists only on the machine that builds it, the lifecycle skips
  any `knowledge` directory when copying a package, and AVEVA-derived
  content never ships. The skill adds `references/knowledge-base.md`: when
  no database exists or one is stale, ask the user first (rebuild locally /
  copy from someone else and validate / skip), with distinct `dbName`
  values keeping data sets apart.
- Execution failure triage and function trust: `PML RPC failed:` transport
  failures now carry a note that they do not prove a function wrong;
  `run_pml_command`/`run_pml_command_list` return `FunctionTrustWarning`
  for flagged functions; new `list_pml_function_trust` and
  `set_pml_function_trust` tools (untrusted requires a user-confirmed wrong
  answer; trusted/remove follow explicit user instruction only), persisted
  in `<install>\trust\pml-function-trust.json`. The skill now states: on
  connection failure check the session/host and ask the user, treat
  method-not-found as "being edited or not loaded yet", and never substitute
  a remembered function that may exist only in a development environment.
- `Register-YuzuhaMcp.ps1` is refactored into a reusable registration check
  so both MCP servers register under the same conflict rules in one run;
  uninstall removes the knowledge MCP as well.

### 0.2.1 — 2026-09-02

- Synchronize the complete PMLUI from the D-drive release source back into
  the public source tree.
- Update the Yuzuha Add-in definitions for Design, Catalog, Draft, and
  Isodraft with `object`, `directory`, and the common startup entry point.
- Add the PMLUI `version.dat` so source archives and installer packages carry
  the same PMLUI payload.
- Fully synchronize the D-drive PMLLIB: use the `!!YuzuhaRpc` and
  `!!YuzuhaExecuter` objects, move legacy `.pmlcmd` commands under `Obsolete`,
  and update `pml.index`.

### 0.2.0 — 2026-09-02

- Add multi-session and multi-module AVEVA discovery, including windows such
  as Design and Paragon.
- Add profiles for AM, PDMS, E3D 2.1, E3D 3.1.0, and E3D 3.1.6, selecting a
  NET35 or NET48 PMLNet host for each product family.
- Select the runtime profile through the custom `Yuzuha` EVAR variable.
- Broaden discovery so every visible window whose title contains `AVEVA` can
  become a candidate, while selection still verifies the PID-bound pipe,
  process start time, and host identity.
- Use one PID-bound named pipe per AVEVA session to support simultaneous
  applications safely.
- Add managed Agent install, update, and uninstall flows with markers, MCP
  conflict checks, and rollback on failed updates.
- After updating, fully restart AVEVA and run `PML REHASH ALL`.

### 0.1.0-preview.4 — 2026-08-26

- Restore optional BOX example definitions to `PMLLIB/Examples` and include
  them in `pml.index`, so AVEVA can discover them without executing them at
  Addin startup.

### 0.1.0-preview.3 — 2026-08-26

- Publish the sanitized Net10 MCP and Net48 PMLNet host source.
- Provide CE, DBREF, and PML global-object graph readers.
- Provide explicit PML command and macro-file execution.
- Add structured list normalization and summary output.
- Remove build output, local configuration, backups, and proprietary AVEVA
  assemblies from the public source tree.
- Add local-trust security guidance and reproducible Release packaging.
