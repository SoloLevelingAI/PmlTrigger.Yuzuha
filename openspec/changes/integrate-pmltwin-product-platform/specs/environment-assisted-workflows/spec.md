## Purpose

把减少路径输入、重复选择和故障排查的易用性落实到可独立调用的后端，让界面与自动化脚本共同使用带来源、时效和诊断的环境结果，而不是把正确性寄托于向导提示。

## ADDED Requirements

### Requirement: Evidence based discovery
系统 SHALL 只读发现安装、版本、架构、初始化文件、项目、模块、UIC 引用及可用工具，返回候选身份、来源、时间、扫描范围和错误；MUST 区分静态推断与实际会话证据。状态：partial；已有扫描器见 [来源清单](../../source-inventory.md)。

#### Scenario: Discovery is incomplete
- **WHEN** 某目录无权限、超时或初始化文件包含未求值的控制流
- **THEN** 返回可用候选及不完整原因，不将不可读当成不存在，不把静态结果称为有效运行环境。

### Requirement: Minimum necessary user input
系统 SHALL 自动预填唯一且有效的候选；人工修改优先于后续扫描，保存偏好须重新验证。多候选冲突、秘密或不可推导的业务选择才需输入；无交互 CLI MUST 返回结构化 needsInput，不弹窗或等待终端输入。状态：planned。

#### Scenario: Repeat a known workflow
- **WHEN** 已有产品、模块和路径偏好仍有效且用户未要求修改
- **THEN** 不再要求手填这些信息，显示可修改的选择依据；不自动取得执行授权。

#### Scenario: Ambiguous headless invocation
- **WHEN** 脚本调用发现两个同优先级安装且未给目标
- **THEN** 返回候选与待选择字段，退出且不启动进程、不写部署文件。

### Requirement: Frozen plans and actionable receipts
系统 SHALL 为有副作用的操作提供预检和冻结计划，包含目标、输入修订、文件哈希、权限需求及影响；执行前复验。结果 MUST 提供阶段、稳定错误码、可理解原因、恢复动作和证据；不确定的执行不得自动重试。状态：partial，统一跨 Feature 契约 planned。

#### Scenario: Environment changed after preview
- **WHEN** EVARS、UIC、加载清单或选定进程身份在预览后改变
- **THEN** 拒绝旧计划并指出变化，要求重新生成计划，不能覆盖最新状态。

#### Scenario: Timeout after possible side effect
- **WHEN** 启动、PML 或部署调用在可能生效后失去回执
- **THEN** 标记 unknown，提供观察或核对入口，不能显示成功、自动重放或误报已回滚。
