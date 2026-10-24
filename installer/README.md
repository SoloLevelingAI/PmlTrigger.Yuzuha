# Phayton Windows Setup v0.3.4.1

发行版 0.3.4.1，交付修复原生窗口的 Phayton 桌面、完整 DocFX、中文演示及本地只读文档宿主。NET10 Native AOT 使用同一桌面修复提交通过 CI 的产物；五套 NET35/NET48 Host 逐文件核对 v0.3.3 已发布清单后复用。Inno Setup + NET48 helper 实现安装，不分发或调用安装 PS1。桌面、文档与 HTML 应用宿主均自包含。需要 Windows 10/11 x64 和 .NET Framework 4.8；Windows 11 圆角效果已实测，生成 EXE 尚未签名。

人工运行 EXE，选择中文或 English、安装目录，查看注册表候选并明确选择环境/Profile；可选 MCP JSON 与 Skill。生成并检查前后预览，确认后安装。AI 从同一个包读取 INSTALL.md，生成计划、获准后用 SHA256 驱动同一个 EXE；已安装软件可通过注册表定位并使用 plan/attach 接入客户端。

完整流程见 [INSTALL.md](INSTALL.md)，实际测试见 [VALIDATION.md](VALIDATION.md)。

## 边界

- 保留原赋值、派生目录、短路径转换和自定义调用；在末尾追加三项配置，更新受管块不重复添加。未知目标写入、提前退出或不明确跳转要求审查，不执行环境文件。
- 标准 mcpServers JSON 合并，不自动改写 Codex TOML。
- 旧 PS1/preview1 或非受管目录不能直接覆盖迁移；请用 VM 新目录。
- 保留原编码及搜索路径；报告与备份位于 LocalAppData/YuzuhaToolkit/SetupReports。
- 不启动 AVEVA、不执行 PML、不保存模型，不递归删除用户数据。
- 恢复为尽力恢复，不是断电级事务。中文覆盖主要流程，少数系统错误英文回退。

## Build / test

Use a fresh output directory, .NET SDK/net48 and Inno Setup 6.7.3:

```text
python installer/build.py --iscc "C:/Tools/Inno Setup 6/ISCC.exe" --output "C:/Builds/PhaytonSetup" --desktop "C:/Builds/DesktopPublish" --documentation "artifacts/docfx/site" --documentation-host "C:/Builds/DocHostPublish"
dotnet run --project installer/tests/SetupTests.csproj -c Release
python installer/tests/verify_delivery.py "C:/Builds/PhaytonSetup"
python installer/tests/verify_agent_bundle.py "C:/Builds/PhaytonSetup/Phayton-0.3.4.1-Windows-Agent.zip" "C:/Builds/PhaytonSetup/Phayton-0.3.4.1-Windows-Setup.exe"
```

Builder verifies AOT hashes and excludes PS1, proprietary references and local databases. VM ZIP includes the EXE, helper, Skill, request template and instructions.

Authored by OpenAI Codex at the maintainer's request. Release v0.3.4.1. Desktop and media evidence is recorded in docs/validation/2026-10-24-window-portfolio.md. Historical installer evidence remains in VALIDATION.md and docs/validation/2026-10-23-phayton-r2-production.md. Installation smoke scripts change machine registration and must only run in an approved disposable environment.

The approved local installation, E3D 2.1 configuration, Codex migration and installed-program checks are recorded in [formal installation validation](../docs/validation/2026-10-24-formal-installation.md), including the frozen package hash and the auxiliary registry-version fix that takes effect in the next rebuilt package.
