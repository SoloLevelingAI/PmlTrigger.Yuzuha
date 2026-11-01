## Purpose

把减少路径输入、重复选择和故障排查的易用性落实到可独立调用的后端，让界面与自动化脚本共同使用带来源、时效和诊断的环境结果，而不是把正确性寄托于向导提示。

## ADDED Requirements

### Requirement: MDB candidates have explicit provenance
后续 MDB 候选发现 SHALL 区分用户手填值、静态配置提示和当前工程登录后的权威列表。当前实现只接受 MDB 输入，尚无列表发现操作；MUST NOT 将常见名称 `/ALL` 猜作默认值或将其他工程的历史列表冒充当前可用列表。

#### Scenario: MDB discovery is unavailable
- **WHEN** 已定位产品和工程但尚无受支持的 MDB 查询能力或必要登录状态
- **THEN** 保留明确的 needsInput 与原因，允许用户提供 MDB；此输入不代表已证明登录权限。后续增加查询应先定义版本化操作、身份校验、只读边界及列表过期规则。

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

### Requirement: Desktop launch uses shared durable facts
桌面启动 SHALL 与 CLI/HTML 共享冻结计划、操作身份和持久受理语义；MUST 区分入口进程创建与工程登录、Host 核验。状态：planned；细化及测试映射见 [续作契约](../../features/continuation-contracts.md)。

#### Scenario: Reopen after an uncertain launch
- **WHEN** 桌面启动返回 unknown，用户重开窗口并输入原操作编号
- **THEN** 可观察已有事实，不自动创建第二次启动；密码不进入恢复记录。
