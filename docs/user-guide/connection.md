# 核验工程会话

打开预期 AVEVA 工程后，在会话页发现候选，明确选择本次操作目标。核对产品、模块、PID 和启动时间身份，随后读取当前连接结果。

如果通过 AI 客户端调用，先使用实际安装的通信 CLI 读取契约：

```powershell
& $cli op invoke aveva.sessions.list
& $cli op describe aveva.session.status
```

`$cli` 是已核对的 `runtime/cli/PmlTwin.Phayton.Cli.exe` 绝对路径，定位方法见[AI 接入](ai-client.md)。随后按实际 Schema 传入选定 PID、模块和完整启动 ticks。ticks 保持十进制字符串，不经过 JavaScript 浮点数转换。

每个 CLI 进程独立核验目标；后续每次 Host 调用都应传入同一组明确身份。不要猜 PID、默认模块或沿用其他会话的选择。

| 看到的结果 | 接下来做什么 |
| --- | --- |
| 没有候选 | 确认工程与正确模块已打开，再查环境和 Host 加载情况 |
| 多个候选 | 根据工程和模块明确选择，核对启动身份 |
| 协议或身份不匹配 | 停止执行，核对当前产品配置与已加载 Host |
| 断线或原进程已退出 | 清除旧目标，重新发现和核验 |
| 当前核验通过 | 按本次任务范围继续；写入、启动、PML 执行分别授权 |

当前已有实机证据只覆盖 E3D 2.1 的已记录范围；AM、PDMS 和其他模块仍需分别核验。
