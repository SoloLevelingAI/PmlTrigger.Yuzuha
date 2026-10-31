# 分层文件索引

目标：按功能查用途、入口、路径与验证；共性一处，差异明示，减少跳转。项目名省略 `LaunchDesk.`，各目录 `INDEX.md` 定位文件；文档站自动链接源码。

索引是归属事实源；子目录前缀只写一次，不另建重复说明。构建自动评价覆盖、唯一归属、职责分离与两跳定位；字数和重复说明另计，不能抵消漏项。复算见工具 `evaluate-index`。

| 层 | 项目索引／职责 |
| --- | --- |
| 视图／交互 | `Desktop`：WPF 视图、资源、草稿、命令及投影 |
| 契约 | `OperationContracts`：端口、不可变模型、请求与租约 |
| 纯规则 | `Kernel`：标识、状态、参数及健康规则 |
| 后端用例 | `UseCases`：提交、协调、启动、交付、变量与监测 |
| 平台后端 | `Windows`：存储、文件、进程、凭据与探针 |
| 装配／执行 | `Bootstrap` 装配；`Worker` 有限提权 |
| 开发宿主 | `DevHost` 只读 HTTP／Scalar；`AppHost` 可选 Aspire |
| 验证 | `Rewrite.Tests` 场景；`Architecture.Tests` 架构及回归 |

正式 UI 仍为原 `../LaunchDesk.App`；本目录含独立验证外壳，不重设计界面。[分层约束](../docs/reference/articles/architecture.md) · [实施／运行](../docs/rewrite/IMPLEMENTATION.md)

公共构建：各项目同名 `.csproj`；存在时 `GlobalUsings.cs` 管公共命名空间。构建：[方案](LaunchDesk.Rewrite.slnx) · [公共属性](Directory.Build.props) · [依赖白名单](architecture.json) · [发布说明](RELEASE-NOTES.txt)。[工具](scripts/INDEX.md)；历史压缩记录：[一](COMPRESSION.md) · [二](COMPRESSION-ROUND2.md)。

[共享注释](Comments.xml)：编译展开，原文只维护一次。
