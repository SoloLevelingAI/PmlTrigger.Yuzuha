# 契约方法预览（internal）

方法目录消费独立Roslyn快照，不把源码写进公开metadata、IR或FigCraft。身份使用完整Assembly::DocID，bindings不按标题或短名称匹配。调用边只表示静态潜在关系，不能证明运行顺序、执行或AVEVA验证。

空symbolIds可显式声明resolution=unavailable（C#但未纳入当前编译图）或not-applicable（非C#等明确不适用），两者都必须有原因；省略resolution时兼容旧配置，默认not-applicable。非空绑定只允许resolved或省略resolution，且所有原始symbolId必须精确存在，不按名称替换。

输入固定在method-bindings.json的analysis路径。没有捕获返回unavailable；已捕获源码或生产者变化返回stale并清除全部旧body；失效绑定、越界路径或损坏已登记DocFX资源拒绝本轮构建，保留共同lastgood。

1. 明确捕获Roslyn：配置仓库.NET环境后运行 `pwsh -File scripts/Trace-Desktop.ps1`。该命令会编译；不要与其他共享项目构建并行。
2. 还原固定文档工具：`dotnet tool restore --configfile src/NuGet.config`。
3. 捕获两项目API：`node scripts/capture-contract-atlas-docfx.mjs`。可设置DOTNET_HOST_PATH指向dotnet.exe；DOTNET_ROOT、DOTNET_CLI_HOME及NUGET_PACKAGES沿用仓库隔离工具链配置。
4. 启动共同watch。它只读取快照，不隐式运行以上捕获命令。

DocFX捕获仅Manager.Core/Workspace，不运行全量Build-Documentation、不删除旧文档、不启动DevHost。每次写入全新 `artifacts/contract-atlas-docfx/captures/<uuid>/`，成功后才替换固定snapshot.json清单。原site保留，工具失败不替换清单。精确成员链接只从真实xrefmap的UID/href取值，资源及HTML原字节SHA必须与清单一致；未发布private/internal或其他项目返回null和理由，不拼猜anchor。

模块API：`methodInputSnapshot(root)` 返回fingerprint/inputs；`buildMethodCatalog({root,metadata,metadataDigest,renderModelDigest,inputSnapshot})` 返回catalog及仓库相对sourcePath的docfx artifacts。catalog上限8MiB/500方法/1500条边，相关手写源码独立512文件/128MiB预算，完整编译输入receipt最多1024项（含生成/XAML/config），原始全图最多96MiB。单正文64KiB，超限标truncated；源码只允许src手写C#，外部及生成正文不展示。相关源、配置、生产者、graph及DocFX原字节都进入指纹。

方法集合是显式契约根及其直接callers/callees，不冒称整个13k节点分析图都进入预览。方法卡保留邻接图范围外的静态边界说明，新增绑定可明确扩展分析主链；如果显式根的直接邻域仍超预算则失败，不按名称选择替代或静默截断调用点。

已纳入邻居仍可能有目录外调用者：按原始边kind聚合为catalog-scope-incoming:<kind>，保留遗漏调用位置数量与最多三个仓内相对位置示例；该说明不把目录外caller或其边误纳入当前可跟随图，也不代表运行时已调用。既有outgoing范围标记继续保留。

DocFX另记录实际MSBuild Compile/ProjectReference/MSBuildAllProjects及全局配置摘要，生成前后必须一致。Roslyn重捕获而API站点未重建时，方法仍可ready，但旧DocFX链接返回null并说明、该轮不发布旧site资产。没有DocFX source receipt的旧站点同样不链接。

真实两项目站点捕获已通过，200资源、92输入，metadata/build零警告零错误。`node tests/contract-atlas-docfx-browser.mjs` 使用本机Playwright/Chromium打开xref中的Plan精确成员锚点（HTTP200、真实DOM id），证据在 `artifacts/contract-atlas-docfx-browser/`。本机浏览器依赖为 `artifacts/ribbon-ui-browser/node_modules/playwright` 和 `%LOCALAPPDATA%/ms-playwright/chromium-1243/chrome-win64/chrome.exe`；不具备依赖时应报告不可运行，不计作验收成功。

浏览器/Node共享method-validator.js，返回 `{valid,errors}`。它不执行源码或XML，不授权任意文件读取。共同发布者另核对manifest的role=docfx资产及双digest。Figma与业务GUI实机结论不受本目录状态升级或下降。
