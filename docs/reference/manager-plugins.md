# 可选管理器插件

基础管理器只引用中立的 Plugins.Contracts，不引用许可或 VMware 工作包。没有插件时，部署、启动、连接与知识操作保持可用。插件清单和实际文件不自动扫描到启用状态，必须显式安装/启用；元数据读取不运行插件。

## 包与登记

`scripts/Build-ManagerPlugins.ps1 -Plugin license,vm` 生成各自独立、框架依赖的 .NET 10 包和 manager-plugin.json，不执行诊断、不安装到活动管理器，也不下载厂商组件。每个包固定 Id/Version/ContractVersion、EXE/DLL 相对入口、全部文件 SHA256 及操作 schema/授权标记。拒绝未声明文件、重复路径、越界/链接和不支持的 schema。包版本和管理协议契约分别核对。

管理 CLI 操作：

1. `plugin.install.preview` 输入 source（包目录），可选 enabled；默认保持停用。返回冻结来源、目标和登记修订。
2. `plugin.install.apply --allow-write` 使用 planId/sha256，把检查后的包复制到状态目录的不可变内容槽，然后最后提交登记。原包保留。
3. `plugin.list` 返回 revision 与每项 ready/disabled/changed/incompatible/unavailable。`plugin.describe` 输入 id 读取清单与状态，不启动进程。
4. `plugin.enable --allow-write` 输入 id、enabled、expectedRevision；过期编辑拒绝，未知顶层字段保留。
5. `plugin.invoke` 输入 pluginId、operation、input。内层 schema 以及 `--allow-write`/`--allow-launch`/`--allow-execution` 来自声明和可信 CLI 上下文，不能从业务 JSON 提权。
6. `plugin.unregister --allow-write` 输入 id、expectedRevision，只取消活动登记；保留包缓存、原源和用户证据，基础功能不依赖它。

调用前再次固定包文件，先启动一次无副作用的 capability 握手，核对实际插件标识、版本、操作与授权，再为所选操作创建单次工作进程。请求/输出有界且关联号匹配，stdout JSON 和 stderr 分开，不使用 shell。进程失败、超时、输出/协议错误不会变成成功；已发出的写入/启动丢失回执保留 mayHaveExecuted，不重发。

## 许可包

许可包 Id 为 license：license.tcp.test 输入 host/port，可选 timeoutMilliseconds；license.service.test 输入 serviceName；license.diagnose 输入 host/rmsPort，可选 rulesPort/serviceName/timeoutMilliseconds。只做 TCP connect 与 Windows 服务只读查询，不发送签出数据、不启动/停止服务。

诊断分别返回 reachable/unreachable/timeout、running/stopped/missing/unavailable 等证据。综合结果的 checkoutState 始终 unverified，端口可达和服务运行不能表示许可已经可用。基础启动不依赖许可插件；配置 licensePrecheck.mode=required 后，调用 license.diagnose 并要求 RMS、所选 Rules 端口 reachable、所选本机服务 running。插件未安装/停用/损坏、规则失败均在创建入口进程前拒绝。requireCheckout=true 当前明确返回 LICENSE_CHECKOUT_UNVERIFIED，不能降级为端口检测。其他应用凭据不会被导入。

## VM 包

Id 为 vm；基础包不包含 VIX、VMware 程序或 VM 镜像。`vm.discover` 只读明确 roots 中的 VMX/VMSD；`vm.plan` 只读并输出冻结计划，不创建输出目录、不加载 VIX。请求包含 allowedVmRoot、vmPath、expectedUuid、expectedMac、profile、outputDirectory、steps、vixLibraryPath，可选 credentialFile/timeoutSeconds。支持 profile 为 AM、PDMS、E3D2.1、E3D3.1.6。所有启用网卡要求 Host-only，VM 必须位于指定允许根下，证据目录须位于该根之外。

steps 只接受 probe/start/stop/createSnapshot/revertSnapshot，快照动作必须有明确 snapshot 名称。计划冻结 VMX/VMSD、VIX 文件、可选保护凭据的 SHA256，以及产品、输出目录和步骤；重名或缺失快照、身份或指纹改变均拒绝。VIX 互操作声明核对 [VMware 官方 VIX 头文件](https://github.com/vmware/open-vm-tools/blob/master/open-vm-tools/lib/include/vix.h)；还原使用 SUPPRESS_SNAPSHOT_POWERON，启动必须是独立显式步骤。只通过明确 vix.dll 路径加载，不自动搜索或下载 SDK。

`vm.execute` 输入 plan、confirmation（原计划指纹）和新的 GUID N 格式 attemptId，需要三个可信标记 `--allow-write --allow-launch --allow-execution`。先落盘 plan.json 和运行意图，再连接 SDK。每步复查 UUID/MAC/Host-only，保存 result.json；超时或发出动作后失去确定回执为 unknown，不自动重跑或恢复快照。同一 attemptId 只允许回收已有证据；当前 VM/快照指纹已变化时仍会拒绝执行请求，应改用只读 `vm.task.read`。执行超时为 1–90 秒；通过 plugin.invoke 指定 workerTimeoutSeconds，并把外层 CLI --timeout-seconds 设置得更长，保留回收时间。

`vm.task.read` 必填 `outputDirectory` 和 GUID N 格式的 `attemptId`，从 `<outputDirectory>/<attemptId>/result.json` 读取有界结果，不接受 `resultFile` 参数。结果关联必须匹配，超过 1 MiB 则拒绝；中断后记录的 running 按 unknown 展示。它不重新核验或执行 VM 动作，不能把读到的旧结果当作当前 VM 状态。通过 `plugin.invoke` 调用时，业务输入例如 `{"pluginId":"vm","operation":"vm.task.read","input":{"outputDirectory":"C:\\Evidence\\VmTasks","attemptId":"<原任务的32位GUID>"}}`。

`vm.simulate` 输入 profile、scenario（ok/no-session/pipe-missing/protocol-old/identity-reused/module-changed），返回 mode=simulation、realConnectionVerified=false、checkoutState=unverified。模拟结果不能点亮真实连接成功灯。`vm.credential.store` 只保存本插件拥有、绑定 VM 路径/UUID/MAC 的 CurrentUser DPAPI 文件；密码只用 stdin/本机输入文件，禁止命令行内联。不会读取 LaunchDesk、VMware 或其他应用的凭据。

## 验证与待交付

真实夹具进程覆盖安装默认停用、启停、取消登记、未知字段和修订冲突、包/依赖变更、实际版本握手失败、声明外操作、授权不足及提交后丢失回执。许可测试只连接临时本机回环端口，另用独立进程读取一个不存在的合成服务名，不构成真实许可签出验收。实际许可/VM 工作包也经临时登记调用诊断/模拟，验证包握手与执行链。

VM 有状态测试使用合成 VMX/VMSD/假 DLL 和注入适配器，验证确认绑定、身份/网络/快照/输出变化、三项授权、超时 unknown 和相同 attemptId 不重发。本机真实 VM/VIX 尚未执行；GUI 入口继续见 OpenSpec 未完成项。
