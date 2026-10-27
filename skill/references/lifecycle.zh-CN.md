# 安装与更新

使用正式 Windows 安装器及包内 INSTALL.md；源码补充 ZIP 不能代替安装器。旧 PowerShell MCP 安装、更新、卸载、注册链已删除。

发现实际安装→生成具体预览→核对目标与哈希→按已批准范围执行。保留用户知识目录与备份；旧安装必须另做迁移计划，不覆盖混装。

当前入口为 runtime/cli/PmlTwin.Phayton.Cli.exe。AVEVA 接入、移除旧 MCP 与 Skill 更新均纳入安装预览；独立 MCP 保持禁用。用 op list / op describe 查询原生契约，不以恢复 stdio 注册排错。
