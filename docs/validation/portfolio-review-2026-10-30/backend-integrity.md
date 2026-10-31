# 部署、知识与可选插件独立验收

2026-10-30。结论：可作为**后端工程能力与离线验证作品**公开展示；目前不能按“真实 AVEVA 已验收、完整安装产品可交付”展示。本次确认的两项缺陷已修复并补充回归；真实产品矩阵、完整包与崩溃恢复验收仍缺证据。

## 范围及证据边界

- 检查 Manager.Core 的 EVARS、RuntimeDeployment、Copy、KnowledgeImport、PluginRegistry，以及独立 License/VM worker。
- 读取 tasks.md 和 implementation-verification.md 后独立对照代码、测试、TRX、包清单和合成运行结果。未发现本范围适用的 AGENTS.md；installer/AGENTS.md 不属于本次代码修改范围。
- 不启动真实 AVEVA/PML/VM/NativeVixSession，不修改安装目录，不读取真实凭据，不读取或输出产品知识正文。
- 后续按主审授权，仅修复 PluginDeployment.cs、KnowledgeLibraries.cs，新增 BackendAuditRegressionTests.cs；VM 执行语义保持原样。
- 初次沙箱运行因临时文件 rename 被拒绝，不计为业务验收。随后获自动审批，在系统临时目录编译纯合成 harness，注入假 IVmwareSession；未加载假 vix.dll。

## 独立核对结果

| 项目 | 本次确认的证据 | 限制 |
| --- | --- | --- |
| 历史离线矩阵 | 三份 TRX 的 Completed/Counters 为 142+221+21=384 全过，0 aborted/0 notExecuted | 此为此前产物，不能替代实机验收 |
| CLI 示例 | cli-examples.json 有 20 项，包含写授权拒绝、EVARS/知识/复制闭环 | 本次读取结果，未重跑示例 |
| 安装器 | 实施报告写明 9 组；Test-Manager.ps1 调用隔离 SetupTests.exe | 本次未独立重跑 9 组，不计为本次通过项 |
| 基础包 | 40 个已声明文件 SHA256 全匹配，52 操作 | 清单 scope 明确为 manager CLI only |
| 许可/VM 包 | 两个包全部声明文件 SHA256 匹配；分别 3/6 操作 | 只有工作包，不含厂商 SDK/镜像或许可 checkout |
| 既有 E3D 2.1 库 | 登记与数据库 SHA 匹配；报告为 11,392 源/50,620 chunk/5 抽样，0 error | 本次仅核对元数据、报告及 DB SHA；未重新扫描产品源 |
| 既有 AM 12.1.SP4 库 | 登记与数据库 SHA 匹配；报告为 12,627 源/51,300 chunk/5 抽样，0 error | 报告中的 current 是原检查时状态，不能称本次重新验证新鲜 |

冻结计划不是只保存路径：ManagerPlanStore 核对 envelope 类型/ID/SHA，RuntimeDeployment.Apply 重建完整预览比较，安装入口身份、环境字节、载荷与目标散列发生变化会拒绝。EVARS 保存编码/BOM/非托管正文，末次检查后以 File.Replace 发布，恢复核对目标与备份散列。复制与运行部署分别保留失败阶段、备份和未恢复项，外部目标修改不应被恢复覆盖。

知识按 Libraries/product/version/name 发布。导入已有 DB 先拒绝 WAL/SHM/journal 与写占用，再用 SQLite Backup 取得一致快照；私有暂存通过后 Directory.Move 同卷发布。已有同名目标拒绝覆盖；personal/project/experience 名称受保护。Inspector 同时检查 schema、关系、FTS 行/正文一致性、FTS integrity-check、MATCH 与抽样。source_hash_algorithm=sha256-bytes-v1 才声称内容指纹；legacy 和默认检索 not-rechecked 均明确降级。

插件元数据读取不启动 worker；安装包全部文件须声明并通过散列核验，调用时持有只读文件租约，核对实际 capability、schema 和授权，再启动单次独立进程。授权来自可信 OperationContext，业务 JSON 不能提权。此为进程与依赖隔离，不能当作不可信插件的 OS 沙箱或签名信任机制。

许可代码仅 TCP connect 和只读 SCM 查询，diagnose 固定 checkoutState=unverified；requireCheckout=true 明确拒绝。VM 发现/计划/模拟不加载 SDK；真实执行入口需要三项授权、确认指纹与持久化 intent，unknown 不自动重发。未发现把端口/模拟/进程创建错误升级为实际 checkout 或 Host 成功的代码路径。

## 已证实并修复的缺陷

### [P1，已修复] 自动失败回滚可把损坏备份写回并误报完整恢复

- 位置：[PluginDeployment.cs:218](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Workspace/PluginDeployment.cs:218)。修复前异常分支直接 File.Copy(backup, destination, true)，与显式 Restore 的备份验证不一致。
- 复现条件：合成源为 new、目标为 original；复制完成后在 receipt 阶段将本次备份改为 CORRUPT 并抛 IOException。故障注入模拟备份损坏/外部改动，未证明生产环境曾发生损坏。
- 修复前实测：actual=CORRUPT，RecoveryErrors=[]，异常却写“已恢复本次复制的修改”。影响是在真正失败时丢失原目标且给调用方错误恢复状态。
- 修复：备份必须匹配冻结 TargetHash；先复制到独立恢复暂存，再核对暂存散列及当前目标，最后原子移动。备份损坏时保留已应用目标与备份现场，返回非空 RecoveryErrors 和“恢复需人工处理”。
- 回归：FailedCopyNeverRestoresAnAlteredBackupOrReportsFullRecovery 断言目标仍为 new package、损坏备份保留且不报完整恢复。后续建议对磁盘损坏、进程中断与恢复日志落盘再做独立故障矩阵。

### [P2，已修复] 其他产品/版本的坏登记阻断明确选定库

- 位置：[KnowledgeLibraries.cs:31](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Knowledge.Core/KnowledgeLibraries.cs:31)；[KnowledgeLibraries.cs:39](C:/Users/26648/Documents/ChatGPT/Example/PmlTrigger.Yuzuha/src/YuzuhaToolkit.Knowledge.Core/KnowledgeLibraries.cs:39)。原实现先反序列化/验证全树登记，随后才按 product/version 过滤。
- 修复前实测：仅在 AM/12.1.SP4/broken/registration.json 写坏 JSON，调用 List(root, E3D, 2.1) 即抛 JsonException。该入口被专属列表/检索共用；其他版本损坏会让健康版本不可用。
- 修复：在枚举产品与版本目录时先过滤，只读取选定范围的登记。同范围 JSON 损坏仍明确拒绝，InvalidDataException 包含具体登记路径；全量列表仍报告遇到的损坏。
- 回归：参数化覆盖 AM/12.1.SP4 与 E3D/3.1.6 的坏登记；健康 E3D/2.1 可正常列出。另验证选定范围自身损坏明确失败并携带路径。

## 重复执行与未证实风险

- VM 合成 harness：假会话创建快照并更新 VMSD 后首次 completed，重复 execute 同 attempt 拒绝“同名快照已存在”；open 计数仍为 1，result.json 保留。docs/reference/manager-plugins.md:32 已明确规定元数据变化后用 vm.task.read 回收，这是既定边界，本轮没有修改。
- vm.task.read 的实际输入为 outputDirectory+attemptId（VmPlugin.cs:25、57）；文档第 32 行提 resultFile 的措辞不够清楚，建议补一条实际调用示例，避免恢复操作歧义。
- 现有测试覆盖预览后变化、异常、占用和条件恢复；不能由此推断跨文件事务在断电/强杀时完全原子。EVARS、多个载荷和回执分步提交，需另验中断定位及人工恢复入口。本次未复现断电或强杀，列为验收缺口，不冒充已证实缺陷。
- 文件散列检查与最终替换之间仍有短窗口；本次没有复现该窗口的并发写损失，不能声称已有完整并发隔离证明。

## 本次修复验证与交付限制

- 新增 4 个合成回归用例；首轮定向 4/4 通过。Manager.Tests 全集修复后 146/146 通过、0 跳过，含显式 D:/VirtualMachines/LaunchDesk/Tools/7zr.exe 的合成包矩阵；最终结果见 artifacts/manager/tests/backend-integrity-after.trx。
- 测试未运行真实许可签出、VMware/VIX、AM/E3D 进程或 PML。既有 384 数字与本次 146 不相加，Manager 的 142 已被 146 替代。
- 先前 manager/plugins 包的 SHA 匹配说明包自身完整；这些旧包未在本次修复后重新发布，不能当作已包含修复的交付包。
- tasks.md 仍有 3.6 完整发布清单/安装器、5.7 双入口报告、7.5 插件入口文档、8.2 实机产品矩阵、8.4 本机具体更新/回退预览、8.5 人工合同审阅等未完成项；4.6 全局错误展示也未勾选。
- 产品验收至少分别记录 AM/NET35、E3D2.1/NET48、E3D3.1.6/NET48 的产品版本、PID、Host、时间、连接及失败恢复；不得由其他产品或合成 Host 推定通过。
- 可公开内容应使用自建合成资料、架构/事务图、脱敏测试摘要及上述诚实边界；真实产品原文、索引正文、凭据与本机状态不随作品集发布。

**交付判断：后端实现可供代码评审与离线演示；完整生产程序交付尚不成立。** 修复后的源代码完成本次两项纠偏，后续需重新构建可追溯包，并补齐实机、完整安装和中断恢复证据后再提升交付声明。
