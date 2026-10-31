# 具体功能脚本与中性后勤接口

> 历史阶段资料：保留当时行为和结果；当前实现与未完成项以[交付状态](docs/status.md)为准。

以dc38690第一版为基线，在codex/explicit-feature-scripts分支重构。原项目、Rewrite及第一版快照保留。

## 阅读顺序

1. Application/Features/LoginLaunch/LoginLaunchCoordinator.cs：看执行顺序和必要失败分支。
2. 同目录Scripts/StartMonitor.cs、StartMarineBatch.cs：直接看产品参数、入口要求和实际启动。
3. Scripts/FindInstalledAveva.cs、CheckAvevaLicence.cs：直接看本机查找及RMS/RULES策略。
4. Application/Infrastructure/Contracts/：后勤只认识进程命令、文件、端口、服务、JSON、秘密句柄与记录。
5. Platform/：查看上述能力在Windows上的实现。版本和保存内容校验属于SavedProfiles，文件实现不认识登录配置。

ILaunchService和IApplicationDiscovery是功能对外API，放在Feature中。每个具体脚本不再另建接口，也未引入工作流DSL。Execution/集中去重准入和记录拼装。

## 行为与验证

保持原有输入和JSON字段、数据目录、密码策略、Monitor八个参数、Marine字符限制、环境隔离、保存一致性、启动前证据门槛、启动后不确定结果和观察状态。UI布局及资源文件未改，没有新增依赖。

28单元＋9集成＋47架构＋3界面用例通过，共87项；发布EXE实际完成安全启动全流程。Release solution零警告零错误。新增ARC-07禁止后勤契约和Platform引用Feature，包含真实违规依赖夹具。具体测试、限制和日志见docs/verification.md及artifacts/refactor/。

文档仍以八股回答六类问题；DocFX从真实.cs复制源码资源，文件索引可以直达，不维护第二套代码正文。OpenSpec新增explicit-feature-scripts变更，严格校验通过。

## 运行与恢复

当前源码可用dotnet run --project src/LaunchDesk.Next.Desktop运行。重构发布包在artifacts/publish-scripts/；artifacts/publish/仍保留第一版，避免替换正在查看的窗口。

基线Git标签snapshot/v1-2026-10-27保持不变。重构提交、相对基线的Diff、源码归档和发布包保存到相邻LaunchDesk.Next.Snapshots的新快照目录。

这次是为缩短阅读与查找路径进行的重构，未把新增文件数或行数视为压缩效果；没有声称全文缩短50%。真实厂商登录、许可签出及后代链行为限制仍在，测试只启动安全进程。
