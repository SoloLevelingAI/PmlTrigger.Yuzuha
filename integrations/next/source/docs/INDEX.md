---
document_kind: index
implementation_status: navigation
reviewed_on: 2026-10-29
---
# 文件与阅读索引

[项目作品集：八幕HTML演示](portfolio/index.html)｜[FigCraft原稿与设计说明](portfolio/README.md)｜[同尺寸往返对照](portfolio/compare.html)。以项目经理视角展示价值、易用性、技术链路和维护方式。

**先看[当前交付与证据边界](status.md)，再选[使用手册](manuals/user.md)、[开发手册](manuals/developer.md)或[核心演示](manuals/demo.md)。** 三类入口引用同一[功能八股](features/launch-by-profile.md)，不维护第二套事实。

**拟议评审：远程Project、手动连接、可迁移配置与本机凭据。** [八节完整方案](reviews/remote-portable.md)｜[FigCraft原稿/预览](design/remote-portable-review.md)｜[TOML选型](decisions/toml-selection.md)。这些是拟议设计，当前程序未实现。

开发验证、实验环境与脚手架源码集中在[开发维护索引](development/INDEX.md)。这些代码与入口不参与产品初始化，只有开发机显式启用才运行；产品日常呈现保持普通远程工程语义。

| 评审任务 | 最短入口 |
|---|---|
| 客户/使用者：价值、怎么操作 | [①价值](reviews/remote-portable.md#brd)、[③操作顺序](reviews/remote-portable.md#usage) |
| 前端：数据如何传入、如何回显 | [⑤前端伪代码](reviews/remote-portable.md#frontend)、[视觉验收契约](design/remote-portable-review.md) |
| 后端/拓展：具体脚本与后勤、提交与失败 | [⑥后端伪代码](reviews/remote-portable.md#backend)、[⑦失败处理](reviews/remote-portable.md#failures) |
| 测试/评审：配置VM与按顺序验收 | [开发维护索引](development/INDEX.md)、[逐条OpenSpec](../openspec/requirements.md) |

以下为**当前已实现功能**的阅读入口；功能事实统一维护在[八股正文](features/launch-by-profile.md)。

| 读者／任务 | 阅读入口 |
|---|---|
| 使用者、兼任管理员：首次准备与日常启动 | [③操作](features/launch-by-profile.md#usage) |
| 营销：一句话说明价值 | [①价值](features/launch-by-profile.md#brd) |
| 前端维护：同一Model绑定、显式事件和回显 | [⑤前端设计](features/launch-by-profile.md#frontend)，源码 src/LaunchDesk.Next.Desktop/Features/Launch/ |
| 后端维护：校验、证据、创建和观察 | [⑥后端设计](features/launch-by-profile.md#backend)，[协调器与具体脚本](decisions/architecture.md)，源码 Application/Features/LoginLaunch/ 与 Infrastructure/Contracts/ |
| 拓展：接口与字段细节 | [⑦工程与扩展／SDS](features/launch-by-profile.md#sds) |
| 验证与复核 | [⑧验证](features/launch-by-profile.md#verification)，[实际结果](verification.md) |

分层职责与完整源码路径见[架构与源码索引](decisions/architecture.md)；六类文档定位见[规格索引](specifications/index.md)。

[设计原稿与预览](design/index.md)保留界面规划；[OpenSpec行为场景](openspec.md)提供变更与自动校验入口。依赖事实见[实际锁定清单](dependencies.md)。

逐项跟踪需求与实施状态，见[OpenSpec需求台账](../openspec/requirements.md)。
