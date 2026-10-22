# UI 源码溯源验证 · 2026-10-22

在现有 Roslyn 文档工具内增量实现，不增加生产项目、第三方包、ViewModel 改造或 RPC 协议。完整使用与边界见 [UI 命令与事件溯源](../reference/ui-tracing.md)。

## 自动测试

先增加测试再实现；`artifacts/ui-trace-red.log`、`ui-trace-more-red.log`、`ui-trace-event-red.log` 保存缺失 CLI 模式、TwoWay/虚分派与事件订阅支持时的失败。`ui-trace-partial-red.log` 还复现了生成 partial 回调被错误定位到空声明的问题，已修正为实际函数体位置。

八项 UI 语义/边界测试包括重载、动态调用、Command 执行与条件委托、生成 setter/OnChanged、接口候选、未知/模板上下文、缺失生成输出、TwoWay、虚分派、事件订阅/解除和 XAML 程序集限定。与现有文档目录测试一起按 `smoke:documentation-contracts` 执行，16/16 通过，TRX 身份规则验证通过；本机证据为 `artifacts/ui-trace-by-id.log` 及该文件末尾定位的 `artifacts/dt` 运行目录。

`artifacts/ui-trace-development.log`：现有产品 xUnit 142/142、ArchUnitNET 11/11，无跳过。Native AOT 的生产代码与同日 GUI 纠偏批次一致，继续复用该构建并执行 CLI 与内置指南 Smoke；证据分别保存于 `artifacts/ui-trace-native-cli.log`、`artifacts/ui-trace-native-builtin.log`。

## 真实仓库检查

`Trace-Desktop.ps1` 分项目导出实际 MSBuild 编译输入及生成代码，所有语义输入须无编译错误。发布阶段必须找到唯一 `SessionWorkbenchView.ReadObject` 的 Click 入口，并从真实方法符号和关系求出到 `RpcClient.InvokeAsync` 的非空连续路径；接口实现仍为候选，回调体不表示执行顺序。`RunVerifiedAsync` 作为桥方法的直接下游同时展示。

完整 DocFX 构建检查静态关系清单、样例连续性、类型反向链接、源码快照行锚点与全部原有 API/测试链接。运行输出为 `artifacts/ui-trace-documentation.log`、`artifacts/docfx/integrity.json`、`artifacts/docfx/ui-trace-metadata.json`。输出包含提交、工作区修改状态及环境；生成页面不是通过凭证，测试目录仍保持默认 NotRun。

没有选择 AVEVA 会话、没有执行 PML、没有保存工程或修改 PMLLIB/PMLUI。没有宣称第三方 MVVM 生成器或 AVEVA 实机通过；源生成 setter 测试使用明确的生成输出夹具。
