# WPF 各页调用链验收 · 2026-10-22

本批次扩展既有 Roslyn / XAML 分析和 DocFX 发布工具，不修改 WPF 业务代码、MCP/CLI/RPC 接口或原生 PML 文件。分支继续使用 `codex/pmltwin-v04-wpf-flows`，PR #6 保持草稿供人工审查。

## 交付与复用

- 从真实 XAML 自动发现 13 个业务页、2 个子页容器、3 组共享/生命周期入口。按 MSBuild 的真实引用遍历当前 6 个项目，补齐 Workspace、Knowledge 等页面依赖源码。没有手工伪造 ViewModel、页面 API 或执行链。
- 复用原有符号 ID、源码 SHA256、生成源码导出、测试关联目录与按 ID 执行器。SVG 使用浏览器原生能力，没有增加图引擎依赖或生产项目。
- 当前输出有 209 个入口/绑定，其中 105 个保留静态未知原因；发布 580 个可达源码成员。未知绑定不等于 WPF 运行绑定错误。未接线、初始禁用、框架函数体、虚分派、回调、接口候选和截断分别表达。
- 图可选择页面/控件、展开层数、缩放、选中节点、从某个方法继续追踪及返回入口。节点与调用处可跳源码；关联测试区分方法、类型和同类型其他方法。没有将关联数量作为覆盖率，也没有展示本次运行通过状态。
- 页面/源码成员保留静态表格与反向链接。原有对象读取至 RPC 的 9 条连续关系样例仍检查非零和连续性。入口与图的限度见 [使用说明](../reference/ui-tracing.md)。

## 测试与本机证据

| 验证 | 结果 / 证据 |
| --- | --- |
| 测试先行 | `artifacts/ui-pages-red.log`：缺少页面目录与图功能时失败；`ui-pages-constructor-red.log` 复现对象初始化器省略括号时漏掉构造调用，已修复 |
| 文档/图契约 | 22/22；含 10 个语义案例与 4 个页面/图模型案例；`artifacts/ui-pages-development.log`，按既有 `smoke:documentation-contracts` 可复刻 |
| xUnit / ArchUnitNET | 142/142、11/11，无跳过；同一开发验证日志及 `artifacts/tests/*20261022095*.trx` |
| Native AOT | 生产代码未改，复用当前构建重新执行 CLI/MCP 与内置指南 Smoke；`artifacts/ui-pages-native-cli.log`、`artifacts/ui-pages-native-builtin.log` |
| DocFX | 1273 页、22591 本地链接、129 测试入口，零警告/错误；全量构建、页面/源码行/测试链接及样例连续性通过；`artifacts/ui-pages-documentation.log`、`artifacts/ui-pages-render.log`、`artifacts/docfx/integrity.json` |
| 浏览器 | 13 个业务页均渲染、无加载失败；每个图节点均有键盘按钮；实际点击源码行 L122 和关联测试，验证测试 ID、运行命令与未运行标记；方法继续追踪和返回入口通过。`artifacts/ui-pages-browser-check.json` |
| AVEVA 实机 | **NotRun**；没有选择会话、执行 PML、保存工程或修改用户数据 |

浏览器验收使用本机不缓存脚本的静态预览，避免旧资源影响当前构建；辅助预览、日志、编译快照和输出均留在本机 artifacts，不纳入 Git。所有结果记录提交、dirty 与环境，不把提交前工作区验证冒充提交后的 CI 或 AVEVA 通过。

图只描述显式调用及已支持的静态关系，没有完整条件/委托数据流证明。共享处理器的不同分支会并列出现，不据图自动重放，也不推断按钮的实际调用权限、工程状态或业务返回值。
