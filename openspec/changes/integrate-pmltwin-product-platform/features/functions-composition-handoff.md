# Feature 注册与函数管理交接

映射：整体任务 2.1 / 5.8；原管理器 M5 函数与权限页面仍未实现。本切片不改变 canonical contract，不声称 CAF、真实 Host 加载或界面验收通过。

## 唯一实现

`ManagerOperationCatalog` 把既有操作注册交给 `FeatureRegistry`；`feature.list` 是只读入口。每个操作只属于一个 Feature，重复 Feature、重复操作所有者、未知契约版本、工厂声明不一致、启用依赖缺失均拒绝。功能元数据读取不调用工厂，禁用功能不调用工厂。既有插件仍通过 `PluginOperations` → `ManagerPluginRegistry` → 通信 worker，不增加 Addin 或 worker 加载器。首期目录按现有操作族编组，功能版本 1.0、契约版本 1、状态 `partial-operation-family-adapter`；目录不把已存在操作族等同于完整产品 Feature。`entryKind=compiled` 表示 Manager 内的薄适配器，函数/会话 owner 指向独立通信 CLI，插件 owner 指向既有 worker 边界。CAF adapter 没有注册。

注册和启用不是授权。`OperationExecutor` 继续执行严格 schema、PID、执行/写入/启动许可校验。工厂仅装配既有处理器；不得在工厂内执行业务动作。`FeatureRegistry` 的显式注册接口可表达禁用扩展；基础目录中的 `plugin` 表示已有插件管理入口，单个包的启用/版本/声明操作仍由原插件注册表权威管理。

实际宿主装配入口是 `ManagerOperationCatalog.Create(featuresOptions: new FeatureCompositionOptions(["functions", "plugin"], ProductProfile: "E3D2.1"))`；该执行器没有函数或插件调用操作，`feature.list` 仍列出其禁用状态，普通管理状态与启动操作继续存在。选项仅对该执行器生效，不写全局配置、不取消旧执行器已受理操作。未知禁用 ID 拒绝，避免拼写错误静默启用。当前只支持 `Runtime="manager-net10"`；指定 CAF/NET35/NET48 为管理宿主运行时直接拒绝，不把现代 Manager DLL 载入 AVEVA。

| 支持声明 | 含义与实际边界 |
|---|---|
| AM / PDMS | 既有 Host profile 映射 NET35；函数/会话薄适配器可传输原协议，具体产品版本和已加载源仍须实机核验 |
| E3D2.1 / E3D3.1.6 | 既有 Host profile 映射 NET48；同上，不从某个 E3D 实测推定其他版本通过 |
| 其他产品或版本 | 显式 ProductProfile 未声明即拒绝，不自动回退或改写 |
| CAF | 未支持；无 CAF adapter 注册，非 XML Addin 兼容承诺 |
| product-independent | SupportedProductProfiles 为空表示本机管理适配器不要求工程产品，不表示支持任意 AVEVA Host |

`CompatibilityStatus` 保留 `manager-adapter-only;CAF-unsupported;Host-unverified`。`SupportedProductProfiles` 只声明既有边界中的可识别 profile，不是认证版本矩阵。禁用实际组合回归会断言对应操作不可发现、基础操作可调用，且没有创建通信客户端或 worker。

## 函数入口

管理 CLI 保留通信 ID：`pml.function.register`、`pml.functions.list`、`pml.function.permission`、`pml.function.invoke`。使用 `op describe <ID>` 查看 schema，使用 `op invoke <ID> --input-stdin` 输入 JSON。

| 操作 | 输入 | 授权与边界 |
|---|---|---|
| pml.function.register | sourceFile、functionName | --allow-write；登记和重新登记均撤销调用许可，不加载源文件到 Host |
| pml.functions.list | {} | 只读列举登记；不证明 Host 已加载 |
| pml.function.permission | functionName、allowInvoke | --allow-write；授予时复验源摘要，撤销独立许可 |
| pml.function.invoke | functionName、parameters | --allow-execution 及明确 PID/启动时间/模块；原后端复验许可、源摘要和类型后只发送一次 |

薄入口仅向独立通信 CLI 发送一次原 ID/输入/context，不复制 PML 解析、持久登记或调用后端。通信 CLI 的进程摘要、请求关联、退出码和输出预算校验保持原样；失败原样返回，包括 Host 拒绝与 `mayHaveExecuted`。unknown 不自动重试，取消等待不声称撤销。源码登记身份与当前 Host 已加载版本仍是不同证据；此切片不新增强制加载或任意 PML 执行。

## 本次验证 · 2026-10-31

工具链 `C:/Users/26648/.dotnet/dotnet.exe`，Debug/net10.0。最终完整 Manager 回归使用 `--no-restore` 重新编译，发生于 Ribbon schema、总 timeout/原始错误保留及 Marine 清理竞态修复之后。

| 本次运行 | 结果 | 证据 |
|---|---|---|
| 全部 Manager.Tests | 199总，197通过、0失败、2跳过 | [最终TRX](../../../../docs/validation/platform-2026-10-31/test-results/manager-platform-final.trx) |
| FunctionRegistryTests + ManagerFunctionContractTests | 9通过、0失败、0跳过 | [函数契约TRX](../../../../docs/validation/platform-2026-10-31/test-results/function-contracts.trx) |
| 管理 CLI → 真实通信 CLI 隔离函数烟测 | 11/11通过，0真实AVEVA派发，0重试 | [JSON及CLI哈希](../../../../docs/validation/platform-2026-10-31/functions-cli-smoke.json) |
| 明确指定本机7z.exe后的SevenZipCopyTests补测 | 16总，14通过、2失败、0跳过 | [原始失败TRX](../../../../docs/validation/platform-2026-10-31/test-results/manager-sevenzip.trx) |

完整 Manager 测试包括 `FeatureRegistryTests`：实际 Manager 组合中禁用 functions/plugin 后不可发现其操作，基础状态仍可调用；目录不创建通信客户端。还验证循环依赖、禁用依赖、未来契约/实现主版本、工厂声明不一致、未知产品/运行时/禁用 ID、独立操作所有者。`FunctionManagementProcessTests` 四个原 ID/输入/目标身份跨真实夹具进程不变。`CommunicationProcessTests` 的超时/取消/输出上限/错误回执保留 unknown 且不重发。

9项函数后端/契约测试重新验证源改变、权限撤销、重登记、签名校验，以及四个入口的严格 schema、授权标记、legacy 名与原通信后端一致。真实 CLI 烟测使用独立临时登记文件、独立日志目录和明确不存在的 PID 2147483647。授予许可后的调用返回 `TARGET_FAILED`，消息 `AVEVA_SESSION_NOT_DISCOVERED`，`mayHaveExecuted=false`；未选择、连接或替换为任何实际会话。源改变与再次授权均拒绝，重登记撤销许可，明确撤权后的调用拒绝。

可重复命令（从仓库根目录）：

```powershell
dotnet test tests/YuzuhaToolkit.Manager.Tests/YuzuhaToolkit.Manager.Tests.csproj --no-restore
dotnet test tests/YuzuhaToolkit.Tests/YuzuhaToolkit.Tests.csproj --no-restore --filter 'FullyQualifiedName~FunctionRegistryTests|FullyQualifiedName~ManagerFunctionContractTests'
dotnet build src/YuzuhaToolkit.Manager.Cli/YuzuhaToolkit.Manager.Cli.csproj --no-restore
dotnet build src/PmlTwin.Phayton.Cli/PmlTwin.Phayton.Cli.csproj --no-restore
./scripts/Test-ManagerFunctions.ps1 -ReportPath ./docs/validation/platform-2026-10-31/functions-cli-smoke.json
```

最终完整回归中的两项7z测试因未提供独立 `YUZUHA_TEST_7ZR_PATH` 跳过。随后按明确本机路径 `C:/Program Files/7-Zip/7z.exe` 补测，两条实际格式测试均被既有“必须独立7zr.exe”的严格白名单拒绝；这是依赖不适配的真实失败，不能称7z验收通过。未改名、复制或放宽白名单冒充7zr；14条其他格式安全测试仍通过。正式7zr矩阵保留 unavailable，待明确受支持7zr依赖后重跑。

前两次全回归分别暴露 Ribbon 对象schema遗漏properties（1失败）及 Marine cmd wrapper释放目录竞态（1失败）；修复后才得到最终197通过/0失败/2跳过结果，没有用定向重跑代替最终全量验证。

## 未完成边界

2.1 当前完成的是既有 Manager 操作族的唯一登记、元数据和显式装配禁用契约；尚不等于整体1.2所有来源需求到最终产品Feature的逐条映射完成。未来Feature须显式登记，不能靠此目录的partial状态宣称已实现。单个 worker 包的兼容/启停继续由原插件注册表负责；不增加第二套插件加载器。具体CAF装配与产品版本认证属于5.7及7.x的独立证据，本切片不扩大结论。

5.8的CLI接线、源变化、撤权、无Host拒绝和共用通信unknown边界已有本次离线/无目标实链证据；真实Host已加载某函数的匹配源码、实际业务返回值及函数页面/视觉验收不在本次通过范围。无真实PML执行或安装部署。
