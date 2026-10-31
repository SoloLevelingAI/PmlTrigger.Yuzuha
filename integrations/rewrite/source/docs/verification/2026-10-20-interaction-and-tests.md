# 视图交互分类、连续翻阅与统一测试验证

## 文档分类与关系

公开类型仍为 232 个，拆分为 31 个接口、99 个数据模型、14 个视图、16 个交互模型、14 个交互支持类型和 58 个功能实现。ViewModel 不再归入功能实现；编辑状态基类与编辑器归交互模型，命令、转换器和对话框等另列交互支持。

新增视图与交互模型之间的强类型构造参数、公开属性及反向引用导航。示例：ProfilesView → ShellViewModel → ProfilesPageViewModel。关系不推断动态 XAML 绑定，也不替代运行时接线验证。主题资源从“许可与资源监测”的关键词误匹配中排除。

类型详情底部提供上一页、当前位置、返回目录、下一页。顺序与当前模块、项目、类别及搜索结果相同；范围外引用明确提示而不悄悄扩展筛选，首尾不循环。目录每页 30 项，分页写入 URL；返回、刷新保留页码，修改筛选重置到第一页。

## 测试框架与旧用例

独立的 `LaunchDesk.Architecture.Tests` 使用固定版本 TngTech.ArchUnitNET.xUnit 0.13.4、xUnit 2.9.2 和现有 VSTest 适配器。测试依赖未进入生产项目引用方向。

`npm run test:rewrite` 检查实际项目引用、循环和审阅类型，并执行统一 xUnit 项目：

| 内容 | xUnit 结果条数 | 原有内部用例 |
| --- | ---: | ---: |
| 编译依赖、ViewModel 归属、副作用端口、Aspire 隔离及守卫反例 | 14 | — |
| Rewrite.Tests 动态发现、独立 STA 进程执行的原场景 | 27 | 27 |
| 原 LaunchDesk.Tests 控制台套件 | 1 | 30 |
| 原 LaunchDesk.Update.Tests 控制台套件 | 1 | 20 |
| 合计 | 43 | 原有场景和套件均保持通过 |

43 条结果全部通过，零失败、零跳过。旧版 Core/Update 保留原程序结构和内部 JSON 结果，并未声称逐条改写为 xUnit。旧程序不在生产代码中引用，旧源码未改写；Rewrite.Tests 只增添场景发现及选择执行入口，整套运行方式保留。

ArchUnitNET 的程序集选择使用真实程序集全名，禁止空选择掩盖违规。副作用规则显式载入必要目标，编译反例包含文件读取调用，必须被守卫拒绝；不会执行该反例方法。

旧场景的执行目录使用隔离短路径，结果另行归档，避免新增报告层级触发 Windows 路径限制。旧套件先禁用构建服务器复用完成编译，再直接执行测试程序；退出及输出读取均有超时，避免构建后台进程持有输出管道而使宿主无法汇总。

## 最终验证

- 完整文档构建、Roslyn XML 注释审计及内容检查通过，零构建警告、零错误；九个文档程序集、82 个审阅类型和 274 个公开方法保持覆盖。
- 十一个实际项目与架构白名单一致，`npm run review` 通过；历史 LikeC4/Archon 审阅材料未改写。
- 浏览器检查 28 项通过，覆盖 15 个页面、视图/交互分类与双向跳转、目录分页、类型翻页首尾、范围外提示、返回和刷新页码，以及主题、搜索和窄屏回归。无脚本错误、失败响应和外网请求。
- 资源指纹与旧缓存升级两项回归通过。
- 实际应用内浏览器验证 ResourceSupervisor 的第 20/21 项可进入 WindowsResourceProbe，第 21/21 项禁用下一页，上一页正确返回 ResourceSupervisor。

原始证据：`artifacts/architecture-tests/architecture.trx`、两个旧套件 JSON、`artifacts/architecture-tests/summary.json`、`artifacts/docfx/verification.json`、`artifacts/documentation/browser/report.json` 和 `artifacts/documentation/browser/iab-type-pager.png`。
