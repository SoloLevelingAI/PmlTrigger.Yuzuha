# 本机 Form / Gadget 合成 Smoke

这是需要显式选择 AVEVA 会话的实机测试，不由 CI 自动执行。先阅读 `tests/manual/form-query.md` 的完整验收边界。这里的成功只代表自有合成夹具在指定产品、Host DLL 和当前运行中的断言通过。

源码包括纯计算函数 `yuzuhav04localadd.pmlfnc` 和专用窗体 `yuzuhav04localprobe.pmlfrm`。窗体含 BUTTON、TEXT、TOGGLE、LIST、普通 STRING/REAL 成员和一个只在按钮回调执行时增加的计数器。没有工程数据库修改、SAVEWORK 或自动保存命令。测试仅重载自己的窗体，不重写原生管理器或装饰回调。

1. 从经人工审查的构建加载匹配产品的 NET35/NET48 Host。新 Host 必须宣告 FormQueryVersion=1 和 BoundedArrayReadVersion=1。升级 DLL 需要关闭占用它的 AVEVA 后按已有部署预览/回执流程替换；不能在加载中覆盖 DLL。也可使用独立临时安装副本启动测试会话。
2. 在自己的开发目录编辑这两个文件，再通过已有 C# PluginDeployment 预览/应用，将它们复制到已登记 PMLLIB 下的独立 `YuzuhaV04LocalTests` 子目录。只新增自己的文件，不覆盖 PMLLIB/PMLUI 的原生文件。脚本本身不负责安装或复制文件。
3. 用 `list_aveva_sessions` 取得实际 PID，确认项目、模块和窗口。启动 E3D 2.1 MAR 时沿用 LaunchDesk 已验证的 Monitor、原始 launch.init、USERDATA 工作目录和独立进程启动机制；MDB 为 `/ALL_NO_MDS`。不要将 evars.init 当成 Monitor 的 launch.init，也不要把临时测试目录当作原安装根目录。凭据仅在本机启动时输入，不写入这里或日志。
4. 显式运行以下入口。所有参数必须替换为实际绝对路径及当前 PID；提供的 HostDll 必须是该测试会话实际加载的镜像，脚本记录其哈希但不会代替安装流程验证加载路径。

```powershell
pwsh -NoProfile -File tests/live/form-query/Invoke-FormQuerySmoke.ps1 `
  -McpExecutable 'C:\test-runtime\YuzuhaToolkit.Mcp.exe' `
  -ProcessId 12345 `
  -PmlDirectory 'C:\test-install\PmlTrigger.Yuzuha\PMLLIB\YuzuhaV04LocalTests' `
  -HostDll 'C:\test-install\PmlTrigger.Yuzuha\runtime\profiles\E3D2.1\net48\YuzuhaToolkit.PmlHost.Net48.dll'
```

执行前比较 Git fixture 与部署副本 SHA256，不一致立即停止。脚本复用 YuzuhaExcuter 执行自有宏，并检查 Query()，不会只看 RPC 的 Success。宏包含 `PML REHASH ALL`、`PML RELOAD FORM !!YuzuhaV04LocalProbe` 和自有内存变量赋值。查询使用生产 MCP 工具；源码、会话、能力、匹配数量、返回值或状态不符合即退出失败，不自动重试超时。

脚本检查 2+3=5、两页原生数组内容、loaded/shown 区别、控件清单及分页、普通成员和超预算的明确拒绝，并对照查询前后的计数器、文本、开关值、回调文本和显示状态。可选属性 unavailable 保留真实错误，不当成 false。每次只使用新的证据目录；默认 `artifacts/manual/form-query/<UTC>/`，包含请求、响应、stderr、宏和 run.json（提交、dirty、PID/启动时间、Host 哈希、fixture 哈希与时间）。这些真实会话证据仅留本机，不上传 CI。

结束后自有窗体和内存变量保留供检查；关闭测试会话时选择不保存工程。NET35 实机、CONTAINER/WPF 内部、显示后交互、完整 PMLFORMS 人工对照与生命周期验收仍需分别验证，不能由该脚本的一次通过推断。
