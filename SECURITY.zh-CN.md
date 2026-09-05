# Security policy / 安全策略

> 中文版（供作者审阅）。英文版 / English: [SECURITY.md](SECURITY.md)

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

---

PmlTrigger.Yuzuha 面向可信的本地工作站设计。MCP 服务器与 AVEVA Host 通过本机
命名管道通信；本项目不提供远程认证、命令沙箱或服务端审批闸门。

执行类工具接受 PML 文本并可以修改活动 AVEVA 数据库：仅应在用户明确提出请求
后调用；执行超时后禁止自动重试。

不要通过网络桥接或代理暴露该命名管道。发现安全问题请私下报告给仓库所有者，
不要直接开公开 issue。
