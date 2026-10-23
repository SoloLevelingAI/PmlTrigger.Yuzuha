# 本地 HTML 应用管理器

2026-10-23 新增独立 `YuzuhaToolkit.WebApps`。网页管理 Form 分类、按钮绑定和有序参数，通过既有 `YuzuhaRpcBridge` / `YuzuhaOperationBackend` / Named Pipe / AVEVA Host 运行 PML。现有 Desktop、MCP、CLI、旧 Walker 调用和 Host 协议不改，不引入 PMLCMD。

## 本版能力

| 内容 | 当前行为 |
| --- | --- |
| 分类与目录 | 新增分类，登记 HTML 表格应用或原生 Form，修改名称/分类/说明/启停状态，搜索应用 |
| 按钮绑定 | 每个应用可有多个稳定动作标识，编辑 PMLFNC、PMLOBJ 方法或 Form 目标及返回协议 |
| 参数 | 按声明顺序传入 string、dbref、attribute、integer、real、bool；配置显示文字、默认值和数值范围 |
| 对象 | 每次创建无参新实例再调用方法；不维持跨请求实例 |
| Form | show 打开原生 Form，也可登记已有方法；生命周期由 AVEVA 管理 |
| 结果 | 真实 Items 表格，搜索全部返回项，100 行分页，导出匹配项 CSV 和完整调用记录 JSON |
| 记录 | 派发前保存请求、绑定快照、会话和来源指纹；保留执行/读取原始回执、错误与未解析数 |

本版提供内置 HTML 数据表和原生 Form 入口。第三方 HTML 包装载、远程市场、包更新卸载、WPF 集成和全部已加载 Form 的自动发现尚未实现。Form 由用户登记分类，不把界面存在当作这些扩展已经交付。

## 启动和部署

需要 Windows、本机 .NET 10 / ASP.NET Core 运行时及已加载 Yuzuha Host 的 AVEVA 会话。本批仅实测 E3D 2.1 / NET48 Host 0.3.2.0；Marine、PDMS 和其他版本分别验证。

```powershell
dotnet build src/YuzuhaToolkit.WebApps -c Release
pwsh -NoProfile -File scripts/Start-HtmlAppManager.ps1
```

默认地址 `http://127.0.0.1:5288`。`-DataDirectory` 指定配置和证据目录，`-PmlLib` 指定已部署源码根目录，`-Executable` 可指定发布包。启动和选择会话不执行 PML，按钮运行才派发。

属性示例依赖新版 `PhaytonObjectWalker` 和 `tests/ai-pml/html-manager/YuzuhaHtmlData.pmlobj`；函数/Form 示例复用前批 `YuzuhaDevelopmentProbe`。先使用已有 `Invoke-PmlDevelopment.ps1` 部署新增目录，清单为 `tests/live/pml-html-manager-manifest.json`。流程核对会话实际 PMLLIB，再复制、REHASH ALL、Object 重载和适配器类型验证。设置 `-PmlLib` 不等于文件已进入会话搜索路径，见[复刻步骤](../../artifacts/docfx/navigation/repository/tests/live/pml-html-manager.md)。

## 调用模型

```text
HTML 按钮 → 应用 ID / 动作 ID / 类型化参数
          → 本地 JSON 绑定 → 自有 PML 宏和结果数组
          → 既有 IPC / AVEVA 主线程 → 原有 Items 解析 → HTML 表格
```

前端不传任意 PML 文本、宏路径或管道名称。目标必须是单个 PML 名称，在 PMLLIB 下有唯一同名源码。拒绝多行、PML `$` 展开符、未知参数、非有限数值和范围错误，再复用现有标量字符串编码器。

数组沿用 `[depth] path<TYPE> value` 协议，其他数组格式需 PML 包装器。标量转为 `Result` 行；`none` 仅表示 `Dispatched`，不冒充业务结果。对象构造参数、任意对象返回和多步事务不在本版范围。

保存绑定只影响下一次调用，不自动复制或重载代码；部署继续使用已有流程。SHA256 证明派发时的磁盘来源，不能单独证明 AVEVA 已加载版本；本批另核对实际 `html-data-v1` 标记。

管理器使用独立本地绑定目录，不修改 MCP 的 `PmlFunctionRegistry` 许可，也不将本地登记冒充该工具授权。既有工具仍保持原语义；新入口执行用户明确配置的本地动作。

## ELDATA 的含义

ELDATA 是 AVEVA 数据库属性名，本次是现场归档元素子项上的大数组，作为“大属性普通读取失败后 VAR SPLIT、缓存格式化及下标访问”的真实用例。它不是 HTML 或 IPC 的专用格式；同一适配器也能查询其他属性。

本批提取 2410 项，VAR SPLIT 返回 PML STRING。没有取得该元素类型的正式属性编码定义，不能把这些字符串直接解释成各槽位的业务含义；本版不宣称解码 ELDATA。界面和 IPC 负责通用调用与展示，业务解释由相应 PML 适配器及产品数据模型承担。

`maxRows` 限制 Walker 格式化行数，不限制 VAR SPLIT 的完整提取量。`Meta.Total` 是提取总数，`Meta.Returned` 是本次输出数组项数，`Meta.HasMore=TRUE` 提示预算截断。单项计数从缓存节点取得，不依赖格式化文本中的空格。

## 本地边界与失败

固定监听 `127.0.0.1`，检查 Host/Origin 和本进程请求令牌。页面不加载远端代码；数据目录不作为静态资源发布。配置、宏、模型读取结果与原始证据仅存本机；关闭管理器不关闭 AVEVA，也不保存模型。

每次调用仍核验固定 PID、启动时间、管道和模块。启动 ticks 按字符串输出，避免 JavaScript 精度损失。派发前保存 `Dispatching`；失败、未知或进程中断后不自动重发。相同请求 ID 返回原记录，参数不同则拒绝。同一管理器串行执行。遗留 `Dispatching` 表示待核对，不能当作未执行。

目录使用版本比较和原子替换。其他页面已修改目录时，旧版本保存和旧绑定调用均拒绝，刷新后再操作。

## 验证入口

离线契约：`dotnet run --project tests/WebApps -c Release`。前端：`node --test tests/web-apps-ui.test.mjs`。真实 IPC：`tests/live/pml-html-manager.ps1`，DBREF 由现场提供。见[本批实测](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-html-app-manager.md)。
