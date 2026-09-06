# 连接和故障定位 / Connect and diagnose

Call list_aveva_sessions, then select_aveva_session using a returned PID. Never guess a PID; do not auto-select among multiple candidates. Continue only with TargetVerified=true. MCP and AVEVA must be in the same Windows environment (also inside a VM).

Count=0 means no visible matching AVEVA window was found. E3D_TARGET_NOT_SELECTED means select a session first. Pipe/identity errors mean check the Host, profile, target process and module. A function load error means check the packaged PML and actual loaded version; do not replace the built-in implementation with a snippet from an index. No automatic retries after execution timeout. If a built-in is confirmed faulty, report the failure and follow the user's decision on a fix or replacement; priority does not make an incorrect function safe.
