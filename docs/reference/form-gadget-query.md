# 原生 Form / Gadget 查询

2026-10-23 补充：用户提供的 Marine `pmlforms.pmlfrm` 已用于核对 Button 和全属性浏览需求。当前工具仍只返回下文列出的有限元数据；属性展开、部署重载自动链路和 HTML 桥的差距见 [PML 开发与加载链路](pml-development-flow.md)。

本批次在现有 C# MCP 与 NET35/NET48 Host 中增加三个只读工具。查询路径与 AVEVA 自带 PMLFORMS 相同：`FMSYS.FORMS()`、`FMSYS.SHOWNFORMS()`、`attributes()`、`attribute()` 和 `objectType()`。不调用 PMLFORMS 的 UI 方法，不替换窗体管理，不修改 PMLLIB/PMLUI 文件。

## 重点复用的能力

| 已有能力 | 本批次如何复用 |
| --- | --- |
| AVEVA FMSYS | 直接读取已加载、已显示窗体；不维护另一套窗体注册表 |
| 原生 PMLFORMS | 沿用其属性枚举和 GADGET 分类路径；不重写 Show/Hide/Kill 或回调管理 |
| 现有 C# Host / RPC | 沿用主线程分派、PID/启动时间/模块核验、串行请求和结果关联编号 |
| Shared 有界数组策略 | 复用预算验证，增加查询项及续读位置；不引入测试平台或新生产项目 |
| LaunchDesk 文档测试机制 | 继续使用 test-map、Roslyn 发现、DocFX 双向链接和按 ID 执行器 |
| YuzuhaExcuter / YuzuhaReadGlobal | 实机测试使用既有宏执行和全局读取来加载专用测试文件、读取真实错误及结果 |

| 工具 | 输入 | 返回内容 |
| --- | --- | --- |
| `list_pml_forms` | `shownOnly` 默认 true；索引与预算 | 已显示或已加载窗体名称、原生引用 |
| `list_pml_gadgets` | `formName`；索引与预算 | 指定已加载窗体的 GADGET 名称、引用、原生 Type |
| `get_pml_gadget` | `formName`、`gadgetName`；字符预算 | 类型、shown / active / visible / tooltip / callback 文本及每项读取状态 |

先使用现有 `list_aveva_sessions` 和 `select_aveva_session` 选择准确 PID。每次查询仍核验 PID、启动时间、模块和专用管道，并在 AVEVA 主线程执行。需要本批次 Host 的 `FormQueryVersion=1`；旧 Host 在发送查询前明确拒绝，不回退到原始命令。

名称只接受 1 到 64 个 ASCII 字母、数字或下划线，并以字母开头。传 `Sample` / `accept`，不传 `!!Sample.accept`、成员路径或表达式。不存在的窗体、非 FORM 全局变量、非 GADGET 成员及必要读取失败都返回失败。沿用 PMLFORMS 的 `set()` 检查，仅列出已设置的 GADGET 引用；MENU、普通成员与未设置的 KEYBOARDFOCUS 不在控件清单中，指定未设置引用的详情明确失败。

## 预算与结果

列表使用 `startIndex`（默认 1）、`maxItems`（默认 100，范围 1..1000）和 `maxCharacters`（默认 32768，范围 1..262144）。`maxItems` 限制**扫描的原生数组槽位**；普通成员、未设置引用和稀疏空索引也占预算。每个索引先经原生 DEFINED 判断，避免将空索引误报为读取失败。因此控件页可能为空且 `NextIndex` 仍非零。`IndexUpperBound` 来自 MAXINDEX，不等于控件数量。

`FormQueryResult.Items` 是完整返回项，`Scanned` 记录本次检查过的槽位（包括因字符预算未返回的候选项），`Characters` 是项内所有字符串字段的 UTF-16 长度总和，不含 JSON 包装。若下一项会超预算，保留该项的 `NextIndex`；首项超预算则明确失败，避免无进展的空页。`NextIndex=0` 才表示当前原生数组已读完。

原生窗体/属性数组在单次请求内复制到请求专有的临时变量，并在结束时清理。预算约束逐项扫描与返回体积，不约束原生枚举方法创建数组或单个字符串的瞬时分配。跨请求会重新取得原生数组；窗体变化可能导致顺序变化，不承诺冻结快照，不自动重试。

## 元数据与边界

详情不读取 `val`、列表数据、模型内容或 CONTAINER/WPF 内部对象，不触发回调。callback 只是读取原生文本。可选属性成功为 `Status=available`，空文本仍是成功的空文本；不受支持或读取失败为 `Status=unavailable`、`Value=null` 并附 `Error`，不能推断成 false。必要类型读取或临时变量清理失败会使整次查询失败。

复刻示例（同一 CLI 批次中先发现并显式选择实际 PID；不能把示例名字当作真实对象）：

```json
{"name":"list_pml_forms","arguments":{"shownOnly":true,"maxItems":20}}
{"name":"list_pml_gadgets","arguments":{"formName":"Sample","startIndex":1,"maxItems":20}}
{"name":"get_pml_gadget","arguments":{"formName":"Sample","gadgetName":"accept"}}
```

每项自动测试和人工案例均进入[测试目录](../../artifacts/docfx/navigation/tests/index.md)，可通过既有 `scripts/run-documented-test.ps1 -TestId <ID>` 确认源码后复刻。例如 `xunit:YuzuhaToolkit.Tests.PmlFormQueryTests.GadgetListUsesNativeAttributeTypesAndSkipsOrdinaryMembers`。

NET35/NET48 RPC Smoke 使用生产 DTO 和共享查询读取策略，但原生返回值来自合成夹具。它验证协议兼容性，不证明 AVEVA 的实际查询语法或属性支持。`manual:aveva-form-query` 与 `manual:aveva-gadget-query` 的人工步骤见`tests/manual/form-query.md`，未实测时保持 NotRun。运行输出留在本机 artifacts；CI 只上传离线结果及提交和环境信息。

实机合成脚本、Git 管理的 PMLFNC/PMLFRM 和运行参数见`tests/live/form-query/README.md`。脚本先比较部署文件与源码 SHA256，再核验会话与 Host 能力；检查真实返回值、分页、普通成员拒绝、预算拒绝和查询前后的计数器/值/回调/显示状态。它不会保存工程，也不会把脚本成功扩展为所有产品、所有控件的完整验收。

## 后续 Web 应用管理边界

用户确认的后续方向是管理 HTML/Web 应用：C# 负责应用目录登记、配置、版本与运行状态，以及部署回执和日志；页面通过本地桥接调用现有 RPC 和 AVEVA 自身的 FORM/PML Object。优先复用已有 WorkspaceSettings、PluginDeployment、RuntimeJournal 和 Host 主线程分派，不要求每个应用重写 PML 窗体，也不引入另一套原生窗体管理器。本批次提供查询接口，尚未交付 Web 应用管理页。

正式安装已有 Inno Setup EXE 与 NET48 SetupBridge。PowerShell 在这里用于开发验证和复刻；后续更新交付应继续采用可审查的安装产物、版本/提交/哈希清单、预览与回滚回执，并保留人工发布审查。本批次不自动发布或更新本机正式安装。

原生参考：[窗体加载和显示](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG16.16.26.html)、[名称规则](https://help.aveva.com/AVEVA_Everything3D/2.1/PMLPUG/PMLCG14.14.2.html)、[TEXT Gadget 元数据方法](https://help.aveva.com/AVEVA_Everything3D/2.1/SCRM/SCRM3.3.80.html)。查询枚举与分类另对照本机 E3D 2.1 自带 `PMLLIB/common/forms/pmlforms.pmlfrm` 的 load/selectForm 实现；原厂源文件不复制进仓库。
