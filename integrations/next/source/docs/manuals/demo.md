---
uid: core-demo
document_kind: demo-guide
implementation_status: navigation
reviewed_on: 2026-10-29
canonical_fact: ../features/launch-by-profile.md
---
# 核心流程演示：修改一次，执行留证

## ① 十秒开场

“把应用、工程和许可参数放在一处核对，启动时留下当次配置；出错能沿记录找到阶段。”依据是[正文①与⑥](../features/launch-by-profile.md#brd)，不宣传尚未交付的迁移或共享授权。

## ② 演示前准备

准备当前产品包、隔离数据目录与已安装目标应用；保留有效登录Addin。无工程/许可时演示配置编辑和失败反馈，不伪称成功登录。真实E3D演示另需有效工程、凭据与许可，不需要启动测试VM；历史许可记录仅作资料展示。本页为演示脚本，本次未运行任何目标程序。

## ③ 选一组具体输入

以已安装E3D3.1候选为例，Project=MAR、User=SYSTEM、MDB=ALL_NO_MDS、Module=DESIGN；路径、init、工作目录与许可地址必须取演示机实际配置，不从示例猜。该组合来自[历史案例](../reviews/licence-checkout.md)，不是所有机器通用默认。密码选Prompt现场输入，不展示或保存。

## ④ 展示找与改

选候选 → 打开Project/Module来源记录 → 返回核对输入 → 修改一项 → 展示草稿已保存；如需可复用名称再点保存配置。操作解释引用[正文③](../features/launch-by-profile.md#usage)，不另列第二套字段规范。

## ⑤ 展示执行数据链

点击启动，讲解下图；随后打开[正文⑤⑥伪代码](../features/launch-by-profile.md#frontend)和[源码索引](../decisions/architecture.md)，逐点定位，不在演示稿复制源码。

```mermaid
flowchart LR
  UI[编辑同一Model] --> Freeze[点击执行并冻结]
  Freeze --> Snapshot[草稿版本检查／快照与记录]
  Snapshot --> Backend[后端校验／许可／凭据]
  Backend --> Script[Monitor等具体脚本]
  Script --> Windows[Windows系统能力]
  Windows --> Result[返回状态／AttemptId记录]
```

## ⑥ 展示结果与失败

展示本次AttemptId、快照与记录；Created不说成登录成功。无有效环境可故意填非法端口，展示原输入留存与拒绝，不尝试启动；恢复参数。NeedsVerification时先人工核查，不再次点击。预期来自[正文④⑥](../features/launch-by-profile.md#requirements)。

## ⑦ 按观众切换深度

客户看价值和结果，使用者看操作次序；维护者看冻结输入及失败阶段；拓展者跟随具体脚本进入后勤接口。四者引用同一正文。需要说明历史真机成功时直接打开[历史证据](../reviews/licence-checkout.md)，明确不是本场新测试。

## ⑧ 结束时留下什么

记录包/提交版本、环境、所演示路径、AttemptId、实际结果与未演示项；截图先脱敏。对照[当前状态](../status.md)说明本次覆盖范围。演示不更新许可、安装应用或开启VM，尚无本轮新的运行证据。
