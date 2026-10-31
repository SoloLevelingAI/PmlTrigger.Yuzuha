# 启动工作流验收 · 2026-10-31

后续更新：旧 Unknown 失败保留为本报告历史；仅升级实装 resolver/Rpc 后，PID 8408 的严格 DESIGN 身份与两次内置读取已通过，会话正常退出。见 [Host/PML 后续严格验收](host-pml-verification-2026-10-31.md)。Host 协议能力仍为 0；没有修改 DLL、EVARS 或模型，也没有把外部验收写回旧 receipt。

范围：OpenSpec integrate-pmltwin-product-platform 的首期启动切片。由 6.1 sol 子代理分别负责发现、执行回执、界面/文档及交叉审查；主代理负责集成、真实 APS、浏览器与串行 FigCraft。没有提交或推送 GitHub。

## 结果

自动发现 → 保存配置 → 冻结计划 → 显式启动 → 持久回执 → 页面重开恢复观察已接通。五个新操作已注册，管理目录共 57 项；旧通信目录保持独立。JSON 为本轮配置/计划/回执的选定格式。独立 Database、CAF/WPF 和平台其余 Feature 未完成。

实际 APS：E3D 2.1 上只执行一次，操作 `df0dfa8c-ec75-45d8-8755-3cdd57b0c48f`，attempt `2b0de3b15a6b443fb5f696a8398cb1fb`，Monitor PID 1916 → des PID 14916。会话发现返回窗口 `ProjAPS | ALL | Model - AVEVA Everything3D`、PID-bound pipe 可见。服务重启和页面刷新后按原编号观察，返回同一尝试及模块候选，没有重复启动。

严格 Host 核验 **未通过**：expectedModel=Design，Host 报 Unknown，返回 E3D_TARGET_MODEL_MISMATCH。未执行 PML、未修改厂商环境或工程模型。静态对比定位到本机旧 bootstrap 缺少仓库的 RefreshModel 调用；具体哈希及升级后的核验步骤见 [Host 后续](../../openspec/changes/integrate-pmltwin-product-platform/features/launch-workflow-host-followup.md)。不把窗口打开等同于通信验收，不将本次 E3D 2.1 结果推广到 E3D 3.1/AM。

## 测试证据

| 检查 | 结果 | 证据 |
|---|---|---|
| 最终 Manager 全套 | 166 通过，2 跳过，0 失败 | manager-complete.trx |
| 原有 Toolkit 全套（兼容修复前） | 234 通过 | regression-review-summary.json 所列 TRX |
| 最终受影响兼容 CLI 子进程测试 | 17 通过 | regression-review-summary.json 所列 TRX |
| 架构边界 | 21 通过 | regression-review-summary.json 所列 TRX |
| HTML JS 语法/行为 | 通过 | test-workflow.cjs |
| 最新 DocFX metadata/site | 0 警告、0 错误 | docfx-build-final.log |
| 真实操作输入 schema | 5 个 ID 精确核对 | tests/final-input-schema-check.json |
| FigCraft/CSS 变量回写与复核 | 11 变量，无漂移 | source/sync-tokens.mjs |

TRX、构建日志位于 `artifacts/manager/launch-workflow`。两项跳过属于可选原生 7-Zip 格式矩阵，因为未设置 `YUZUHA_TEST_7ZR_PATH`；与本轮启动链无关，不算通过。不同轮次测试集合有重叠，不能相加为唯一测试总数。

已修复的实测/审查缺陷：发现草稿携带只读字段导致保存失败、原始模块名误作启动选项、计划未冻结 EVARS、刷新后重复建配置、观察丢失后无法恢复、入口退出误导为工程退出、HTTP 重复 JSON 键，以及旧兼容 CLI 的密码 argv 入口。计划目标变化、受理写失败、unknown 不重放、PID 复用、异安装/错误父进程候选、Origin/token/操作授权和秘密不回显均有对应隔离测试。

浏览器四尺寸与独立 FigCraft 见 [视觉交接](../design/launch-workflow/design-review.md)。本轮没有获得用户视觉认可，没有验收安装升级/回滚、真实断电、所有产品版本或 PML 读写。冻结摘要覆盖入口、显式初始化及选中 EVARS，不覆盖厂商脚本所有间接输入。模块候选只是有限进程证据，不提供跨进程身份认证。

## 继续工作

先读 [精简交接](../../openspec/changes/integrate-pmltwin-product-platform/features/launch-workflow-handoff.md)，再按需要读当前 Feature 契约与 Host 后续。入口为 `scripts/Start-LaunchWorkbench.ps1`；它不自动升级已加载 Host。当前测试工程保留供后续核验。
