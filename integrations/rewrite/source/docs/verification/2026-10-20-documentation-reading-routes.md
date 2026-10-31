# 业务阅读路线与文档完整性验证

## 修改范围

- 首页增加新人路线、五条业务流程和术语入口；增加启动、交付、变量、分享迁移及资源监测的实际调用顺序、存储、失败／取消及 UI 返回说明。
- 九个模块分别维护三个推荐类型、真实入口方法和结果说明。分享导入、后台迁移和持续监测按当前实现分别说明，不把静态类型关系冒充完整运行调用图。
- 十一个命名空间补职责说明，包括父命名空间和 Aspire 生成的元数据容器。
- 依据当前 Roslyn 清单，仅为自动生成的 record／record struct 成员和隐式构造函数补说明；生成部分默认折叠。手写方法仍受严格摘要检查，不使用兜底文字隐藏业务注释缺口。
- 每个重载组只输出一次原锚点，各签名原锚点保留；手机流程表格转换成逐步卡片。
- 仅修改文档、生成／验证工具和资源描述相关 XML 注释；应用运行逻辑不变。

## 最终结果

| 检查 | 结果 |
| --- | --- |
| .NET 解决方案与可选 AppHost 编译 | 0 警告、0 错误 |
| DocFX metadata / build | 均为 0 警告、0 错误 |
| 文档程序集 / 类型 / 元数据条目 | 9 / 232 / 2,049 |
| 手写公开方法摘要 | 274 项通过 |
| 命名空间摘要 | 11 项通过 |
| 生成记录成员 / 隐式构造说明 | 576 / 21 |
| HTML 成员节空摘要 | 0 / 1,024；修复前为 597 / 1,024 |
| HTML ID 重复 | 280 个 HTML 文件中为 0；修复前 84 个 API 页存在重复重载锚点 |
| 阅读路线 / 链接 | 17 个入口、291 条链接及目标锚点通过 |
| 模块阅读指南 | 9 个模块均具备三个推荐类型和至少一个真实方法入口 |
| 浏览器检查 | 41 项、25 页通过；0 页面错误、0 失败响应、0 外网请求 |
| 文档完整性与缓存回归 | 5 项通过 |
| 本机 5275 预览 | 新人路线 HTTP 200、当前内容、HTML no-cache |

## 验证方式

```powershell
.\scripts\build-documentation.ps1 -SkipRestore
node scripts/check-documentation.mjs
node --test scripts/test-documentation-integrity.mjs scripts/test-documentation-assets.mjs
node scripts/check-documentation-browser.mjs
git diff --check
```

手机布局最后调整后，仅清理已确认的 `artifacts/docfx/site` 并重新执行 DocFX build、资源指纹和完整性检查；随后重新运行全部 41 项浏览器与 5 项回归检查。没有重复编译未改动的应用逻辑。

完整性反例验证：重复重载锚点必须被拒绝；生成成员补充不能覆盖缺摘要的手写 override；目标页面存在但成员锚点缺失的导读链接也必须被拒绝。

## 本机证据

- `artifacts/docfx/verification.json`：发布站点计数、摘要、链接及 ID 唯一性。
- `artifacts/docfx/enrichment.json`：自动生成说明的分类计数。
- `artifacts/documentation/reading-build.log`、`reading-layout-build.log`：编译与 DocFX 构建记录。
- `artifacts/documentation/reading-regression-tests.log`：五项完整性／缓存回归。
- `artifacts/documentation/browser/report.json`：浏览器断言、错误及网络记录。
- `artifacts/documentation/browser/project-overview.png`、`module-reading.png`、`module-reading-dark.png`、`flow-mobile.png`：已目视核验的实际生成页面。

生成证据由 Git 忽略，维护源文件与本记录纳入版本管理。此验证不作为真实项目登录、部署或许可授权的端到端业务成功证明。
