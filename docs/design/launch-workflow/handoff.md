# 启动工作台 · Feature 交接

Feature：environment-launch。Token 版本：launch-workflow-1。HTML 是本切片的真实交互基线，FigCraft 由主任务串行维护。

## 文件与设计

- `index.html`：中文工作台，环境选择、保存配置、计划、执行与诊断。
- `tokens.css`：浅深主题语义变量，FigCraft 对应 bg/surface/soft/text/muted/line/accent/accent-soft/warn/danger。
- `launch-workflow.css`：布局与组件样式，无内联样式，兼容 `style-src 'self'`。
- `launch-workflow.js`：同源操作适配器与页面状态，无产品发现/启动决策的重复实现。
- `test-workflow.cjs`：离线适配器和流程回归测试。

无字体、CDN、图片或其他外链依赖。HTML/CSS/JS 已展开多行，便于断点与源码定位。

## 实际交互

1. 初载读取操作目录和 `launch.profile.list`。存在唯一保存配置或唯一默认配置时自动选中；多配置无默认时由用户明确选择。
2. 保存配置的非秘密字段填入表单，目标程序作为 `preferredExecutable` 交由发现后端验证。配置已失效时不自动替换为其他安装。
3. 安装、项目、模块下拉先调用共享后端刷新，再打开自定义候选面板。支持方向键、Home/End、Enter/Space、Escape、外部点击及焦点退出。
4. 模块只使用 `fields.module.alternatives` 的最终可启动值，不把扫描到的脚本原名当模块。AM 使用 Outfitting、E3D 使用 Draw；来源与验证状态仍单独显示。
5. 项目、模块、用户、MDB、初始化文件和工作目录可以覆盖。覆盖进入 resolve；`needsInput` 未清空时不会保存或生成计划。首次通常仍需明确选择项目/模块并填写用户/MDB；不猜登录身份。
6. “保存配置并生成计划”读取当前修订、preview、save，再用保存回执实际配置 ID 与修订生成计划。页内刷新与浏览器重新打开均可复用已保存 ID，避免重复新增。显式选“新建配置”才开始独立草稿。
7. 复用配置时保留名称、默认/收藏、环境与许可预检策略；不因发现草稿覆盖而关闭 required 预检。口令始终本次输入，不从保存配置读取。
8. 修改字段使计划失效；确认启动才提交执行。提交前消费内存计划，失败不自动重试。后端错误 data 中的回执继续展示，网络未知结果提示观察原 operationId。
9. 观察只刷新执行事实。入口进程创建、进程存活、Host 身份核验是不同状态，不显示虚假的工程连接成功。

产品安装标签优先使用 versionHint，文件 ProductVersion 不同时标为“组件版本”，避免把入口组件版本误作产品发行版本。

## 操作映射

| UI | Operation | 授权 |
|---|---|---|
| 初载保存配置 | launch.profile.list | 只读 |
| 初载、刷新、候选意图 | environment.discover / environment.resolve | 只读 |
| 保存并计划 | launch.workspace.read → launch.profile.preview → launch.profile.save → launch.plan | save: allowWrite |
| 确认启动 | launch.execute {plan,password,requestId} | allowLaunch |
| 刷新执行状态 | launch.observe {operationId} | 只读 |

GET `/api/operations`；POST `/api/operations/{id}` 请求 `{input,allowWrite,allowLaunch}`。仅同源，meta[name=pmltwin-token] 注入会话令牌，header 为 X-PmlTwin-Token。固定 OperationResult schemaVersion=1 包络，发现 needsInput 的失败仍可携带可读候选 data。

plan 绑定 profileId、expectedRevision、operationId、目标文件哈希、expiresAt 和 planDigest。执行未知时不能生成第二次自动启动。密码只用于内存 POST，请求结束清空，不写浏览器存储、URL、配置、计划或诊断。

## 响应式预期

桌面最大宽 1600，210 导航，内容双栏 1.6:1，卡片内边距 24。1050 以下内容单栏；620 以下导航横向滚动，表单单列。路径与诊断允许换行；候选面板限制高度并滚动。浅深主题共享所有颜色 token。

## 宿主与排障

静态宿主白名单：index.html、tokens.css、launch-workflow.css、launch-workflow.js。文档 `/guide`，源码 `/source/` 路由由宿主固定白名单提供，不能开放任意磁盘路径。

源码定位：LaunchDiscovery.cs（环境证据）、LaunchDiscoveryOperations.cs（发现契约）、LaunchOperationCatalog.cs（配置操作）、LaunchWorkflow.cs（计划与持久执行）、PmlTwin.Phayton/Operations.cs（统一回执）。完整路径见 DocFX uid `pmltwin.launch-workflow`。

## 验证与缺口

`node --check docs/design/launch-workflow/launch-workflow.js` 与 `node docs/design/launch-workflow/test-workflow.cjs` 通过。测试覆盖同源/token、显式授权、needsInput 禁写、用户/MDB override、最终模块候选、重复计划身份稳定、清空页内身份后从保存配置复用、多配置不猜选、未知回执观察、口令清除、无内联样式与外链。

这些是隔离行为证据，不能证明 AVEVA 已登录。没有演示数据或演示模式；服务未连时显示未连接。实机登录、Host 核验、浏览器视觉与窄屏检查由主任务单独记录，本文不宣称通过。浏览器刷新后不自动恢复未保存草稿、密码、未完成计划或观察编号。可在「按操作编号恢复观察」中粘贴持久回执或日志中的 operationId，仅调用 launch.observe；不会重新执行。没有观察列表接口时不伪造历史下拉。

执行观察展示补充：主状态与显眼的进程/登录证据面板直接显示 processObservation 和 hostVerification 的中文含义。即使操作 state=success，进程已退出仍显示「入口已退出」；运行中不等于登录成功，unverified 显示「登录尚未核验」。入口退出使用中性提示色并注明模块可能接管，只有身份不匹配用异常色。


模块交接证据仅展示后端 childCandidates 的程序名、PID、父PID、窗口与SessionID，不从窗口或进程推断登录。无候选不等于工程已退出；入口退出不等于启动失败。HostVerification仍以真实回执为准。

主任务已完成浏览器四尺寸与 FigCraft 变量固化，见 [视觉交接](design-review.md)。实际 APS 工程窗口及 Host mismatch 见 [本轮验收](../../validation/launch-workflow-2026-10-31.md)，不从本文的隔离测试推断实机结果。
