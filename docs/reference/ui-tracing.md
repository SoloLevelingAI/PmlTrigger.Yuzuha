# UI 到服务的源码溯源

DocFX 负责展示关系；现有 `tools/Documentation` 使用 Roslyn **语义分析**与 XAML 映射，不更换项目结构或 RPC 接口。入口见 [WPF 各页可点击调用链](../../artifacts/docfx/navigation/ui-trace/index.md)。

阅读重点是[从按钮逐级追到后端的实际计算与排查](backend-tracing.md)。前端重点保留 Valid / Validate 涉及的校验、可执行条件、拒绝原因和状态变化，其余 UI 关系辅助定位。配置本机 SDK 后可继续到 NET35/NET48 Host；RPC 路由对应单独标注，原生 PML 内部行为仍是边界。决定与完成条件见[迭代与意见记录](iteration-log.md)。

目标关系包括：`View → Click / Command / TwoWay 属性写入 → 处理器或 ViewModel → 服务 → RPC`。当前 WPF 主要使用事件处理代码；按真实源码展示，不因文档需求改成 MVVM。

## 按页面使用

自动发现当前 13 个业务页：总览、启动工程、插件部署、工程会话、能力与函数、Form/Gadget 与数组、AI 运行、工具日志、知识库、设置、评审中心、测试中心和 Web 应用。另外保留 2 个子页容器与 3 组共享/生命周期入口；共享导航不会被任意归到某个业务页。

1. 选择页面与按钮/事件，点击 **＋** 逐级展开。每个调用位置独立显示，保留父级路径；相同方法在不同分支调用不会合成一个难以阅读的节点。
2. 点击函数名查看实际函数体、调用参数、语法条件、源码和测试。方法详情保留反向 XAML 入口与上下游表格，禁用 JavaScript 时也能浏览。
3. “快速展开到下游函数”自动找到一条静态可达路径并展开其父级；这是导航路径，不是已执行的路径。其他分支继续保留。返回入口可以收起所有分支。
4. 调用树不限制分析深度；单次页面最多渲染 1200 个调用位置，达到上限明确提示。递归仅停止当前路径的重复展开；框架、外部库或隐式成员缺少函数体时保留边界。原有 SVG 关系图仍可切换查看。

方法关联、类型关联和同类型其他方法的测试分别展示。沿调用图可达某方法不表示 UI 行为已被该测试覆盖，类型测试也不冒充方法测试。测试详情继续使用原有 DocFX 目录、测试 ID 与执行器，页面不显示实时通过状态。

评审、测试和 Web 页面中的未接线按钮显示“XAML 未声明 Click / Command”。初始禁用状态单独记录，既不补造业务链，也不据此断言运行时永远不可用。绑定、回调与动态目标的原因会显示在琥珀色节点及详情中。

## 能定位什么

| 输入 | 识别方法 | 准确边界 |
| --- | --- | --- |
| XAML Click 等事件 | x:Class、唯一两参数处理器及真实方法符号 | 缺失或歧义保留原因；真实项目先经 WPF 编译验证 |
| Command 绑定 | 显式 XAML DataContext、简单属性路径、命令构造器中的方法委托注册 | Execute / CanExecute 按实参对应参数区分；注册不代表已执行 |
| TwoWay 属性变更 | 明确写有 Mode=TwoWay 的绑定指向真实 setter，沿其中调用溯源 | 不猜测控件默认绑定方向；支持简单属性名 |
| OnFooChanged 等回调 | 普通源码或生成源码中实际存在的调用 | 不凭 On 前缀、属性名或特性推导一条不存在的边 |
| C# 事件 | 事件触发源码、具名处理器的 += 订阅 | 处理器只列候选；-= 单独标记，不推断此时订阅状态；匿名/动态处理器保留边界 |
| C# 方法调用 | SemanticModel 的唯一方法符号、程序集、文档 ID 和签名 | 同名类型、重载和泛型不按文本名称串接 |
| 计算与校验 | 实际函数体、源码 getter/setter、调用实参、if/else/循环/catch/finally 等语法上下文 | 展示源码本身；不声称已经算出运行值，也不把语法嵌套当完整路径条件 |
| 传入的匿名回调 | 单独的源码节点，标出接收参数名，同时保留包装器调用 | 回调内调用不再摊到外层；没有跨方法委托数据流证明，不代表绕过包装器或已执行 |
| RPC 到 Host | 常量 RpcMethod 路由、RpcService/RpcOperation 契约、ForService/AddImplementation 注册同时吻合 | 单独标为跨进程对应；不证明实例已启动、选中了哪个框架或负载兼容；缺一保留未知 |
| 接口、虚方法、委托 | 接口列源码实现候选；虚方法/委托显示动态分派边界 | 不推断当前注入实例，也没有跨方法委托数据流或完整控制流分析 |
| 无法确定的调用 | 显式记录动态调用、模板/动态上下文和不支持的绑定 | ElementName、RelativeSource、多段路径、转换器、运行时 DataContext、反射与 PML 运行时不自动补链 |

`ObservableProperty` / `RelayCommand` 特性只说明存在生成器输入。构建脚本导出本次实际生成源码，再由 Roslyn 分析其中真实 setter/Command 调用。未取得生成输出时保留边界，不按生成器惯例伪造。当前生产项目没有使用 CommunityToolkit MVVM；自动测试通过提供生成 setter 夹具验证此路径，没有声称已运行该第三方生成器。

## 本次范围与复刻

```powershell
./scripts/Trace-Desktop.ps1
./scripts/Build-Documentation.ps1
# 可选：使用自己的本机 SDK 目录继续分析两个 Host；不会部署或运行它们
./scripts/Build-Documentation.ps1 -LegacySdkDirectory '<AM/PDMS 二进制目录>' -E3dSdkDirectory '<E3D 二进制目录>'
```

脚本从 Desktop 开始，按 MSBuild 的真实 ProjectReference 自动遍历依赖；基本范围是 Desktop、MCP、NET10 RPC、Workspace、Knowledge、CommandLine 六个项目。两个可选 SDK 参数也可由 `YUZUHA_TRACE_LEGACY_SDK` / `YUZUHA_TRACE_E3D_SDK` 环境变量提供，额外导出对应 Host；未配置则在页面列出未分析范围，配置错误或编译失败则失败。NET48 使用本机 Visual Studio MSBuild。不会用伪造 AVEVA 类型替代实际 SDK，也不复制专有 DLL 到文档站点。

各项目分别使用真实引用、条件常量、链接源码、WPF Page/ApplicationDefinition 和生成器输出，不把不同程序集源码混成一个 Compilation。语义输入编译失败即失败。编译导出、生成源码、身份和 JSON 在本机 `artifacts/docfx`，不会进入 Git。

DocFX 中有全部页面与入口清单、可达源码成员、方法调用方/下游、源码行快照和所属类型的 API/测试链接；所属类型页面也有返回溯源的链接。各页、符号、源码行与测试链接缺失会使完整性验证失败；原有对象读取至 RPC 的样例也继续检查非零与连续性。这里只发布仓库源码和编译产物，不读取用户 AVEVA 工程。

`OnSthChanged` 若由生成的 setter 调用，能追到该 setter 与回调的实际调用；事件订阅后的触发者、命令是否可用、回调是否真正执行，仍需运行证据。静态关系不证明调用顺序、权限已通过、测试覆盖或实机成功。真实执行链后续可叠加既有 RequestId 日志和 DocFX 测试证据，继续保留提交、环境和 NotRun/历史结果的区分。

本分析以显式方法调用、对象构造、源码属性访问、事件及已支持绑定为基础；不声称完整追踪字段初始化顺序、框架 getter、using 释放等所有编译器展开，也不进行完整控制流或跨方法委托数据流证明。函数体的普通赋值与计算可直接阅读，回调体内的调用可能来自条件分支，不能据树重放执行。

## 验证

`tests/documentation-ui-trace.test.mjs` 覆盖：重载与动态调用、Command 与 CanExecute、生成 setter 与 OnChanged、显式接口实现候选、模板/未知 DataContext、缺少生成器输出、显式 TwoWay 及虚方法分派。开发验证脚本统一执行。真实仓库路径另由文档发布阶段进行非零及连续性校验。

独立验证也可使用既有 ID：`./scripts/run-documented-test.ps1 -TestId 'smoke:documentation-contracts'`，包含 UI 语义/边界、页面发现、构造调用、测试关联、环路/菱形关系和图预算测试，以及原有文档、目录与执行器验证。

实现依据：[Roslyn SemanticModel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel?view=roslyn-dotnet-4.14.0)、[RelayCommand 生成器](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand)、[ObservableProperty 生成器](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty)。
