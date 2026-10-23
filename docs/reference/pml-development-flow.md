# PML 开发、加载与 HTML 界面链路

2026-10-23 按用户提供的 `pmlforms.pmlfrm` 核对需求，并新增可运行的开发脚本，把复制、REHASH ALL、Object/Form 重载和结果核验串联。独立夹具已在 E3D 实测，见[操作说明](../../artifacts/docfx/navigation/repository/tests/live/pml-development/README.md)与[本批报告](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-pml-development.md)。该流程尚未接入现有桌面按钮。

## 需求记在哪里

| 内容 | 维护位置 | 用途 |
| --- | --- | --- |
| 用户原意、采纳决定、状态、验收条件 | [迭代记录](iteration-log.md)，Markdown | 记录需求及变化，不把计划写成已交付 |
| 功能行为、流程与边界 | 本页及其他 `docs/reference/*.md` | 连接操作、代码与实际支持 |
| 类型、成员、参数、返回值、示例 | 源码内 XML 注释 | C# 使用 `///`，PML 使用 `--`；DocFX 提取 |
| 功能和 Script 归属、测试关联 | `capabilities.json`、`test-map.json` | 维护可校验的目录与关联 |
| 版本、环境、实际执行与限制 | `docs/validation/*.md` | 记录本批证据；含模型数据的原始日志留在本机 |

DocFX 将这些内容组织为可搜索网页。需求不只埋在代码注释中，XML 也不代替验收记录。

## Form、Button 与全量信息

参考文件为用户本机 Marine OH12.1.SP4 的 `PMLLIB/common/forms/pmlforms.pmlfrm`，SHA256 为 `608AC5C1AFA559A1E5BAA8F7BB8575F8CE755B1DE20461E7FE5CA56F9B95BA27`。只记录源码观察，不复制原厂文件入仓库；Marine 源码检查不等于 Marine 实机测试。

`selectForm()` 按 `attributes()` / `attribute()` / `objectType()` 把 FORM 成员分为 FMPROP、GADGET/MENU 和普通成员。`objectDefinition()` 继续枚举对象属性，数组转入数组展示；`definition()` 对已设置的 GADGET 读取 `type()`，并尝试读取标签。Button 属于 GADGET 体系，需要保留原生类型和各可读属性。

[当前 Form/Gadget 查询](form-gadget-query.md)只列出已设置 GADGET，详情固定读取 shown、active、visible、tooltip、callback。它还不是参考文件那样的属性浏览器；MENU、FMPROP、普通成员、控件全部可读属性及嵌套展开属于待扩展范围。现有 callback 字段只读文本，不模拟按钮点击。

后续“全量信息”应区分成员清单、控件类型、属性值和可继续展开路径，逐项保留成功、unset、不支持或读取失败。数组使用真实定义下标，提供预算、续读和截断状态；CONTAINER/.NET 内部对象与原生未暴露内容单列范围。读取可读信息不等于执行所有方法或回调，不支持的属性也不能当作空值。

## 从源码到 AVEVA

| 步骤 | 当前证据 | 接通条件 |
| --- | --- | --- |
| 编写代码及 XML 契约 | 已有独立草稿目录、源码和 DocFX | 明确目标、差异、版本及测试 |
| 复制到自定义 PMLLIB | `PluginDeployment` 已有预览、哈希、备份和回执 | 核对会话搜索路径、目标文件和覆盖备份 |
| `PML REHASH ALL` | 开发脚本在复制后发送并记录回执，E3D 实测通过；旧桌面复制动作保持原行为 | 每步记录目标与结果；确需重建全部路径时使用 |
| `pml reload form !!Name` | 开发脚本按清单执行；合成 Form v1→v2 与实际按钮交互通过 | 记录结果并核对构造版本，其他窗体生命周期仍按具体对象验证 |
| `pml reload object Name` | 开发脚本按清单执行；合成 Object v1→v2 通过；Walker 前批另有实测 | 新建实例并核对实际行为，不以成功回执代替值核对 |
| 行为与结果核验 | 开发脚本比较 IPC 结果行数、路径和值；最终夹具 8 项匹配、0 未解析 | 记录预期、实际值、原生错误及清理；源码指纹与版本标记共同保存 |

`Invoke-PmlDevelopment.ps1` 与 `PmlDevelopment.psm1` 复用现有 PluginDeployment 和 Phayton CLI；每一步在派发前记录状态，失败或结果未知即停止、不自动重发。覆盖备份、来源/目标哈希和完整 RPC 证据留在本机。它是开发命令入口，现有桌面复制按钮保持原行为；没有改 Host 协议或旧对象调用。

AVEVA 的 [Storing and Loading PML Files](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG4.04.09.html)说明搜索路径；[Rebuilding All PML File Indexes](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG4.04.10.html)说明 REHASH ALL；[PML Directives](https://help.aveva.com/AVEVA_Everything3D/3.1/PMLPUG/PMLCG14.14.7.html)说明 Form 重载；[Methods on User-Defined Object Types](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG3.03.18.html)说明 Object 重载。Form 引用带 `!!`，Object 使用类型名，按文件类型选择；不是每次都须两个都执行。引用包含 E3D 2.1/3.1 文档，各现场版本仍需独立验证。

## HTML 作为界面

用户提出 HTML 作为操作界面，后端继续使用现有 C# 服务、Host 主线程和 PML。建议链路为 HTML 操作 → 本地应用桥 → 现有服务/会话核验 → Host → FORM/PML Object → 结构化回执 → HTML 展示。HTML 可承载按钮、属性浏览和部署测试流程，原生 FORM/PML Object 承担 AVEVA 侧能力。

本批把表格展示与导出从 PML 中移出：已有 IPC 返回结构化 Items，`pml-data-table.mjs` 在客户端生成可搜索/导入/导出 CSV 的 HTML 快照。PML 不必负责写表格文件。HTML 页面尚无持续执行桥，应用登记、生命周期和生产调用仍在[计划功能](features/planned-tools.md)中。

对当前的数据浏览/筛选/导出场景，优先 HTML 是工程判断：表格排版和交互更容易独立迭代。WPF 也能做精致界面，适合现有本机设置、窗口和系统集成；PMLFRM 适合 AVEVA 原生拾取、控件和流程嵌入，只是复杂表格与排版需要更多代码，并非不能编写。新夹具已实际运行 PMLFRM 按钮并调用 PMLOBJ/PMLFNC。

浏览器页面不能直接使用 .NET 命名管道。后续可用 WebView2 作为薄宿主，通过 [Web messaging](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis) 传结构化请求，再复用现有 C# / IPC / Host 主线程。来源、会话、请求 ID、错误和权限应保留在本地桥中。AVEVA 也提供 [CONTAINER Gadget](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG21.22.10.html) 承载外部控件；选用何种宿主须按实际产品/运行时验证，本批没有增加该部署依赖。
