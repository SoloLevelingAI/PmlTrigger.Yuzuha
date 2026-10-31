---
uid: delivery-status
document_kind: status
implementation_status: mixed
verified_through: 2026-10-28
reviewed_on: 2026-10-29
implementation_baseline: 78b8dc4
---
# 当前交付与证据边界

## ① 读状态，不把方案当功能

截至2026-10-29，本页核对实现基线78b8dc4与已有证据，未重跑产品或VM。已实现表示存在实现与所列范围内验证；拟议表示方案或选型，不能作为操作承诺；历史表示当时的实验或版本结论，不代表现在机器状态。混合页面必须逐项标明范围。

## ② 当前能用什么

| 已实现范围 | 唯一事实与证据 |
|---|---|
| 目录发现应用，静态查找Project/Module，保留来源与人工修改 | [功能③⑥](features/launch-by-profile.md#usage)、[发现证据](verification.md#discovery-baseline) |
| 同一Model编辑、草稿代次检查、冻结执行快照、后端校验和具体启动脚本 | [功能④—⑥](features/launch-by-profile.md#requirements)、[模型证据](verification.md#current-evidence) |
| XML Addin控制已编译Feature装配，产品/开发双包共用后端 | [Addin说明](development/addins.md)、[Addin证据](verification.md#current-evidence) |
| List<Record>、日志与AttemptId关联，失败返回和创建后不确定状态 | [功能⑥⑧](features/launch-by-profile.md#backend) |

## ③ 不能当成已交付的部分

手动远程连接向导、TOML迁移/导入预览、共享凭据批量授权与撤销、新FigCraft界面均待实现或评审。Tomlyn 2.10.1已批准，尚未安装。现有Stored引用不等于共享授权管理；普通远程路径不依赖实验开关，也不等于已实现连接向导。CAF API适配、任意第三方DLL动态加载、热卸载和外部草稿自动合并未实现。DocFX主题已于2026-10-31统一到作品集的视觉语言，见[实际主题与证据](design/docfx-reading-theme.md)；仍待用户视觉认可。

## ④ 历史证明了什么

| 历史记录 | 能证明／不能证明 |
|---|---|
| [G0/G1](reviews/remote-project-lab-results.md) | 当时隔离网络/账户/共享检查通过；不能代替产品远程场景验收 |
| [许可与FNF实验](reviews/licence-checkout.md) | 当时宿主E3D用原生句柄进入Model并签出/归还许可；不能保证当前环境可用，也未完成新可配置脚手架的真实VM回归 |
| [原LaunchDesk复制调查](development/original-deploy-diagnostic.md) | 找到空Error传递链；历史底层复制原因未恢复，原程序与实际SeaGend未修复/更新 |

## ⑤ 哪一份资料负责什么

[功能八股](features/launch-by-profile.md)是价值、操作、规则和设计的唯一正文；[需求台账](../openspec/requirements.md)负责ID、状态与规格定位；OpenSpec维护约束/场景；本页聚合交付边界；[验证页](verification.md)按批次保存证据。三种手册入口只编排阅读和演练，不再维护第二份字段、接口或执行规则。

## ⑥ 面向谁，从哪里开始

[使用手册](manuals/user.md)解决准备、操作与失败；[开发手册](manuals/developer.md)解决从功能找到实现；[核心演示](manuals/demo.md)安排讲解、输入、预期和证据。各页沿用八节，链接回正文相同锚点。

## ⑦ 相比原LaunchDesk的提升与限度

| 问题 | 当前可见改进 | 结论限制 |
|---|---|---|
| 为什么失败 | 原版已观察到三次空Error；新快照边界有阶段、异常类型/HResult兜底，并关联记录 | 尚未修复原版；不能称所有故障都已覆盖 |
| 功能在哪里实现 | 入口直接链接协调器、Monitor脚本、后勤接口和Windows实现 | 属于可检查的查找路径改善，未做新手/熟手计时对照 |
| 怎么用、哪些可用 | 操作、来源、Created等状态含义集中；拟议与历史分开 | 文档更明确不等于UI更美观或使用成功率已提高 |
| 同一事实是否分叉 | Model/后端共用；手册引用唯一正文，包内共用DLL哈希验证 | 未声称完整替代原产品全部功能 |

当前收益主要是功能解释、查找与故障证据更明确。尚无用户研究支持“理解成本降低多少”或压缩率数值。

## ⑧ 元信息如何维护

文档头统一记录document_kind、implementation_status、reviewed_on；状态取implemented/proposed/historical/mixed/navigation。implemented指页面所述范围，不能扩大解释。历史页保留当时日期与结论，顶部明确历史身份。verified_through仅表示引用证据日期，reviewed_on仅表示文字核对日期；本轮没有新运行证据。后续行为变更先改八股/规格，再改台账状态与证据引用，最后检查三类入口及DocFX链接。

本轮[文档校验记录](../artifacts/documentation-review/verification.json)记录元信息、三份八节入口与站点链接检查；它不是新增产品测试报告。
