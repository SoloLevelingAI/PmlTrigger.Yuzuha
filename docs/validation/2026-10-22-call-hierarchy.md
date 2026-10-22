# 逐级调用树与 Host 对应验证

本批响应 TRACE06：从按钮/事件逐级追到后端命令、计算函数与最下层可定位源码。测试基线为 `d4012c13aa9dfbdd0a478103685ceba7378cd0b1` 加本批工作区改动，分支 `codex/pmltwin-v04-wpf-flows`；独立提交后仍保留 PR 人工审查，不合并 Main。

## 交付范围

- 默认采用逐级展开的调用树，保留每个调用位置和父级路径；可以直接定位下游函数，原有关系图仍可切换。
- Roslyn 提供实际函数体、摘要、实参、源码属性访问和语法条件。匿名回调单独建节点，保留接收它的包装器，不把回调体摊到外层冒充直接调用。
- 两个 Host 可从本机实际 SDK 取得编译输入。NET35 的 AddService 与 NET48 的 ForService/AddImplementation 分别识别，结合客户端只读路由常量、操作契约与注册证据建立跨进程对应。
- 复用现有 Documentation C# 项目、MSBuild、DocFX 模板、源码快照、test-map 和按 ID 执行器，没有新增依赖或修改生产接口及 PMLLIB/PMLUI。

基本范围为六个桌面依赖项目；本机另加入 AM/NET35 与 E3D2.1/NET48，合计八个编译输入。对应 13 个业务页、209 个 XAML/生命周期入口，105 个未解析入口保留原因。编译得到四条带证据的 RPC 对应（两个操作 × 两个框架），不等于四次请求已运行。

## 验证记录

环境：Windows 11 build 26200，.NET SDK `10.0.400-preview.0.26322.102`、PowerShell 7.6.5、Node 24.18.1；Host 使用本机 AM 12.1.SP4 与 E3D 2.1 引用。

| 检查 | 结果与证据 |
| --- | --- |
| 先加测试 | 调用位置/条件、独立回调、getter、分支路径/递归、RPC 注册/缺失/可变路由的测试先失败；`artifacts/call-tree-red.log`、`call-rpc-red.log`、`call-rpc-legacy-red.log` |
| 文档自动测试 | 27/27；`artifacts/call-tree-node-final.log`。覆盖既有测试关联和新增层级/语义边界 |
| xUnit | 142/142；`artifacts/tests/development_net10.0_20261022110322.trx` |
| ArchUnitNET | 11/11；`artifacts/tests/development_net10.0_20261022110201.trx` |
| 测试身份/零匹配保护 | 既有 PowerShell 运行器验证通过；`artifacts/call-tree-development.log` |
| 真实 Host 编译与语义输入 | NET35/NET48 均通过；`artifacts/call-tree-capture.log`、`call-tree-documentation.log` |
| Native AOT Smoke | CLI/MCP 与内置工具测试本次重新通过；`artifacts/call-tree-native-cli.log`、`call-tree-native-builtin.log` |
| DocFX 完整构建 | 通过，零警告/错误；源码、类型、入口和测试链接完整性通过；`artifacts/call-tree-documentation.log`。最终渲染与真实路径复核见 `artifacts/call-tree-final-render.log` |
| AVEVA 实机 | **NotRun**；未启动、连接或保存工程，静态编译不代替实机测试 |

本批不改运行时源码。Native AOT 使用已有 `artifacts/native-corrections` 构建，执行前核对其 `native-aot-manifest.json` SHA256；本次 Smoke 是当前运行结果，二进制不是本批重新发布，也不是 AVEVA 实机证据。

## 可复刻方式与边界

执行 `scripts/Test-Development.ps1`；文档构建用 `scripts/Build-Documentation.ps1`，需要 Host 时传入自己的 `-LegacySdkDirectory` 与 `-E3dSdkDirectory`。SDK 缺失时明确列出未分析项目，配置错误和编译错误直接失败，不使用假 SDK 填链。测试输出和编译输入保留本机 artifacts，不提交或上传用户工程、专有 DLL。

打开 DocFX 的“后端调用链与排查”，默认查看读取窗体入口到 Host 查询算法。点击函数名检查计算与返回值；展开下级继续到 `PmlFormQueryHost.Execute` 和 AVEVA `Command.Run`，后者没有仓库函数体，必须显示外部边界。生成的 `reference/ui-trace/backend-samples.json` 保留两个框架经查询算法到原生边界的静态路径，发布时缺链即失败。

调用树提供静态关系，不提供运行值或完整控制流证明。接口、虚分派、委托目标仍有候选；回调需要包装器调度；RPC 对应不证明选中了该 Host、注册已执行或负载兼容。普通计算可从实际函数体核对；外部框架、PML 动态对象和原生实现内部不编造。测试关联与覆盖、本次通过分别判断。
