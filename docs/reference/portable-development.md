# 单仓开发与换电脑接手

只克隆 PmlTrigger.Yuzuha 即可取得 rewrite、Next、Ribbon/UIC 的必要源码、契约、测试、原生设计与作品集。来源保存在 `integrations/`，不再从相邻 LaunchDesk 或 UicRibbonDesigner 目录读取构建输入。保留原名称和需求 ID 以便追溯；这不表示旧协调器会自动装配为另一套产品后端。

## 环境

使用 Windows、Git、PowerShell 7、Node.js/npm 和 `global.json` 固定的 .NET SDK。`scripts/Use-YuzuhaDotnet.ps1` 会从 DOTNET_ROOT、用户安装位置及 PATH 中选择匹配 SDK。首次还原需要 NuGet/npm 源，或常规依赖缓存；不能把本机源仓库当依赖缓存。

Ribbon 的原 NET48 后端与 WPF 构建使用 Windows .NET Framework 编译器及程序集；Next/WPF 需要 Windows Desktop 运行时与目标包。AVEVA SDK 只用于显式 CAF/Host 编译及实机验证，不是默认离线验收前置。7zr 和 Aspire/VM 开发实验也不进入基础产品依赖。

## 验证与启动

在仓库根目录运行：

```powershell
./scripts/Test-IntegratedWorkspace.ps1
node scripts/Serve-IntegratedPortfolio.mjs
```

第一条命令依次验证导入哈希、项目引用闭包、当前 Manager/Database/架构、rewrite、Next 和 Ribbon。任一步失败立即停止，报告位于 `artifacts/portable-validation/result.json`；只有全部执行完才写 `completed=true`。可选7zr、真实设备发现和交互桌面测试不冒充默认离线通过。

第二条命令提供作品集入口 `http://127.0.0.1:4176/`，不安装 npm 依赖。三个来源的 HTML、CSS、图片、原生 FigCraft 和阅读资料均在本仓；各模块保留自己的详细交接。

```powershell
./scripts/Start-IntegratedWorkbench.ps1
```

此命令还原并构建 Ribbon、启动仓内 HTTP 服务，将实际端口传给 Manager，再打开启动工作台。两个服务的端口可指定，也可自动分配；Ctrl+C 清理本命令启动的服务。Ribbon 默认用户数据位于本机 LocalAppData/PMLTWIN/Ribbon/workspace，可用 `-RibbonDataDirectory` 显式指定；不迁移或覆盖原 Ribbon 用户数据。`-NoBuild` 仅用于已经完成构建的克隆。

## 源码所有者

| 目录 | 用途 | 运行边界 |
|---|---|---|
| src/YuzuhaToolkit.Manager.* | 当前管理 CLI、启动工作台、操作装配 | 当前产品权威入口 |
| integrations/rewrite/source | 操作、交付、恢复及原需求与设计 | 迁移与回归来源，不启动第二协调者 |
| integrations/next/source | 发现、启动、Addin及原型测试 | 迁移与回归来源，现有发现实现继续复用 |
| integrations/next/site | 完整作品集和文档阅读资产 | 静态展示，历史证据保留历史状态 |
| integrations/ribbon/source | 原 HTTP 后端、契约、Web/WPF、UIC规则 | 由 Manager HTTP 适配调用，无第二存储引擎 |

各 `manifest.json` 同时保存原始和集成后 SHA256。迁入所需路径/SDK/测试隔离修复记为 transformation。`node scripts/check-integrations.mjs` 只检查仓内资产，`node scripts/source-baseline.mjs` 也改为读取仓内来源；历史报告中的原机器路径仅是来源记录。

## 数据与实机

知识库、凭据、厂商 SDK、运行数据库、备份 DLL 及现场 VM 数据不在源码仓库中。新电脑分别迁移合法持有的资料并重验来源路径；凭据重新设置。已验证的 E3D2.1 APS 证据仅证明原机器当时的状态，新电脑应重新发现并核验 PID、启动时间、模块、Host/PML 协议。

单仓完整交付与整个平台全部业务融合是不同验收范围。数据库切换、完整 Feature UI、CAF/UIC 实机加载等仍按 OpenSpec 未完成项推进，不因导入代码或设计而升级为已验证。
