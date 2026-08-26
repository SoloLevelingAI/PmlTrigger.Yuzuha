# Security policy

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
