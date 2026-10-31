# 构建和维护文档

在 Windows / .NET 10 SDK 环境的仓库根目录执行：

```powershell
npm ci
.\scripts\build-documentation.ps1
.\scripts\serve-documentation.ps1
```

浏览器入口：

- `http://127.0.0.1:5275/docs/index.html`：DocFX 站点。
- `http://127.0.0.1:5275/scalar/v1`：实时 Scalar。
- `http://127.0.0.1:5275/openapi/v1.json`：实时 OpenAPI。

静态站点的 `http-api/index.html` 使用生成时取得的同版本 Scalar 本地资源；默认请求服务地址是 `http://127.0.0.1:5275`。接口请求需要本机 DevHost 正在运行。实时 Scalar 使用当前运行宿主的文档。

## XML 注释规则

所有维护中的 `rewrite/` 项目生成同名 XML。编译将缺失公开成员注释、无效 XML、无效参数名、遗漏参数注释及不能解析的 `cref` 视为错误，不用 `NoWarn` 隐藏这些问题。

- 公开类型说明职责与所有权。
- 端口方法说明实际副作用、结果核验和取消边界，并记录每个参数及返回值。
- 实现方法可使用可解析的 `inheritdoc` 继承端口契约。
- 位置记录参数要有逐项 `param`；构造函数参数也属于公开契约。
- 已保存秘密只记录其生命周期和遮蔽行为，不把示例凭据写进文档。
- 新增 HTTP 路由使用具名处理方法和具名 DTO；不要把 XML 注释放在 lambda 上。

## 生成步骤

构建脚本还原固定版本的本地 DocFX 工具，严格编译 C#，准备九个项目的 DLL / XML，并提供 .NET 10 Core、WPF 和 ASP.NET 运行时的托管程序集。反射引用保留 System.Private.CoreLib 的类型转发链，排除原生 DLL。可用 `-SkipAspire` 跳过可选开发编排程序集。

DocFX 的元数据输入为程序集，不再次以 MSBuildWorkspace 设计时加载 WPF 项目。Roslyn 仍在正常编译时检查源码和 XML；缺注释与程序集解析失败是不同问题。引用文件通过显式 src 和文件名映射，避免 Windows 绝对 glob 或反斜杠转义导致引用失效。

OpenAPI 从使用独立空数据目录启动的 DevHost 获取。构建不会扫描真实项目或运行桌面应用，也不会把操作日志复制到站点。

生成结果位于 `artifacts/docfx/site`，API 元数据位于 `artifacts/docfx/api`。这些生成目录被 Git 忽略；配置、注释和脚本纳入版本管理。

预览脚本运行独立的宿主副本和空工作空间，不会锁住后续编译输出。关闭预览终端即可停止；可用 `-Port 5280` 改端口。普通开发宿主也可接受 `--urls http://127.0.0.1:5280`。

构建结束时，`scripts/finalize-documentation-assets.mjs` 根据完整模板资源树的内容计算版本，将 CSS、脚本及其相对导入一起放入 `assets-<内容指纹>/`，并更新各级 HTML 引用。目录保持与原 `public/` 相同的深度，保证搜索 worker 的 `../index.json` 仍指向站点索引。只给入口脚本添加查询参数无法更新它间接导入的旧模块。预览宿主让 HTML 和 JSON 每次检查更新，内容指纹资源可长期缓存；重新构建后普通刷新即可取得同一版本的页面和主题。

## 自动检查

```powershell
npm run docs:check
npm run docs:test-guards
npm run docs:integrity-check
npm run docs:ui-check
```

`docs:check` 核验 DocFX 诊断、82 个已审阅类型的说明、接口注释继承、分类目录、HTTP 路由和 DTO 字段说明。`docs:test-guards` 在隔离目录编译五个有意写错的注释反例，必须分别被编译器拒绝。`docs:ui-check` 启动临时预览，检查首页、接口、模型、枚举、分类目录、实时与静态 Scalar，同时验证表格密度、筛选、全站搜索、折叠深链、手机导航和深色模式，并拒绝外网资源加载；需要 Playwright Chromium 或已安装的 Microsoft Edge。

验证结果分别输出到 `artifacts/docfx/verification.json`、`artifacts/documentation/guard-results.json` 和 `artifacts/documentation/browser/report.json`。这些检查与文档生成只使用隔离数据，不作为真实许可或项目登录验证。

## 现代文档模板

`docfx.json` 在内置 `default`、`modern` 后叠加仓库中的 `docs/reference/template`。无需更换生成器或修改 C# 类型：

| 文件 | 可调整的内容 |
| --- | --- |
| `template/public/main.css` | 系统无衬线字体、蓝色强调色、浅深主题、字号、间距、圆角和窄屏布局 |
| `template/partials/api.parameters.tmpl.partial` | 参数、类型参数与返回结果的紧凑表格 |
| `template/partials/class.tmpl.partial` | 属性、字段与事件表，以及方法卡片和辅助成员折叠 |
| `template/partials/class.header.tmpl.partial` | 类型标题、职责说明、签名与关联类型 |
| `template/ManagedReference.extension.js` | 构建阶段补充展示字段；保留原始 UID 和成员锚点 |
| `template/public/main.js` | 当前分类筛选与折叠成员深链接 |
| `template/token.json` | 导航、章节、搜索和主题切换的中文标签 |

`scripts/build-documentation-navigation.mjs` 在元数据生成后，将接口、模型、View、ViewModel、交互支持和功能实现分别归类。分类页和目录生成到 `artifacts/docfx/navigation`。分类只作用于文档，不移动源码、不改变命名空间和依赖。

## 业务阅读路线与完整性

新人从 [10 分钟路线](getting-started.md)开始；[五条流程](flows/index.md)说明用户入口、用例顺序、存储、失败／取消与返回 UI 的回路。[术语表](glossary.md)集中解释稳定编号、租约和身份。流程是人工审阅的当前行为说明，不声称是自动推导的运行时调用图。

`docs/reference/reading-map.json` 维护每个模块的三个推荐类型、真实入口方法、结果说明，以及命名空间职责。生成时必须解析到真实编译类型和唯一方法；方法签名改动时链接随元数据更新，删除或改名导致构建失败，不能静默生成失效导读。

Roslyn 源码清单在元数据丰富前生成，包含 namespace 与声明种类。`scripts/documentation-enrichment.mjs` 仅为有源码证据的 record／record struct 自动成员、隐式无参构造函数补充明确的生成说明；不会给手写业务方法套默认摘要。生成成员默认折叠，原签名与深链接保留。命名空间职责来自维护的阅读配置，工具生成的命名空间也会说明用途。

`docs:check` 还检查所有命名空间、类型与成员的最终摘要，以及全站每页 ID 唯一性；分别报告手写方法、生成记录成员和隐式构造函数数量。链接校验覆盖流程、术语与模块阅读路线中的相对链接、xref 结果和成员锚点。不要用“编译零警告”替代这些发布站点检查。

全项目与功能导航的使用方式见[按项目与功能阅读](exploring.md)。功能规则由 `docs/reference/modules.json` 维护；`scripts/documentation-graph.mjs` 读取真实项目引用、编译实现关系和构造依赖，生成可双向追踪的索引。

`template/public/appearance.js` 在页面绘制前恢复阅读偏好；`template/public/explorer.js` 实现项目、模块、类型目录、跨项目关系与外观控件。关注范围通过 URL 传递，外观偏好保存在浏览器，二者分别维护。

直接修改生成目录会在下次构建时丢失。修改模板或配置后，执行 `npm run docs:build`，再执行 `npm run docs:ui-check` 检查实际浏览效果。模板资源与字体均为本机提供。

## 测试与实现关联

顶部“测试”进入测试目录；实现和接口页的“相关测试”可转到测试详情，方法卡片列出明确关联的方法测试。模块阅读路线也提供本模块测试入口。测试详情展示验证目的、真实源码行号与提交链接、可展开源码，以及可复制的 PowerShell 命令；网页本身不执行本机命令。

`docs/reference/test-map.json` 维护人工审阅的测试与类型／方法关联。生成器使用 Roslyn 从四个测试项目提取 xUnit 方法、场景注册和旧版标签，并通过编译后的测试程序列出实际场景。未知测试、缺失 API 方法、重复或含糊的场景来源均使文档构建失败。接口也可显示“通过实现关联”的测试，并与直接维护的关系区分。

单项命令调用 `scripts/run-documented-test.ps1`，再次核对当前源码和测试身份：xUnit 按方法过滤，Theory 执行该方法的全部数据行；独立进程场景只执行所选场景；旧版 Core / Update 仍执行整个套件，并核对所选标签实际输出 PASS。所有方式都检查执行数量，零匹配不能当作成功。结果与执行回执输出到本次 `artifacts/dt/` 子目录。

测试入口数不等于断言数、覆盖率或实时通过数量。未维护关联的类型会明确显示暂无关联，不推断为没有测试。文档不会读取真实工作空间、密码或许可文件。

## 六类需求与设计文档初稿

[需求与设计总览](~/artifacts/docfx/navigation/specifications/index.md)集中展示 BRD、PRD、SRS、FRD、SDD、SDS；[追踪矩阵](~/artifacts/docfx/navigation/specifications/traceability.md)连接编号条目、编译 API 与明确选择的测试，[待确认清单](~/artifacts/docfx/navigation/specifications/pending.md)汇总业务及验收问题。

`docs/reference/specifications/*.md` 保存六类叙述源稿，`catalog.json` 保存稳定编号、范围、候选验收、上游关系、API 方法、测试关联理由与确认问题。SDD 说明整体设计，SDS 描述详细契约；这是本项目采用的职责约定。初稿描述当前实现与候选意图，不等同于批准后的需求基线。

`scripts/documentation-specifications.mjs` 在 API / 测试目录生成后解析这些记录。每个 API 和方法必须匹配编译元数据及真实源码；每个测试必须匹配已发现的身份，并明确验证范围。未知方法、测试、上游编号、循环关系或未说明的验证缺口导致生成失败。实现、方法、模块与测试页提供反向入口。源码链接固定到代码提交；存在未提交修改的证据文件只标当前工作区位置，不伪造提交行号。

原日常桌面与 `rewrite/` 分开标注；共享参数构造不表示完整协调器、网关和 ViewModel 已接入原界面。业务目标、收益、优先级和质量指标不能从源码确定时，保留“待确认”及问题，不用默认常量补成对外承诺。

证据清单 `artifacts/docfx/navigation/specification-catalog.json` 保存代码基线、源稿及相关代码/测试文件摘要；`docs:check` 拒绝来源已经改变的旧快照。生成器不会执行测试或导入历史 PASS 状态。测试入口用于查看断言和复制运行命令，完整需求是否验收需另有绑定提交及环境的运行记录。

```powershell
npm run docs:specifications-check
npm run docs:build
npm run docs:ui-check
```

确认业务意图后修改源稿和稳定编号记录；增加测试时填写本条实际验证的条件。先重新构建，再审阅追踪矩阵与待确认问题，不直接修改生成页。

## 官方资料链接

- [DocFX：程序集与 XML 元数据输入](https://dotnet.github.io/docfx/docs/dotnet-api-docs.html)
- [ASP.NET Core 10：XML 注释进入 OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/openapi-comments?view=aspnetcore-10.0)
- [Scalar：ASP.NET Core 集成](https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md)

## 压缩稿及文档索引

[文档总索引](~/artifacts/docfx/navigation/documents/index.md)按问题和文件位置导航，原稿分别维护于 docs/INDEX.md 与 docs/reference/specifications/INDEX.md；文件列表、六类入口和条目关系由构建生成。

规格配置 v2 复用 UID/测试前缀、来源组和固定说明；空列表可省略，状态、范围及每条测试的验证理由保持显式。生成前展开为完整记录并执行原有验证；未知引用、漏项、失效关联及超过三跳的实现定位会阻断构建。

目录包含非忽略的 Markdown、JSON、YAML 源文件，模板实现文件除外；历史记录标明用途，不转换为当前需求或测试通过结论。
