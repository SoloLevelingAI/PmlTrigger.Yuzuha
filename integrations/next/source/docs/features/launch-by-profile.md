---
document_kind: reference
implementation_status: implemented
reviewed_on: 2026-10-29
uid: launch-feature
---
# 按配置启动应用

本页描述当前实现；新增远程连接、配置迁移与共享凭据方案见[八节评审稿](../reviews/remote-portable.md)，尚未实施，不与当前行为混写。

<a id="brd"></a>

## ① 价值（BRD）
保存应用、项目和许可连接组合，减少重复填写。首版仅完成发现、编辑、保存、启动与证据查看；不管理目标应用安装、项目创建或许可分发。

<a id="prd"></a>

## ② 范围（PRD）
Windows 使用者可兼任配置维护者。本机 WPF 界面和后台代码均独立于旧工程。现支持 Monitor 原生入口、受约束的 Marine 批处理入口和 Direct 入口。真实厂商登录与许可签出不在自动测试结论内；独立[历史实验](../reviews/licence-checkout.md)曾成功，不能代替当前环境验收。

<a id="usage"></a>

## ③ 操作（FRD）
启动后自动扫描已知目录。左侧列出所有候选，选中后检查路径、初始化文件、项目、用户、MDB、模块和许可地址。所有预填均可改；来源显示发现、预设或人工修改。默认 E3D/DESIGN/ALL 是可改建议，不是已核实项目数据。项目、用户、许可地址未知时留空。字段编辑自动保存为模型草稿，执行时冻结本次输入；不再要求先点“保存配置”。该按钮仍用于保存命名配置。再次扫描只刷新候选列表，不覆盖当前编辑。

Project/Module会从初始化文件及配置子目录收集候选。选中应用后，打开“快照与记录”，查看 `ProjectModules / StaticCandidates` 的Report，可看到代码、路径、来源文件/行号、目录是否存在及诊断；回到工作台将选定的Project代码和Module填入现有输入框，再保存。多个候选不自动选首项；只有空字段且唯一明确声明时才预填，预设及手改值保留。E3D3.1共用E3D2.1项目路径是允许的，不按版本改写。目录存在和静态候选都不等于远程连接、许可或数据库已验证。

保存与启动为独立操作：启动失败不撤销保存。选择已保存配置会载入快照。密码策略 None/Prompt/Stored 分别表示不需要、每次询问或使用本机凭据引用。Stored 仅引用既有凭据；当前窗口不提供新建存储凭据入口，使用 Prompt 可每次输入。普通配置不保存密码。可通过“数据目录”查看配置与 JSONL。

字段错误先修改再保存；版本冲突重新读取。许可检查只是 TCP 可达性，不代表签出。Created 只证明创建进程；WindowObserved 只证明观察到窗口。Unconfirmed/NeedsVerification 必须人工核查目标，不自动重试。取消密码不会启动。离开界面取消订阅，不终止目标应用。

<a id="requirements"></a>

## ④ 约束（SRS）
LaunchModel 是唯一可编辑信息类，LaunchWorkspace在执行时保存完整快照并转换成LaunchRequest，深复制集合。请求不包含密码。后端校验字段、草稿文件Generation与许可。AttemptId 进程内去重，同一编号不同数据拒绝。创建前证据写入失败不启动，创建后失败保留事实并要求核验。崩溃恢复、跨重启去重和断电零丢失不作承诺。

<a id="frontend"></a>

## ⑤ 前端设计（SDD）
原窗口、XAML和样式不变。LaunchViewModel只有转发属性，界面与后端编辑同一个LaunchModel；通知、忙碌状态、候选/记录呈现与错误回显属于前端，候选合并、文件版本、校验、脚本选择属于后端。

```text
Initialize:
    共用WorkspaceHost装配LaunchWorkspace与同一个Model
    Workspace加载draft.json -> Model.Apply -> 绑定通知
编辑:
    UI属性 setter <-> Model字段
    Model.Changed -> Workspace保存草稿 -> 通知界面
Click Save / Launch:
    Save -> Workspace.SaveAsync -> 保存命名配置，不覆盖执行中新增编辑
    Launch -> Workspace.ExecuteAsync(临时秘密句柄)
    前端只显示返回状态、按AttemptId查看记录，不在控件事件中准入
切页 -> 取消订阅，不停止目标进程
```

Model使用不依赖WPF的Changed通知，Toolkit只用于薄适配器；不再维护Load/Capture业务字段副本。端口字符串原样保留，后端负责解析，非法值不会默默变成0。详细文件与失败返回集中在[开发索引](../development/INDEX.md)。

<a id="backend"></a>

## ⑥ 后端设计（SDD）
协调器只列具体执行顺序；点进 `Scripts/` 即可看到产品步骤。准入去重和记录拼装放在 `Execution/`，避免淹没功能主线。脚本是普通C#类，不为每一步新增接口或注册框架。

```text
LaunchWorkspace.ExecuteAsync(secret):
    冻结同一个LaunchModel -> Generation检查与草稿保存
    原子保存snapshots/<AttemptId>.json与必需Record
    解析冻结模型 -> LoginLaunchCoordinator.ExecuteSnapshotAsync
    LaunchAdmission 去重并取得配置准入
    LaunchAttemptRecords.SubmittedAsync -> 字段校验 -> ValidationPassedAsync
    草稿执行不再要求等于命名配置；旧ExecuteAsync兼容入口保留CheckSavedProfile
    CheckAvevaLicence.ExecuteAsync       // RMS本机服务或TCP，按需检查RULES
    ResolveLoginSecret.Execute          // 临时秘密与凭据所有权
    RequireBeforeStartAsync              // 记录成功才允许继续
    按协议选择具体脚本：
        Monitor -> StartMonitor.Execute
        Marine  -> StartMarineBatch.Execute
        Direct  -> StartDirect.Execute
    返回 Created -> ObserveAvevaSession 发布后续状态与记录
    创建后不确定 -> NeedsVerification，不自动重试
```

发现的具体读取链：

```text
FindInstalledAveva -> Prefill -> FindAvevaProjectsAndModules.Execute
  读 evars.bat / evars.init / launch.init（补充 launch.bat / pdms.bat 的模块线索）
  -> 有界展开普通变量、静态call与if exist引用
  -> 沿 projects_dir 检查子目录和evars文件，跳过数据库和已声明数据目录
  -> 返回 Project/Module候选 + 来源 + 诊断 -> 现有记录视图
```

当前安装查找是配置目录递归，不读注册表。AM按Marine识别；PDMS增加目录识别测试，本机无真实PDMS安装则不声称兼容。读取器不执行脚本，不模拟goto/for/动态展开，结果是静态候选而非实际运行环境；UTF-8与BOM文本受字节、文件、深度、目录和时间上限约束。缺失文件、编码或复杂控制流的限制由报告诊断说明。

`StartMonitor` 直接写明八个参数及初始化文件要求；`StartMarineBatch` 写明批处理参数与字符限制；`FindInstalledAveva` 写明搜索目录、匹配规则和预填值。这些功能决定不放进Windows适配器。[具体文件索引](../decisions/architecture.md)提供源码直达链接。

后勤契约在 `Infrastructure/Contracts/`：IProcessHost只接收ProcessCommand；ITcpProbe接收主机与端口；IWindowsServiceProbe接收服务名；IJsonDocumentStore只读写JSON。它们不认识LaunchRequest或AVEVA。SavedProfiles负责功能校验和版本，JsonDocumentStore负责文件替换；CheckAvevaLicence负责许可策略，系统探针只提供测量。ILaunchService、IApplicationDiscovery属于功能对外API，单独放在Feature中。

这样增加了少量具体文件，却缩短了“功能问题→具体做法”的查找路径。后勤接口只在系统边界上抽象，不给每个脚本再套接口。ARC-07同时禁止后勤契约和Platform引用Feature，防止职责重新混合。

NLog仍由单一JSONL路径返回实际写入回调及刷新结果；List<Record>保留不可变快照和重启历史。秘密仅在执行期交给进程后勤，参数证据保留占位符。精确依赖仍以[依赖清单](../dependencies.md)和Directory.Packages.props为准，本次不新增依赖。

<a id="sds"></a>

## ⑦ 工程与扩展（SDS）
输入字段在Feature的 `LaunchModel.cs`，执行请求/返回在 `LaunchInput.cs`；功能API在 `ILaunchService.cs`、`Discovery.cs`；中性的进程、文件、TCP、服务、JSON、凭据和记录接口在 `Infrastructure/Contracts/`。完整路径与直接源码链接见[分层索引](../decisions/architecture.md)。增加产品时先阅读现有具体脚本及原协议实现，再接入所需后勤能力；不凭接口猜产品行为。WorkspaceHost提供login.launch的共用装配/释放入口；开发工具包可移除，不影响登录Feature，具体CAF插件API适配尚未完成。 新的远程连接、TOML迁移、凭据共享授权均按[逐条需求台账](../../openspec/requirements.md)记录；这些是待实施提案，不能用现有引用字段代替授权管理。

<a id="verification"></a>

## ⑧ 验证
见[验证报告](../verification.md)。自动验证分为规则、隔离进程、真实本机只读发现、架构正反例和界面操作。视觉由独立 FigCraft 资料对照。任何一个类别不替代实际 AVEVA 登录与许可验证。
