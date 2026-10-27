# 源码导读

先读具体动作，再查接线和支撑。源码页首屏给出 Script、源码行与直接下游调用；接口页给出职责和实现候选。

| 要改什么 | 具体入口 |
| --- | --- |
| 启动配置 | [保存、恢复与校验](../../artifacts/docfx/navigation/features/launch.md) |
| 部署、加载、调试 | [部署脚本](../../artifacts/docfx/navigation/features/deployment.md) |
| 模型数据 | [对象查询](../../artifacts/docfx/navigation/features/objects.md) · [窗体](../../artifacts/docfx/navigation/features/forms.md) · [数组](../../artifacts/docfx/navigation/features/arrays.md) |
| 查资料 | [知识检索与分块](../../artifacts/docfx/navigation/features/knowledge.md) |

前端集中在 Desktop：`Views`=窗口/交互，`ViewModels`=状态/筛选/结果投影，`Presentation`=显示支撑，`Testing`=界面验证。ViewModel 不要求继承基类或套完整 MVVM；只在复用和测试有帮助时提取。

后端位于 Workspace、Mcp.Core、Knowledge.Core。`Scripts` 放具体功能及顺序流程；`Composition` 接 ID→方法/JSON；`Infrastructure` 管存储、权限、日志和传输。编排负责顺序、失败与结果，具体功能负责“这一步怎么做”。按主要用途归组，不要求每类只做一件事，也不靠类名推断调用。

[前端](../../artifacts/docfx/navigation/frontend/index.md) · [后端功能](../../artifacts/docfx/navigation/implementations/index.md) · [编排与接线](../../artifacts/docfx/navigation/orchestration/index.md) · [支撑](../../artifacts/docfx/navigation/infrastructure/index.md)。目录保留命名空间兼容；筛选可组合前后端、模块、项目与输入查找。

`IOperationHandler` 只收发调用，注册可直接传方法；`IConfigurationStore<T>` 只管版本化存储；`IProductModuleCatalog` 只提供目录读口，AM/E3D 规则在 `ProductModuleCatalog`。基础功能保持具体函数，不逐功能造接口或 Command。

[全部 Script](../../artifacts/docfx/navigation/scripts/index.md) · [支撑接口](../../artifacts/docfx/navigation/interfaces/index.md) · [调用图](../../artifacts/docfx/navigation/ui-trace/index.md)

Script 用途维护一次，签名/位置/直接关系由 Roslyn 生成。候选实现、委托目标和 RPC 路由不冒充直接调用或实机结果。

新业务只用 C#；保留平台必需 PML 和现有界面技术。PowerShell 限 Windows 构建/测试胶水，不新增业务实现；复用现有工具链，不额外引入语言或运行时。旧 PML 命令行编排是已知例外，已列待定。

## 启动的数据边界

`UI/CLI → LaunchProfile → AvevaLaunchScript → 存储/凭据/进程 → 回执`

| 层 | 实际职责 |
| --- | --- |
| WorkspaceSettings / LaunchProfile | 仅数据；无 Load/Save、环境解析或 WPF 类型 |
| 窗口 | 编辑、密码输入与清空、确认提醒、交互状态、显示回执 |
| AvevaLaunchScript | 保存与凭据关联、启动前检查、密码选择、进程创建、日志与最近时间 |
| WorkspaceConfigurationStore | 显式快照/修订提交；返回新状态，不修改调用者数据 |
| WindowsCredentialStore / UserDataPaths | OS 凭据与环境路径；不在数据类里读写 |

留空沿用已存密码；取消记住不删除已有凭据。凭据先准备、配置后提交；失败只清理已确认无引用的新凭据，替换已发生时保留引用。共享凭据最后解除后才清理，清理失败返回已提交状态和警告。

进程创建只执行一次，返回 PID 不代表登录/Host 已验证。最近时间或日志故障单独反馈，不能把已启动改成启动失败。CLI 的显式密码（含空值）不读取已存凭据、不隐式写配置。

副作用测试仅替换 OS 进程/凭据委托；配置规则和真实存储保持原路径，未增加逐功能接口或 Command。

旧 `WorkspaceSettings.Load/Save` 调用改为显式快照提交；新状态从返回值取得：

```csharp
var store = new WorkspaceConfigurationStore();
var snapshot = store.Read();
snapshot.Value.PluginName = "MyPlugin";
var saved = store.Commit(snapshot.Value, snapshot.Revision, new("修改插件名称"));
```
