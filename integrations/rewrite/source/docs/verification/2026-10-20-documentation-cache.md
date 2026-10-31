# 文档资源一致性与窄窗导航验证

用户实际打开的 5275 端口页面出现标志放大、导航文字竖排、功能索引停留在加载提示。此前独立浏览器截图未覆盖已有缓存的窗口。

在当前应用内浏览器复核后，确认页面内容已更新，而自定义样式和扩展功能没有生效；服务端对应新资源均返回 200。将完整模块树切换到新的内容指纹地址后，同一个标签页恢复主题与十张项目卡片。继续处理 824 像素宽度下的顶部导航换行。

## 修改

- `finalize-documentation-assets.mjs` 为完整 CSS、脚本和字体目录计算内容指纹；入口和间接模块共享同一版本。更新所有生成页面的本地资源引用，并输出资源清单。
- 版本目录与原 `public/` 保持相同深度，保留搜索 worker 对 `../index.json` 的相对路径约定。针对固定 DocFX 2.81.0 发布脚本中的硬编码 worker 地址，改成通过 `import.meta.url` 定位同目录 worker；匹配不符合预期时构建失败，要求审阅适配器。
- DevHost 的文档 HTML、JSON 等未版本化文件使用 `no-cache` 重新验证；内容指纹资源使用一年不可变缓存。HTTP 诊断端点的契约没有变化。
- 顶部导航在 1200 像素以下收进浮层菜单，导航标签保持整行，正文不被展开菜单向下推移。
- 浏览器检查等待 DocFX 初始化与导航收起动画结束，避免在控件尚未就绪时点击。

## 验证

完整 .NET 解决方案、可选 AppHost、Roslyn 注释、DocFX 元数据和页面构建通过，零警告、零错误。最终资源发布适配再次通过严格 DocFX 构建与 `check-documentation.mjs`：942 个声明、274 个方法、82 个已审阅类型保持覆盖。

`node scripts/check-documentation-browser.mjs`：21 项检查通过，涵盖 11 个页面、搜索、接口双向跳转、筛选、主题、手机及 824/1024 像素窄窗导航和真实缓存头。无脚本错误、失败响应和外网请求。

`node --test scripts/test-documentation-assets.mjs`：两项测试通过，覆盖间接依赖修改改变内容指纹、各级页面资源引用、搜索 worker 地址适配；并使用真实 HTTP 缓存先载入空的旧主题与旧扩展，再升级和普通刷新，确认十张卡片与 29 像素标志恢复。

实际应用内浏览器也复核了主题恢复、菜单展开/收起与 `ICatalogService` 全站搜索结果。截图保存在 `artifacts/documentation/browser/iab-recovered.png`，自动检查结果在 `artifacts/documentation/browser/report.json` 和 `artifacts/documentation/cache-regression/report.json`。
