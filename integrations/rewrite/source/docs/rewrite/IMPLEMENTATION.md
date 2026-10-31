# Archon 架构实施

正式 UI 是原 `LaunchDesk.App` 2.0.4；`rewrite/` 3.0.1 仅为独立验证外壳，不替换安装。接线保留原 DataContext 属性、命令、通知、选择、视图资源和操作位置；新增控件另审。原 Core 复用 Monitor／Marine 参数规则，依赖 App→Core适配→UseCases→Contracts→Kernel；原 ViewModel／协调器迁移未完。

依据：2026-10-19 授权、`f451953` Archon A2＋F06；历史 `rewrite/archon` 审阅不回写。[文件索引](../../rewrite/README.md)、[依赖与落地决策](../../rewrite/architecture.json)、[分层](../reference/articles/architecture.md)列当前入口；静态类型不证明功能，[接线／验收证据](../verification/3.0.1-interface-wiring.md)独立保留，阶段状态记 TASKS.md，未完成项不算实现。

写入只准本机 NTFS 普通目录，拒绝 UNC、设备路径、重解析、身份未知或控制根重叠，不退回前缀检查。默认探测 3×2 秒／失败3轮／成功1轮／有效15秒，可配置；窗口观察90秒，无会话证据仍未确认。历史、未决回执、保护根、迁移副本和凭据引用不自动回收。

运行参数、Archon 数据目录、空目标迁移、同账户凭据及旧版退回见[发布说明](../../rewrite/RELEASE-NOTES.txt)；另支持 `--config`、portable.json，相对路径以 EXE 为准。没有远程执行或凭据传输；Worker 认证见[交付](../reference/articles/flows/delivery.md)，关闭不强杀目标。

FigCraft：`docs/design/LaunchDesk-fusion-v2.figcraft`＋既有 PNG；1440×1000、1180×800、1120×740，原 View 差异与冒烟分验。DevHost 为 localhost:5275 只读诊断，AppHost 可选 Aspire，均不进发布依赖。目标进程须 Job breakaway，无法独立则拒绝，不纳入编排自动终止树。

Windows x64／.NET 10 SDK（本次为预览），发布需 Desktop Runtime。设 `MSBuildEnableWorkloadResolver=false`，还原并 Release 构建 `rewrite/LaunchDesk.Rewrite.slnx`，运行 `scripts/test-rewrite.ps1`。候选冒烟用 `--ui-smoke`，publish-rewrite.ps1 不覆盖旧输出。DevHost 用 `--data-dir`；AppHost 用 `--desktop <EXE> --data-dir <目录>`。
