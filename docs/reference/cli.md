# CLI 与 MCP 使用同一程序

两个服务程序都支持双入口：`YuzuhaToolkit.Mcp.exe` 提供工程执行工具，`YuzuhaToolkit.Knowledge.exe` 提供知识工具。默认模式和 `--mcp` 都启动标准 MCP stdio；`--cli` 在终端使用。

## 作为 MCP 服务启动

MCP 客户端可直接调用同一个程序，无需额外包装服务器。配置示例中的路径需替换为实际安装位置：

```json
{
  "mcpServers": {
    "yuzuha": {
      "command": "D:\\PmlTrigger\\runtime\\net10\\YuzuhaToolkit.Mcp.exe",
      "args": ["--mcp"]
    },
    "yuzuha-knowledge": {
      "command": "D:\\PmlTrigger\\runtime\\net10\\YuzuhaToolkit.Knowledge.exe",
      "args": ["--mcp"]
    }
  }
}
```

MCP 模式的 stdout 只承载 JSON-RPC，诊断写 stderr。已有不带参数的 MCP 注册方式继续有效。

## 终端调用

```powershell
.\YuzuhaToolkit.Mcp.exe --help
.\YuzuhaToolkit.Mcp.exe --cli list
.\YuzuhaToolkit.Mcp.exe --cli call get_builtin_usage --arguments '{"query":"当前元素"}'
.\YuzuhaToolkit.Mcp.exe --cli call generate_pml_call --arguments '{"methodName":"Test","parameters":[{"type":"bool","value":true}]}'
.\YuzuhaToolkit.Knowledge.exe --cli call search_knowledge_layers --arguments '{"query":"当前元素"}'
```

`list` 返回实际 MCP 工具及 JSON Schema，`call` 返回原始 MCP `CallToolResult`。参数还可通过 `--file arguments.json` 或 `--file -` 从文件/stdin 读取；这能避免复杂 PML 字符串经过 shell 转义。

CLI 不维护另一份业务规则。它通过 SDK 启动**同一程序的一个 `--mcp` 子进程**，使用原有工具注册、校验和处理器；结束后关闭该连接与子进程。CLI 增加一层本机进程与协议开销，换取两种入口的行为一致。

## 保持同一会话

单次 CLI 调用结束时会释放会话。需要多步状态时使用 batch，在同一 MCP 子进程中顺序处理所有请求：

```json
[
  {"name":"get_builtin_usage","arguments":{"query":"connection"}},
  {"name":"list_aveva_sessions","arguments":{}}
]
```

```powershell
.\YuzuhaToolkit.Mcp.exe --cli batch --file requests.json
```

先发现并由用户明确选择真实 PID，再构造 `select_aveva_session`、`get_connection_status` 以及已授权查询/执行的 batch。每一项沿用该进程内的选择状态；不能猜测 PID，也不能因为超时而重新执行整批命令。

首次失败即停止，JSON 输出报告已完成项；不会重试已发送的请求。`--timeout-seconds` 控制单个工具请求的等待期限，支持 1–3600 秒，默认 120 秒。取消等待不等于撤销已经开始的 AVEVA 操作。

| 退出码 | 含义 |
| --- | --- |
| 0 | 请求成功完成 |
| 1 | MCP、传输或业务结果报告失败 |
| 2 | CLI 参数或输入 JSON 无效 |
| 124 | 请求取消或超时，执行结果需要核验 |

知识服务原有 `--refresh-project` 维护入口继续按既有规则工作。CLI 也可能调用写入知识源或经验的工具；只有用户明确要求的写入才能执行，资料保持在用户选定的本机目录。

## 持久开发身份与测试证据

执行程序新增 `--cli dev` 简写，支持连接、授权模式、文件写入、运行、证据、函数登记和日志导出，完整流程见 [PML 开发与授权](pml-development-plan.md)。保守模式仅登记；标准模式逐函数授权；开发模式限定完整会话身份和源码目录。每次非只读执行自动 MARKDB，未知结果不重试。
