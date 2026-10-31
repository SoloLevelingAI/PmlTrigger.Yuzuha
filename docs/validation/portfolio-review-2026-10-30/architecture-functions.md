# 架构与函数生命周期独立验收

日期：2026-10-30；结论：**带条件可交付，限离线后端与合成演示**。本轮发现的模块身份边界缺口已修复；真实 AVEVA 加载版本、跨模块调用和完整新管理 GUI 仍没有本轮实机验收证据，不能据测试数宣称整个产品达到作品集交付标准。

范围：Manager.Core/CLI → communication CLI → AVEVA，46/52 操作、WPF 后台适配、函数 register/list/permission/invoke。先查了仓库及祖先目录，未发现适用的 AGENTS.md；installer/AGENTS.md 不在本任务修改范围。未连接真实 AVEVA、执行真实 PML、启动 VM、修改 FigCraft 或检查凭据。

## 已证实的结构与行为

| 核对项 | 证据与判断 |
| --- | --- |
| 管理与通信分离 | [Manager.Core.csproj](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Manager.Core/YuzuhaToolkit.Manager.Core.csproj:13) 不引用 RPC/Mcp Core；[通信 Core](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/YuzuhaToolkit.Mcp.Core.csproj:9) 不引用 Manager/Knowledge.Core/SQLite。 |
| 独立进程协议 | [CommunicationCliClient](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Manager.Core/CommunicationCliClient.cs:74) 使用明确路径、文件指纹、无 shell 子进程、UTF-8 stdin/stdout，并核对版本/请求ID/操作ID/退出码；超时/输出超限不自动重发。 |
| 兼容组合层 | [CLI 入口](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/PmlTwin.Phayton.Cli/Program.cs:3) 为旧 op 路径组合启动操作，`--manager-protocol` 单独进入通信目录。旧知识/启动能力仍由兼容层承接；“46”不是纯通信目录数量。 |
| 46/52 | 本轮重建后的原 CLI `op list` 实测46项，与 `artifacts/manager/operation-inventory.json` 中46个 descriptor 序列化比较，差异0；实际Manager `op list`复核52项。管理52项没有在本轮逐项invoke。 |
| WPF 后台 | [DesktopCliSession](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Desktop/DesktopCliSession.cs:22) 首次固定发现的PID/启动时间/管道及Host模块，后续带完整目标；状态失败清空目标。[McpWorkbenchHost](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Desktop/McpWorkbenchHost.cs:54) 对注册调用先本机校验，再经CLI由子进程校验。 |
| 函数生命周期 | [PmlFunctionRegistry](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/PmlFunctionRegistry.cs:55) 只登记源文件、SHA256与标量签名；首次及重新登记均默认拒绝，授予权限与invoke均核验哈希，参数数量/类型/转义由既有生成器处理。 |
| 没有自动加载冒充 | [register描述](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/CommunicationOperationCatalog.cs:177) 明示不加载PML；list列的是本机登记表，既不枚举AVEVA全局函数，也不证明当前加载版本。[桌面提示](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Desktop/SessionWorkbench.cs:129) 将调度成功、业务返回与加载版本分开。 |

函数权限是“已登记源码修订的调用许可”，与人工信任列表分开，也不限制已授权的原始命令入口，见 [PmlFunctionTools](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/PmlFunctionTools.cs:21)。源码不匹配时会拒绝；重新登记会撤权。当前实现不在检测到不匹配时持久写回 `AllowInvoke=false`，恢复登记时原字节可复用原许可；因此不能把它描述为“任意编辑事件都会永久撤权”。这是实现语义边界，本轮不将其认定为未授权执行漏洞。

## 发现、修复与建议

**F1 · P2 · 已修复：带启动时间的通信执行请求可以省略模块。**

- 原因定位：[CommunicationCliEndpoint.cs:37](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/CommunicationCliEndpoint.cs:37) 原来只要求PID对应启动ticks；[YuzuhaRpcBridge.cs:314](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/YuzuhaRpcBridge.cs:314) 只在ExpectedModel非空时比对模块。管理器/WPF调用方会补齐模块，外部协议调用方却可绕过同等要求。
- 修复前复现：复用 `NativeCliCapabilityTests.NativeFixture` 的合成Host，以 `new OperationContext(42,null,true,false,false,12345)` 调用 `pml.command.execute`，观察到 `Success=true / ExitCode=0 / SyntheticDispatches=1 / ExpectedModel=null`。无真实AVEVA执行。反射夹具的SQLite清理失败属于独立宿主环境限制，不计为产品测试失败或完整新测试通过。
- 影响：PID和启动时间不变但Host模块切换时，未显式模块的协议执行会接受当前模块，弱于目标冻结契约。
- 修复：[统一检查](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/CommunicationOperationCatalog.cs:126) 对“需要PID且执行、并携带启动ticks”的操作要求非空模块；原6项与扩展执行路径均在连接前检查。[协议入口](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/CommunicationCliEndpoint.cs:43) 同样检查，失败返回5 / MODULE_IDENTITY_REQUIRED / MayHaveExecuted=false。
- 保留行为：初次只读select/status允许先取得Host模块；旧CLI未带启动ticks时维持既有兼容行为，旧ID/schema/descriptor不变。建议发布说明写清新协议必须传递完整目标。

**F2 · P2 · 文档缺口，已转交总验收合并：现行发现指南仍指导已禁用的MCP部署。**

- 定位：[docs/aveva-discovery.md:48](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/docs/aveva-discovery.md:48) 指导运行旧安装/注册脚本并称两个程序为stdio服务；[NativeCliHost.cs:65](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Cli.Compatibility/NativeCliHost.cs:65) 已返回MCP_DISABLED。
- 影响/复现：按未标历史状态的指南准备接入，会走到已禁用入口，不能复现新的CLI架构演示；无需实际修改客户端即可从两处文本复核矛盾。
- 建议：更新指南及其skill镜像到当前安装器/CLI路径，历史MCP流程显式归档。该文档由其他验收任务处理，本子任务未修改。

**F3 · P2 · 已确认整合缺口：Manager的52项操作没有函数登记四入口。**

- 定位：[CommunicationOperationCatalog.cs:177](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Mcp.Core/CommunicationOperationCatalog.cs:177) 注册了 `pml.function.register`、`pml.functions.list`、`pml.function.permission`、`pml.function.invoke`；[ManagerOperationCatalog.cs:65](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Manager.Core/ManagerOperationCatalog.cs:65) 没有组合这些操作或提供对应别名。
- 复现/影响：本轮实际运行Manager CLI `op list`，得到52项；逐一比对四个ID均不存在，而原CLI/通信目录均存在。现有WPF仍经本机注册表与通信CLI完成这些流程；因此“函数服务已存在”不等于“Manager GUI/CLI统一函数管理已完成”。
- 建议：在后续Feature设计中明确源码登记、许可修改、目标调用分别由哪层拥有，并让Manager经既有通信边界提供等价入口及对照证据；本轮不新增API，不把整合缺口包装为运行时故障。

## 本轮验证与证据限制

- Release构建成功；新增13个合成回归覆盖空/空白模块、9条执行路径、只读取得模块、模块切换和旧无ticks兼容。
- `CommunicationProtocolTests`：19/19通过，0失败/0跳过；[TRX](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/artifacts/manager/portfolio-review-2026-10-30/architecture-functions-protocol.trx)。
- `DesktopCliBoundaryTests | NativeCliCapabilityTests | LaunchOperationTests | FunctionRegistryTests`：24/24通过，0失败/0跳过；[TRX](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/artifacts/manager/portfolio-review-2026-10-30/architecture-functions-compatibility.trx)。
- [DesktopCliBoundaryTests:60](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/tests/YuzuhaToolkit.Tests/DesktopCliBoundaryTests.cs:60) 使用注入委托证明桌面目标传递、清空和不重发；它本身不启动真实通信子进程。
- [NativeCliCapabilityTests:17](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs:17) 覆盖30项核心操作，另 [LaunchOperationTests:85](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/tests/YuzuhaToolkit.Tests/LaunchOperationTests.cs:85) 覆盖16项启动操作；Host为合成服务，不是AVEVA实机通过。
- 普通沙箱构建未返回可用诊断并已中止；单节点、关闭编译服务的本机定向运行通过。未重复全部384项历史测试，也没有用历史总数替代本次证据。
- [runtime.c4](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/docs/architecture/runtime.c4:1) 已按管理→独立通信→RPC→Host、旧CLI组合层、Knowledge/Builtins与可选worker更新；运行关系与生成的ProjectReference图分开。本机现有LikeC4执行 `validate docs/architecture` 为 `Valid (2 files)`，`node scripts/architecture.mjs check` 通过；未联网安装依赖。

复刻命令：先在仓库根执行 `. ./scripts/Use-YuzuhaDotnet.ps1`，再运行下列命令（仅合成Host）：

```powershell
& $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Tests/YuzuhaToolkit.Tests.csproj --no-restore -c Release -m:1 /nodeReuse:false /p:UseSharedCompilation=false --filter 'FullyQualifiedName~CommunicationProtocolTests'
& $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Tests/YuzuhaToolkit.Tests.csproj --no-build --no-restore -c Release --filter 'FullyQualifiedName~DesktopCliBoundaryTests|FullyQualifiedName~NativeCliCapabilityTests|FullyQualifiedName~LaunchOperationTests|FullyQualifiedName~FunctionRegistryTests'
```

## 作品集交付条件

离线可展示“发现→明确选择→状态核验→函数登记默认拒绝→授权→参数预览→调用一次→unknown不重发”，必须标注合成Host。发布前应收齐真实AM/E3D目标的PID/启动时间/管道/模块、加载版本、函数调用回执与模块变化拒绝证据；还需将新管理GUI与52项CLI的已接线/未接线范围公开对照。[既有实施记录](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/openspec/changes/separate-cli-manager-deployment/implementation-verification.md:57) 也明确这些未完成，当前不能标为完整实机交付。
