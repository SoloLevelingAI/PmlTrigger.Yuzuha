# 原生 CLI

0.3.4.2 使用 `runtime/cli/PmlTwin.Phayton.Cli.exe` 作为统一入口，直接调用业务服务或 AVEVA Named Pipe Host，不创建 MCP 子进程。旧执行和知识程序的默认/MCP 服务入口返回 `MCP_DISABLED`，新安装包不包含独立 MCP 服务程序。历史项目名和程序集名保留兼容。

## 发现、调用与 JSON 输入

```powershell
$cli = '.\runtime\cli\PmlTwin.Phayton.Cli.exe'
& $cli op list
& $cli op describe launch.profile.update
& $cli op invoke builtin.usage --input '{"query":"当前元素"}'
& $cli op invoke knowledge.layers.search --input-file query.json
Get-Content query.json -Raw | & $cli op invoke knowledge.layers.search --input-stdin
```

`op list` 返回实际注册目录与输入 Schema，`op describe` 返回单个契约。当前包括原有 30 个业务能力和 16 个启动配置操作；查询、注册、知识和运行状态使用同一入口。元命令 list/describe/invoke 不额外包装成业务操作。

复杂 PML 和秘密输入优先使用 stdin 或权限受控的临时文件，避免命令行转义和 shell 历史泄漏。JSON 输入不能自报授权；`--allow-write`、`--allow-launch`、`--allow-execution` 分别控制持久化写入、创建进程和 PML 执行。

## 会话与底层调用

```powershell
& $cli op invoke aveva.sessions.list
& $cli op invoke aveva.session.status --pid 1234 --expected-model DESIGN
& $cli op invoke pml.array.read --pid 1234 --expected-model DESIGN --allow-execution --input-file read-current.json
```

PID 必须来自本机发现结果，不能照抄示例。每次目标调用核验 PID、启动时间、管道和模块，不依赖上次 CLI 的内存选择。`pml.array.read` 的 `pmlCommand` 是返回数组的表达式，例如 `!!YuzuhaReadCurrentElement(20,2,'')`；Host 将它赋给 `globalVar`，不要在表达式里再写赋值。准确函数签名以 `builtin.usage` 为准。

收到超时、传输断开或 `error.mayHaveExecuted=true` 时，先检查目标与实际结果，不自动重跑。PML 运行可能已经改变状态，取消等待不撤销操作。

## 启动配置

使用 `launch.workspace.read` 或 `launch.profile.read` 获取修订，保存时带上 `expectedRevision`：

```json
{
  "profileId": "my-e3d",
  "changes": { "mdb": "/REVIEW" },
  "expectedRevision": "从读取结果复制的修订"
}
```

```powershell
& $cli op invoke launch.profile.update --allow-write --input-file update.json
& $cli op invoke launch.profile.history --input '{"profileId":"my-e3d"}'
```

完整操作为 `launch.workspace.read`，以及 `launch.profile.list/read/validate/preview/save/update/history/restore.preview/restore/delete/duplicate/favorite/default/start`、`launch.modules.list`。恢复先预览，再带当前修订写入。保存不自动启动，启动另外需要 `--allow-launch`。见[开发、测试与使用约定](workbench-lifecycle.md)。

## 批处理

`op batch --input-file requests.json` 在同一个原生调度器中顺序执行；没有 MCP 会话或自动重试。每一项显式指定需要的目标：

```json
[
  {"operationId":"builtin.usage","input":{"query":"connection"}},
  {"operationId":"aveva.sessions.list","input":{}}
]
```

先校验全部条目的结构，执行中首错停止；`data.completed` 与 `data.results` 保留已执行结果，后续条目不执行。批处理不是事务，早先的修改不会自动回滚。权限仍来自命令行 flags；条目可包含 `processId` 和 `expectedModel`，输入不能自授权限。`--timeout-seconds` 为每项等待期限，范围 1–3600 秒，默认 120 秒。

## 回执与退出码

统一外层包含 `schemaVersion`、`operationId`、`success`、`exitCode`、`data`、`error`。已有底层结果在 `data` 中保留原来的字段大小写，不能假定所有内部字段都已改为 camelCase。

| 退出码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 2 | 参数、输入或业务校验失败 |
| 3 | 未知操作 |
| 4 | 缺少所需授权 |
| 5 | 目标缺失或核验失败 |
| 6 | Host 能力不足或返回失败 |
| 7 | 传输失败 |
| 8 | 超时或取消，需核验实际结果 |
| 9 | 内部错误 |
| 10 | 配置修订冲突，重新读取后比较 |

配置冲突代码为 `CONFIGURATION_CONFLICT`，带 `data.actualRevision`；校验失败为 `LAUNCH_VALIDATION_FAILED`，带字段问题。日志记录关联 ID、目标、耗时和实际结果；状态目录可通过 `YUZUHA_STATE_DIR` 隔离。知识库不会因安装或普通检索自动重建。

旧Host缺少数组窗口或Form/Gadget协议时，返回 `PML_BOUNDED_ARRAY_UNSUPPORTED` 或 `PML_UI_HOST_UNSUPPORTED`（退出码6），并明确 `mayHaveExecuted=false`。这是目标能力不足；请求在发送任何PML前被拒绝，需更新匹配Host并重新启动、核验会话。
