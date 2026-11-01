# Ribbon UI → v2 接线契约

状态：实施细化；续作契约第 2 节及 ribbon-v2-continuation.md 的 UI 消费方，不修改后端或真实加载验收。

## 实际入口与输入

Web main.ts 保存当前草稿后进入 deployment.ts；已有文件走 file-update，module-transfer.ts 处理选定 XML。WPF 实际工作区为 StudioToolPage，DeploymentWindow 保留可调用/验证入口，两者已有文件及登记都接 v2。安装发现、扫描、工作区保存仍用既有只读/编辑 v1。新建文件首次生成不是当前 v2 支持能力，保留明确标注的 v1 首次部署入口，不将其称作冻结部署。

- 文件预览：POST /api/v2/deployment/file-preview `{project}`。
- 登记预览：POST /api/v2/deployment/registration-preview `{project,monPath,manifestName,entryName}`。
- apply：只 POST `/api/v2/deployment/{file|registration}-apply` `{planId,planDigest}`；不传项目、路径、名称或旧哈希，不在确认后自动重新预览替代用户批准。
- 展示服务端 targetPlan 的真实路径、变更、备份说明，另显示冻结编号、源版本与有效期。UI 保留项目快照，编辑/选择变化使预览不可执行；后端源版本/目标重验仍为权威。

## 多项登记

同文档任一登记会推进历史版本，因此不能一次预览多个 XML 后盲目连续应用。Web/旧 WPF 对话框每次预览并确认一个选定模块，其余选择保留待下一次显式预览；StudioToolPage 对每项依次预览并弹出独立确认。共享 XML 的多文档登记同样逐项确认，不隐式刷新已确认计划。UI 说明本次项数及剩余选择。

## 状态与不确定结果

状态为 idle → previewing → ready → applying → deployed/unchanged/rejected/unknown。执行开始即消费 UI 计划引用、禁用按钮；正在执行时不关闭/重复点击。HTTP 已知拒绝显示原状态/原因并需重新预览。HTTP200 仅在 contractVersion=2、计划编号/摘要一致、state 有效、activation.runtimeVerified=false 且 reloadRequired 为布尔时确认结果。

发送写入前先持久记录非秘密 `{planId,planDigest,kind,endpoint}`，记录失败拒发请求；成功确认才清除，只有 v2 的明确前置拒绝（HTTP4xx，408 除外）可清除后重新预览。v1 旧引擎可能在拒绝前已保存历史，任何发送后的错误都保守保持 unknown。Web 使用 localStorage，跨工具页、标签和刷新保留；WPF 使用 LocalApplicationData/RibbonStudio/pending-deployment.json（测试可设置 UIC_UI_STATE_DIR），CreateNew + Flush(true) 后才发送，跨进程重启保留。启动发现 pending 即按 unknown 暂停，即使上一进程尚未来得及捕获断连。

Web 使用同源 navigator.locks 独占锁覆盖“重读暂停状态 → 持久占用 → 整个写请求 → 处理回执”，第二页面不得覆盖原 pending；浏览器不支持该锁时拒发写入。WPF 独立 pending-deployment.json.lock 文件以 FileShare.None 持有整个写请求，跨进程互斥。人工核对解除操作也取得同一锁，请求仍在执行时拒绝解除。锁内优先重读持久编号并与对话框展示的 expectedId 比较，本地旧 unknownA 不能遮蔽或删除新持久 unknownB；getter 同样优先持久编号。

断连、超时、HTTP500、成功响应损坏、身份不符或 state=unknown：显示“结果不确定”，保留原计划编号，停止批次，不自动重试或重新预览。关闭重开不能解锁。只有显式“已人工核对文件与历史，解除写入暂停”并二次确认后才清除 pending，旧计划仍已消费，必须新预览；该确认不代表实际加载或 runtimeVerified。首次部署 v1 兼容入口也先记录本地关联编号并采用同样暂停机制，但不冒称后端已有冻结计划。离开页前可复制编号/信息。

deployed 表示磁盘回读通过；展示“已部署，需重新进入对应模块或重启 AVEVA 后检查；实际加载未核验”。unchanged 仍显示“磁盘内容相同；实际加载未核验”。不得因为后端重启/界面刷新/用户重启进程改成 runtimeVerified=true；不提供自称已验证的按钮。不会自动启动/重启或点击宏。

## 验收

验证公共 Web v2 客户端实际请求体仅引用、未知/错身份回执及网络失败不重发；测试模块流程每次仅一计划，失败停止且保留编号。WPF 使用同一薄 v2 客户端及独立模型，编译并做临时安装 UI 烟测（至少预览路径与字段/状态），保留原桌面布局重启验收。完整 Ribbon 离线回归、Web build/typecheck、Desktop build 通过。真实 AVEVA 重启加载只由主代理依具体安装预览及用户授权执行，记录产品/版本/模块、UIC/XML 哈希、会话身份及加载/视觉证据；本切片不假造该结论。

## 最终离线验收与边界 · 2026-10-31

- 完整 `node --import tsx --test tests/*.test.ts`：53/53 通过，0 skipped，退出码 0，由主代理运行。
- 公共 Web/桌面客户端专项：9/9 通过，覆盖实际请求仅计划引用、原计划消费一次、写前持久化、无可靠存储/锁时拒发、跨进程/标签保留、并发写与执行期 ack 拒绝、旧 A/新 B 身份拒绝，以及 v1 发送后冲突保守 unknown。
- `npm run build`：退出码 0，包含 net48 后端编译、TypeScript 检查及 Vite Web 构建；Desktop csc 构建通过。
- Chromium 实际 DOM：文件更新真实落盘；两份清单逐项预览确认；apply 请求只含 planId/planDigest；丢失成功回执后刷新、新标签与发送后关闭页面仍保留原编号；另一标签提交及执行期人工解除均未增加写请求。截图状态明确“实际 Ribbon 加载未核验”。
- WPF 原生 smoke：StudioToolPage 主工具实际执行 ReviewDeployment，显示 v2 file-update 冻结计划确认框，截图后自动取消，**未在这条原生 UI 用例调用 Apply**。保留 DeploymentWindow 的 v2 registration-add 预览同时验证。桌面 Apply 的真实 HTTP 请求及跨进程 pending/锁由独立客户端测试覆盖。原有 seed/restore/docked 三阶段工作区进程重启恢复均通过；这不等于 AVEVA 重启加载。

精选证据及 SHA256 清单见仓库 [ribbon-ui-2026-10-31.evidence](../../../../docs/validation/ribbon-ui-2026-10-31.evidence/commands.json) 和 [证据哈希](../../../../docs/validation/ribbon-ui-2026-10-31.evidence/sha256.json)。均来自自有临时安装/文档，不含真实工程秘密。原始临时日志保留在 artifacts/continuation/ribbon-ui，包含首次浏览器并发测试只接受较晚“另一页面”提示、但实际更早按持久 pending 显示“结果不确定”的失败断言；修正断言后最终场景通过，未改变运行代码以适应测试。

首次生成文件仍是明确标识的 v1 兼容入口，采用 UI pending/unknown 保护，但不称为后端 v2 冻结部署。真实 AVEVA 加载与重启证据由主代理单独记录；本 UI 的 runtimeVerified 始终不自行升级。
