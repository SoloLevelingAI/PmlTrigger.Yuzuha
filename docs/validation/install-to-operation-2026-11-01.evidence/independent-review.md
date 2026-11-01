# 安装到工程操作独立审查

日期：2026-11-01。范围：当前工作树的 Inno manager 模式、旧 v2 所有权移交、Manager 部署/EVARS/恢复、WPF 准备任务状态、只读 HTTP 目录与 TS CLI 适配器。此次只读审查源码，未编译、运行安装器、修改生产环境或执行 AVEVA；本文件是唯一审查输出。

发现 4 项 P2，无已确认 P1。共享工作树正在修复，以下保留发现时的触发链；已观察到的源码修正不等于测试复验通过。

## R1 / P2：RECOVERY_REQUIRED 未绑定回执，后续恢复成功仍永久锁住工作区

- 发现时位置：`src/YuzuhaToolkit.Desktop/PreparationWorkbench.cs` 的 `Call`、`RestoreAsync`（原 127–134、154 行），`src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentOperations.cs` 49–53 行。
- `Call` 对 `RECOVERY_REQUIRED` 也设置 `Unresolved`，而 `RestoreAsync` 只对 `MayHaveExecuted=true` 绑定 `unresolvedRecovery`。真实 `deployment.restore` 的恢复冲突返回 `RECOVERY_REQUIRED`，没有传输未知标记。
- 触发：部署成功 → 修改一个目标 → 恢复待处理 → 人工处理冲突 → 按原回执恢复成功。最后一次成功删去 `Receipt`，却无法清除 `Unresolved`，之后不能生成预览，也没有恢复引用。
- 修复方向：同一回执上的待处理恢复与传输未知使用一致的精确身份绑定，保留历史回执不能清除后来未知的隔离规则。
- 当前状态：已通知根代理；审查结束前看到 `NeedsRecovery` 抽取及恢复分支使用该判断，尚未执行修复后测试。

## R2 / P2：单独 EVARS 模式接受产品与 Host profile 错配

- 发现时位置：`src/YuzuhaToolkit.Manager.Core/EvarsOperations.cs` 的 `evars.preview`（原 52–57 行），`src/YuzuhaToolkit.Desktop/PreparationWorkbench.cs` 的 `PreviewAsync`。
- AM 安装可在新 WPF 中选 PDMS profile，再选“仅预览 EVARS”；旧校验只核对 E3D 与 net48，`EvarsEditor` 只验证 profile 字符串可表示。
- 结果：可明确 apply 出 `set Yuzuha=PDMS` 到 AM 环境，或反向。完整 `deployment.preview` 已拒绝该组合，两个入口行为不一致。
- 修复方向：两个后端入口复用交付 profile 与产品/框架映射，未知 profile 同样拒绝；在生成计划前拒绝，不写环境及状态计划。
- 当前状态：已通知根代理；已看到 `ManagerHostProfiles.Framework` 及 AM/PDMS、E3D/未知版本反例测试源码，未执行测试。

## R3 / P2：可绕过旧所有权移交，写后令旧安装升级/卸载及移交同时被拒绝

- 位置：`src/YuzuhaToolkit.Desktop/PreparationWorkbenchView.xaml.cs` 的 `InstallationChanged`（发现时 84–87 行）直接进入 preview；`PreparationWorkbench.PreviewAsync`；`RuntimeDeploymentService.Preview` 对 Inno 所有权只检查 copy 目标应用根；`EvarsOperations` 没有旧环境所有者检查。
- 触发：旧 v2 安装仍在 `.yuzuha-inno.json.managed` 拥有所选环境 → 不执行 ownership 阶段 → reference/copy 或单独 EVARS 预览并 apply。
- 结果：Manager 改写环境，旧 marker 仍记录旧 `afterSha256`。`installer/SetupBridge/PlanEngine.cs` 的 `CreatePlan`（154–156 行）及 `UninstallPlan`（441–442 行）拒绝漂移；`SetupHandoffService.PreviewLegacy`（69–70 行）也拒绝移交。普通路径只能先恢复原修改，再重新移交。
- 修复方向：后端生成与执行环境写入计划时识别所选环境的旧安装所有权，并给出明确移交出口；不能只依赖用户顺序阅读阶段。至少所选 packageRoot 的旧 marker 必须验证，未知/漂移拒绝。
- 当前状态：已通知根代理；未宣布已修复。

## R4 / P2：部分恢复已恢复 EVARS 后，按同回执再次恢复永远失败

- 位置：`src/YuzuhaToolkit.Manager.Core/EvarsEditor.cs` 的 `Restore`（发现时 128–145 行），`RuntimeDeploymentService.Recover`（发现时 195–219 行）。这是既有编辑器条件被本轮多文件恢复/unknown 动线暴露的问题。
- `Recover` 先恢复 EVARS，再恢复载荷。如果某载荷因外部编辑保留，第一次返回待处理，但 EVARS 已等于 `BeforeHash`。
- 用户处理载荷冲突后再次恢复，`EvarsEditor.Restore` 只接受当前内容等于 `AfterHash`，把已恢复的 `BeforeHash` 误报为外部修改；运行 ownership 标记也因此无法完成恢复。单独 EVARS 替换成功后丢回包亦存在同类问题。
- 修复方向：以精确摘要确认“已恢复”作为成功，不覆盖其他内容；ownership 标记也需能确认已恢复状态。`PluginDeployment.Restore` 264 行已采用相同原则。需真实临时目录的“第一次部分恢复 → 处理冲突 → 原回执再次恢复成功”测试，仅模拟 WPF 成功回包不覆盖此问题。
- 当前状态：已通知根代理；未宣布已修复。

## 限定范围中未发现其他问题

- 新 Inno 交互默认 manager；该请求拒绝环境列表，保留显式 legacy aveva/none；完成页和回执不声称 AVEVA 已接入。安装完成入口仅填草稿，未携带写入授权。
- 旧移交冻结 marker/环境摘要、拒绝未知 owner 和漂移、保留备份及回执；只移除选定所有权，不编辑 EVARS；标记已替换后的错误保留未知结果。
- HTTP 新增只有三个 GET 元数据目录，无 Manager mutation/invoke 路由；docs-only 在注册这些路由和 OpenAPI 之前返回。
- TS 通过无 shell 的参数数组、stdin 输入与显式授权调用真实 CLI；ticks 保持十进制字符串；不自动重试；已启动进程的传输失败保守标为可能执行；结果数据保持 unknown/raw JSON。

以上为源码审查结论，不替代修复后专项测试、真实包构建、人工 UI 验收或真实 AVEVA 运行验收。

## 修复后复查 / resolution

2026-11-01 第二轮仅复查当前源码与测试定义，未执行 build 或测试。四项原问题的触发链均已有对应修复，限定范围内未发现修复引入新的 P1/P2。

| 项目 | 当前源码结论 | 已检查的验收定义与执行状态 |
| --- | --- | --- |
| R1 | `NeedsRecovery` 同时覆盖 `MayHaveExecuted` 与 `RECOVERY_REQUIRED`；`RestoreAsync` 对同一回执绑定恢复身份，成功后只解除该身份的未决状态。历史回执不能清除后来无引用未知的规则仍保留。新增 `UnresolvedResults` 防止只读调用覆盖未知诊断。 | `RecoveryRequiredRestoreKeepsExactReceiptUntilSuccessfulRetry` 覆盖无传输未知标记的冲突→成功；`HistoricalRecoveryRequiredAndSuccessfulRestoreCannotClearLaterUnreferencedUnknown` 保留反例。本轮未重跑。 |
| R2 | `ManagerHostProfiles.Framework` 集中限定 AM/AM、PDMS/PDMS、E3D/已交付 profile；WPF、单独 EVARS 与完整部署复用该映射，错误组合在生成计划前拒绝。 | `WrongProductProfileIsRejectedWithoutWritingOrFreezingPlan` 覆盖 AM↔PDMS、E3D/AM、未交付版本；`SupportedProductProfileCanPreviewApplyAndRestore` 覆盖四种有效组合。本轮未重跑。 |
| R3 | `SetupEnvironmentOwnershipGuard` 核对显式 package/runtime 根与当前 HKCU 64 位已登记根；未知/缺失/重复记录拒绝，精确目标仍归旧环境所有者时返回移交要求。EVARS 计划使用 `EvarsOperationPlan` 保留包上下文，并在 apply 再检查；旧无包装写计划要求重预览，原恢复回执兼容。完整部署预览、apply 的重新预览和实际 EVARS 写前均检查；写前重新归旧所有者时先补偿已复制载荷。WPF 将明确的移交要求变为 ownership 出口，不自动转交。 | `OwnedEnvironmentBlocksBothPreviewsUntilExplicitTransferThenAllowsApplyAndRestore`、`OwnershipReclaimedAfterPreviewBlocksBothApplyWithoutEnvironmentWrites`、`RegisteredOwnershipAndUnknownRecordsCannotBeBypassedByOmittingPackageRoot` 覆盖原绕过、预览后/复制后重新占有及省略参数反例；`LegacyFrozenEditWithoutOwnershipContextRequiresNewPreview` 覆盖兼容性。本轮未重跑，等待负责代理最终执行结果。 |
| R4 | `EvarsEditor.Restore` 先确认当前精确 `BeforeHash`，已恢复时不再写入；需要替换时继续验证 `AfterHash`、备份及暂存摘要。运行标记也接受精确原指纹，其他标记不覆盖，并核验恢复后摘要。部分恢复及丢回包可按同回执完成，同时后续无关修改继续拒绝。 | `PartialRestoreResumesAfterEnvironmentWasAlreadyRestoredAndConfirmsLostReply` 使用真实临时目录覆盖载荷冲突→EVARS先恢复→处理冲突→同回执完成→再次确认→后来改动拒绝；`AlreadyRestoredFingerprintConfirmsLostReplyWithoutReplayingAndStillRejectsLaterEdits` 也覆盖无变化回执之后被改的反例。根代理报告相关 targeted 23/23 通过；本审查没有独立重跑。 |

R3 的查验范围是明确来源和当前用户已登记安装，不声称扫描全机或发现其他用户/未登记且未提供的安装根；未知已知记录保持拒绝，未扩大写入权限。

交付前仍须重新导出契约投影：本次复查时 `docs/contracts/manager-journey-client.ts` 的 `evars.preview` 尚缺新增可选 `packageRoot`。请在最新 Manager CLI build 后重新生成五项 manager-journey 投影，再执行 exporter `--check`、TS 语义检查及相关 CI；这是生成物同步待办，未把它当作后端修复的运行验收。
