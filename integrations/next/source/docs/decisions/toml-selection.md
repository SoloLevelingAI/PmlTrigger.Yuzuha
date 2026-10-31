---
document_kind: design
implementation_status: proposed
reviewed_on: 2026-10-29
uid: toml-selection
---
# TOML库选型：Tomlyn 2.10.1已批准，暂不安装

调研日期2026-10-28，目标是当前net10.0-windows项目。**2026-10-28用户已批准Tomlyn 2.10.1；同时明确暂不安装、不实现WPF。库未安装、未运行。** 表中的兼容/功能是官方声明；编译、注释往返和业务映射都还没有实测。现有包、锁文件及JSON实现保持不变。

| 方案与精确版本 | 解决的问题 / 为什么考虑 | .NET与依赖、许可证 | 本项目的代价与判断 |
|---|---|---|---|
| **Tomlyn 2.10.1** | TOML解析/输出、带诊断位置的语法树；适合保留用户注释和仅修改指定字段 | 官方提供net8.0、net10.0、netstandard2.0；net10.0资产声明无NuGet运行依赖；BSD-2-Clause。包内包含生成器 | **已批准选型**。本项目优先语法树/显式映射，不把类型属性撒到Domain/输入类上。TOML1.1定位与旧API破坏性变化需注意；不用旧版ToModel/FromModel示例冒充当前API |
| CsToml 1.8.6；可选CsToml.Generator 1.8.6 | DOM和类型序列化，适合强调类型映射/生成器的方案 | 主包net8/9/10；依赖System.IO.Hashing>=10.0.11。MIT；自定义类型生成需要另选生成器，DOM方式不要求它 | 次选。额外生成器与传递依赖也要评审；官方语义模型说明不能替代“注释/格式无损”实测。Extensions/Configuration不在此次候选安装范围 |
| Tommy 3.1.2 | 简单节点式解析/写入，支持TOML1.0和基本注释 | netstandard2.0及旧Framework资产；NuGet声明无依赖，MIT。该版本发布时间2022-01-11 | 备选。版本发布时间较久，维护响应与边界测试需再评估；“基本注释”不等于完整格式无损 |
| 保留System.Text.Json | 继续使用现有可运行存储，零新增包 | 当前平台自带能力 | 可作为实施延期的回退，但不满足TOML编辑需求；不能把JSON重命名成.toml |

来源分别为[Tomlyn包元数据](https://www.nuget.org/packages/Tomlyn/2.10.1)、[当前发布](https://github.com/xoofx/Tomlyn/releases/tag/2.10.1)、[CsToml元数据](https://www.nuget.org/packages/CsToml/1.8.6)、[生成器元数据](https://www.nuget.org/packages/CsToml.Generator/1.8.6)、[Tommy包](https://www.nuget.org/packages/Tommy/3.1.2)和[Tommy源仓库](https://github.com/dezhidki/Tommy)。这是针对项目需求的比较判断，不是性能排名或安全背书。

**推荐的理由。** 我们的文件很小，优先级是错误定位、保留解释性注释、可审查的字段映射，而非吞吐。Tomlyn公开提供保留trivia的语法树，比仅用POCO重新序列化更贴近目标。推荐是基于官方能力的推断，不代表所有保存路径天然保留注释；若验收未通过，就不能以“库支持”为理由放行。

**版本与方言。** Tomlyn当前以TOML1.1为目标，不提供TOML1.0严格模式。拟议导出只使用1.0/1.1共同的普通字符串、表、数组和整数；应用模式用schema_version=1。跨库和工具互通仍是待测，不宣称兼容所有1.0消费者。若用户要求严格1.0解析，应重新评审候选，而不是自行降级包。

**依赖边界。** 只允许Platform的TomlDocumentStore适配层引用候选库，Feature通过格式中性的IDocumentStore使用它。现有IJsonDocumentStore需要后续重命名/适配；UI和协调器不承担序列化。不选配置Provider包、不引入另一个DI/配置框架，不在此次修改项目文件。生成器即便随包分发，也需在批准后的构建/分析器清单中确认是否启用。

**保存与迁移。** 编辑现有TOML保留原DocumentSyntax和字节基线，按稳定ID找节点后最小改动；不做“读POCO→重新输出整文件”。未经识别的模式/键只读且保留原文件，不静默丢弃。全新导出可以用一致格式与新生成的说明注释；这与修改用户已有文件是两个不同验收路径。generation提交、秘密排除和JSON迁移规则统一见[八节设计④⑥](xref:remote-portable-review)，不在此再维护第二份业务模式。

## 获准开始实施后才执行的验证清单

| ID | 要验证什么 | 通过条件 |
|---|---|---|
| TL-01 | 精确包版本/net10.0构建 | 在隔离分支审查实际锁文件与分析器，无未批准附加包 |
| TL-02 | 中文、UNC、反斜杠、空数组、可选值 | 与[静态样例](../reviews/examples/portable-profile.toml)语义一致；不以TOML不存在的null冒充空值 |
| TL-03 | 注释/空白/顺序 | 无改动往返字节保持；只改Module时，其他字段及相关注释不消失、不改义；失败则不写文件 |
| TL-04 | 解析和业务错误 | 重复键/坏语法/未知模式/悬空引用有可定位诊断；敏感值不进入异常显示 |
| TL-05 | 导出边界与导入授权 | 没有密码、本机凭据ID/blob、有效授权；导入槽位保持未绑定 |
| TL-06 | 并发与中途失败 | 修订冲突、磁盘满、指针替换失败保留旧generation；不出现一半新配置一半旧授权 |
| TL-07 | JSON迁移 | 只在预览确认后转换，原JSON备份可恢复，原JSON不覆盖 |
| TL-08 | 外部互通 | 共同子集样例被候选和独立解析器接受；不拿独立解析器通过代替候选库通过 |

本次只会用已存在的Python标准库tomllib检查文档样例语法；它不是产品依赖，也不等于Tomlyn验证。没有下载nupkg、执行restore或运行候选库。
