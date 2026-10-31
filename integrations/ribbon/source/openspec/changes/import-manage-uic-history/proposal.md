# Proposal

## Why

用户需要打开现有 YtoolsAM.uic 后编辑并重新部署，保留原来的 Ytools 绝对路径加载条目；还需要可靠区分部署器管理与系统原生条目，并在修改后持久记录、重新打开和复原。

## What Changes

- 导入现有普通 Macro 按钮 Ribbon，保留快捷键、额外属性及未编辑的 XML。
- 提供按名称、路径、来源和加载状态筛选的表格，系统保护项只读，自定义项明确接管后可管理。
- 接管保留已有编辑草稿，部署更新原 UIC 和原加载项，不另建重复哈希文件。
- 编辑自动保存持久化版本，保留原始字节、部署前后快照、恢复记录；可恢复到编辑器后继续修改或部署。
- 托管条目可以移除或恢复加载，保留 UIC 文件及历史；真实安装修改由用户选择和确认。

## Capabilities

### New Capabilities

- uic-import-roundtrip：导入与保留非编辑 XML 数据。
- uic-managed-inventory：条目筛选、来源保护、明确接管和原路径更新。
- uic-persistent-history：自动草稿、原始快照、部署记录和版本恢复。

### Modified Capabilities

无已归档能力。补充前两项进行中变更的工作流。

## Impact

新增 packages/uic-workspace，扩展生成核心、API、网页和部署绑定。持久数据在 data/uic-workspace，不依赖浏览器缓存。不得将路径或名称启发式当成厂商签名鉴定。AVEVA 实际加载和命令执行仍在验收范围之外。
