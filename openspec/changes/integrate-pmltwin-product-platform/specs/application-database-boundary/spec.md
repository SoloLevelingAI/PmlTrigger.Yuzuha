## Purpose

通过独立数据访问边界让各 Feature 共用可靠的应用数据管理，同时保护已有配置、知识库和 UIC 历史；将应用持久化与 AVEVA 工程数据库访问清楚区分，支持分阶段迁移和恢复。

## ADDED Requirements

### Requirement: Independent application database contract
系统 SHALL 通过独立数据库契约读写应用配置、文档修订、绑定和操作记录；客户端不得直接依赖数据文件布局或供应商类型。部署和工程对象动作 MUST 保留独立授权边界。状态：planned，现有存储来源见 [来源清单](../../source-inventory.md)。

#### Scenario: Shared document revision
- **WHEN** GUI 和 CLI 对同一修订分别提交修改
- **THEN** 仅匹配预期修订的写入成功，另一请求返回冲突及最新修订，不静默丢失修改。

### Requirement: Scoped migration and recoverability
系统 SHALL 按产品、版本和数据用途隔离知识及应用数据；迁移须先生成清单、备份和检查报告，再发布新位置。MUST 保留原始数据直到迁移验收，禁止把用户数据作为安装缓存删除。状态：partial，统一迁移 planned。

#### Scenario: Failed migration
- **WHEN** JSON 或 UIC 工作区迁移发生损坏、哈希不符或版本不支持
- **THEN** 拒绝切换活动存储，旧目录可继续读取，报告具体项和恢复路径。

#### Scenario: Isolated product libraries
- **WHEN** 导入 E3D 2.1 与 AM 12.1.SP4 两套资料
- **THEN** 分别登记和校验，禁止跨产品版本默默合并；新鲜度和实机证据分别标记。

### Requirement: Independent database assembly delivery
系统 SHALL 交付独立的 `PmlTwin.Database.dll` 及其契约边界，GUI 与业务层不得直接依赖其存储实现或供应商类型；MUST 在匹配的管理运行时加载。状态：planned，不能以同一 EXE 内接口替代独立程序集交付。

#### Scenario: Inspect product dependency graph
- **WHEN** 验收产品发行包与程序集引用
- **THEN** Database 实现 DLL 独立存在，由装配层接入；UI/业务只引用契约，NET35/NET48 Host 不加载 net10 Database DLL。

### Requirement: Separate engineering database access
系统 SHALL 将 AVEVA 对象身份与查询通过匹配产品/运行时的通信适配器访问，不能由应用数据库文件替代。MUST 校验会话身份，不把 DBREF 字符串视为跨工程永久有效对象。状态：planned，沿用现有独立通信 CLI 边界。

#### Scenario: Old runtime host
- **WHEN** 现代管理器连接 NET35 或 NET48 产品宿主
- **THEN** 使用版本匹配的进程协议和宿主适配，不尝试将现代 Database DLL 直接加载到旧宿主。

### Requirement: Read only legacy migration preview
迁移预览 SHALL 明确支持格式、来源摘要、字段及历史映射、秘密剔除和不支持项；MUST 只读选定来源并保持 activate=false。状态：planned；细化见 [续作契约](../../features/continuation-contracts.md)。

#### Scenario: Legacy source contains credentials and unsupported launch mode
- **WHEN** 来源包含秘密、凭据引用或 Direct 启动配置
- **THEN** 输出剔除路径及不支持诊断而非秘密值；不将 Direct 改成 Monitor，不写来源或激活配置。
