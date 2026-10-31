# Archon 独立方案：操作协调与证据投影

版本说明：本页为原生审阅前初稿；当前修订及真实运行证据请从 [审阅入口](README.md) 进入。初稿不是已批准的实现规范。

状态：Proposed；架构先行，未批准实现。分支：`architecture/archon`。输入仅为 [共同产品需求](PRODUCT-BRIEF.md)，从用户操作与故障点重新规定架构。Archon 是开发工作流工具，不是 LaunchDesk 的运行时依赖。

类级基线：[类与职责](CLASS-DESIGN.md)、[接口与数据](CONTRACTS.md)、[调用顺序](CALL-FLOWS.md)。这些规定具体类/接口、方法语义、依赖、生命周期与验收归属，尚未生成应用骨架。

## A01 先规定一次操作怎样结束，再分模块

工作台最容易失去可信度的地方，是点击后不知道发生了什么。本候选把“可执行操作及其结果”作为组织中心：目录提供配置快照；协调器先准备计划，之后按操作类型执行和核对；页面读取一致的状态投影。仍是本机模块化 WPF 单体，没有常驻工作流服务器。

| 逻辑模块 | 自己拥有的事实 | 禁止承担的职责 |
| --- | --- | --- |
| WorkspaceCatalog | 应用、版本、登录、模板、工具定义和修订 | 不执行安装或启动；扫描不能自动覆盖人工绑定 |
| OperationCoordinator | OperationId、状态阶段、幂等请求索引、回执关联和结果投影 | 不在核心代码调用 Win32、WPF 或 Aspire API |
| ResourceSupervisor | 许可/服务/虚拟机的独立证据、时效和复查计数 | 不把端口成功当作签出成功，不因网络失败宣称虚拟机不存在 |
| LaunchHandler / DeliveryHandler | 启动计划、文件计划及各自的动作顺序 | 不各自维护第二份跨页面任务状态 |
| PolicyKernel / OperationContracts | 纯规则、版本化请求、结果、观测与有限平台端口 | 不携带 SDK 资源类型、UI 控件或共享密码 |
| PlatformAdapters / WorkspaceStorage | Windows 凭据、进程、文件、JSON、UAC 与恢复记录 | 不决定页面导航；不能绕过已确认计划执行写入 |
| DesktopShell / Bootstrap | 编辑、预览、确认、任务列表和集中装配 | 页面不能成为唯一运行记录，装配例外不能扩散到 ViewModel |
| PrivilegedWorker | 已确认的有限受保护写入 | 不接受任意脚本、登录密码或“以管理员权限执行任何程序” |

逻辑角色不要求各自一个程序集。实现起步为纯 Kernel / Contracts、UseCases、Windows、Desktop 和受控 Worker 的少量边界；角色通过命名空间与接口受检。AppHost 若采用则独立为开发项目。

## A02 操作协议是统一外壳，业务处理器仍然具体

`OperationRequest = ContractVersion + OperationId + Kind + WorkspaceRevision + PlanRef`。Kind 首版只有 Launch、Deploy、ApplyVariables、ImportConfiguration 四类；不能把任意 JSON 动作或脚本当作扩展协议。

协调器共享排队、取消、阶段记录、快照订阅和重入检查。LaunchHandler 负责进程证据；DeliveryHandler 负责文件与变量的回执；导入处理器只产生待保存草稿。不要为了复用状态强行把登录和安装合成一个万能事务引擎。

状态外壳为 `Prepared → Validating → AwaitingConfirmation → Running → Reconciling → Completed / Failed / Cancelled / Indeterminate`，不同操作携带自己的 Stage 和 Evidence。纯检查无需确认；配置导入无需管理员权限；受保护写入先计划确认再 UAC。崩溃后读取回执核对，无法证明完成则保留不确定状态，不能自动重放有副作用动作。

启动证据由 `EntrypointStarted / ChildObserved / WindowObserved / SessionReady / Exited` 表达。只有具体提供者能证明会话就绪；入口退出不能代表子进程退出。90 秒无窗口记未确认，最多观察至 5 分钟；之后停止观察但保留任务与手动核对入口，不再次创建进程。停止跟踪不结束外部应用。

## A03 依赖与副作用规则

`DesktopShell → OperationContracts → PolicyKernel`；Catalog、Supervisor、Coordinator 和具体 Handler 只依赖 Contracts / Kernel，通过端口获取其他模块数据，不引用对方内部实现。平台实现向内依赖契约；Bootstrap 唯一允许引用所有具体实现。依赖图不得成环。

首版只采用直接方法调用与本机订阅，不加消息代理。OperationCoordinator 是应用用例协调代码，不是常驻服务。执行过程中写入必要阶段和回执，不为目录数据建立全量事件溯源。

所有操作先冻结工作空间修订。部署计划额外冻结来源摘要、目标真实路径、差异、备份与变量作用域；执行前重验。相同真实目标互斥，重解析点和目录重叠必须解析后判断。取消写入依据回执进入可恢复状态，不假设已撤销。

## A04 外部状态和秘密必须留在适当边界

资源分别记录服务证据、端口证据和虚拟机进程证据。每轮最多三次、间隔 350ms、TCP 单次最多 2s；曾成功的资源连续三轮失败才通知一次，成功清零，历史 Ready 不能越过有效期。启动前独立确认当前依赖。

许可地址在子进程环境覆盖，不改全局变量。Windows 凭据管理器保存秘密，本机引用仅在执行端解析；输入显隐随保存/离开清除，不回填已保存密码。目标应用强制密码作为参数时仍有本机进程可见风险；日志只记录脱敏计划，不记录完整参数。管理员 Worker 不能获取登录秘密。

工具包本身是数据：ZIP / 7z、文件夹与普通文件使用统一来源描述；扫描只列候选，旧版选择和刷新最新有不同命令。模板提供字段和默认值，不具备执行代码权限。

## A05 数据拥有者与共享边界

WorkspaceStorage 保存工作空间 JSON 和修订；任务回执按 OperationId 单独持久化。写入采用锁、原子替换和恢复备份；另一个进程修订冲突必须显式报告。默认目录、portable 和显式 --config 可选，portable 相对路径以 EXE 为基准，不允许不可写时静默转成其他空配置。

分享包仅含声明式目录及关系。导入先重映射 ID、检查本机路径和关联，排除密码、credentialRef、回执、备份和变量应用状态；保存配置、部署文件和应用变量是后续不同操作。

旧 schema v1 由兼容读取器转换成新目录快照，凭据与工具历史位置不改写。新格式如有必要，必须先准备可读旧数据副本、迁移报告和退出方案；新旧 EXE 不同时写一个工作空间。从零重写指内部实现重新建立，不丢弃用户数据。

## A06 编排、诊断与多机

Bootstrap 可选实际 Aspire DevHost 或普通 .NET 本机协调，两者消费同一 OperationContracts 与诊断事件。生产 WPF 不引用 AppHost / Dashboard；开发测试资源与生产工作空间隔离；停止开发会话不能停止真实 VMware、许可或 AVEVA。

未来执行端复用受约束的版本化操作信封，补充设备身份、能力协商与交互用户会话检查；密码留在执行端。首版只留接口，远端离线不影响本机任务，不把 Windows 服务会话误当作 GUI 桌面。

## 为什么值得比较，以及代价

集中协调让启动和更新共享一致的任务列表、恢复入口和诊断事实，便于之后增加设备执行端。代价是协调器可能变成新的大类；必须限制它只处理外壳，把进程、部署和目录规则保留在具体 Handler / Policy 中。

与纯功能切片相比，本候选多一个稳定的 Operation 协议和投影；收益应通过“切页面结果不丢、断电能核对、两类操作状态一致”验收证明。若实现团队发现协调层只转发调用，就保留薄外壳，不额外引入队列/调度框架。

## 批准后如何验证

先以假时钟、假进程树、临时目录与假凭据端口实现 [场景清单](SCENARIOS.md)，再接真实 Windows 适配器。依赖测试必须检查编译产物并故意加入非法引用。最后按现有 FigCraft 的令牌、图标、圆角和三种窗口尺寸做逐页视觉核对；已安装应用只在兼容、实际会话与视觉审阅均通过后才替换。
