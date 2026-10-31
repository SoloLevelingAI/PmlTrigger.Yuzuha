# DocFX / OpenAPI / Scalar 验证

范围：`rewrite/archon` 当前 3.0.1 重写版。2026-10-20 在 Windows / .NET 10 环境验证。旧 2.0.4 应用源码和历史架构审阅资料保持原状。

## 实现

- 仓库本地工具固定 DocFX 2.81.0；DevHost 使用 Microsoft.AspNetCore.OpenApi 10.0.9 和 Scalar.AspNetCore 2.18.0。
- 九个生产程序集生成 XML 注释；严格编译覆盖缺失公开注释、坏 XML、无效参数及无法解析的 `cref`。
- DocFX 从 DLL / XML 生成类型参考，提供完整运行时托管引用；不对 WPF 项目重复执行 MSBuildWorkspace 设计时加载，不把原生 DLL 当成元数据引用。
- 补充类型职责、参数、返回值、临时秘密和资源释放说明；接口实现继承内部契约，无法继承的成员使用明确说明。
- DevHost 改用具名 HTTP 处理器和响应 DTO，使方法、响应及字段的 XML 说明进入 OpenAPI。
- Scalar 提供实时入口和随静态站点分发的同版本本地资源。预览运行独立宿主副本，避免锁住编译输出。

## 实际执行结果

| 检查 | 结果 |
| --- | --- |
| 普通解决方案和可选 Aspire AppHost 编译 | 零警告、零错误 |
| DocFX 元数据和静态站点构建，warningsAsErrors | 零警告、零错误 |
| 手写源码 Roslyn 语法与文档清单 | 942 个公开声明，无缺失注释、无语法错误 |
| 已审阅架构类型 | 82 / 82 具有生成后的职责说明 |
| 源码公开方法说明 | 274 / 274 在元数据中具有解析后的说明，含内部 inheritdoc |
| OpenAPI | 两个实际 GET 路由，两个响应模型、十个字段均有说明；无 DI 参数泄漏 |
| 编译器反例 | 五项全部拒绝：缺注释、坏 XML、无效 cref、错误参数名、遗漏参数 |
| 浏览器 | DocFX 首页、C# 接口页、实时 Scalar、静态 Scalar 四页通过 |
| 浏览器资源与脚本 | 无 HTTP 加载失败、页面脚本错误或外网请求 |
| 核心回归 | 27 / 27 通过 |
| 架构检查 | npm review 与 check-rewrite 通过；历史批准状态未改写 |

对应结果位于 Git 忽略的 `artifacts/docfx/verification.json`、`artifacts/documentation/guard-results.json`、`artifacts/documentation/browser/report.json` 和 `artifacts/documentation/core-regression/results.json`。浏览器截图保存在同一 browser 目录。

## 复现

```powershell
npm ci
npm run docs:build
npm run docs:test-guards
npm run docs:ui-check
npm run docs:serve
```

入口为 `http://127.0.0.1:5275/scalar/v1`、`http://127.0.0.1:5275/openapi/v1.json` 和 `http://127.0.0.1:5275/docs/index.html`。

HTTP 文档只描述现有 `/health` 和 `/api/operations`，C# 端口并未因此变成远程业务 API。全部生成和预览使用独立空工作空间；本次未执行真实 AVEVA 登录、许可签出、VMware 操作或 UAC。没有重新发布桌面 EXE；现有桌面安装与视觉布局没有因文档变更而改动。
