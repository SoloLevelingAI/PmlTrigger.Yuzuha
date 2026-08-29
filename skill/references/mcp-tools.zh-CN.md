# MCP 工具——YuzuhaToolkit

中文 | [English](mcp-tools.md)

MCP Server 名称为 `YuzuhaToolkit`。Native AOT stdio 进程通过本机命名管道
`yuzuha.pml.command.v1` 与加载在 AVEVA 中的 Net48 Host 通信。默认心跳间隔为
2 秒，业务调用连接等待约 3 秒。

## `get_connection_status`

无副作用地返回心跳状态：

- `State`：`Connecting`、`Connected`、`Degraded`、`Disconnected` 或 `Disposed`。
- `IsConnected`：近期心跳是否成功。
- `LastAttemptUtc`、`LastSuccessUtc`、`LastLatency`。
- `ConsecutiveFailures`、`LastError`。
- `HeartbeatRunning`、`HeartbeatLoopError`。

`Connected` 只证明 Host 近期可达，不证明某条 PML 已成功执行。

## `generate_pml_call`

根据方法名和有序参数生成 `!!Method(...)` 文本，不执行 PML。

| 参数 | 类型 | 说明 |
|---|---|---|
| `methodName` | string | 不带 `!!` 和括号的方法名 |
| `parameters` | array/null | 有序 `{type,value}`；无参数时使用空数组 |

类型别名：`string/str`、`bool/boolean`、`double/real/number`。字符串使用
PML 单引号转义，布尔值生成 `TRUE/FALSE`，数字使用与区域无关的格式。

## `run_pml_command`

在 AVEVA 主线程执行一条完整 PML 命令。它可能修改活动模型，只能在用户明确要求后
调用一次，超时后禁止自动重试。

主要响应字段：

- `Success`：业务调用是否成功。
- `Code`、`ErrorMessage`：失败分类和消息。
- `PmlCommand`：实际命令回显。
- `RequestId`、`ExecutionThreadId`：关联信息。
- `ServerRuntime`、`ServerTimeUtc`：Host 运行时和时间。

以 `PML RPC failed:` 开头的文本是传输失败，不是成功结果。

## `run_pml_command_list`

执行一个产生全局数组的 PML 表达式，并将数组转换为适合 Agent 使用的结构化 JSON。

| 参数 | 类型 | 说明 |
|---|---|---|
| `pmlCommand` | string | 完整 PML 表达式 |
| `globalVar` | string | 不带 `!!` 的全局数组名 |
| `deleteGlobalVar` | bool | 读取后是否删除临时变量 |
| `includeEmpty` | bool | 是否在 `Items` 中保留空值，默认 true |

`pmlCommand` 必须是一个**返回 ARRAY 的表达式**，Host 会把它赋给临时全局变量，
随后调用 `SIZE()`。推荐使用正式读取入口，例如：

```pml
!!YuzuhaReadCurrentElement(30,3,'member')
!!YuzuhaReadDbref(30,3,'member','/SITE/ZONE/EQUI')
!!YuzuhaReadGlobal(30,3,'','YUZUHA_SAMPLE')
```

不要传 `!!TEMP = ...` 或 `!!TEMP[1] = ...` 之类的赋值语句；这会破坏 Host 自己的
赋值与 `SIZE()` 流程，并通常返回 `PML_COMMAND_EXCEPTION`。

每个规范化条目包含 `depth`、`path`、`type`、`value` 和 `empty`。
`Summary` 始终针对过滤前的完整结果统计；`includeEmpty=false` 只过滤 `Items`。

不能解析为对象遍历行格式的普通字符串进入 `Unparsed`。例如
`!!YuzuhaTriggerCommand.Query()` 的结果可能出现在这里，因此不能只检查
`Items` 和 `Count`。

## 路径和查询建议

- 属性路径使用点号：`hpos`、`member`、`member.1`。
- E3D 交互命令行中的 `@` 分隔方式不适用于当前 RPC 执行路径。
- 首次查询推荐小 `size`、`depth=2`，需要时再定向增加。
- 修改型工作流见
  [file-macro-workflow.zh-CN.md](file-macro-workflow.zh-CN.md)。
- `$P <path>` 只打印文本，不是可靠的 CE 切换方式。需要定位时，通过站点已部署的
  命令包装对象执行路径命令，然后重新读取 CE 验证。
