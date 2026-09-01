# Changelog / 更新日志

## 中文

### 0.2.1 — 2026-09-02

- 将 D 盘发布源中的完整 PMLUI 同步回公共源码树。
- 更新 Design、Catalog、Draft 和 Isodraft 的 Yuzuha Add-in 定义，补充
  `object`、`directory` 与统一的启动入口。
- 补充 PMLUI `version.dat`，确保源码包与安装包内容一致。

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

### 0.2.1 — 2026-09-02

- Synchronize the complete PMLUI from the D-drive release source back into
  the public source tree.
- Update the Yuzuha Add-in definitions for Design, Catalog, Draft, and
  Isodraft with `object`, `directory`, and the common startup entry point.
- Add the PMLUI `version.dat` so source archives and installer packages carry
  the same PMLUI payload.

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
