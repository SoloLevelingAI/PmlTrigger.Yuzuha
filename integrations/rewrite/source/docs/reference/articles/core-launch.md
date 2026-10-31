# 核心能力：AM / E3D 启动

多版本启动是 LaunchDesk 的核心能力。工作空间、目录和协调器负责把它整合为工具箱；修改具体产品的参数时，从下面两个纯方法开始，不必先遍历整套架构。

## 两种协议，两个方法

| 入口 | 方法 | 固定参数顺序 | 可变配置 |
| --- | --- | --- | --- |
| Monitor EXE | [BuildMonitorArguments](xref:LaunchDesk.UseCases.LaunchPlanBuilder.BuildMonitorArguments(LaunchDesk.OperationContracts.ApplicationDefinition,LaunchDesk.OperationContracts.ProfileDraft)) | `PROD <product> init <init> <project> <user>/<password> /<mdb> <module>` | mon.exe 路径、产品代码、init 路径、项目、用户、MDB、模块 |
| Marine BAT / CMD | [BuildMarineArguments](xref:LaunchDesk.UseCases.LaunchPlanBuilder.BuildMarineArguments(LaunchDesk.OperationContracts.ApplicationDefinition,LaunchDesk.OperationContracts.ProfileDraft)) | `<module> noconsole <project> <user>/<password> /<mdb>` | 批处理路径、模块、项目、用户、MDB |

密码不传给这两个构造方法。它们返回 `LaunchArgument` 数组，登录参数只保留 `SecretSlot=true`、空值和 `SYSTEM/` 等非秘密前缀。

## 三组配置示例

以下密码均为遮蔽占位符，不可直接作为真实凭据使用。路径来自明确应用配置，显示版本只是分组，不能决定所有启动行为。

```text
"C:\Program Files (x86)\AVEVA\Everything3D2.10\mon.exe" PROD CATALOGUE init "C:\Program Files (x86)\AVEVA\Everything3D2.10\catalogue.init" SEA SYSTEM/****** /ALL paragon
"C:\Program Files (x86)\AVEVA\Everything3D3.1\mon.exe" PROD E3D init "C:\Program Files (x86)\AVEVA\Everything3D3.1\launch.init" SEA SYSTEM/****** /ALL Design
C:\AVEVA\Marine\OH12.1.SP4\marine.bat Paragon noconsole SEA SYSTEM/****** /ALL
```

E3D 2.1 Paragon 的这组已确认命令使用 **CATALOGUE + catalogue.init**。Design 通常使用 **E3D + launch.init**。在“应用与版本”分别登记两个入口定义，可以共用同一个 mon.exe，但分别设置产品代码与初始化文件；各登录选择对应定义。不要为修复 Paragon 而覆盖仍供 Design 使用的入口。

## Marine 引号为什么会导致失败

本机 Marine 12.1 SP4 脚本使用 `set moduleArg=%1`，再与 `Paragon` 比较。把模块写成字面 `"Paragon"` 会把引号带入比较，无法选择预期模块。脚本随后还通过 `cmd /c` 将其余参数转交原生程序。

[WindowsProcessGateway.StartAsync](xref:LaunchDesk.Windows.WindowsProcessGateway.StartAsync(LaunchDesk.OperationContracts.LaunchPlan,LaunchDesk.OperationContracts.SecretLease,System.Threading.CancellationToken)) 因此只给**批处理路径**编码引号；五个协议词元先通过 [MarineBatchPolicy](xref:LaunchDesk.Kernel.MarineBatchPolicy)，再不带额外引号传递。不能原样穿过多层 cmd 的空白、控制字符、双引号、百分号、感叹号、命令控制字符或参数分隔符会返回明确错误；原生 EXE 仍使用 ArgumentList，并保留其更宽的字符支持。

运行时密码只在创建进程边界临时渲染；处理器及时释放 SecretLease，LaunchDesk 不把它记录为计划或日志。厂商脚本自身的回显行为属于外部程序，不构成 LaunchDesk 的秘密日志保证。

## 示例怎样变成断言

`LaunchPlanExamplesTests` 从编译生成的 XML 中读取两个方法的 `<example><code>`，与真实参数数组的遮蔽渲染逐字比较；同时单独断言顺序、参数个数、MDB 和唯一秘密插槽。改注释或改构造结果时，任意一侧不一致都会失败。

纯参数断言之外还有真实进程测试：

- Marine 模拟脚本保留厂商 `%1` 比较和嵌套 cmd 转交；先证明旧的带引号模块返回失败，再断言新网关实际交给接收进程的是五个正确参数。
- 两个 Monitor 接收进程分别核验 `E3D + launch.init` 与 `CATALOGUE + catalogue.init`，包括带空格路径、秘密槽渲染和独立许可环境。
- 不支持的 Marine 秘密输入必须在创建批处理前失败；测试脚本的执行标记不得出现。

执行 `npm run test:rewrite` 会运行这些检查及既有架构、配置、交付和兼容场景。测试使用虚构凭据与接收程序，不启动真实 AVEVA，不证明真实项目登录或许可签出成功。

## 想改哪一步，就到哪里

| 修改目标 | 最短阅读位置 |
| --- | --- |
| Monitor / Marine 参数顺序、校验、MDB 规则 | `rewrite/LaunchDesk.UseCases/Launch/LaunchPlanBuilder.cs` 的两个公开构造方法 |
| 工具变量展开和每次启动的环境 | 同类的私有 BuildEnvironment 与 Kernel.ProcessEnvironmentPolicy |
| 原生进程、批处理参数编码及系统错误 | `rewrite/LaunchDesk.Windows/Platform/WindowsProcessGateway.cs` |
| 密码解析／释放和启动阶段 | `rewrite/LaunchDesk.UseCases/Launch/LaunchOperationHandler.cs` |
| 让注释示例跟随功能一起修改 | `rewrite/LaunchDesk.Architecture.Tests/LaunchPlanExamplesTests.cs` |
| 跨进程真实参数回归 | `rewrite/LaunchDesk.Rewrite.Tests/LaunchArgumentsScenarios.cs` |

完整受理与 UI 回路见[启动流程](flows/launch.md)。批处理行为参考微软的 [cmd](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/cmd) 与 [call 参数替换](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/call)说明，产品行为以本机入口脚本和明确配置为准。
