# UI 到服务的源码溯源

DocFX 负责展示关系；现有 `tools/Documentation` 增加 Roslyn **语义分析**与 XAML 映射，不更换项目结构或 RPC 接口。入口见 [真实 WPF 样例及完整绑定清单](../../artifacts/docfx/navigation/ui-trace/index.md)。

目标关系包括：`View → Click / Command / TwoWay 属性写入 → 处理器或 ViewModel → 服务 → RPC`。当前 WPF 主要使用事件处理代码；样例按真实源码展示，不因文档需求改成 MVVM。

## 能定位什么

| 输入 | 识别方法 | 准确边界 |
| --- | --- | --- |
| XAML Click 等事件 | x:Class、唯一两参数处理器及真实方法符号 | 缺失或歧义保留原因；真实项目先经 WPF 编译验证 |
| Command 绑定 | 显式 XAML DataContext、简单属性路径、命令构造器中的方法委托注册 | Execute / CanExecute 按实参对应参数区分；注册不代表已执行 |
| TwoWay 属性变更 | 明确写有 Mode=TwoWay 的绑定指向真实 setter，沿其中调用溯源 | 不猜测控件默认绑定方向；支持简单属性名 |
| OnFooChanged 等回调 | 普通源码或生成源码中实际存在的调用 | 不凭 On 前缀、属性名或特性推导一条不存在的边 |
| C# 事件 | 事件触发源码、具名处理器的 += 订阅 | 处理器只列候选；-= 单独标记，不推断此时订阅状态；匿名/动态处理器保留边界 |
| C# 方法调用 | SemanticModel 的唯一方法符号、程序集、文档 ID 和签名 | 同名类型、重载和泛型不按文本名称串接 |
| 接口、虚方法、委托 | 接口列源码实现候选；虚方法/委托显示动态分派边界 | 不推断当前注入实例，也没有跨方法委托数据流或完整控制流分析 |
| 无法确定的调用 | 显式记录动态调用、模板/动态上下文和不支持的绑定 | ElementName、RelativeSource、多段路径、转换器、运行时 DataContext、反射与 PML 运行时不自动补链 |

`ObservableProperty` / `RelayCommand` 特性只说明存在生成器输入。构建脚本导出本次实际生成源码，再由 Roslyn 分析其中真实 setter/Command 调用。未取得生成输出时保留边界，不按生成器惯例伪造。当前生产项目没有使用 CommunityToolkit MVVM；自动测试通过提供生成 setter 夹具验证此路径，没有声称已运行该第三方生成器。

## 本次范围与复刻

```powershell
./scripts/Trace-Desktop.ps1
./scripts/Build-Documentation.ps1
```

脚本从 MSBuild 获取 Desktop、MCP、NET10 RPC 三个项目的真实编译文件、引用、条件常量及输出类型，包含链接源码、WPF 生成代码和源生成器产物。三个项目依次分析，不能把不同程序集的源文件混成一个大 Compilation；语义输入编译失败即失败。编译导出、生成源码、身份和完整 JSON 在本机 `artifacts/docfx`，不会进入 Git。

DocFX 中有入口清单、对象读取至 RPC 的实际路径、方法调用方/下游、源码行快照和所属类型的 API/测试链接；所属类型页面也有返回溯源的链接。示例路径断裂或找不到唯一入口会使完整文档构建失败，不能零匹配后仍展示成功。这里只发布仓库源码和编译产物，不读取用户 AVEVA 工程。

`OnSthChanged` 若由生成的 setter 调用，能追到该 setter 与回调的实际调用；事件订阅后的触发者、命令是否可用、回调是否真正执行，仍需运行证据。静态关系不证明调用顺序、权限已通过、测试覆盖或实机成功。真实执行链后续可叠加既有 RequestId 日志和 DocFX 测试证据，继续保留提交、环境和 NotRun/历史结果的区分。

## 验证

`tests/documentation-ui-trace.test.mjs` 覆盖：重载与动态调用、Command 与 CanExecute、生成 setter 与 OnChanged、显式接口实现候选、模板/未知 DataContext、缺少生成器输出、显式 TwoWay 及虚方法分派。开发验证脚本统一执行。真实仓库路径另由文档发布阶段进行非零及连续性校验。

独立验证也可使用既有 ID：`./scripts/run-documented-test.ps1 -TestId 'smoke:documentation-contracts'`，其中包含八项 UI 语义/边界测试及原有文档、目录与执行器验证，还检查 XAML 程序集限定不会错接同名类型。

实现依据：[Roslyn SemanticModel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel?view=roslyn-dotnet-4.14.0)、[RelayCommand 生成器](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand)、[ObservableProperty 生成器](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty)。
