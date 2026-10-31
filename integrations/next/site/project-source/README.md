# LaunchDesk.Next

独立的 Windows 启动工作台：查找本机 AVEVA 应用，修改预填，自动保存草稿，执行时冻结快照，再查看启动与记录证据；保存按钮用于命名配置。

- [当前交付状态](docs/status.md)
- [使用手册](docs/manuals/user.md)｜[开发手册](docs/manuals/developer.md)｜[核心演示](docs/manuals/demo.md)
- [文件与阅读索引](docs/INDEX.md)
- [用户与工程说明](docs/features/launch-by-profile.md)
- [架构与源码索引](docs/decisions/architecture.md)
- [验证结果及边界](docs/verification.md)

运行：`dotnet run --project src/LaunchDesk.Next.Desktop`。需要 Windows 与 global.json 指定的 .NET SDK。配置和日志默认在 `%LOCALAPPDATA%/LaunchDesk.Next`，测试可设置 `LAUNCHDESK_NEXT_DATA` 到隔离目录。不会读取或迁移旧工作空间。

构建：`dotnet restore LaunchDesk.Next.slnx --locked-mode`，`dotnet build LaunchDesk.Next.slnx -c Release --no-restore`。架构用 Debug 执行：`dotnet test tests/LaunchDesk.Next.ArchitectureTests --settings test.runsettings`。文档：`dotnet docfx docs/docfx.json`。

当前源码已按具体功能脚本与中性后勤接口组织，见[重构说明](REFACTOR-SCRIPTS.md)。第一版保留在Git标签 snapshot/v1-2026-10-27。
