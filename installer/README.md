# Phayton Windows Setup v0.3.4.2

0.3.4.2 当前构建入口交付原生 CLI、正式启动配置工作台、默认用户说明与本地只读文档宿主。完整 API、源码、测试与 UI 追踪放在独立开发参考 ZIP；这次文档分层是新候选的源码行为，不代称已安装版本已更新。CLI 直接调用已有底层库及 Host，不经过独立 MCP 进程；新包排除旧 runtime/net10 MCP 可执行文件。NET35/NET48 Host 继续按产品单独交付。Inno Setup + NET48 helper 负责安装，程序不分发安装 PS1。

人工和 AI 默认先安装应用，随后由管理器准备环境、EVARS和部署；`integrationMode: manager`与明确仅文件的none分开。旧显式aveva计划继续兼容。安装和后续管理写入分别预览批准；向导不提供MCP注册。源码构建与安装实机验证分别记录。

完整流程见 [INSTALL.md](INSTALL.md)，实际测试见 [VALIDATION.md](VALIDATION.md)。

## 使用指南与可选开发参考

默认 `runtime/documentation` 只包含任务指南、独立导航和搜索；工作台的“使用指南”打开它。目录必须有 `documentation-profile.json`：schema 为 `phayton.user-guide/1`，逐文件记录路径、字节数与 SHA256，并绑定同修订开发 ZIP。构建器拒绝仅有首页的完整开发站、API/源码/测试/UItrace 目录、source map，以及超过40个HTML或15MiB的用户站。根 `docs` 源目录不再整树复制进安装载荷。

`Phayton-<版本>-Developer-Reference.zip` 与 Setup、Agent ZIP并列交付并进入发行 `SHA256SUMS.txt`，不会嵌套进安装载荷或 Agent ZIP。它保留完整 `site/` 与 `source-docs/`，根 `developer-reference.json` 绑定源码提交、版本和逐文件摘要。手工核对ZIP摘要并解压到独立目录后，在工作台“开发参考”选择包含此清单的包根；只读宿主显式挂载 `/reference/`，默认指南仍在 `/docs/`。未选择、错误修订或文件漂移时明确说明，不下载、不自动解压，也不在设置中保存选择。

文档分层不清理旧安装留下的历史文档。升级继续按新的冻结清单写入，任何额外清理需另行预览；新载荷缩小不等于现有安装已回收相同磁盘空间。内置指南仍嵌入程序集，Manager HTML与自己的指南资产随Manager CLI发布，不依赖根 `docs` 的副本。

## 边界

- 保留原赋值、派生目录、短路径转换和自定义调用；在末尾追加三项配置，更新受管块不重复添加。未知目标写入、提前退出或不明确跳转要求审查，不执行环境文件。
- CLI-only：不合并 mcpServers JSON，不改写 Codex TOML；Skill 可单独安装。
- 已有未托管Skill默认拒绝；高级`adoptSkill:true`明确接管包内目标，冻结完整有界目录和安装所有权，保留首次原文与额外文件，具体预览批准后才执行。
- 升级中的生成索引只补 `/Bootstrap/YuzuhaStartupPolicy.pmlobj` 缺失项，列入预览、备份和同一事务；不分发或重建 `pml.index`，不将它纳入长期所有权校验。
- 旧 PS1/preview1 或非受管目录不能直接覆盖迁移；请用 VM 新目录。
- 保留原编码及搜索路径；报告与备份位于 LocalAppData/YuzuhaToolkit/SetupReports。
- 不启动 AVEVA、不执行 PML、不保存模型，不递归删除用户数据。
- 恢复为尽力恢复，不是断电级事务。中文覆盖主要流程，少数系统错误英文回退。

## Build / test

Use a fresh output directory, .NET SDK/net48 and Inno Setup 6.7.3:

```text
python installer/build.py --iscc "C:/Tools/Inno Setup 6/ISCC.exe" --output "C:/Builds/PhaytonSetup" --version 0.3.4.2 --runtime "C:/Builds/RuntimeInputs" --cli "C:/Builds/NativeCliPublish" --manager "C:/Builds/ManagerCliPublish" --desktop "C:/Builds/DesktopPublish" --documentation "C:/Builds/UserGuide" --developer-reference "C:/Builds/Phayton-0.3.4.2-Developer-Reference.zip" --documentation-host "C:/Builds/DocHostPublish"
dotnet run --project installer/tests/SetupTests.csproj -c Release
python installer/tests/build_cli_test.py
python installer/tests/verify_delivery.py "C:/Builds/PhaytonSetup"
python installer/tests/verify_agent_bundle.py "C:/Builds/PhaytonSetup/Phayton-0.3.4.2-Windows-Agent.zip" "C:/Builds/PhaytonSetup/Phayton-0.3.4.2-Windows-Setup.exe"
```

Builder hashes every supplied payload file and excludes legacy MCP servers, PS1, proprietary references and local databases. Supply an independently validated Native AOT CLI publish directory; --version controls manifest, Inno metadata, EXE and archive filenames. `--documentation` requires the bounded user profile, and `--developer-reference` requires its matching ZIP/manifest/source identity. Developer inventory limits match the shared validator: 8MiB manifest, 12000 files, 512MiB total and 128MiB per file. The Agent ZIP includes the EXE, helper, Skill, request template, instructions and small documentation identity profile; the developer corpus remains a separate download.

`--runtime` selects independent profile/webapp inputs so packaging does not overwrite
a development or installed runtime. It requires all four compiled profile outputs;
when omitted, the historical repository `runtime` directory remains the source.
Reused binaries need verified provenance; their prior execution does not validate
the rebuilt package. Manager CLI, Desktop and documentation host inputs are
published separately for the current source.

NET48 fixtures keep transactions in the ordinary temporary directory. Optional
`YUZUHA_SETUP_TEST_ROOT` selects another isolated transaction root. If a Windows
sandbox blocks junction creation from a NET48 child, prepare a real junction in
an isolated workspace root, set `YUZUHA_SETUP_LINK_TEST_ROOT` and
`YUZUHA_SETUP_LINK_JUNCTION` to it, and verify its ReparsePoint attribute first.
The mandatory link test moves only that fixture entry into its fresh Skill,
checks preview/execution refusal, and moves it back; it never skips link checks.

Authored by OpenAI Codex at the maintainer's request. The previous 0.3.4.1 desktop and media evidence is recorded in docs/validation/2026-10-24-window-portfolio.md; it is not proof that a new 0.3.4.2 installer has been executed. Historical installer evidence remains in VALIDATION.md and docs/validation/2026-10-23-phayton-r2-production.md. Installation smoke scripts change machine registration and must only run in an approved disposable environment.

The historical approved local installation, E3D 2.1 configuration, Codex migration and installed-program checks are recorded in [formal installation validation at its retained source revision](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/f2e038531b3221c30ded6980ee8d87a900386e10/docs/validation/2026-10-24-formal-installation.md), including the frozen package hash and the auxiliary registry-version fix for that rebuild. These historical reports are also retained in the separate developer source-document archive; their links do not require a default installed `docs` source tree.
