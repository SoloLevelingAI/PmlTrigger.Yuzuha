# 沿功能流程读代码

以下路线描述仓库当前的静态实现和授权边界，便于从入口追到结果。运行时通信可同时查看 [LikeC4](../../artifacts/architecture/index.html)。

| 场景 | 入口与处理顺序 | 结果与失败边界 |
| --- | --- | --- |
| 连接工程会话 | MCP / CLI `list_aveva_sessions` → `AvevaSessionDiscovery` → 用户选择 PID → `select_aveva_session` → `YuzuhaRpcBridge` → Host 身份查询 | 锁定 PID、启动时间、模块、专用管道；窗口存在或管道可达不能单独证明目标正确 |
| 读取当前元素 / DBREF / 全局对象 | `get_builtin_usage` 取得内置方法 → `generate_pml_call` 生成文本 → 选定并核验目标 → 已授权的 `run_pml_command_list` → Host 主线程 → BFS 解析 | 保留原始无法解析行，统计基于完整结果；超时后不得自动重发 |
| 内置优先检索 | 知识 MCP / CLI `search_knowledge_layers` → `BuiltinGuideCatalog` → 可选补充索引 → `QueryPlanner` → SQLite FTS5 → 加权融合 | 匹配内置指南时无需数据库；补充内容是资料，不能变成执行授权或自行替代内置方法 |
| 记录窗体操作 | PML 监控代码调用 `BeginLog` → `FormRecordWriter` 保存 before → 操作 → `EndLog` 保存 after → 连续 JSONL | 同一编号只配对一次；截图失败与数组记录状态分别说明；MCP 工具调用另有统一 JSONL 日志与心跳，尚未自动关联截图 |
| 安装与更新 | 显式选择范围 → `SetupBridge` 发现与自动请求 → `CreatePlan` → 逐项 `Preview` → 用户批准 → 原有应用与恢复入口 | 文档生成和索引不会执行安装器；只读检查不构成对安装或 AVEVA 模型修改的许可 |
| HTTP 预览 | DevHost → `DevelopmentEndpoints` → 内置指南 / 参数格式化 / 查询规划 → 具名 DTO | 只计算预览，OpenAPI 对应真实四个路由；模型执行仍走工具服务 |

| 插件开发 | 原生工作台 → 启动配置 / 复制预览 / 目录检查 → MCP 原生 PML → MARKDB / ENDMARKDB | 相关开发任务不逐条重复审批；目录超预算提醒清理，备份位于注册路径外 |
| AI 工具活动 | MCP 请求过滤器 → RuntimeJournal → 本机滚动 JSONL / 心跳 → 原生工作台 | 报告真实工具执行、失败和待核验状态，不推断 AI 内部思考 |

## 核心术语

- **PID 与启动 ticks**：二者联合识别一个进程实例，避免操作系统重用 PID 后误连其他工程会话。
- **Host 主线程**：PMLNet 对象创建时绑定的 AVEVA 线程。RPC 到达后必须回到该线程才处理模型操作。
- **DBREF / BFS**：工程元素引用与按层展开的查询结果；深度、路径和原始值用于解释来源。
- **来源角色**：内置、项目、官方、经验、自定义资料分别管理；同一片段编号只能在其所属数据库中解释。
- **成功与可达**：连接、身份核验、命令执行和结果有效是不同判断，不能彼此替代。

[全项目与类型目录](api/index.md) · [CLI/MCP 双入口](cli.md) · [覆盖报告](../../artifacts/docfx/navigation/coverage.md)
