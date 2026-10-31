# 六类需求与设计初稿及双向追踪

日期：2026-10-26。代码基线：`2733f5314ab5e8ee8d818c2eab9241a609ef27a8`。本次为该基线上的本地文档变更，不表示新产品版本已经发布。

## 结果与范围

DocFX 新增“需求与设计”入口，统一展示 BRD、PRD、SRS、FRD、SDD、SDS 初稿、追踪矩阵及待确认清单。共 49 个稳定编号：BRD 5、PRD 7、SRS 14、FRD 8、SDD 6、SDS 9；其中 8 个条目待确认，集中列出 25 个业务及验收问题。

记录显式关联 47 个实现类型、32 个实际测试入口。条目可进入编译 API、具体方法、固定提交源码及测试详情；类型、方法、功能模块与测试详情可返回相应条目。每个测试关联单独说明验证范围，不按类型名猜测覆盖率；无直接测试的条目保留验证缺口。

日常产品仍为原 `LaunchDesk.App → LaunchDesk.exe`。原 Core 已共享 Monitor / Marine 参数规则；新网关、操作协议及完整 ViewModel 迁移仍按 `rewrite/` 的实际范围标注。没有把独立 Archon 外壳的实现观察写成原界面已经全面接入。

## 源文件与生成

- `docs/reference/specifications/*.md`：六类叙述源稿。
- `docs/reference/specifications/catalog.json`：条目、状态、适用范围、候选验收、上游关系、API/方法、测试关联理由与确认问题。
- `scripts/documentation-specifications.mjs`：解析编译元数据、真实源码和测试身份，生成六类页面、49 个条目页、矩阵、确认清单及双向关系。
- `docs/reference/template/public/specifications.js`：在 API 类型和方法页显示反向入口。
- `artifacts/docfx/navigation/specification-catalog.json`：生成证据清单；包含代码基线、55 个相关输入文件摘要及整体摘要。生成目录不纳入 Git。

常规 `npm run docs:build` 先生成 API/测试元数据，再生成规格初稿并构建站点。`docs:check` 核对快照摘要和双向边；源码或源稿变化后必须重新构建。稳定编号不以文件路径或类型名代替。

## 本轮验证

| 检查 | 结果 |
| --- | --- |
| Release 编译与 DocFX metadata / build | 成功，零警告、零错误 |
| 文档完整性 | 440 个 HTML 页面；176 条阅读路径、1906 个站内链接核对；无重复 HTML ID |
| 规格关联 | 6 类、49 个条目；47 个类型、32 个测试入口；55 个输入摘要核对通过 |
| 规格、测试关联与完整性守卫 | 15 项通过，包括不存在的方法/测试、悬空/循环关系、缺失验证范围及待确认问题反例 |
| 浏览器 | 55 项检查通过；六类页面、待确认、方法/测试往返、移动端及深色模式；零页面错误、零外网资源请求 |
| 资源与缓存 | 2 项通过，含普通刷新恢复旧缓存的浏览器回归 |
| 现有 xUnit / ArchUnitNET | 62 项通过、零失败；包含原 Core 和 Update 两个套件入口 |
| 原套件内部报告 | Core 32 项、Update 20 项均通过；不与上行数量相加冒充独立 xUnit 数量 |

报告位于 `artifacts/docfx/verification.json`、`artifacts/documentation/browser/report.json`、`artifacts/documentation/cache-regression/report.json`、`artifacts/architecture-tests/architecture.trx` 及两个原套件 JSON。站点使用隔离 DevHost，测试使用隔离目录和模拟进程；未进行真实 AVEVA 项目登录、许可签出或真实 UAC 验收。

规格页面不导入这些报告作为条目 PASS 状态。本记录证明本轮检查的结果；业务目标、量化质量承诺及原界面完整迁移仍须按确认问题与目标环境单独验收。

## 本机审阅

运行 `npm run docs:serve`，入口为 `http://127.0.0.1:5275/docs/reference/specifications/index.html`。顶部“需求与设计”进入六类初稿，追踪矩阵按编号汇总；待确认清单保留问题编号、未指定的负责人和期限。
