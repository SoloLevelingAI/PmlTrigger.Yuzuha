# WPF 启动续作实施细化

映射 platform 3.4 / manager 6.5 的有限子项；遵循 continuation-contracts.md 第 1 节。保留 launch.profile.start 等公开兼容入口，不覆盖 Host 灯态或完整 GUI 验收。

WPF 保存配置后，以同一 LaunchProfileService 构造 LaunchWorkflowService；显式点击生成 operationId，再 Plan / ExecuteAsync。移除桌面的直接 Start 和启动 journal 写者。凭据沿用现有用户明确选择的 WindowsCredentialStore，执行密码仅局部变量；异常 UI 只显示稳定 code、阶段、异常类型，避免输出底层含秘密消息。

新增操作编号输入、观察按钮和回执正文。正文显示 state/phase/code/message、operationId/attemptId、入口进程观察与 Host 未核验。unknown 或执行调用失去回执后锁住当前窗口的新启动，保留原编号，只观察、不重放。重新打开可输入原编号恢复观察，不新增持久指针或第二份事实。观察 unknown 不解除锁，编辑编号不解除锁。

验收以专用 DesktopLaunchWorkflowTests 检查 WPF 接线，并使用真实 Manager 协调层和临时合成文件验证重复编号、冻结目标/修订变化、unknown 恢复和无秘密持久化。另构建 WPF；无实机启动/部署，无 Host 全灯态验收。SDK 就绪前不构建。

## 实现与本次验证（2026-10-31）

- `MainWindow.LaunchWorkflow.xaml.cs` 是桌面适配，唯一执行/观察后端为 `LaunchWorkflowService`。桌面不再直接 Start，也不新增启动 journal 事实。保留现有最近入口创建时间字段；更新时间失败不改变协调回执。
- `MainWindow.xaml` 独立常显恢复卡片提供原编号输入、观察按钮及完整回执。unknown 锁住当前窗口新启动；新窗口可粘贴原编号，经正式按钮读取同一持久回执。
- 明确输入/授权/修订拒绝返回 rejected；执行后未取得可靠回执保留编号，禁止自动重放。异常正文不显示、不记录；阶段、稳定 code 与异常类型用于定位。
- 总览的“已核验”精确化为“目标身份已核验”，不声称登录、协议或部署全绿。
- launch-lifecycle smoke 专用启动参数禁止定时刷新与 Loaded 的真实安装/会话自动扫描；普通启动保持原行为。唯一既有 smoke hook 在新打开窗口调用新增控件核验。

验证使用仓库隔离 SDK 10.0.302。最终 WPF 增量 build 成功、0 警告/错误；`YuzuhaToolkit.Tests --filter FullyQualifiedName~Desktop` 56/56 通过（含本切片5项）。专用行为测试覆盖同编号唯一尝试、unknown 重启观察、文件/修订变化拒绝及秘密不持久化；少量源码断言只作为架构接线检查。

实际 WPF 隔离 smoke 通过，`launch-lifecycle-ui.json` 为 Passed；`launch-workflow-ui.json` 记录正式按钮观察、unknown 禁止新启动、编号只读、控件可见均 true；`binding-errors.log` 为0字节。最终 `launch-workflow-unknown.png` 已人工查看：编号、恢复观察按钮、未知回执可见。最初发现恢复卡片处于折叠诊断区，已移出并重新构建/截图复验，不把最初 `controlsVisible=false` 当通过证据。

日志：`artifacts/continuation/wpf-launch-build.log`、`wpf-launch-tests.log`；最终截图/绑定日志/两份UI回执位于 `artifacts/continuation/wpf-launch-smoke-final/`。这些是工作区产物，不是AVEVA实机验收，不包含凭据。未启动真实产品、未部署、未提交推送；Host全灯态、真实登录、跨产品矩阵与窄窗视觉专项仍待后续。共享任务与规范状态由主代理维护，不能据本切片勾选platform3.4或manager6.5整项。

页头补充：启动页的 HeaderActionButton 同步 LaunchButton 禁用态；SetPage 在其他页面恢复其各自入口，返回启动页再次取当前禁用态。最终 smoke 已断言 unknown 页头禁用、切换总览可用、返回启动页仍禁用；回执新增 headerLaunchDisabled/otherPageHeaderEnabled=true。最终截图已目检页头灰态，新增控件仍可见；最终 build 0警告/错误，binding-errors.log仍0字节。
