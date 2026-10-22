# DSH Web 调整后的恢复与验证

执行基线为 `9c9708f` 加本批工作区修改，分支 `codex/pmltwin-web-repair`。保留 DSH 的五组导航、任务式首页、阅读样式、全量文件清单及 Mcp.Core/Knowledge.Core 拆分；未修改 DSH 原始目录、PMLLIB/PMLUI 或用户 AVEVA 工程。PR 保留人工审查。

## 已复现的问题与修复

1. DSH 导出的四个补丁是 UTF-16 文本，Git 直接读取时报 No valid patches。仅将本机工作副本转换为 UTF-8，保留原件及两个版本的哈希；换行差异通过上下文空白匹配处理。输入回执在 `artifacts/dsh-inputs/patches.json`。
2. DocFX 实际仍输出 api/mcp、api/knowledge，而模块配置把 Core 的 alias 改成 mcp-core、knowledge-core。生成器读不到对应方法，完整构建报 PmlFunctionRegistry.Register 零匹配。将 API 输出归属交给真实 Core 项目，薄入口保持源码参考，既有 API URL 不变。新增配置回归测试先失败再修复。
3. 功能与 Script 的项目限定类型仍指向旧程序集，独立目录检查报 Unknown feature type。更新 capabilities、维护示例和后端说明的当前源码链接；历史提交链接保留历史含义。
4. AOT RPC 兼容夹具链接的四个源文件已迁移，原引用变成不存在的路径。先补链接存在性测试，再同步到 Core 目录，并实际重跑 NET35/NET48 离线往返。
5. 浏览器保留了旧静态预览地址，5284/5286 均无监听服务。使用现有 Serve-Documentation 在本机 5284 启动正式开发宿主；文档入口为 /docs/index.html，Scalar、OpenAPI 和纯文本预览在同一端口工作。

DSH 的六个筛选测试纳入既有开发与按 ID 文档 Smoke 入口；生产依赖检查扩展到两个 Core。目录说明修正为“历史默认隐藏”，与实际复选框一致。

## 本批验证

环境：Windows build 26200，.NET SDK 10.0.400-preview.0.26322.102，PowerShell 7.6.5，Node 24.18.1。结果对应上述 dirty 基线，不冒充提交后的干净 CI。

| 检查 | 结果与本机证据 |
| --- | --- |
| 失败复现 | `web-repair-build-red.log`、`web-repair-alias-red.log`、`web-repair-catalog-red.log`、`web-repair-linked-source-red.log`，均在 artifacts 下 |
| 文档回归 | 38/38，含 DSH 的六个测试；`artifacts/web-repair-development.log` |
| xUnit | 142/142，0 失败/跳过；`artifacts/tests/development_net10.0_20261022185950.trx` |
| ArchUnitNET | 13/13，0 失败/跳过；`artifacts/tests/development_net10.0_20261022185830.trx` |
| Native AOT | 本批重新发布到 artifacts/native-web-repair；清单记录二进制哈希，CLI/MCP 与内置工具 Smoke 通过；`web-repair-native-build.log`、`web-repair-native-cli.log`、`web-repair-native-builtin.log` |
| NET35 / NET48 RPC | 按 ID smoke:rpc-net35、smoke:rpc-net48 通过；回执分别位于 artifacts/dt/d1572be8ceb08954/0926c48b30ef4b68a2d2fb93ddaae769 和 artifacts/dt/806f96243c635776/343fc5ce3be44d558de4eef8536a3090。只使用合成夹具 |
| Web 与 HTTP | 浏览器实测首页、五组导航、Script 搜索、测试到 API 方法锚点、后端调用树及 Scalar；/health、OpenAPI 与 POST /api/pml/preview 实际成功，预览只生成文本 |
| 完整文档 | OpenAPI、LikeC4、Roslyn、真实 NET35/NET48 Host 编译输入、DocFX 与全站链接检查；最终结果见 `artifacts/web-repair-documentation.log` |
| AVEVA 实机 | **NotRun**，本批未连接、操作或保存实际工程 |

## 启动与边界

在仓库根目录先运行 `npm run docs:build`，再运行 `npm run docs:serve -- -Port 5284`。打开 `http://127.0.0.1:5284/docs/index.html`；HTTP API 入口为同端口 /scalar/v1。退出预览宿主后，本机 URL 不再可访问，不能把缓存页面当作服务仍在运行。

Core 拆分改变了程序集身份，外部 C# 开发者需更新到 Core 项目/程序集引用；不声称旧托管二进制无需重新编译即可兼容。CLI/MCP 工具名、参数及原生 RPC 通过本批 Smoke 核对。测试和运行输出默认留在本机 artifacts。
