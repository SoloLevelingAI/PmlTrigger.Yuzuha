# Ribbon HTTP v1 兼容入口

管理 CLI 的 `ribbon.*` 操作转发给已有 Ribbon Studio 后端，不另建 UIC 解析、历史或部署引擎。默认端点为 `http://127.0.0.1:4318/`，可通过 `YUZUHA_RIBBON_ENDPOINT` 指定其他本机端口；后端未运行会返回诊断，不自动启动或创建工作区。

操作名称以原路由替换斜线，例如 `workspace/open` 对应 `ribbon.workspace.open`，输入仍是原 HTTP v1 JSON。调用 `op describe` 查看授权要求。写入、导入和恢复需要 `--allow-write`。恢复编辑历史仍不写安装源文件；文件覆盖与加载登记保持两个独立动作。

结果 `data.httpStatus` 保留原 HTTP 状态，`data.response` 保留原 JSON。403、409、423 分别返回 `RIBBON_HTTP_403/409/423`，不吞掉原始诊断；错误 JSON 损坏时保留字节并标记 `bodyParseError`。导出字节放在 `data.bytesBase64`，不经字符串转换。输入限制与原 HTTP 服务一致为 20 MiB，响应也设 20 MiB 预算；原后端更严格的业务限制继续生效。总超时覆盖响应头及正文读取。

写请求失去确定回执返回 `RIBBON_RESULT_UNKNOWN`，调用者应在原工作区查看历史和实际文件，不自动重发。适配器不改变原服务的保护条目、服务端绑定或摘要复验，也不会执行宏。

源码定位：`src/YuzuhaToolkit.Manager.Core/RibbonHttpAdapter.cs` 为传输与映射，原 Ribbon 的 `Features/Workspace` 和 `Features/Deployment` 为业务决策。入口兼容夹具见 `tests/YuzuhaToolkit.Manager.Tests/RibbonHttpAdapterTests.cs`。统一 Database、平台 WPF/CAF 装配与完整兼容任务仍独立记账；本仓 Ribbon 编辑器的接线及指定实机切片见下文。

本片仅接 `Routes` 中列出的 POST 路由。原服务的 registration 配置、图片处理、GET 列表和联合部署入口尚未映射；完整 HTTP v1 兼容任务保持未完成。用户可继续使用原服务这些入口，管理器不伪造相应注册操作。

## v2 冻结部署入口

新增 `ribbon.v2.deployment.file-preview`、`file-apply`、`registration-preview`、`registration-apply`，对应 `/api/v2/deployment/` 下的同名 POST 路由。v1 输入及操作 ID 保留。本仓 Ribbon Web 编辑器、桌面 StudioToolPage 与保留的 DeploymentWindow 已有文件更新/模块登记接此 v2；首次新建文件仍是明确标注的 v1 兼容入口，不具备 v2 完整冻结保证。

先使用 `op describe` 查看精确 schema。file-preview 输入为已有 `documentId` 的 `project`；registration-preview 另需 `monPath`、`manifestName`、`entryName`。apply 仅接受预览返回的 `planId` 和 `planDigest`，需要 `--allow-write`，不接受重新传入的项目或目标。可通过 `op invoke <id> --input-file <json文件>` 调用。

服务端缓存已规范化项目、源修订及完整目标预览，五分钟有效；进程重启后未执行计划失效，应重新预览。源修订、目标字节、计划身份或期限不符时在业务写入前拒绝。文件与历史跨资源写入仍可能部分完成；unknown 应人工核对工作区、目标及备份，不自动重放。内存计划/回执不是持久操作日志，不能替代跨进程恢复。

成功回执通过文件哈希回读后才报告 deployed/unchanged；`activation.reloadRequired=true`、`runtimeVerified=false` 表示待重新加载及实机验证。磁盘写入、服务重启、AVEVA 重启均不能单独证明 Ribbon 已加载。接口不会自动重启或执行 Macro。

契约与验证边界：[Ribbon v2 交接](../../openspec/changes/integrate-pmltwin-product-platform/features/ribbon-v2-continuation.md)。

## UI 与运行时证据

UI 在发送前持久记录非秘密计划身份；关页或进程中断后保留未决写入提示，人工核对文件/历史/备份后才可解锁。多模块登记逐项重新预览并确认，不将首项写入前的旧修订用于后续项。详细状态及入口边界见 [UI 契约](../../openspec/changes/integrate-pmltwin-product-platform/features/ribbon-ui-continuation.md)。

[本轮实机报告](../validation/ribbon-restart-2026-10-31.md)证明指定 E3D2.1 APS/ALL/DESIGN 上，直接调用 HTTP v2 部署后正常重启，测试页签、32/16图标及两Tooltip实际可见。测试登记已精确恢复，未点击宏；这不是最终UI全程实机点击、CAF或其他产品验证。
