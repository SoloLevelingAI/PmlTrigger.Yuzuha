---
document_kind: reference
implementation_status: implemented
reviewed_on: 2026-10-29
---
# 从功能找到具体做法

先读协调器看顺序，再打开相应脚本看做法；后勤接口只描述系统能力。下面链接指向真实源码，DocFX构建时直接复制源文件，不维护第二份代码投影。

| 你要找什么 | 具体源码 |
|---|---|
| 登录启动的完整顺序 | [LoginLaunchCoordinator](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/LoginLaunchCoordinator.cs) |
| Monitor的八个参数和初始化要求 | [StartMonitor](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/StartMonitor.cs) |
| Marine批处理参数和限制 | [StartMarineBatch](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/StartMarineBatch.cs) |
| 无附加参数的直接启动 | [StartDirect](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/StartDirect.cs) |
| AVEVA子进程环境 | [AvevaProcessCommand](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/AvevaProcessCommand.cs) |
| 到哪里找应用、怎样预填 | [FindInstalledAveva](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/FindInstalledAveva.cs) |
| 从初始化和子目录查Project/Module | [FindAvevaProjectsAndModules](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/FindAvevaProjectsAndModules.cs)、[报告与限制](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/ProjectModules.cs) |
| RMS、RULES检查次序 | [CheckAvevaLicence](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/CheckAvevaLicence.cs) |
| 保存内容和版本一致性 | [CheckSavedProfile](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/CheckSavedProfile.cs)、[SavedProfiles](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/SavedProfiles.cs) |
| 临时密码与凭据所有权 | [ResolveLoginSecret](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/ResolveLoginSecret.cs) |
| 启动后状态观察 | [ObserveAvevaSession](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/ObserveAvevaSession.cs) |
| 去重准入、记录字段和故障门槛 | [LaunchAdmission](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Execution/LaunchAdmission.cs)、[LaunchAttemptRecords](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Execution/LaunchAttemptRecords.cs) |
| 执行请求和返回值 | [LaunchInput](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/LaunchInput.cs) |
| 功能对外API | [ILaunchService](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/ILaunchService.cs)、[Discovery](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/Discovery.cs) |
| 通用进程命令与接口 | [ProcessHost](../../src/LaunchDesk.Next.Application/Infrastructure/Contracts/ProcessHost.cs) |
| 文件、TCP、服务、JSON、凭据及日志后勤接口 | [SystemCapabilities](../../src/LaunchDesk.Next.Application/Infrastructure/Contracts/SystemCapabilities.cs) |
| 前端显示与显式事件 | [LaunchViewModel](../../src/LaunchDesk.Next.Desktop/Features/Launch/LaunchViewModel.cs)、[LaunchInteraction](../../src/LaunchDesk.Next.Desktop/Features/Launch/LaunchInteraction.cs) |
| 同一输入模型与编辑/执行后端 | [LaunchModel](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/LaunchModel.cs)、[LaunchWorkspace](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/LaunchWorkspace.cs) |
| 唯一Feature装配与窗口入口 | [WorkspaceHost](../../src/LaunchDesk.Next.Bootstrap/WorkspaceHost.cs)、[CompositionRoot](../../src/LaunchDesk.Next.Desktop/Bootstrap/CompositionRoot.cs) |

Domain保存通用命令行与敏感字段规则；Application的Features保存具体功能，Infrastructure.Contracts保存中性能力；Platform实现Windows系统操作；Desktop显示和收集输入，仅Bootstrap装配Platform。Platform和后勤契约均不引用Features。

ARC-01—06继续守卫层次、ViewModel、DTO、组合根和依赖方向；ARC-07守卫后勤不认识功能类型，生产检查和真实违规夹具使用相同规则。XAML、反射、运行副作用及美观度由其他验证覆盖。

发现默认根、深度、文件上限和预设直接见FindInstalledAveva。独立数据目录的discovery.settings.json与命令行工具共享选项。扫描只读，不跟随目录连接；丢失目录、权限、上限与超时有诊断。单次被OS阻塞的枚举不能强制中止。

依赖与许可的唯一索引见[依赖清单](../dependencies.md)。

产品与开发包复用WorkspaceHost及同一后端DLL；独立实验代码与包哈希见[开发索引](../development/INDEX.md)。新Bootstrap层只装配Application与Platform，不放产品流程或VM逻辑。
