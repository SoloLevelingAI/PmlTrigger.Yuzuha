# PMLTWIN 设计约定与功能 Script 目录验证

本批以《软件设计要求（20261022脚本设计优先版）》v0.3 Draft 为输入，在既有分支 `codex/pmltwin-v04-wpf-flows` 的 `86fd4e1` 基线加本批改动上完成项目适配与 DocFX 组织。代码和文档提交前验证，PR 继续供人工审查，不合并 Main。

## 交付

`docs/reference/pmltwin-design-contract.md` 规定现有职责、技术和原生边界，直接对应到 DocFX 的真实来源与输出。`catalog-maintenance.md` 给出自动发现、候选认定、人工新增/修改/删除/查询、完整 JSON 例子、检查和重建命令。

新目录复用 `tools/Documentation` 的 Roslyn 清单、原生 PML 文件清单、modules、test-map、源码和调用树。人工信息集中在 `capabilities.json`，目前聚合 13 个功能、37 个 Script 条目，另有 85 个 C# 待分类类型。37 项包含 29 个原生 PML 文件、6 个 C# Script 及 2 个 PowerShell 工具；历史文件保留，默认不混入当前筛选。

原生脚本自动发现，未维护用途和归属保持未知；C# 不把所有类型自动认定为 Script。人工登记选择真实类型或唯一方法，依靠源码提取当前签名。共享源码在不同程序集的实现上下文分别显示。

## 测试与证据

环境为 Windows build 26200、.NET SDK `10.0.400-preview.0.26322.102`、PowerShell 7.6.5、Node 24.18.1。表内证据均为本批运行，dirty 基线不冒充交付提交的干净 CI。

| 检查 | 结果与本机证据 |
| --- | --- |
| 测试先于实现 | 初始目录缺失、签名搜索、限定方法名/候选搜索及隐式构造函数误作调用树入口的测试先失败；`artifacts/script-catalog-red.log`、`script-catalog-search-red.log`、`script-catalog-symbol-red.log`、`script-catalog-trace-red.log` |
| 目录行为 | 覆盖发现、归属、历史、签名/名称搜索、空结果、人工增改删、重复与失效选择、缺失说明、越界路径和错误审核字段；`artifacts/script-catalog-targeted.log` |
| 文档自动测试 | 30 项通过；`artifacts/design-contract-node-final.log` |
| xUnit | 142 项通过、0 失败/跳过；`artifacts/tests/development_net10.0_20261022155548.trx` |
| ArchUnitNET | 11 项通过、0 失败/跳过；`artifacts/tests/development_net10.0_20261022155428.trx` |
| 按 ID 运行器 | 既有 PowerShell 身份与非零匹配保护通过；`artifacts/design-contract-development.log` |
| Native AOT Smoke | 清单哈希核对后重跑 CLI/MCP 和内置工具通过；`artifacts/design-contract-native-cli.log`、`design-contract-native-builtin.log` |
| 完整 DocFX | OpenAPI、LikeC4、真实 Host 编译输入、Roslyn、目录和页面完整构建通过；`artifacts/design-contract-documentation.log`；最终内容与模板复核见 `design-contract-final-render.log` |
| AVEVA 实机 | **NotRun**；本批没有连接或保存工程 |

生产源码、协议和 PMLLIB/PMLUI 原生文件没有修改。Native AOT 复用 `artifacts/native-corrections` 的既有发布二进制，本次重新执行 Smoke，不声称本批重新发布。

## 可独立验收的行为

浏览器实际验证：搜索 `YuzuhaGetGadget` 找到两个原型；按 forms 与 C# 筛选得到共享查询及 NET35/NET48 两个框架适配器。打开 Script 详情后，源码链接定位到 `PmlFormQuery.cs:87`，关联测试可查看环境、命令和“未附加本次证据”的实际状态，再返回功能页查看复用条目。共享类型以程序集区分，避免同名入口混淆。

在 C# 候选页搜索 `ReadCore`，找到三个编译上下文中的 Reader；历史文件 `FileBrowseTest` 默认零匹配，勾选显示历史后出现一项且标为历史。筛选后的全量静态备用清单默认折叠，明确说明不参与筛选，避免把备用清单误读为搜索结果。

调用树入口只匹配实际方法的类型、源码路径和行号；多方法时列出选择，不将隐式构造函数当作查询入口。

在隔离测试中添加原生文件会自动发现；补人工条目后进入功能，改用途后身份不变；删原生补充条目后文件仍可找到，删 C# 登记后类型回到候选。未知功能、失效类型/方法、重复身份、无效路径/guide 或伪造 reviewed 字段会失败。通过目录登记不能获得执行许可，也不能生成已验证事实。

浏览器支持查阅和筛选，实际增删改在 Git 源码、JSON 与 Markdown 中完成。没有实现网页写回、GUI 人工批准或迁移；它们以计划功能显示，不因本批目录交付改变状态。机器本机工程、函数注册表、知识数据库和 artifacts 不进入自动发现范围。

复刻：`npm run docs:catalog:check`，再运行 `npm run docs:build`。真实 Host 下游分析继续使用既有两个 SDK 参数。测试与构建产物留在本机 artifacts，生成页可重建，不维护第二份手写页面副本。
