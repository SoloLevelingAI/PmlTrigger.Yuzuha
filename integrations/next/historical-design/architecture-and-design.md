# 共用架构与界面规划

状态：用户已指定使用 ArchUnitNET，并要求界面规划考虑 FigCraft；本轮记录规划，尚未实现规则、安装依赖或编辑设计文件。原项目和 rewrite 不修改，Agent 不派发。版本与依赖批准以[依赖报告](dependency-review.md)为准。

## 架构为多个 Feature 服务

共性包括输入快照、显式交互入口、后台协调、结果回传、Record/Log 和验证；本 Feature 只接入应用发现、配置编辑和启动这一条完整流程。可复用是职责稳定、第二个功能能沿同一路径接入，不是先造通用插件系统或泛型万能协调器。产品入口协议、预设和专用发现代码允许保持独立。没有第二个实际用例前，不宣称复用效果已被验证。

## ArchUnitNET 守卫

规则只放新项目 `tests/LaunchDesk.Next.ArchitectureTests/`，用 NUnit 执行；加载全部相关目标程序集并断言受检查类型数量不为零，避免空集合通过。

| ID | 必须约束什么 | 验证方式 |
| --- | --- | --- |
| ARC-01 | Domain 不依赖 Application、Platform、Desktop、WPF、Toolkit 或日志实现 | 检查类型依赖；允许明确列出的基础 .NET 值类型/集合，不用“System 全允许”掩盖副作用 |
| ARC-02 | Application 不依赖 Desktop、Platform 具体类、WPF 或 Toolkit | 类型依赖规则；后台经端口访问外部能力 |
| ARC-03 | ViewModel 不依赖 LaunchCoordinator、平台适配、进程/文件/注册表 API | 类型/成员依赖规则；前端显式交互入口才可引用协调器 |
| ARC-04 | LaunchInput/LaunchRequest 不依赖 ViewModel、WPF、通知或命令接口 | DTO 依赖规则；快照复制/不可变语义另做行为测试 |
| ARC-05 | Desktop 中只有 Bootstrap 能依赖具体 Platform 实现 | 按命名空间及类型检查，不把整个 Desktop 放进豁免；同程序集组合根例外不能靠程序集级规则实现 |
| ARC-06 | Platform 不依赖 Desktop；模块依赖无环；产品不引用旧项目或测试程序集 | ArchUnitNET 检查类型与环；项目引用和共享编译源另作构建输入检查 |

V-ARCH：先让正常样例通过，再逐条引入实际违规测试夹具，证明规则失败且指出依赖；测试报告关联规则 ID。不要在正式实现里留下故意违规代码。官方建议分析 Debug 构建；Release 编译另验，不能把 Debug 架构检查当发布验证。

ArchUnitNET 依据编译产物分析，不能独自证明 XAML 绑定不触发业务、反射没有越界、秘密不泄漏或 UI 好看；这些分别用交互测试、边界检查、日志核验及视觉对比补足。依据：[官方指南](https://github.com/TNG/ArchUnitNET/blob/main/documentation/docs/guide.md)。

## FigCraft 规划

候选产物：新项目 `docs/design/launch-workbench.figcraft` 和同目录 `previews/`；不覆盖原设计。先只读参考原工作台的布局、颜色、图标、按钮、间距，再在新设计文件中规划“发现候选 → 编辑预填 → 保存 → 启动与状态”。必要状态包括多候选、空结果、字段错误、等待、失败、未确认和窗口已出现；采用同一组件表达，避免为每种状态重做页面。

只允许一个指定编辑者串行操作 FigCraft；A 负责界面规划，其他 Agent 只提供文字约束。编辑前读取工具说明、保留组件/变量/原型关系，导出并检查预览；新增设计必须与原视觉基线对照，不能因工程化而简化掉美观。FigCraft 是设计资料工具，不成为产品运行依赖。此处没有启动编辑会话，也没有导出预览。

## 面向客户与维护者的阅读面

[客户八股简介](customer-brief.md)是同一 Feature 的简要入口；使用者不必理解架构和包名才能操作。维护者再进入[完整方案](launch-by-profile.md)的伪代码、基础能力和验证。客户简介只压缩表达，不另维护需求编号或扩展功能范围；需求变更以完整方案为依据同步核查简介。
