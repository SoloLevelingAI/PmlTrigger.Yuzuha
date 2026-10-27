# 从具体功能开始

| 要改什么 | 直接入口 |
| --- | --- |
| 纯配置数据 / 显式修订存储 | [WorkspaceSettings](YuzuhaToolkit.Workspace/WorkspaceSettings.cs) / [WorkspaceConfigurationStore](YuzuhaToolkit.Workspace/Infrastructure/WorkspaceConfigurationStore.cs) |
| 配置校验、保存、冲突、恢复 | [LaunchProfileService](YuzuhaToolkit.Workspace/Scripts/LaunchProfileService.cs) |
| 产品模块差异 | [ProductModuleCatalog](YuzuhaToolkit.Workspace/ProductModuleCatalog.cs) |
| 启动参数 / 创建进程 | [LaunchService](YuzuhaToolkit.Workspace/Scripts/LaunchService.cs) / [AvevaLaunchScript](YuzuhaToolkit.Workspace/Scripts/AvevaLaunchScript.cs) |
| 凭据关联、清理与 OS 存取 | [AvevaLaunchScript](YuzuhaToolkit.Workspace/Scripts/AvevaLaunchScript.cs) / [WindowsCredentialStore](YuzuhaToolkit.Workspace/Infrastructure/WindowsCredentialStore.cs) |
| 备份复制 / 加载验证 | [PluginDeployment](YuzuhaToolkit.Workspace/Scripts/PluginDeployment.cs) / [PmlDevelopmentWorkflow](YuzuhaToolkit.Workspace/Scripts/PmlDevelopmentWorkflow.cs) |
| 知识检索 / 分块 | [KnowledgeRepository.Search](YuzuhaToolkit.Knowledge.Core/KnowledgeRepository.cs) / [Chunking](YuzuhaToolkit.Knowledge.Core/Chunking) |
| 对象 / 窗体 / 数组查询 | [PmlObjectQuery](YuzuhaToolkit.Mcp.Core/Scripts/PmlObjectQuery.cs) / [PmlFormQuery](Shared/PmlFormQuery.cs) / [PmlArrayWindow](Shared/PmlArrayWindow.cs) |

前端集中于 `YuzuhaToolkit.Desktop`：`Views` 放窗口/事件，`ViewModels` 放界面状态/筛选/投影，`Presentation` 放样式与窗体支撑，`Testing` 放界面验证入口。后端实现位于 Workspace、Mcp.Core、Knowledge.Core，可供其他入口调用。命名空间保留，目录不等于强制架构。

`Scripts` 放具体动作及流程；`Composition` 把操作 ID 接到方法；`Infrastructure` 管保存、修订、传输、授权与日志。命名空间和公共操作 ID 保持兼容；目录按职责分，不强迫每个项目套相同层级。

接口只放实际可替换的支撑边界；操作可直接注册方法。产品模块、PML 生成、筛选、切分保持具体函数，不逐功能建立接口、Command 或通用流程引擎。

这里的 `Scripts/*.cs` 是编译进程序的 C#。新业务不写 PowerShell；旧 MCP 安装/注册链与重复 HTML 启动器已删除。构建/测试胶水保留，旧 PML 命令行编排列待定，不增加新语言或运行时。

DocFX 从[源码导读](../docs/reference/source-reading.md)进入；首屏区分前端、功能、编排和支撑，链接由 Roslyn 声明与直接调用生成。
