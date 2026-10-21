# PMLTWIN v0.4 第一阶段

开发基线：`codex/yuzuha-native-workspace` / `5d73e1dc5d1dc648f57cef16771938830a87671b`。在独立分支上增量开发，未修改 PMLLIB/PMLUI 原生文件，未实现新的窗体管理器。Form/Gadget 查询、WPF 调试页与本地插件目录属于后续批次。

## 函数注册和调用权限

同一 MCP / CLI 服务新增 `register_pml_function`、`list_registered_pml_functions`、`set_pml_function_permission`、`invoke_pml_function`。先登记本机 `.pmlfnc`，从源码解析函数名与有序标量签名并记录 SHA256；默认 `AllowInvoke=false`。只支持 STRING、REAL、BOOLEAN 参数，复杂对象、多行签名和多行字符串在本阶段明确拒绝。

```json
{"name":"register_pml_function","arguments":{"sourceFile":"C:\\development\\Sample.pmlfnc","functionName":"Sample"}}
{"name":"set_pml_function_permission","arguments":{"functionName":"Sample","allowInvoke":true}}
{"name":"invoke_pml_function","arguments":{"functionName":"Sample","parameters":[{"type":"string","value":"example"}]}}
```

使用真实存在的源码和匹配签名替换示例。授权后仍要通过现有接口明确选择 AVEVA 会话；每次调用前核对许可、源码哈希、参数数量和类型。源码变化要求重新登记和授权。注册表存储在 `%LOCALAPPDATA%/YuzuhaToolkit/functions.json`，开发可使用 `YUZUHA_STATE_DIR` 或 `YUZUHA_FUNCTION_REGISTRY` 隔离；不随 Git 或 CI 工件上传。并发写入使用文件独占锁和临时文件替换，损坏的状态失败退出。

此许可只约束注册调用入口，不是进程安全沙箱；原始 `run_pml_command` 仍按既有授权语义工作。登记不加载 PML，不证明 AVEVA 当前加载的文件就是登记版本，调用前需由使用者确认。返回值是既有 Host 的调度状态，不冒充函数业务返回值。函数信任提示仍保持独立，不将传输错误自动改成“不可信”。

## 数组有界读取

`read_pml_array` 读取已经存在的全局数组：

```json
{"globalVar":"SampleArray","startIndex":1,"maxItems":100,"maxCharacters":32768}
```

需要更新后的 NET35 或 NET48 Host 宣告 `BoundedArrayReadVersion=1`。会话桥核验身份后检查能力；旧 Host 在发送任何数组命令前明确拒绝，不回退到旧全量接口。请求只读取已有数组，不执行生成表达式，不删除源数组，因此续页不会重放函数。

预算范围为 1–1000 个索引槽位、1–262144 个 UTF-16 字符。返回 `ArrayWindow` 包含 `StartIndex`、`IndexUpperBound`、`Rows`、`Characters`、`Truncated`、`NextIndex`（0 表示结束）。按 `NextIndex` 人工续读；页间数组可能变化，不承诺快照一致性。`IndexUpperBound` 取原生 MAXINDEX，不是稀疏数组有效元素数量。保留原生显示和槽位位置，不将 unset 压缩；若该产品不能读取空槽位则返回失败，需按人工案例验证。

字符预算只限制返回行内容，不含 JSON 转义、协议包装，也不限制 AVEVA 把单个值转换成字符串所需的临时内存。超预算的首行明确失败，不拆行、不制造无法前进的续页。其他情况停止并返回截断标记。请求使用唯一临时变量并清理，源数组保持原样。

原有 `run_pml_command_list` 为兼容性保留完整读取语义；需要预算时使用新的 `read_pml_array`。本阶段不改变原生遍历函数或代替 PMLFORMS/FMSYS。

## 验证边界

新增 xUnit 覆盖默认拒绝、授权撤销、源码改变、签名、参数、损坏状态、预算、首行过大、边界溢出与旧 Host 拒绝；实际 MCP 进程和 Native AOT CLI Smoke 检查工具暴露及拒绝顺序。NET35/NET48 独立 RPC 夹具复用生产 DTO 和窗口策略，验证跨运行时序列化，但不执行 AVEVA。

`manual:aveva-bounded-array` 记录目标产品中的实际数组与清理检查，目前未实测。Gadget/FormUse 的缺失能力和操作步骤见 [支持登记](formuse-audit.md)。所有测试按 [测试目录](../../artifacts/docfx/navigation/tests/index.md) 和 [关联维护说明](test-associations.md) 复刻；静态页面不把历史结果展示为当前通过。
