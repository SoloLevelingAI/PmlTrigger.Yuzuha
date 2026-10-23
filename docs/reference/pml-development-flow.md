# PML 开发、加载与 HTML 界面链路

2026-10-23 按用户提供的本机 `pmlforms.pmlfrm` 核对需求和现有源码。此页描述当前支持与后续验收条件，不表示整条自动流程已经实现。

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
| `PML REHASH ALL` | 有原生命令入口和官方语法；复制不会自动执行 | 记录目标与结果；确需重建全部路径时使用 |
| `pml reload form !!Name` | 有原生命令及参考源码；本批未执行 Form 重载验证 | 记录结果，必要时处理已有窗体生命周期 |
| `pml reload object Name` | 本次 Walker 重载、实例化及大数组读取已通过 | 新建实例并验证本次行为；其他对象不能继承此结论 |
| 行为与结果核验 | Walker 已实测；通用自动流程未完成 | 记录加载版本/可观察行为、预期、结果、错误及清理 |

已有能力可支撑在明确任务授权内逐步复制、重建索引、重载及测试，但当前桌面没有组织成完整自动操作。后续应串联各步回执，失败或结果未知时记录停在哪一步，诊断后再决定下一步，不能以复制成功替代加载成功。

AVEVA 的 [Storing and Loading PML Files](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG4.04.09.html)说明搜索路径；[Rebuilding All PML File Indexes](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG4.04.10.html)说明 REHASH ALL；[PML Directives](https://help.aveva.com/AVEVA_Everything3D/3.1/PMLPUG/PMLCG14.14.7.html)说明 Form 重载；[Methods on User-Defined Object Types](https://help.aveva.com/AVEVA_E3D_Design/2.1/PMLPUG/PMLCG3.03.18.html)说明 Object 重载。Form 引用带 `!!`，Object 使用类型名，按文件类型选择；不是每次都须两个都执行。引用包含 E3D 2.1/3.1 文档，各现场版本仍需独立验证。

## HTML 作为界面

用户提出 HTML 作为操作界面，后端继续使用现有 C# 服务、Host 主线程和 PML。建议链路为 HTML 操作 → 本地应用桥 → 现有服务/会话核验 → Host → FORM/PML Object → 结构化回执 → HTML 展示。HTML 可承载按钮、属性浏览和部署测试流程，原生 FORM/PML Object 承担 AVEVA 侧能力。

已有开发 DevHost 和文档网页；HTML 应用登记、生命周期及生产调用桥仍在[计划功能](features/planned-tools.md)中，网页预览不能当作运行链已经接通。后续应验证应用来源/入口、准确会话、真实按钮请求、错误和回执展示，以及一次完整的复制—重载—行为核验。
