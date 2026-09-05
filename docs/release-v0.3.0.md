# PmlTrigger.Yuzuha v0.3.0

## 中文

本次发布引入项目、官方、本地经验三种独立知识库，并修复双 MCP 注册失败可能留下半安装状态的问题。

- **可靠安装与升级**：两个 MCP 和 EVAR 全部预检；失败撤回本次新增配置，失败升级恢复旧安装。已有配置保持不变；撤销本身失败时保留部署文件并明确提示恢复需求。
- **三层本地知识库**：项目库随安装/升级刷新；官方 PMLLIB/PMLUI/WebHelp 从用户指定的本机目录索引，升级不修改；经验库独立追加、升级保留。支持跨库检索并标注来源。
- **保留本地数据**：升级保留官方库、经验库、旧库、函数信任记录和自定义 Host。
- **PDMS/AM**：保留传统 12.1 系列支持目标，当前参考程序集基线为 12.1.4.0。自定义 Legacy Profile 通过 YuzuhaFramework 明确选择 NET35。
- **双语文档**：[中文说明](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/v0.3.0/docs/v0.3.zh-CN.md)、[中文验证记录](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/v0.3.0/docs/v0.3-validation.zh-CN.md)，以及安装、Skill、构建等中英文资料。

已通过真实知识库二进制集成测试、Windows PowerShell 5.1 生命周期故障注入和包校验。**这是待 AVEVA 实机验收的预发布版本**，不承诺所有 PDMS/AM 历史小版本兼容。预置 Host 沿用 0.2.3；两个 MCP 为 0.3.0。

附件提供 Windows x64 安装包、源码包、双语发布说明和 SHA256 清单。数据库仅在本机生成，不随包发布或上传。升级请从新解压的安装包运行 Update-YuzuhaAgent.ps1，更新前关闭相关 MCP 和 AVEVA 进程。

## English

This release introduces separate project, official and local-experience knowledge databases and fixes partial installation after dual MCP registration failures.

- **Recoverable installation and upgrades:** preflight both MCPs and EVAR; roll back new registrations and restore failed upgrades while retaining reusable entries. If rollback itself fails, preserve deployed files and report recovery requirements.
- **Three local knowledge layers:** refresh the project index during installation/upgrades; index official PMLLIB/PMLUI/WebHelp from user-selected local paths without changing it during upgrades; append local lessons to a separate retained experience DB. Cross-database search identifies each source.
- **Preserved local state:** retain official, experience and legacy databases, function-trust records and custom Hosts.
- **PDMS/AM:** retain the legacy 12.1 support target with reference assembly baseline 12.1.4.0. Custom Legacy profiles select NET35 explicitly through YuzuhaFramework.
- **Bilingual documentation:** [English guide](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/v0.3.0/docs/v0.3.en.md), [validation record](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/v0.3.0/docs/v0.3-validation.md), and paired installation, Skill and build documentation.

Real knowledge-binary integration tests, Windows PowerShell 5.1 lifecycle fault-injection tests and package checks passed. **This is a prerelease pending live AVEVA acceptance**, not a compatibility guarantee for all historical PDMS/AM patches. Prebuilt Hosts are retained from 0.2.3; both MCPs are 0.3.0.

Assets include the Windows x64 agent, source archive, bilingual release notes and SHA256 checksums. Databases are generated locally and are neither shipped nor uploaded. Run Update-YuzuhaAgent.ps1 from a newly extracted package after closing the relevant MCP and AVEVA processes.
