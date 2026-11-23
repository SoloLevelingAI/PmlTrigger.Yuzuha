# Phayton Windows Setup v0.3.5.4

开始菜单的「Phayton Workspace」打开随安装包装入的 PmlTwin WPF。安装包还包含原生 CLI、按产品选择的 NET35/NET48 Host、DocFX 与本地只读文档宿主。Inno Setup 和 NET48 helper 负责安装。

普通安装选择安装目录并同意 Apache License 2.0。升级时向导列出将备份并替换的同名程序文件。成功后可从完成页打开管理器。PML 资源部署和 EVARS 切换在管理器中单独确认。计划引擎保留现有客户端配置。

完整流程见 [INSTALL.md](INSTALL.md)，实际测试见 [VALIDATION.md](VALIDATION.md)。

## 边界

- 保留原赋值、派生目录、短路径转换和自定义调用；在末尾追加三项配置，更新受管块不重复添加。未知目标写入、提前退出或不明确跳转要求审查，不执行环境文件。
- CLI-only：不合并 mcpServers JSON，不改写 Codex TOML；Skill 可单独安装。
- 资源包采用独立目录部署。AVEVA 按本机流程生成 PML 索引，管理器保留旧资源和原 EVARS 备份以供回退。
- 旧 PS1/preview1 或非受管目录不能直接覆盖迁移；请用 VM 新目录。
- 保留原编码及搜索路径；报告与备份位于 LocalAppData/YuzuhaToolkit/SetupReports。
- 不启动 AVEVA、不执行 PML、不保存模型，不递归删除用户数据。
- 恢复为尽力恢复，不是断电级事务。中文覆盖主要流程，少数系统错误英文回退。

## 程序与资源部署

- 安装器选择程序目录，包含工作台、CLI、程序依赖和随包 PML 源码。工作台把源码同步到同一个共用根目录下的 PMLLIB 和 PMLUI，兼容现有的固定 Bootstrap 路径。
- 工作台一次预览安装版本、Host、EVARS 与同名文件变动。确认接入后逐一备份 EVARS 和替换的 PML 文件。
- EXE 以原始字节存放 `deployment/<版本>/PMLLIB` 和 `deployment/<版本>/PMLUI` 的源文件。`pml.index` 由 AVEVA 管理。

## Build / test

Use a fresh output directory, .NET SDK/net48 and Inno Setup 6.7.3:

```text
python installer/build.py --iscc "C:/Tools/Inno Setup 6/ISCC.exe" --output "C:/Builds/PhaytonSetup" --version 0.3.5.4 --cli "C:/Builds/NativeCliPublish" --desktop "C:/Builds/PmlTwinWpf" --documentation "artifacts/docfx/site" --documentation-host "C:/Builds/DocHostPublish"
dotnet run --project installer/tests/SetupTests.csproj -c Release
python installer/tests/build_cli_test.py
python installer/tests/verify_delivery.py "C:/Builds/PhaytonSetup"
python installer/tests/verify_agent_bundle.py "C:/Builds/PhaytonSetup/Phayton-0.3.5.4-Windows-Agent.zip" "C:/Builds/PhaytonSetup/Phayton-0.3.5.4-Windows-Setup.exe"
```

Supply the PmlTwin WPF Windows x64 publish output built with `--self-contained true -p:BundlePhayton=false` and the verified Native AOT CLI. The builder hashes the payload and uses `--version` for the manifest, installer and Agent archive. The Agent archive contains the EXE, helper, Skill, request template and instructions.

Run `installer/tests/Test-SetupResponsive.ps1` on an interactive Windows desktop with WinAppCLI and the staged payload. It holds a test helper open and checks mouse drag and cancellation while the wizard waits.

Authored by OpenAI Codex at the maintainer's request. The previous 0.3.4.1 desktop and media evidence is recorded in docs/validation/2026-10-24-window-portfolio.md; it is not proof that a new 0.3.4.2 installer has been executed. Historical installer evidence remains in VALIDATION.md and docs/validation/2026-10-23-phayton-r2-production.md. Installation smoke scripts change machine registration and must only run in an approved disposable environment.

The approved local installation, E3D 2.1 configuration, Codex migration and installed-program checks are recorded in [formal installation validation](../docs/validation/2026-10-24-formal-installation.md), including the frozen package hash and the auxiliary registry-version fix that takes effect in the next rebuilt package.
