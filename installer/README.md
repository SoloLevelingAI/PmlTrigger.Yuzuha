# Phayton Windows Setup v0.3.4.2

0.3.4.2 构建入口交付原生 CLI、正式启动配置工作台、DocFX 与本地只读文档宿主。CLI 直接调用已有底层库及 Host，不经过独立 MCP 进程；新包排除旧 runtime/net10 MCP 可执行文件。NET35/NET48 Host 继续按产品单独交付。Inno Setup + NET48 helper 负责安装，程序不分发安装 PS1。

人工和 AI 均先选择 AVEVA 环境与可选 CLI 使用 Skill，再生成具体预览并确认。向导不提供 MCP 注册；计划引擎拒绝非空 mcpJson 和旧 MCP 修改计划，保留现有客户端配置。源码构建与安装实机验证分别记录。

完整流程见 [INSTALL.md](INSTALL.md)，实际测试见 [VALIDATION.md](VALIDATION.md)。

## 边界

- 保留原赋值、派生目录、短路径转换和自定义调用；在末尾追加三项配置，更新受管块不重复添加。未知目标写入、提前退出或不明确跳转要求审查，不执行环境文件。
- CLI-only：不合并 mcpServers JSON，不改写 Codex TOML；Skill 可单独安装。
- 升级中的生成索引只补 `/Bootstrap/YuzuhaStartupPolicy.pmlobj` 缺失项，列入预览、备份和同一事务；不分发或重建 `pml.index`，不将它纳入长期所有权校验。
- 旧 PS1/preview1 或非受管目录不能直接覆盖迁移；请用 VM 新目录。
- 保留原编码及搜索路径；报告与备份位于 LocalAppData/YuzuhaToolkit/SetupReports。
- 不启动 AVEVA、不执行 PML、不保存模型，不递归删除用户数据。
- 恢复为尽力恢复，不是断电级事务。中文覆盖主要流程，少数系统错误英文回退。

## Build / test

Use a fresh output directory, .NET SDK/net48 and Inno Setup 6.7.3:

```text
python installer/build.py --iscc "C:/Tools/Inno Setup 6/ISCC.exe" --output "C:/Builds/PhaytonSetup" --version 0.3.4.2 --cli "C:/Builds/NativeCliPublish" --desktop "C:/Builds/DesktopPublish" --documentation "artifacts/docfx/site" --documentation-host "C:/Builds/DocHostPublish"
dotnet run --project installer/tests/SetupTests.csproj -c Release
python installer/tests/build_cli_test.py
python installer/tests/verify_delivery.py "C:/Builds/PhaytonSetup"
python installer/tests/verify_agent_bundle.py "C:/Builds/PhaytonSetup/Phayton-0.3.4.2-Windows-Agent.zip" "C:/Builds/PhaytonSetup/Phayton-0.3.4.2-Windows-Setup.exe"
```

Builder hashes every supplied payload file and excludes legacy MCP servers, PS1, proprietary references and local databases. Supply an independently validated Native AOT CLI publish directory; --version controls manifest, Inno metadata, EXE and Agent ZIP filenames. VM ZIP includes the EXE, helper, Skill, request template and instructions.

Authored by OpenAI Codex at the maintainer's request. The previous 0.3.4.1 desktop and media evidence is recorded in docs/validation/2026-10-24-window-portfolio.md; it is not proof that a new 0.3.4.2 installer has been executed. Historical installer evidence remains in VALIDATION.md and docs/validation/2026-10-23-phayton-r2-production.md. Installation smoke scripts change machine registration and must only run in an approved disposable environment.

The approved local installation, E3D 2.1 configuration, Codex migration and installed-program checks are recorded in [formal installation validation](../docs/validation/2026-10-24-formal-installation.md), including the frozen package hash and the auxiliary registry-version fix that takes effect in the next rebuilt package.
