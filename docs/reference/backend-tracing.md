# 后端调用链与排查

后端溯源优先解释每一步接收什么、计算或判断什么、输出什么，以及失败如何返回。读者应能从症状定位到改变结果的业务步骤，再跳到源码和测试。前端重点保留 Valid / Validate 所涉及的校验、可执行条件、拒绝原因和状态变化；其余控件及框架关系作为辅助。

[迭代决定 TRACE01 至 TRACE03](iteration-log.md) · [可选的理解方法](understanding-notes.md) · [现有静态调用图](ui-tracing.md) · [测试与运行证据](test-associations.md)

## 当前能力与未完成范围

现有 Roslyn 分析从 Desktop 的真实项目引用出发，覆盖六个项目中的显式调用和部分 XAML 映射。可点击图能定位源码与关联测试。NET35/NET48 Host 另有仓库源码参考，但尚未自动接入该调用图；RPC 服务路由到 Host 实现、PML 表达式到原生对象行为也没有自动补链。

下文是对基线 `7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3` 的人工源码核对示例。跨进程对应关系根据稳定路由和 Host 注册代码确认，并非 Roslyn 已经推导出的连续执行轨迹。AVEVA 实机状态保持 **NotRun**。接口实现候选、条件分支、回调与动态调用仍需分别核对。

## Form 查询的后端计算

以已核验会话中的 `shown-forms` 查询为例，其他操作分支单独说明。表中序号表示理解这一条业务路径的顺序，不表示已记录到这些方法依次执行的运行日志。

| 步骤与实现 | 输入和处理规则 | 输出或失败边界 |
| --- | --- | --- |
| 1 [SessionWorkbench.QueryAsync](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.SessionWorkbench.md) | 新查询从索引 1 开始；验证名称、种类和预算，核验目标；要求 `FormQueryVersion=1`。续读必须保持种类、名称和预算，并使用上次 `NextIndex` | 条件变化不能拼接旧结果；无已核验目标或旧 Host 时拒绝 |
| 2 [McpWorkbenchHost.QueryAsync](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.McpWorkbenchHost.md) | 开始本机活动记录，把类型化请求交给已有桥；完成时保存成功、失败或未知及 Host 回执编号 | 日志保留操作及回执线索；没有逐个内部方法的耗时或中间值记录 |
| 3 [YuzuhaRpcBridge.QueryPmlFormsAsync](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Mcp/YuzuhaToolkit.Mcp.YuzuhaRpcBridge.md) | 验证请求，在连接锁内要求已选择服务，再读 Host 身份；核对 PID、启动 ticks、专用管道和模块 | 核验失败不发送查询；同一桥上的会话操作串行处理 |
| 4 [PmlFormQueryRequestSender.SendAsync](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Mcp/YuzuhaToolkit.Mcp.PmlFormQueryRequestSender.md) | 再核对查询和 Host 能力；构造只含 `FormQuery` 的请求 | 不携带原始表达式或删除选项；旧 Host 不回退到原始命令 |
| 5 [AotPmlCommandService](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Mcp/YuzuhaToolkit.Mcp.AotPmlCommandService.md) 与 [RpcClient](../../artifacts/docfx/navigation/source/PlantHost.Rpc.Net10/PlantHost.Rpc.RpcClient.md) | 使用生成的 JSON 元数据序列化；路由为 `yuzuha.pml-command.v1 / run-pml-command`。RPC 核对传输响应的 RequestId | 空响应、编号不匹配或远端错误抛出异常；客户端没有命令重试 |
| 6 Host 注册与分派 | [NET35 Host](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net35/YuzuhaToolkit.PmlHost.PmlCommandRpcHost.md) 与 [NET48 Host](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net48/YuzuhaToolkit.PmlHost.PmlCommandRpcHost.md) 把同一服务注册到当前同步上下文；服务入口断言绑定的 AVEVA 主线程 | 这是人工确认的 RPC 对应关系；错误线程不能处理模型操作 |
| 7 [NET35 服务](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net35/YuzuhaToolkit.PmlHost.PmlCommandRpcService.md) 与 [NET48 服务](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net48/YuzuhaToolkit.PmlHost.PmlCommandRpcService.md) | 检查 `FormQuery` 没有与命令、数组操作或删除混用，调用共享 `PmlFormQuery.Read` | 捕获执行异常并返回失败回执；传输成功与查询成功分别判断 |
| 8 [PmlFormQuery.Reader](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Mcp/YuzuhaToolkit.Pml.PmlFormQuery.Reader.md) | 为本请求建立专有临时变量；`shown-forms` 调用 `!!FMSYS.SHOWNFORMS()`；`forms` 调用 `!!FMSYS.FORMS()`；其他查询使用 `attributes()` / `attribute()` | 复用原生能力，不实现 Show/Hide/Kill；跨请求重新枚举，不保证冻结快照 |
| 9 扫描与预算计算 | 先读 MAXINDEX；每个扫描槽位计入 `Scanned`，先判断 DEFINED，再读必要元数据。将一项所有字符串的 UTF-16 长度相加，决定是否放入结果 | 保留完整项；字符预算不够时 NextIndex 指向未返回的项；首项过大明确失败，避免空页不前进 |
| 10 原生执行与清理 | [NET35 适配器](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net35/YuzuhaToolkit.PmlHost.PmlFormQueryHost.md) / [NET48 适配器](../../artifacts/docfx/navigation/source/YuzuhaToolkit.PmlHost.Net48/YuzuhaToolkit.PmlHost.PmlFormQueryHost.md) 调用 AVEVA Command；false 转异常，并尝试读取 `!!error.text / command / line`。finally 尝试清理本请求变量 | 原生错误详情也可能不可读取；必要读取或清理失败不能冒充成功。Gadget 可选属性失败单独标为 unavailable |
| 11 回执与接收 | Host 返回结果、请求编号、线程和时间等信息；桌面检查 `Success`、结果种类和起始索引，再接收本页 | 失败不能显示为空查询成功；取消或丢失回执不能证明远端未执行，不自动重放 |

源码中的 `Validate` 不只在界面执行：桥、发送器和共享 Host 查询也有检查。前端给出及时反馈，后端继续维护自身边界。具体代码正文可从现有图中的源码快照查看；Host 源码参考当前以类型和成员声明为主，完整函数体还需在仓库文件中核对。

按上述基线查看完整源码：[会话桥](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3/src/YuzuhaToolkit.Mcp/YuzuhaRpcBridge.cs#L226)、[NET35 服务](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3/src/YuzuhaToolkit.PmlHost.Net35/PmlCommandRpcService.cs#L33)、[NET48 服务](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3/src/YuzuhaToolkit.PmlHost.Net48/PmlCommandRpcService.cs#L39)、[共享扫描与预算计算](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3/src/Shared/PmlFormQuery.cs#L155)、[原生执行与错误读取](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3/src/Shared/PmlFormQueryHost.cs#L17)。

### 分页算例

假设原生数组索引上界是 3，索引 2 未定义，`StartIndex=1`、`MaxItems=2`，字符预算足够。槽位 1 读出 Form1，槽位 2 只计扫描、不读值；结果为一项、`Scanned=2`、`NextIndex=3`。从 3 续读才取得 Form3。这里的 3 是索引上界，不能当作窗体数量。

字符预算比较的是 `项长度 <= MaxCharacters - 已累计长度`。下一项放不下且已有返回项时，在该索引停止；`Scanned` 已包含该候选项，`NextIndex` 仍指向它。若第一项本身就超限，返回错误，调用者应调整预算，不能无限重试同一空页。预算限制扫描和返回字符串体积，不限制原生枚举或单个原生字符串的瞬时分配。

关联断言：[稀疏槽位](../../artifacts/docfx/navigation/tests/test-ca2d98e78c56bc11.md)、[字符预算与续读](../../artifacts/docfx/navigation/tests/test-ec6decd9b5bfcf1c.md)、[原生枚举与扫描预算](../../artifacts/docfx/navigation/tests/test-4a1972b63670ee44.md)。这些是使用合成运行时的自动测试，不能证明 AVEVA 原生语法已实测。

## 从症状缩小范围

| 症状 | 先核对什么 | 避免的误判 |
| --- | --- | --- |
| 请求发送前被拒绝 | 请求种类与预算、所选 PID/启动时间、Host 能力、原始错误 | 界面有按钮或窗口存在，不等于目标可执行 |
| 收到空页 | `Success`、结果种类、StartIndex、Scanned、NextIndex | 空页不等于枚举结束；索引上界不等于有效项数 |
| 部分属性不可读 | 对应属性的 Status、Value、Error 及原生版本 | unavailable 不等于 false；成功的空字符串也不等于失败 |
| 命令或读取失败 | Host Code/ErrorMessage、原生错误文本及临时变量清理 | RPC 往返成功不等于业务成功 |
| 超时、取消或回执丢失 | 本机活动记录、目标身份和已有回执；确认操作是否已经发出 | 等待取消不能撤回远端副作用；不要自动重放函数调用 |
| 关联测试看似很多 | 测试是方法、类型还是同类型其他方法关联，断言覆盖哪个分支 | 关联数量不等于覆盖率，更不等于当前运行通过 |

现有传输 RequestId、Host 回执 RequestId 和 RuntimeJournal 活动编号用途不同。当前日志记录了部分对应线索，尚未形成每个后端步骤共享一个编号的完整运行轨迹。排查时记录各自编号及环境，不假定它们天然相同，也不为展示链路改 RPC 契约。

## 测试与证据入口

| 要核对的主张 | 可复刻案例 | 证据边界 |
| --- | --- | --- |
| 旧 Host 不能收到查询 | [OldHostIsRejectedAndCapableHostReceivesOnlyTypedQuery](../../artifacts/docfx/navigation/tests/test-c0155ff1a3de54cf.md) | 验证发送器与合成服务，不是 AVEVA 实机 |
| 非法预算和混合操作提前失败 | [InvalidBudgetsKindsAndMixedOperationsFailBeforeNativeAccess](../../artifacts/docfx/navigation/tests/test-1742bdf9d88b8b5c.md) | 共享查询策略的拒绝断言 |
| 清理失败不伪装成成功 | [CleanupFailureFailsAndStillAttemptsBothTemporaries](../../artifacts/docfx/navigation/tests/test-5528536283f18ef8.md) | 合成原生运行时；源码已扩展为逐项清理，测试名保留历史命名 |
| 已发送函数丢失回执不重试 | [LostInvocationReplyStaysUnknownAndIsNeverRetried](../../artifacts/docfx/navigation/tests/test-794ee04ad76bd9a2.md) | 桌面函数调用场景的合成 Host，不据此声称所有异常均有覆盖 |
| 原生窗体查询实际有效 | [manual:aveva-form-query](../../artifacts/docfx/navigation/tests/test-da475a21649095d0.md) | 当前为 NotRun；按自有夹具、准确产品和 Host 版本单独验证 |

测试详情提供 ID、验证目的、源码、环境、执行命令和预期结果。按 ID 执行后，实际输出默认留在本机 `artifacts/dt`；只有带提交、环境和结果的那次证据才支持运行通过结论。测试目录与源码继续由 Git 管理，不把用户工程内容上传 CI。

## 后续自动溯源的验收要求

以本例逐步补全自动分析。每条关键关系应说明来源是语义解析、明确路由对应、人工核对还是运行观察；条件和候选不能画成无条件执行。方法节点要能定位实际计算和改变结果的条件；只展示方法名不算解释完成。

复用现有 Roslyn 符号、源码快照、DocFX 双向测试链接和 RuntimeJournal。未知的注入实例、动态 PML、原生对象状态和跨进程对应分别保留原因，不能用文档关联替代实现证据。先补测试再实现，每批报告独立的范围、结果和未完成项。
