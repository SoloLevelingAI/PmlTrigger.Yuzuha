# CLI / Host 兼容性验证 — 2026-10-31

状态：E3D 2.1 APS DESIGN 实机严格验收通过。OpenSpec 8.9、8.10 已完成，整体进度 10/46；其余产品与平台任务保持原状态。会话已正常退出，截图与收尾回执已永久保存。

## 当前契约与范围

仓库 `skill/SKILL.md` 描述现代独立通信 CLI。用户明确指出，已安装的旧版 toolkit 技能仍描述已移除的 MCP 工作流；本次遵循仓库当前 CLI 契约，不沿用旧安装技能。

实机 Form Query 脚本已改用 `op batch --input-file`、operationId/input 请求及 OperationResult 的 data/results 响应。每项请求显式固定 PID 和 expectedModel DESIGN，后续批次再固定已核验的进程起始 ticks。原有数组续页、原生 Form/Gadget 过滤及分页、字符预算拒绝、回调/值/显示状态无副作用断言全部保留。参数 `-CliExecutable` 兼容旧名 `-McpExecutable`，`-DotnetExecutable` 可固定运行时。

## 部署证据

仅更新已安装目录中的 `runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll`。证据为 `artifacts/manager/cli-host-compat/host-plan.json`、`host-receipt.json` 和 `host-update-result.json`。

- 新 DLL SHA256：`1F1A0E30C60BACA342F173AE90EB4958AEE08B83D406BF76DDE6A5C03BC4B7F9`。
- 原 DLL SHA256：`EF6CDB934D7F69BD429B66F6C83F52928B3A14A66BF49B1EF3ABF28046C48987`。
- 备份收据：`20261031T054039-4a15135a0cdb4fdb947385bb62fe8a70`；原 DLL 保存在 `artifacts/manager/cli-host-compat/Deployments/` 下。
- Inno 安装标记及 AVEVA `evars.init` 未变；基线哈希见 `unchanged-before.json`。

这是用户授权的开发期定向修复，使用已有 `PluginDeployment.PreviewSelection/Apply` 文件事务，未运行旧 PS1 安装器、重注册 MCP 或伪造 Inno 包版本。正式发行仍需将新 Host 纳入发布包清单。

### 条件回退

先正常退出使用该部署的 AVEVA 会话。核对 `host-plan.json` 中唯一目标仍等于新 DLL SHA256，且 `host-receipt.json` 的 BackupDirectory 中对应相对路径仍等于原 DLL SHA256；任何不一致都停止，保留后续用户改动。核对通过后，调用同一 `PluginDeployment.Restore(plan, receipt)` 事务（本轮适配器为 `artifacts/manager/cli-host-compat/repair/bin/Debug/net10.0/Repair.dll` 的构建输出，入口参数 `restore`），或按该冻结清单仅恢复这一 DLL，随后再次验证原摘要。不得恢复整个目录或覆盖 EVARS、PML、知识库。旧 Host 恢复后查询版本会回到 0，新协议操作应继续被拒绝；必须重新发现进程并严格核验。本轮未执行回退演练。

管理 CLI 的新包位于 `artifacts/manager/cli-host-compat/manager`，旧包 `artifacts/manager/launch-workflow/packages/20261031T051959-ef7d29f2/manager` 保留；工作台切回旧包会重新引入输出等待问题，不能沿用本轮通过状态。

## 自动化验证

最终 CLI 捕获测试 26 项通过，见永久证据目录中的 `cli-capture-native-detached.trx`。Host 协议测试 33 项通过，见 `artifacts/validation/host-query-protocol-20261031/host-query-protocol.trx`。DocFX 零警告、零错误，见 `artifacts/test-results/cli-capture/docfx-native-detached.log`。

通信 CLI 使用 SDK 10.0.302 的 Release 构建成功，零警告、零错误。首次构建因离线 NuGet 漏洞元数据 NU1900 失败，增加 `-p:NuGetAudit=false` 后成功；两次日志均保留在 `artifacts/manual/cli-harness-20261031/`。该目录还保存七项 `op describe` 以及未知 operation 的批处理响应结构验证；后者没有调用 AVEVA。describe 退出码为零，但 stderr 有日志目录权限诊断。

```powershell
$taskDotnet = 'C:\Users\26648\.dotnet\dotnet.exe'
& $taskDotnet build src/PmlTwin.Phayton.Cli/PmlTwin.Phayton.Cli.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
$taskCli = 'src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.dll'
& $taskDotnet $taskCli op describe pml.forms.list
```

## APS 实机观察与历史参照

前两次启动结果捕获均返回 CLI 退出码零，耗时分别为 278 ms、263 ms。这仅证明输出能及时捕获。APS 显示“Unable to open communications file to read”，没有完成登录，不能据此判定启动就绪或 Host/Form Query 通过。证据位于 `artifacts/manager/cli-host-compat/`：`aps-launch-first.json`、`aps-launch-second.json`、捕获计时文件及 `aps-communications-error.png`。

第三次调整参数引号、第四次继承 stdin 后仍出现通信文件错误。最终实现不接管厂商标准句柄（bInheritHandles=false、不设置 STARTF_USESTDHANDLES、不使用 NUL，也不继承 CLI stdin），并对 Monitor 使用 DETACHED_PROCESS、Marine 使用 CREATE_NO_WINDOW，两种标志互斥；CLI 自身输出 EOF 严格测试继续保留。不能将前四次失败单独归因于引号或 stdin。第五次该实现完成 APS 实机验证。

历史参照为同机相邻仓库 [LaunchDesk.Next 的 FNF7 与原生启动契约记录](../../../LaunchDesk.Next/docs/reviews/licence-checkout.md)，本次审阅日期 2026-10-31。该历史对照中，恢复原生等价引号但保留标准流重定向仍失败；恢复原生标准句柄、不重定向后两次进入 E3D3.1 项目界面，隐藏控制台本身被排除。其结论收敛到实验器重定向标准流/继承管道句柄的启动模式，未单独区分 stdin、stdout、stderr。该记录只是本次调查线索，既不是当前 APS 故障的最终归因，也不是当前实机通过证据。

实机交互复现脚本位于本机 `artifacts/manager/cli-host-compat/launch-aps.ps1`、`launch-aps-held.ps1`；密码交互读取，不作为落盘输入。重新执行会启动 APS，须处于用户授权的实机验证范围。

## 后续验收与交接边界

APS DESIGN 成功登录后，获取实机 PID，再使用新的证据目录运行严格脚本：

```powershell
& tests/live/form-query/Invoke-FormQuerySmoke.ps1 `
  -CliExecutable $taskCli -DotnetExecutable $taskDotnet `
  -ProcessId <verified-design-pid> `
  -PmlDirectory 'C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha\PMLLIB\YuzuhaV04LocalTests' `
  -HostDll 'C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha\runtime\profiles\E3D2.1\net48\YuzuhaToolkit.PmlHost.Net48.dll'
```

`<verified-design-pid>` 是占位符，须替换为核验后的 PID。最终验收必须完成身份/能力核验及全部原严格断言，并保留每批输入、输出、stderr 和最终运行元数据。启动 CLI 成功、捕获耗时和单元测试数量均不能代替实机验收。会话正常退出与截图已完成；任务状态见 OpenSpec 8.9、8.10。


## 最终实机结果与永久证据

第五次启动 operationId 为 `543e20ae-5c62-43dc-ba15-0c1f252e9842`，CLI 在 374 ms 返回完整 JSON、退出码零。Host 严格核验确认为 PID `25624`、起始 ticks `639290226957908482`、模块 DESIGN、工程 APS、Host 0.3.4.2，FormQueryVersion 与 BoundedArrayReadVersion 均为 1。

`live-smoke/run.json` 的 status 为 Passed：真实数组续页、Forms/Gadgets 查询与原生分页、非 Gadget/字符预算拒绝、回调/值/显示状态无副作用均通过。旧内置 CE 兼容查询为 OK 30、Unparsed 0。最终通过依据为上述实际 Host/查询证据，而非启动回执本身。

永久原始证据目录：[cli-host-compatibility-2026-10-31.evidence](cli-host-compatibility-2026-10-31.evidence/)。包含第五次启动/计时/Host/CE 原始 JSON、完整严格 smoke 输入输出与元数据、单 DLL 部署计划/回执及未变基线、最终 26 项 CLI 和 33 项 Host TRX、DocFX 日志。复制保留原始字节，未通过 JavaScript JSON 解析重写大整数 ticks。前四次失败回执与通信错误截图分别保留，未被第五次成功覆盖。



## 收尾与工作台交接

验证父 PowerShell 正常 exit0 后，独立 CLI 严格查询仍通过，APS 持续处于 ProjAPS，证明运行不依赖父终端持有（strict-after-owner-exit.json）。截图 aps-verified.png 已保存。随后通过厂商原生 Leave Model and Exit 正常退出 PID25624，无保存提示（session-cleanup.json），没有工程模型写入。

工作台已从核对后的旧 PID22236 切换到新包 PID19600，沿用端口18642及原 live-state；旧包保留（workbench-update.json），HTTP 200 已核对。浏览器自动刷新接口不可用，已打开页面需手动刷新以取得新服务会话。以上证据文件均位于永久证据目录。
