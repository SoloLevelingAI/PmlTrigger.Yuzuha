## Purpose

让 PMLTWIN 将启动、部署、通信和界面编排呈现为连续任务，同时允许开发实验独立交付；用户不必理解内部项目，维护者能核对每项能力的唯一所有者及真实实现状态。

## ADDED Requirements

### Requirement: Unified feature identity and ownership
系统 SHALL 为每个 Feature 提供稳定身份、契约版本、操作清单、所属后端、依赖和交付状态；GUI、CLI 和 CAF 入口 MUST 引用同一业务契约。状态：planned；现有装配与操作目录证据见 [来源清单](../../source-inventory.md)。

#### Scenario: Multiple entry points
- **WHEN** 同一启动能力从界面或脚本调用
- **THEN** 使用相同校验、冻结输入、执行后端和错误语义，不维护第二套启动实现。

### Requirement: Optional composition without implicit execution
系统 SHALL 分别表达注册、启用、运行时兼容和动作授权；未启用扩展不得被装配或执行。CAF 兼容声明 MUST 绑定实际 SDK、宿主版本与验收证据。状态：partial，本地 Addin 与 worker 已存在，统一和 CAF 适配 planned。

#### Scenario: Remove development features
- **WHEN** 发行包不包含 VM 实验 Feature
- **THEN** 普通本机和远程启动不受影响，不加载 VM 依赖，不把实验开关当作普通远程授权。

#### Scenario: Missing or incompatible adapter
- **WHEN** CAF 适配器未提供或版本不匹配
- **THEN** 返回明确的不支持状态，不宣称 CAF 已连接；独立管理 CLI 仍可使用其已支持能力。

### Requirement: Preserve compatibility during integration
系统 SHALL 保留既有 CLI 和 Ribbon HTTP v1 的兼容语义，记录跨项目原需求 ID；LaunchDesk 历史资料导入 MUST 与功能融合、配置激活分开。状态：planned；既有净化导入为 partial 基础。

#### Scenario: Import legacy launch data
- **WHEN** 导入旧配置或不支持的 Direct 启动类型
- **THEN** 保留来源和逐项诊断，剔除秘密；不静默改成 Monitor，不自动激活，不修改原数据。

### Requirement: Single durable operation admission
系统 SHALL 对每个有副作用的业务操作使用唯一协调者、持久受理和执行租约，所有入口共享操作身份和事实；MUST 在受理持久化成功前禁止执行。状态：planned，复用 rewrite 已有操作语义。

#### Scenario: Duplicate request across entry points
- **WHEN** GUI 与 CLI 提交相同 operationId 和摘要
- **THEN** 返回同一操作记录，不产生第二次执行；相同 ID 不同摘要返回冲突。

#### Scenario: Admission storage failure
- **WHEN** 受理记录无法持久化或无法取得执行租约
- **THEN** 返回明确失败且不启动外部动作；失去回执的既有操作只能按原编号核对，不能换入口盲重放。
