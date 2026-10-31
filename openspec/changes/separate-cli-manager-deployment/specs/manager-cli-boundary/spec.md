# Spec Delta

## Purpose

使用户通过桌面管理器或命令行完成相同管理任务，同时把 AVEVA 实时通信保留在独立命令行进程中。明确两个 CLI 的职责、结果和失败语义，使自动化调用无需依赖桌面窗口或遗留 MCP 服务。

状态：候选，未 sync/archive。Manager.Core/CLI、通信进程协议、兼容组合层与既有 WPF 会话/开发流程转接已实施并有离线验证；新管理页面和跨产品实机验收仍待完成，不能据后端测试推断 GUI/CLI 全部一致。当前源码见 [design.md](../../design.md#现有源码与测试定位)，实施范围与限制见 [implementation-verification.md](../../implementation-verification.md)。设计采用共享基础与九个独立 Feature，当前模型可自行设计，DeepSeek 非前置条件；各 Feature 的操作映射与实现差距单独交付。

## ADDED Requirements

### Requirement: 管理器的 GUI 与 CLI 提供同一管理能力

管理器 SHALL 提供独立 GUI 与 CLI 入口，使用相同的配置修订、预览、执行和错误规则。管理器 CLI MUST 能在未运行 GUI 时完成安装发现、部署、启动、复制、数据及知识管理；普通命令不得隐式打开窗口。

#### Scenario: 桌面关闭时执行管理任务
- **WHEN** 用户在 GUI 关闭时以管理器 CLI 预览部署或导入计划
- **THEN** 返回结构化计划和退出码，且不要求启动 GUI

#### Scenario: 两种入口处理同一过期计划
- **WHEN** GUI 或管理器 CLI 提交的计划引用已变化的文件或配置修订
- **THEN** 两种入口均拒绝应用，并要求重新预览

### Requirement: 管理器经独立通信 CLI 调用 AVEVA

管理器 SHALL 调用明确选择的通信 CLI，使用有版本的 UTF-8 JSON 请求和结果。通信 CLI MUST 独立核验每次调用的进程身份、启动时间、管道、模块与协议；不得依赖上一次 CLI 进程中的选择状态。日志与结果通道 MUST 分离。

#### Scenario: 新 CLI 进程重新核验目标
- **WHEN** 管理器对先前发现的 PID 发起下一次通信调用
- **THEN** 通信 CLI 重新验证该目标，PID 重用或身份变化导致拒绝

#### Scenario: 传输等待超时
- **WHEN** 已发出的 AVEVA 操作未在期限内返回，或其响应无法解析
- **THEN** 管理器保留请求标识并显示结果未知，不自动重发，也不宣称操作已撤销

### Requirement: 既有自动化入口按明确策略迁移

系统 SHALL 保留已发布通信操作的 ID、字段、授权与退出码语义；管理操作迁入管理器 CLI 时 MUST 提供可验证的兼容转接和迁移说明。移除兼容入口必须在后续版本明确列为破坏性变更。

#### Scenario: 旧脚本调用迁移中的管理操作
- **WHEN** 兼容期内旧通信 CLI 收到既有启动或知识管理命令
- **THEN** 转接到管理服务并保持结果语义，或给出预先公布的明确迁移错误，不静默改变行为
