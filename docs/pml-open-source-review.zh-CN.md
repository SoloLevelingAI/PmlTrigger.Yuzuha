# YuzuhaToolkit PML 开源发布审查

审查日期：2026-08-29  
审查范围：`PMLLIB`、`PMLUI`、.NET 10 Native AOT MCP、Net48 PMLNet Host、Agent Skill 与发布脚本。

## 结论

**适合以 Preview/Experimental 形式开源，暂不建议标记为稳定 v1。**

公开树中未发现凭据、个人目录、业务模型数据或 AVEVA 专有程序集。Net10 MCP 已改用
`PlantHost.Rpc.Net10` 的强类型调用和 source-generated JSON，不再依赖
`DispatchProxy`；发布后的 Native AOT EXE 已通过启动烟雾测试。Net48 Host 仍需由用户
已安装并获得许可的 AVEVA/.NET Framework 4.8 环境承载。

## 本次已解决

- `YuzuhaAddin` 自动注册 `!!YuzuhaRpcCommand` 和 `!!YuzuhaTriggerCommand`；RPC Host
  的重复启动由 Host 端去重。
- MCP 提供 2 秒心跳和无副作用的 `get_connection_status`，报告连接状态、延迟、连续
  失败次数和最后错误。
- CE、DBREF、全局对象图读取均有深度和数量上限；DBREF 读取结束后恢复原 CE。
- BOX 是包内示例；盘管使用站点提供的 `!!NewSpiral(...)` 与
  `!!RotateMyspiral(...)`。公开文档和 Skill 会教给 Agent，但不会伪造或分发站点私有实现。
- 执行型 MCP 工具明确要求用户授权，超时后禁止自动重试。
- Release 不包含 `Aveva.Core.Utilities.dll`、`PMLNet.dll` 或 `ForeignLanguage.dll`。

## 稳定 v1 前仍需完成

### P0：动态 PML 输入边界

DBREF、全局变量名、属性路径和宏路径最终会进入 PML 动态求值。Preview 依赖“可信本机
用户明确请求”的安全边界；稳定版应增加字符白名单、最大长度和路径范围校验，并将高级
动态入口显式标记为 unsafe。

### P1：遍历和错误模型

对象图 BFS 仍主要依赖深度与数量上限。建议增加稳定对象标识、visited 集合、明确的
`truncated` 原因，并逐步用结构化错误取代字符串哨兵。

### P1：真实 AVEVA 集成证据

仍应记录目标 E3D 版本、位数、Addin 加载、心跳重连、命令执行、异常恢复和退出清理测试。
盘管演示还应在实际包含站点函数的环境中单独验证。

### P1：第三方发布流程

继续保留 PlantHost.Rpc 与 Newtonsoft.Json 的许可证；每次发布都应扫描专有 DLL、运行
Release 校验脚本、验证 SHA-256，并优先通过 GitHub Release 分发构建产物而不是提交到
源码历史。

## 发布门禁

- [x] Net10 Native AOT 启动烟雾测试。
- [x] 心跳与连接状态工具。
- [x] 正确的 `YuzuhaTriggerCommand` 名称和盘管说明。
- [x] Skill、PMLUI、PMLLIB、MCP 与安装脚本成套交付。
- [x] 排除 AVEVA 专有程序集。
- [ ] 输入白名单与结构化遍历错误。
- [ ] 目标 AVEVA E3D 环境集成验证。
- [ ] 稳定版独立复审与签字。
