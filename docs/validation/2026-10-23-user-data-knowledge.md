# 2026-10-23 统一用户目录与知识库管理验证

对应 TRACE32，基线 3754c44，Windows x64 / .NET SDK 10.0.400-preview.0.26322.102 / .NET runtime 10.0.9。验证对象为仓库源码和隔离数据；没有覆盖本机安装，没有向 AM/E3D 发送请求。此前大 ELDATA 读取导致无响应的问题仍开放。

## 已验证

后端 xUnit **173/173**（含本批 11 项），架构 **14/14**，WebApps 后端 **10/10**，文档/XML/表格 Node 检查 **48/48**。安装器 NET48 回归全部通过。公开源码 XML 覆盖检查：303 个类型声明，缺失注释 0，语法错误 0。

DocFX metadata 与全站构建 0 警告、0 错误；完整性检查 **2435 页、47387 个本地链接、0 错误**。首次构建发现验证报告的仓库链接写成了生成目录链接，已修正为源码相对路径，并重新生成导航、构建和校验通过。UI 调用追踪的动态未解析项继续显式保留，不作为已验证运行链。

- 共用目录设置、旧 knowledge 兼容、环境覆盖优先；查询路径不创建数据。
- 真实 personal.sqlite3 和 HTML 应用配置迁移后可读取，旧源保留，原启动配置/凭据引用保持。
- 同名内容冲突、源文件新增/修改、设置变化、数据库写锁及 WAL/SHM 阻止切换；安装/内部状态/资料目录重叠拒绝。
- 中途失败夹具实际完成第一个文件复制，再由后续目标目录冲突触发回退；新增目标移除，旧源、既有目标和设置保留。
- 使用离线桌面生成的真实合成笔记，写入专用 workspace.json，仅配置 UserDataRoot；新启动知识 CLI 经内部 MCP 列出同根 Knowledge/personal.sqlite3，独立 WebApps 在端口 5294 的健康信息确认同根 Apps，结束后关闭该临时服务。没有读取真实用户知识库。
- 个人笔记新建、更新、检索、软删除、恢复；旧修订保持，过期编辑无法覆盖当前内容。
- 资料库添加、备份后重建、索引回收和恢复；外部源码保持；同名恢复拒绝覆盖，保留库不能按普通资料库删除。
- NET48 安装器测试证明旧程序原样文件允许升级、用户改动拒绝覆盖；用户子目录、数据库及标记目录拒绝作为程序载荷。现有安装计划/回退回归保持通过。
- WPF 编译 0 警告、0 错误；离线 UI 夹具完成个人笔记四修订链路，已有知识正文读取保持通过；浅/深色、14 页、3 个窗口尺寸和 1440×1024 设计画布完成，绑定错误日志为空。截图已人工检查设置、笔记和资料库页，长页通过快捷按钮与滚动操作。

## 可复刻入口

```powershell
dotnet test tests/YuzuhaToolkit.Tests/YuzuhaToolkit.Tests.csproj -c Release
dotnet test tests/YuzuhaToolkit.Architecture.Tests/YuzuhaToolkit.Architecture.Tests.csproj -c Release
dotnet run --project installer/tests/SetupTests.csproj -c Release
dotnet run --project tests/WebApps/WebApps.Tests.csproj -c Release
dotnet build src/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.csproj -c Release
# --smoke-output 必须使用新的独立目录，不能指向用户资料。
# 本次额外覆盖了知识、用户根、应用目录环境变量，使夹具完全隔离。
pwsh -NoProfile -File scripts/Build-Documentation.ps1 -Port 5291
```

机器结果保存在 artifacts/tests/user-data/*.trx；WPF 截图、desktop-smoke.json 和 binding-errors.log 使用本次外部隔离目录。它们不是 AVEVA 业务验收。公开方法使用 DocFX XML 注释，需求/边界见[用户目录与知识库管理](../reference/user-data-and-knowledge.md)。

本批不执行安装。更新本机前需要具体预览，再提醒关闭 AM、E3D、工作台及相关知识/HTML 宿主，确认后才更新。浏览器下载、自有 PMLLIB 的实际环境登记、经验旧接口、现有调用权限均保持原有行为。
