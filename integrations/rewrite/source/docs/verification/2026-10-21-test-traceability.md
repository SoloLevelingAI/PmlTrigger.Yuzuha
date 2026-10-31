# DocFX 实现与测试双向关联

2026-10-21：在现有 DocFX 模板中加入测试目录、实现／接口的相关测试、方法级测试链接、源码位置及本机运行命令。WPF 应用、ViewModel 和业务实现文件保持本次开始时的内容。

## 实现范围

- Roslyn 从四个真实 C# 测试项目提取 96 个声明和注册点；编译测试程序发现 30 个场景，其中两个来自同一个参数化注册点，最终形成 97 个测试入口。
- `docs/reference/test-map.json` 为首批 43 个入口明确维护功能关联。生成时核验测试身份、类型和唯一方法；未知或含糊关联会失败。接口通过实现得到的关系单独标明。
- 实现及接口页可转到测试详情；方法卡片显示方法关联，模块页按模块转入测试目录，项目概览的验证项目可转入对应测试项目。目录支持模块、项目、方式、关键词及翻页。
- 测试详情提供验证目的、真实源码行号、固定提交的 GitHub 源码链接、可展开源码和复制运行命令。入口数不表示断言数、覆盖率或实时通过数量。
- `scripts/run-documented-test.ps1` 在本机核验当前源码中的身份，再构建并执行所选入口。xUnit 按方法过滤，Theory 执行全部数据行；独立场景只运行一项；旧版程序执行整个套件并确认所选标签出现于 PASS 输出。零匹配和未知身份不能作为成功结果。
- 网页只提供本机终端命令，不提供 HTTP 命令执行接口。测试源代码和生成规则纳入 Git，生成站点、运行报告及索引留在 artifacts 中。

## 验证

| 验证项 | 结果 |
| --- | --- |
| DocFX metadata 与 build | 0 警告、0 错误 |
| 测试目录 | 97 个入口、43 个明确关联入口、97 份源码范围核验通过 |
| 文档完整性 | 381 页 HTML 无重复 ID；1,027 个成员节无空摘要；309 条阅读链接通过 |
| 新增测试关联反例 | 5 项通过：改名、缺失或含糊方法、未注册场景、重复身份及错误类型 |
| 合并既有完整性与缓存回归 | 共 10 项通过 |
| xUnit 单方法命令 | Marine 示例 1 项通过 |
| xUnit Theory 命令 | Monitor 产品／初始化参数的 3 行数据通过 |
| 独立场景命令 | Marine 厂商模块比较与嵌套 cmd 转交 1 项通过 |
| 旧版套件命令 | Core 32 项通过，并核对所选适配测试标签 |
| 无效测试身份 | 明确失败并输出失败回执 |
| 浏览器 | 44 项检查通过；实现／方法到测试及反向链接、源码、剪贴板、筛选、侧栏同步、主题和手机布局通过；页面错误与外网资源请求均为 0 |

手机详情页检查发现的长源码路径链接溢出已修复，并由最终浏览器回归覆盖。测试使用虚构凭据和隔离目录，未登录真实 AVEVA 项目或操作真实许可及工作空间。

## 本机证据与入口

预览入口：`http://127.0.0.1:5275/docs/reference/tests/index.html`。

本次命令报告位于 `artifacts/dt/d4f22aae4499/`（单方法）、`23e7493bdaa2/`（Theory）、`a3da82eae85d/`（场景）、`56e7ac758980/`（旧版套件）。无效身份运行的回执保存于同一父目录的对应运行编号中。

汇总证据：`artifacts/docfx/verification.json`、`artifacts/documentation/test-links-build.log`、`test-links-final-site.log`、`test-links-guards.log`、`test-links-browser.log` 及 `artifacts/documentation/browser/report.json`。截图包括 `test-detail.png`、`test-detail-mobile.png`、`test-directory.png`、`test-directory-mobile.png`。
