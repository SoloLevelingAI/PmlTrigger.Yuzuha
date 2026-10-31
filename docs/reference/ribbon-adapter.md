# Ribbon HTTP v1 兼容入口

管理 CLI 的 `ribbon.*` 操作转发给已有 Ribbon Studio 后端，不另建 UIC 解析、历史或部署引擎。默认端点为 `http://127.0.0.1:4318/`，可通过 `YUZUHA_RIBBON_ENDPOINT` 指定其他本机端口；后端未运行会返回诊断，不自动启动或创建工作区。

操作名称以原路由替换斜线，例如 `workspace/open` 对应 `ribbon.workspace.open`，输入仍是原 HTTP v1 JSON。调用 `op describe` 查看授权要求。写入、导入和恢复需要 `--allow-write`。恢复编辑历史仍不写安装源文件；文件覆盖与加载登记保持两个独立动作。

结果 `data.httpStatus` 保留原 HTTP 状态，`data.response` 保留原 JSON。403、409、423 分别返回 `RIBBON_HTTP_403/409/423`，不吞掉原始诊断；错误 JSON 损坏时保留字节并标记 `bodyParseError`。导出字节放在 `data.bytesBase64`，不经字符串转换。输入限制与原 HTTP 服务一致为 20 MiB，响应也设 20 MiB 预算；原后端更严格的业务限制继续生效。总超时覆盖响应头及正文读取。

写请求失去确定回执返回 `RIBBON_RESULT_UNKNOWN`，调用者应在原工作区查看历史和实际文件，不自动重发。适配器不改变原服务的保护条目、服务端绑定或摘要复验，也不会执行宏。

源码定位：`src/YuzuhaToolkit.Manager.Core/RibbonHttpAdapter.cs` 为传输与映射，原 Ribbon 的 `Features/Workspace` 和 `Features/Deployment` 为业务决策。入口兼容夹具见 `tests/YuzuhaToolkit.Manager.Tests/RibbonHttpAdapterTests.cs`。当前仍未接入统一 Database、UIC 编辑 UI 或完成实机 Ribbon 加载；这些任务独立记账。

本片仅接 `Routes` 中列出的 POST 路由。原服务的 registration 配置、图片处理、GET 列表和联合部署入口尚未映射；完整 HTTP v1 兼容任务保持未完成。用户可继续使用原服务这些入口，管理器不伪造相应注册操作。
