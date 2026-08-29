# Security policy / 安全策略

## 中文

PmlTrigger.Yuzuha 面向可信本机工作站。MCP 与 AVEVA Host 通过本机命名管道
通信；项目不提供远程身份验证、命令沙箱或服务端审批门。

执行型工具接受 PML 文本并可能修改活动 AVEVA 数据库。只有用户明确要求后才能
调用，超时后禁止自动重试。不得通过网络桥接或代理公开命名管道。安全问题请私下
报告给仓库维护者，不要在公共 Issue 中披露细节。

## English

PmlTrigger.Yuzuha is designed for a trusted local workstation. The MCP server
and AVEVA host communicate through a local named pipe. The project does not
provide remote authentication, command sandboxing, or a server-side approval
gate.

Execution tools accept PML text and can modify the active AVEVA database. Call
them only after an explicit user request, and never automatically retry a
timed-out execution.

Do not expose the named pipe through a network bridge or proxy. Please report
security issues privately to the repository owner rather than opening a public
issue.
