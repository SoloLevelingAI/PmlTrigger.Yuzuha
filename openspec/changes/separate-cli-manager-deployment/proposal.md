# Proposal

## Why

Yuzuha 目前把 AVEVA 通信、启动配置、目录部署和知识管理集中在工作台及原生 CLI 的依赖链中。用户要求独立的管理器 GUI/CLI，吸收 LaunchDesk 的启动与复制能力，再由管理器调用专用通信 CLI；许可检测和虚拟机模拟按需附加。

取证日期：2026-10-29（Asia/Shanghai）。Yuzuha 分支 `codex/openspec-contract-review-20261027`，提交 `57e6e8d51a0271b2fae5f2a60363af51a5a32da3`；LaunchDesk.Next 分支 `codex/remote-portable-design-review`，提交 `78b8dc48b4b1746d67e5cc40b1012fecd38bef41`。LaunchDesk.Next 有既有文档未提交修改，本提案不修改该仓库。

## What Changes

- 管理器提供 GUI 与独立 CLI，共用管理服务；管理器经受控子进程调用现有 `PmlTwin.Phayton.Cli.exe`，通信 CLI 再连接 NET35/NET48 Host。
- 以 LaunchDesk 为产品原型，把安装发现、启动配置、工程启动、目录/压缩包复制、备份与恢复集成进管理器，不依赖外部 LaunchDesk 窗口完成日常流程。
- 管理器管理 Yuzuha 运行文件，支持复制到指定运行目录、引用自身安装目录内运行文件两种模式，预览后更新选定产品实际使用的 EVARS.INIT/BAT。
- 增加 AM/E3D 连通测试按钮和分层状态灯；只有进程身份、专用管道、Host 协议和模块核验成功才显示连接成功。
- 复用已有独立用户目录与知识管理，补充已有数据库导入、来源映射、完整性/兼容性/FTS/新鲜度检查及失败报告。
- 增加可选许可诊断插件与虚拟机实验插件，同一操作可从管理器 GUI/CLI 调用。未安装插件时基础部署、启动和通信可用。
- 前端可按用户最新授权使用 Zcode/GLM Flash 或当前模型；本轮采用当前模型，不再以 DeepSeek 凭据作为设计前置。FigCraft 按 Feature 独立文件串行编辑，子 Agent 并行做作品集标准验收，再实现 WPF 界面。
- CLI 兼容迁移：保留既有通信操作 ID、JSON、退出码和目标核验；启动/知识管理命令分阶段迁入管理器 CLI，兼容期保留明确转接。任何未来删除或修改既有入口均视为 **BREAKING**，需单独列出迁移清单。

## Capabilities

### New Capabilities

这些是待审行为增量；当前 `openspec/specs/` 没有既有能力文件。

- `manager-cli-boundary`：管理器 GUI/CLI 同源、独立通信 CLI 和进程调用契约。
- `managed-yuzuha-deployment`：运行目录两种部署模式、EVARS 事务、升级与数据保护。
- `aveva-connection-probe`：按产品与进程测试连接、灯态及证据失效。
- `integrated-launch-and-copy`：吸收 LaunchDesk 原型能力，统一启动、复制与操作回执。
- `manager-plugins`：可选许可诊断、虚拟机实验的安装启停和 GUI/CLI 能力发现。
- `knowledge-import-validation`：资料源/已有数据库导入、分层校验与来源映射。

### Modified Capabilities

无既有 OpenSpec capability 可修改。`docs/specification/contract.json` 继续是唯一生效的结构化规范源；实施时回填原有 R/B/T/S ID 和状态，不能用本提案替代或自动迁移它。

## Impact

主要涉及 `src/YuzuhaToolkit.Desktop`、`src/YuzuhaToolkit.Workspace`、`src/YuzuhaToolkit.Knowledge.Core`、`src/YuzuhaToolkit.Mcp.Core`、`src/PmlTwin.Phayton.Cli`、安装器和发布脚本；拟增加 Manager.Core、Manager.Cli、通信进程适配器及插件契约。NET35/NET48 Host 的既有执行协议优先复用。

LaunchDesk.Next 的启动脚本和插件登记方式、旧 LaunchDesk 的复制引擎作为迁移参考，不能把 Next 的启动能力误写成已有复制能力；旧版空 Error 故障必须在集成时修正。当前未发现运行中的 AM/E3D 会话，不能宣称连通测试已通过；知识服务返回空库列表，尚未选择真实导入资料。

本变更只生成规划材料；不部署本机程序、不修改实际 EVARS、不启动工程或虚拟机、不导入官方资料、不改变现有规范状态。完整设计、阶段交付和验收见 `design.md`、`specs/`、`tasks.md`、`verification.md`。
