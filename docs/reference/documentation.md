# 构建和维护文档

需要 Windows x64、.NET 10 SDK、Node.js 22.22.3 或更新版本，以及 PowerShell 7。仓库根目录运行：

```powershell
npm ci
npm run docs:build
npm run docs:serve
```

预览入口：

- `http://127.0.0.1:5276/docs/index.html`：DocFX C# API 与导读。
- `http://127.0.0.1:5276/docs/architecture/index.html`：LikeC4。
- `http://127.0.0.1:5276/scalar/v1`：实时 HTTP API。
- `http://127.0.0.1:5276/openapi/v1.json`：实时 OpenAPI。

`Build-Documentation.ps1` 还原固定版本的本地 DocFX 工具，编译 DevHost，启动临时本机进程取得 OpenAPI，验证并构建 LikeC4。随后从六个 .NET 10 项目的源码生成 DocFX 元数据，使用 `tools/Documentation` 中的 Roslyn 工具扫描全部十个生产项目，生成完整项目、模块、类型和源码索引。临时进程按其 PID 关闭。站点输出在 `artifacts/docfx/site`；任何步骤失败都会使脚本和 CI 失败。

WPF 工作台另提供手写源码参考并单独编译。NET35、NET48 Host 与 SetupBridge 安装器提供明确标记的源码签名参考，不使用假 SDK 或编译桩。现有使用、安装、维护文档以及全部 PML/PMLUI 文件纳入导航；Obsolete 文件标明历史状态。[覆盖报告](../../artifacts/docfx/navigation/coverage.md)由实际输入清单生成。

WPF 的 [UI 命令与事件溯源](ui-tracing.md) 从真实编译输入获取方法符号，补充 XAML 入口、Command 委托、setter/OnChanged 和事件候选关系；生成的页面提供调用方、下游、源码行及类型/API/测试链接。它描述静态关系，不替代运行日志或实机测试证据。

[迭代与意见记录](iteration-log.md)集中保存意见、采纳决定、实现状态和验收条件；批次结果继续放在 `docs/validation` 并从记录中链接。当前阅读重点是[后端每一步的计算和排查](backend-tracing.md)，前端优先查看校验、可执行条件与状态变化。解释方法按具体问题选取，见[理解与排查笔记](understanding-notes.md)；5W2H、康奈尔笔记等均为可选示例。

脚本接受 `-Port` 更改临时宿主端口；预览脚本也支持 `-Port`。构建时不选择 AVEVA 会话、不执行 PML、不扫描真实资料、不读写用户知识库。Scalar 使用包中提供的本地脚本，字体使用系统默认设置。

OpenAPI 描述新增 DevHost 的四个 HTTP 端点；现有 MCP 的 JSON-RPC 工具和 Named Pipe 协议仍在各自的 C# API 与内置指南中描述。DocFX 和 OpenAPI 是从代码生成的文档，不是后端业务代码生成器。

## 注释维护

生产项目的公开类型和成员启用严格 XML 文档诊断；Roslyn 扫描另行检查全部类型及公开、内部、受保护成员摘要，覆盖暂不能在此环境编译的 Host 和安装器。新增入口缺摘要或源码语法错误会使构建失败。HTTP 处理器使用具名方法和 DTO，.NET 10 的 OpenAPI 源生成器读取这些注释。私有实现成员也可在源码参考中查看，不把编译器生成成员计入手写源码覆盖。

修改源码、配置或 DSL 后重新运行构建。构建会核对绝对路径并清理生成的架构、API、导航和站点目录，避免删除的 API 或旧资源残留。索引器核对项目集合、类型模块归属和文档相对链接；静态检查核对页面 ID、本地链接与类型目标。生成内容与依赖目录不进入 Git。

## 官方参考

主题适配自 LaunchDesk，位于 `docs/reference/template`，在内置 `default`、`modern` 后叠加。`public/main.css` 维护系统字体、卡片和深浅色样式；`partials/` 维护紧凑 API 表格，`token.json` 维护中文标签。右上角“外观”支持颜色模式、强调色和信息密度，偏好保存在浏览器中。来源和 MIT 声明见模板目录。

- [ASP.NET Core OpenAPI XML 文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/openapi-comments?view=aspnetcore-10.0)
- [Scalar ASP.NET Core 集成](https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md)
- [DocFX .NET API 文档](https://dotnet.github.io/docfx/docs/dotnet-api-docs.html)
- [LikeC4 CLI](https://likec4.dev/tooling/cli/)
- [ArchUnitNET 使用指南](https://github.com/TNG/ArchUnitNET/blob/main/documentation/docs/guide.md)
