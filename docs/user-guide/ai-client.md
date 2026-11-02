# 接入 AI 客户端

客户端需要在 AVEVA 所在的 Windows 环境运行本地 CLI，传入 JSON，并读取标准输出和退出码。

从安装目录找到 `runtime/cli/PmlTwin.Phayton.Cli.exe`，核对实际路径。也可读取 `HKCU\Software\YuzuhaToolkit\Installations` 中的安装记录。给客户端提供这个绝对路径，例如：

```powershell
$cli = 'C:\实际安装目录\PmlTrigger.Yuzuha\runtime\cli\PmlTwin.Phayton.Cli.exe'
& $cli op list
& $cli op describe aveva.session.status
```

支持 Skill 的客户端可加载随包的 `yuzuha-toolkit`。不同客户端分别加载；更新后按客户端的方式重新读取，再核对当前操作目录。当前接入使用原生 CLI，独立 MCP 入口已禁用。

复杂输入可以通过 `--input-file` 或 `--input-stdin` 提供，一次调用只选择一种来源。启动凭据使用本次安全输入，不放进 argv、公开命令记录或配置历史。

持久化写入、创建进程和 PML 执行分别使用 `--allow-write`、`--allow-launch`、`--allow-execution`，仅在本次任务已授权时提供。每次 Host 调用都明确传入经过核验的目标身份。

从[核验会话](connection.md)开始实际任务，并让客户端保留完整回执。需要源码和 SDK 细节时按需使用[开发参考](developer-reference.md)。
