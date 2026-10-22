# 原始设计基线归档与 PMLTWIN 适配验证

执行基线为 `4ba3f17` 加本批工作区修改，分支 `codex/pmltwin-design-baseline`。用户明确授权将其提供的原始 Word 按版本归档到本仓库；提交前验证，PR 继续供人工审查，不合并 Main。

## 原稿与归档

来源是用户于2026年10月22日在本对话提供的《软件设计要求 20261022脚本设计优先版》v0.3 Draft，文档标识 ARCH-SCRIPT-FIRST-001。保留原始文件名和原始字节，存入 docs/design/baselines/2026-10-22-v0.3-draft；来源、日期、状态、字节数、哈希和适配路径由 manifest.json 管理。

Word 为 424089 字节，SHA256 为 `cbeac605b60a647441e5e2cda0eaecd151d1bb0961d9e69bfc02a0f3cbd19830`。输入、归档、站点发布副本与实际 HTTP 下载均核对同一哈希。原稿未重排、未另存为、未清理属性，状态维持 Draft；没有批注或修订标记。归档完整性通过不表示设计稿完成评审。

现有 C# 文档工具新增只读 OOXML 提取入口；复用 .NET 自带 ZIP/XML，未增加第三方依赖。自动生成 853 段正文及表格内段落的阅读内容，保留两张内嵌图、原始换行及固定版本的正文定位锚点。阅读页不声称复刻 Word 分页、自动编号或完整版式，原文件可直接下载。

## 项目适配

design-adaptation.md 记录 18 项条款适配以及原稿 D01–D13 的项目处置，关联当前职责、已有代码/说明、测试、迭代决定和待完成事项。保留可选 Valid、测试复用、CLI 归属、对象目录及用户所需后端调用树；不为原稿的示例结构改动现有业务接口。

原文样例的七程序集和 36 项历史测试与 PMLTWIN 本次结果分别标明。一名人工审阅、AI 草稿隔离、迁移待接入及 AVEVA 实机未执行等边界继续保留。DocFX 首页、开始分组、项目约定和技术侧栏均能进入来源及适配页。

## 验证与本机证据

环境为 Windows build 26200、.NET SDK 10.0.400-preview.0.26322.102、PowerShell 7.6.5、Node 24.18.1。记录对应 dirty 基线，不冒充提交后的干净 CI。

| 检查 | 结果与证据 |
| --- | --- |
| 先补测试 | 缺失归档模块时失败；artifacts/design-baseline-red.log |
| 归档专项 | 3 项通过，覆盖原字节、哈希变化、错误大小、路径越界、重复身份、缺失关联、Draft 状态及真实 Word 的正文/表格/双图发布；design-baseline-targeted.log |
| 文档回归 | 41 项通过；artifacts/design-baseline-development.log |
| xUnit | 142 项通过、0 失败/跳过；artifacts/tests/development_net10.0_20261022201842.trx |
| ArchUnitNET | 13 项通过、0 失败/跳过；artifacts/tests/development_net10.0_20261022201721.trx |
| 按 ID 运行器 | 现有身份、源码和非零匹配保护通过；同开发日志 |
| Native AOT Smoke | 先核对上批 artifacts/native-web-repair 二进制清单哈希，再重跑 CLI/MCP 与内置工具通过；design-baseline-native-cli.log、design-baseline-native-builtin.log；本批未重新发布 |
| Word 下载 | HTTP 200，正确 DOCX 类型，424089 字节与原稿哈希一致；artifacts/design-baseline-download.json |
| DocFX | 完整 OpenAPI、LikeC4、Roslyn、真实 Host 编译输入及全站完整性检查；最终记录见 artifacts/design-baseline-documentation.log |
| AVEVA 实机 | **NotRun**；本批未连接、操作或保存用户工程 |

生产源码及原生 PMLLIB/PMLUI 未改。原始 Word 与项目文档由 Git 管理，提取页、图示、下载验证副本和运行输出均在 artifacts，可由既有构建流程重建。
