---
uid: developer-manual
document_kind: developer-guide
implementation_status: navigation
reviewed_on: 2026-10-29
canonical_fact: ../features/launch-by-profile.md
---
# 开发手册：从功能追到具体实现

## ① 先定范围

先读[状态](../status.md)，再读唯一[功能八股①—⑧](../features/launch-by-profile.md)。价值/范围对应BRD/PRD，操作对应FRD，约束对应SRS，前后端设计对应SDD，工程与扩展对应SDS。此页提供路线，不重写业务事实。

## ② 获取可核对的基线

当前核对基线为78b8dc4，历史首版见Git标签snapshot/v1-2026-10-27。先看[依赖](../dependencies.md)、[验证批次](../verification.md)及[模型交付](../development/model-driven-review.md)。仓库global.json所需SDK与本机现有SDK可能不同；不要用修改版本锁或自动安装依赖来掩盖差异。编译命令和目标见[仓库说明](../../README.md)。

## ③ 从输入追到后端

按[正文⑤⑥的伪代码](../features/launch-by-profile.md#frontend)阅读：界面/薄ViewModel → 同一LaunchModel → LaunchWorkspace冻结并保存 → LoginLaunchCoordinator。每个源码入口集中在[架构索引](../decisions/architecture.md)；版本冲突与保存失败路径见[模型说明⑦](../development/model-driven-review.md)。

## ④ 看具体脚本再看接口

协调器 → StartMonitor/StartMarineBatch/StartDirect → ProcessCommand → IProcessHost → WindowsProcessHost。参数策略属于具体脚本，系统调用属于后勤；先理解[正文⑥](../features/launch-by-profile.md#backend)，再按[源码表](../decisions/architecture.md)打开文件，不能仅凭接口猜原协议。

## ⑤ 修改前先写规则

从[逐条需求台账](../../openspec/requirements.md)找到规格，再补场景、正文和实现。保持后端业务、薄前端与中性后勤边界；扩展入口与理由见[正文⑦](../features/launch-by-profile.md#sds)。新增依赖与新界面仍遵守独立选型和FigCraft评审，不把本手册当实施批准。

## ⑥ 如何验证改动

按风险选择[验证入口](../verification.md)：规则用例、隔离进程、真实违规架构夹具、界面回归。检查冻结后编辑不影响执行、记录失败零启动、创建后不确定不自动重试。通过自动测试不等于真实AVEVA工程、许可或新VM参数回归通过。

## ⑦ Feature与开发工具如何隔离

先读[Addin](../development/addins.md)，再读[开发八节调用链](../development/INDEX.md)。清单决定装配，本机开关只控制VM脚手架；普通远程不依赖它。两包共用后端，CAF/第三方动态加载仍属未实现范围。历史实验只用于理解，不能作为第二启动实现继续维护。

## ⑧ 完成什么才可交付

正文、规格、需求状态与对应证据一致；手册/演示引用仍有效，DocFX无坏链。不要把旧批次测试数累加成当前覆盖，也不以构建通过代替视觉通过。理解成本是否降低可按[对比维度](../status.md)做新手/熟手任务评估，本轮没有该类实测数据。
