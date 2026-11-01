# Ribbon 重启后运行时验收契约

状态：待实机验证；本文件先定义验收，不把磁盘部署、后端重启或宿主重启当作 Ribbon 已加载。映射 platform 7.3 的专项证据；不升级整体 change 状态。当前子任务只读调查与仓库内准备，宿主启停、登录、安装写入由主代理统一操作。

## 事实边界

1. `file-update` apply 成功只说明冻结计划绑定的 UIC 字节已写入并备份；原 XML 必须保持原字节。`registration-add` apply 成功只说明选定 XML 中已有 UIC 的 Name/Path 登记完成；UIC 与未选 XML 必须保持原字节。记录 planId/digest、目标绝对路径、前后 SHA256、备份位置和部署回执。
2. activation 在部署后仍为 `reloadRequired/runtimeUnverified`。只有本次部署后新宿主的实际 Ribbon 可观察证据才可写 `runtimeVerified`；不得回写旧 apply 成功结果冒充当时已经激活。
3. 重启前后记录入口进程及工程模块进程各自 PID、UTC创建时刻、EXE绝对路径、产品/profile/模块。新 PID 不是充分条件，同PID创建时刻变化也需作为新身份；入口Monitor退出不能推断工程退出。多会话必须明确选择，不自动选任意会话。
4. 对重启后同一 PID/starttime 做 CLI 严格 expectedModel 核验，记录原始JSON、退出码、Host版本/协议和管道。Host核验只证明通信身份，不能证明 Ribbon加载；Host不可用与Ribbon外观失败分开记录。
5. 实际 UI 必须观察到本次唯一标记的Tab/Group/Button、32/16图标、Tooltip、尺寸与文字。截图保留产品主窗口、选中菜单及唯一标记，注明观察时间与模块PID关联依据。静态UIC解析/截图、编辑器预览或宿主窗口标题不能独立证明加载。
6. 默认最小Ribbon使用唯一命名空间和明显非业务文案，不绑定模型写入、宏文件或账号。按钮动作仅允许主代理在预览后明确选定的无模型副作用验证命令；未另行确认时只观察、不点击。声明“外观加载通过”不等于“按钮动作通过”。
7. 明确拒绝（计划过期/修订或目标摘要变化/权限不足）为 rejected，不能启动或应用。磁盘部分写入/回执丢失、宿主重启失去确定回执为 unknown，保留原编号及现场，不自动重复apply/重启/点击。启动失败、Host身份失败、Ribbon未出现、加载错误各自记录阶段/code/非空原因，不将未观察到误记为已回滚。
8. 回退只依据本次精确备份与前后指纹；恢复前重验，冲突时停止。恢复磁盘后同样需再次宿主加载核验。不得保存工程模型作为UI验收步骤。

## 有限验收标准

分开记录 deployment、hostIdentity、ribbonAppearance、approvedAction 四项。最低验收包含：正确产品/profile与模块、重启后新进程身份、严格Host身份（若不可用如实单记）、唯一标记Tab/Group及Large/Small按钮可见、图标与Tooltip截图、原始回执和前后摘要、可定位备份与恢复说明。动作未获准则 `notRun`，不阻止仅外观验收记录，但不能称全部7.3通过。其他产品版本不能沿用APS E3D2.1结果。

后续调查记录及主代理可执行步骤追加于下，不在本子任务执行宿主或安装动作。

## 当前本机只读调查

2026-10-31 本工作区机器与历史报告中的 `C:\Users\26648` 机器不同，以下均为本轮重新读取：

| 项目 | 当前绝对路径/结果 |
|---|---|
| E3D2.1入口 | `D:\AVEVA\Everything3D2.10\mon.exe`；APS样例根 `D:\AVEVA\Projects\E3D2.1\AvevaPlantSample\aps000` |
| 初始化与登记 | `D:\AVEVA\Everything3D2.10\launch.init`、`evars.init`、`DesignCustomization.xml` |
| 实装 Phayton 根 | `C:\Users\huang y t\AppData\Local\Programs\Phayton\PmlTrigger.Yuzuha` |
| 已装通信CLI | 上述根下 `runtime\cli\PmlTwin.Phayton.Cli.exe`，文件版本0.3.4.2 |
| 已装Host | 上述根下 `runtime\profiles\E3D2.1\net48\YuzuhaToolkit.PmlHost.Net48.dll`，版本0.3.4.2，SHA256 `7CE0246C55E80DBBEE44A324E1EAD0A9D7308F49709BE1669FB04AE4DABF6706` |
| 引导 | evars.init托管段指向该实装根，Yuzuha=E3D2.1，startup=none；Rpc.pmlobj包含RefreshModel调用。磁盘证据不等于已加载证据 |
| 会话 | 已装CLI `aveva.sessions.list` 成功，Count=0；没有目标，未运行session.status，不能宣称Host已验证 |
| Ribbon后端 | `F:\OneDrive\Documents\ChatGPT\PmlTwin\integrations\ribbon\source\apps\backend\bin\RibbonStudio.Backend.exe` 已存在；默认127.0.0.1:4318本轮validate调用返回RIBBON_UNAVAILABLE，未启动后端 |
| 管理CLI | `F:\OneDrive\Documents\ChatGPT\PmlTwin\src\YuzuhaToolkit.Manager.Cli\bin\Debug\net10.0\Yuzuha.Manager.Cli.exe`，v2四操作已注册 |

DesignCustomization.xml 本轮解析19条，均为原生模块/功能登记，无可安全覆盖的已知自定义Ribbon。不能将 `Project/$1.uic` 之类动态原生入口作为测试替换目标。采用工作区独立UIC、导入外部文档，再v2更新及仅Design登记最清楚。用户后续明确本次MDB=`/ALL`；此前静态样例文本调查未能推导MDB，不把数据库二进制作为文本读取，也未读凭据。

`op describe aveva.session.status`：input={}，requiresPid=true；requiresExecution/Write/Launch均false。每次调用必须显式 `--pid <本次PID> --expected-model DESIGN`，独立比较返回Host的PID/ProcessStartTimeUtcTicks与操作系统捕获身份。旧会话和仅文件版本都不能替代核验。

v2 file-preview精确input={project}；registration-preview={project,monPath,manifestName,entryName}；两种apply仅={planId,planDigest}并要求`--allow-write`。v2不接受apply重新传project或路径。schema原始回执保存在 `artifacts/continuation/ribbon.v2.deployment.*-schema.json`。导入要求write授权，因为写入Ribbon工作区历史，虽然不写回源UIC。

## 主代理执行清单（本子任务未执行）

1. 先启动隔离Ribbon后端，指定独立`UIC_DATA_DIR`和`UIC_PORT`；管理CLI使用同端点`YUZUHA_RIBBON_ENDPOINT`，避免混入旧工作区。通信CLI的日志使用隔离`YUZUHA_STATE_DIR`。后端不需要发布到AVEVA安装；本次已装Host磁盘版本具备核验候选条件，先实际验证再决定是否需更新，禁止为测试先盲目替换DLL。
2. 最小输入位于 `F:\OneDrive\Documents\ChatGPT\PmlTwin\openspec\changes\integrate-pmltwin-product-platform\features\fixtures\ribbon-runtime-project.json`：唯一namespace=PmlTwinRuntime20261031；Tab“Phayton 实机核验 1031”，Group“本次加载证据”，Large/Small各一个，使用既有示例PNG；Macro均为`q var !!PhaytonSession`，默认不点击。项目SHA256=`169DA94CE07D59AB78D81A958287A94D453EDEDC9CF0E40E4E474D14B6D07800`。不把存在该只读命令文本等同于动作已获准。
3. 先调用`ribbon.projects.validate`，须业务valid=true；再`ribbon.uic.export`。管理适配器非JSON响应为`data.bytesBase64`，解码写工作区独立`.uic`（例如`artifacts/ribbon-ui-live/PmlTwinRuntime20261031.uic`），不能写安装目录。通过`ribbon.uic.import`输入{path:该绝对路径}并显式write，仅建立导入历史；后续所有project使用服务返回的documentId/origin，不继续使用初始假定身份。
4. 对导入项目做v2 file-preview/apply，审阅冻结的source/target摘要与备份。再registration-preview，目标monPath=`D:\AVEVA\Everything3D2.10\mon.exe`、manifestName按`ribbon.deployment.modules`本次返回的精确值选择Design，entryName=`PmlTwinRuntime20261031`；apply仅用计划编号与摘要。manifestName是后端标识，不能未经modules结果直接猜绝对XML路径或带扩展名。apply后比较UIC、Design XML及未选XML摘要；保留两个apply原始回执与全部备份。
5. 主代理按本次登录授权启动/重启APS DESIGN；新工程PID/创建时刻出现后先session discovery再严格status。不要在本子任务或脚本内读取密码、沿用历史PID、自动切换目标或降级expectedModel约束。
6. 在新宿主打开唯一Tab，截图Large/Small、图标、Tooltip。保存操作系统PID/starttime/EXE证据、Host原始回执、截图与具体观察时间；任何动作点击须另在主代理确认范围内，只读查询也不是默认执行。UI失败检查只读加载日志，不通过任意PML“补加载”掩盖启动加载缺陷。
7. 如需结束验收由主代理正常退出，不保存模型；回退前先核对当前文件仍匹配本次after摘要。恢复计划/动作与恢复后加载记录另存，不把复制备份文件当已恢复激活。

## 本子任务验证边界

只执行describe、无连接的sessions.list、版本/摘要/登记读取和仓库fixture生成；未启动后端或AVEVA、未停止宿主、未导入、未部署、未读密码、未执行PML。最小项目的实际服务validate目前未完成：默认后端未运行，结果为`RIBBON_UNAVAILABLE`且mayHaveExecuted=false；不能称fixture已通过服务校验。未占用dotnet构建窗口。

原始证据位于 `F:\OneDrive\Documents\ChatGPT\PmlTwin\artifacts\continuation\`：`ribbon-installed-host.json`、`ribbon-installed-status-schema.json`、`ribbon-installed-sessions-before.json`、各操作schema、`ribbon-runtime-fixture-validation.json`。本文件契约和步骤不升级任务完成状态；实际结果由主代理追加。

## 2026-10-31 后续实际验收结果

主代理已按本契约执行E3D2.1 APS/ALL/DESIGN实机部署及正常重启。新模块PID127436/UTC ticks639290578642959920严格Host TargetVerified=true，实际宿主显示“Phayton 重启核验 1031”页签、分组、Large/Small图标及各自Tooltip；实际UIC PNG头核验32×32/16×16。未点击Macro。验收后正常退出宿主并恢复唯一Design登记，七清单SHA256均等于before，本次后端已停止。完整层次、计划/备份/身份/截图与限制见 `docs/validation/ribbon-restart-2026-10-31.md` 及其 `.evidence` 目录。此结果只覆盖该明确环境和外观加载，不推定动作、其他产品或恢复后加载通过，不更改旧apply回执中的runtimeVerified=false。
