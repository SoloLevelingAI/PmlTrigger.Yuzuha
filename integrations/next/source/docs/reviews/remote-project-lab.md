---
document_kind: design
implementation_status: proposed
reviewed_on: 2026-10-29
uid: remote-project-lab
---
# VMware远程Project：配置与分层验收手册

2026-10-28。**已确定使用VMware Host-only + Windows SMB + 专用测试账户。** 本页为配置与验收手册；[实际执行记录](xref:remote-project-lab-results)中G0/G1已通过，10/10项真实SMB检查通过，配置前和Ready两份快照保留，原VMX保护例外如实记录。实际复用VMnet1的192.168.244.0/24，下面56网段仍只是新环境示例。未安装依赖或实现WPF。功能调用见[八节方案](xref:remote-portable-review)，步骤本身不代表产品验收通过。

<a id="scope"></a>

当前克隆的Windows主机名已按许可恢复为ZHANGSH，实际账户前缀使用`ZHANGSH\`；VMware显示名仍为LD-PROJ-VM。恢复前新增UID3快照，许可签出已通过、完整应用仍报通信文件错误，见[最新复测](licence-checkout.md)。下表仍是新环境示例。

## ① 目标与边界：先证明远程目录能正确检查

开发机运行客户端，一台Windows VM提供SMB共享，使用假APS目录和普通marker文件。Host-only是宿主机与来宾机共用的私有网络；VMware Shared Folders不属于本次SMB验证。[Broadcom网络说明](https://knowledge.broadcom.com/external/article/339371/using-the-virtual-network-editor-in-vmwa.html)

先验收网络、账户和文件权限，再验收未来协调器、连接、授权、导入和失败处理。**现有产品尚未实现后半段，不能从资源管理器访问成功推导LaunchDesk新功能已通过。** 假目录也不能证明真实AVEVA数据库、许可、锁或登录正确。

以下按Windows 11 Pro/Enterprise来宾机描述，记录实际Windows和VMware版本；菜单翻译可能不同。已有VM即可，不要求新装系统。需要管理权限的设置只由环境操作者实施，本次仅写步骤。

<a id="topology"></a>

## ② 固定实验参数：先登记，再配置

| 参数 | 本手册示例 | 实施前需确认 |
|---|---|---|
| LabRunId | ld-proj-日期-序号 | 每轮唯一，证据不含密码 |
| Host-only网络 | 空闲VMnet，例如VMnet2 | 不改其他VM正在用的VMnet；记录原配置 |
| 子网/掩码 | 192.168.56.0 / 255.255.255.0 | 与物理网、VPN、WSL及已有VMnet无冲突；冲突则整组替换 |
| 宿主机Host-only IPv4 | 192.168.56.1 | 只改该虚拟适配器，不改Wi-Fi/以太网 |
| 来宾机IPv4 | 192.168.56.10 | 静态地址，不与宿主机或其他VM重复 |
| 网关/DNS | 本实验适配器留空 | 本轮不需要外网或域认证 |
| 来宾机名 | LD-PROJ-VM（新环境示例） | 克隆先核对许可绑定，不要求一律改名；账户前缀使用实际机器名 |
| 本地共享根 | C:\LaunchDeskLab\AVEVAProjects | 必须在NTFS卷的专用新目录；不共享C盘根 |
| 共享名 | AVEVAProjects | 不使用管理共享C$，不自动重连 |
| 首轮服务器标识 | 192.168.56.10 | 首轮用固定IP排除名称解析；同一轮不混用名称/IP |
| 工程路径 | \\192.168.56.10\AVEVAProjects\E3D2.1\AvevaPlantSample\APS000 | init夹具和Profile一致；之后换名称也需重新核对注册/授权 |
| 测试账户 | LD-PROJ-VM\ld_reader、LD-PROJ-VM\ld_denied | 非管理员；各用独立非空密码，密码不写本文或截图 |

图稿中的LD-PROJ-VM是逻辑主机名。名称解析是额外检查项：名称确实解析到192.168.56.10后可从新一轮改用名称路径。没有DNS时先保留IP路径，不为跑通自动改宿主机hosts。遇到1219时不换名称/IP绕过。工作组/IP连接可能受组织NTLM策略限制，应记录实际策略结果，不自动放宽。[微软SMB故障说明](https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/access-denied-access-smb-file-share)

<a id="network"></a>

## ③ VMware网络：只连接指定Host-only网络

1. **保存原状。** 记录VM目录、已有网卡/VMnet和宿主机路由/VPN情况。若需完整克隆现有VM，先将其VMX原文私有备份并计算哈希，因为VMware克隆也可能写回原配置；不将加密元数据放进Git，不从猜测文本重建配置。关闭测试VM，在VM → Snapshot中建立Clean快照；不要恢复其他VM或删除既有快照。如果当前版本无快照功能，关机后按既有备份流程保存该测试VM，不能假定一定支持。
2. **配置虚拟交换网络。** 在VMware Workstation的Edit → Virtual Network Editor，必要时由操作者点击Change Settings。选择空闲VMnet，类型选Host-only，勾选Connect a host virtual adapter to this network。使用②的子网/掩码；本专用网络关闭VMware DHCP以便固定地址，不改其他VMnet DHCP。Apply后记录该VMnet名称。
3. **配置VM网卡。** VM → Settings → Network Adapter，选择Custom: Specific virtual network并指向刚才的Host-only VMnet；如果界面仅有Host-only选项，核对它实际指向哪个VMnet，不能靠名称猜。勾选Connected和Connect at power on；本轮断开测试VM的其他NAT/桥接网卡，不动别的VM。
4. **配置宿主机虚拟适配器。** 打开ncpa.cpl，选对应VMware Network Adapter，IPv4属性设192.168.56.1/24，网关和DNS留空。保留宿主机其他网卡、默认路由、防火墙及Internet Connection Sharing原状；不建立网桥或转发。
5. **先核对身份绑定，再配置来宾机。** 克隆先用厂商工具只读核对主机名、MAC、UUID等是否参与许可，记录当前指纹与许可选择器；不能为了区分克隆就直接改名。新建空白环境可用LD-PROJ-VM；已绑定环境的身份变更须另行评审。来宾机ncpa.cpl选唯一测试网卡，IPv4设192.168.56.10/24，网关/DNS留空；IP若参与绑定也先评审。核对地址和路由。当前克隆改名造成许可失配的实际教训见[根因记录](licence-checkout.md)，本次未恢复身份。
6. **验收网络前置。** 记录宿主机和来宾机适配器截图。若已有只读诊断工具，可查看路由/地址确认无冲突；ping可作辅助，ICMP被拦不能直接判SMB失败。最终以⑤的TCP445/UNC访问验证为准。此时仍未对外提供工程共享。

Host-only及虚拟适配器设置依据[Broadcom编辑器说明](https://knowledge.broadcom.com/external/article/339371/using-the-virtual-network-editor-in-vmwa.html)。不要为了省事选择Restore Defaults，这会影响其他虚拟网络。VM快照只覆盖VM状态，不覆盖宿主机VMnet、SMB会话和凭据；这些另行登记与清理。

<a id="permissions"></a>

## ④ 账户、NTFS与共享：按两层权限建立可比较样本

**A. 创建账户。** 在来宾机用独立管理员打开计算机管理 → 本地用户和组 → 用户，创建ld_reader和ld_denied，各设置独立非空测试密码；测试时不要求首次网络登录改密码。二者仅保留普通Users成员身份，不加入Administrators。记录账户是否启用、有效期和锁定策略，不降低系统锁定策略；坏密码场景一次测试即可，避免反复触发锁定。若版本没有本地用户管理控制台，使用设置 → 账户 → 其他用户创建本地标准账户，并记录界面差异。保持密码保护共享开启，不启用Guest/匿名。

**B. 创建假目录。** 由来宾机管理员在专用新根下创建以下结构；marker.txt内容为LabRunId和“假工程夹具”，不放实际AVEVA数据库或生产配置。

```text
C:\LaunchDeskLab\AVEVAProjects\
  E3D2.1\AvevaPlantSample\APS000\marker.txt
  Denied\marker.txt
```

**C. 设置NTFS权限。** 对AVEVAProjects → 属性 → 安全 → 高级，先记录原ACL。在这个新建实验根上禁用继承并将继承项转换为显式权限；保留SYSTEM和Administrators完全控制，去掉会额外授予普通用户访问的Users、Authenticated Users、Everyone等宽泛Allow项。仅在该实验根操作，不能对C盘或父目录递归更改。

添加ld_reader和ld_denied的“读取和执行、列出文件夹内容、读取”，应用于本文件夹、子文件夹和文件。对Denied子目录再禁用继承、转换显式权限，仅移除ld_denied的Allow，保留ld_reader、SYSTEM和Administrators；无需增加显式Deny。检查是否仍存在给ld_denied所属组的额外Allow；用高级安全中的有效访问核对。APS000继承根目录，两账户都应可读；Denied只允许reader读。

**D. 建立SMB共享。** AVEVAProjects → 属性 → 共享 → 高级共享 → 共享此文件夹，名称AVEVAProjects。在共享权限中移除Everyone默认项，仅添加ld_reader和ld_denied“读取”，Administrators“完全控制”。不授予普通账户更改或完全控制。共享权限和NTFS权限都需允许才能成功，两者的结果在⑤实测，而不是只看其中一个勾选框。[微软权限说明](https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/access-denied-access-smb-file-share)

**E. 只开放实验SMB入口。** 来宾机网卡属性中确认File and Printer Sharing for Microsoft Networks已绑定，services.msc中确认Server服务运行。打开来宾机wf.msc → 入站规则 → 新建规则 → 自定义：TCP，本地端口445、本地地址192.168.56.10、远程地址192.168.56.1、允许连接，选择该测试适配器当前实际使用的配置文件，名称为LaunchDeskLab-SMB-本轮标识。若有组策略禁止本地规则则记录阻塞，不绕过。核查已有SMB允许规则是否比此范围更宽；只允许在测试VM内按登记调整，不能关闭整机防火墙。规则地址限制与配置文件匹配见[微软防火墙配置](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/configure)。

使用显式UNC不依赖资源管理器“网络”列表出现设备，无需为设备发现开启额外端口。保留SMB2/3及当前签名策略，不启用SMB1、不关闭签名、不放宽宿主机Guest策略。[微软SMB建议](https://learn.microsoft.com/en-us/windows-server/storage/file-server/troubleshoot/detect-enable-and-disable-smbv1-v2-v3)

| 账户 | 共享权限 | NTFS APS000 | NTFS Denied | 客户端预期 |
|---|---|---|---|---|
| ld_reader | Read | Read & Execute | Read & Execute | 两处marker都可读；不能创建、改写或删除 |
| ld_denied | Read | Read & Execute | 无有效读取授权 | APS000可读，Denied下marker拒绝访问 |
| 错误账户/密码 | 不应认证成功 | 不适用 | 不适用 | 认证失败；不能误判数据库损坏 |

Read只验证目录预检；真实AVEVA登录可能需要写入/锁权限。将来需用授权的可写数据库副本另设权限实验，不能直接把此只读共享作为真实登录已就绪的证明。

<a id="baseline"></a>

## ⑤ 先做Windows基线验收：不依赖尚未实现的产品

以下由未来环境操作者执行，客户端使用与应用相同的Windows登录用户、普通权限。不能先用管理员会话连通，再声称普通用户也一定能读。首次测试前确认没有其他业务使用同一VM服务器标识。

1. **账户reader基线。** 宿主机资源管理器地址栏输入②的完整IP UNC；出现凭据窗口选“使用其他账户”，输入LD-PROJ-VM\ld_reader，不勾记住凭据。若未出现提示，先查清是否已有会话，不能把复用当成新账户认证通过。打开APS000和Denied的marker，各记录可读；尝试在APS000新建临时文件应拒绝，不应产生文件。
2. **账户denied对照。** 关闭本轮文件、资源管理器窗口和打开的句柄。在来宾机计算机管理 → 共享文件夹 → 会话中核对并关闭仅属于本轮测试客户端的会话；该VM只供本轮使用。客户端若曾勾选保存密码，到控制面板 → 凭据管理器 → Windows凭据核对该VM准确目标，只移除本轮误存项；本轮映射的驱动器在“此电脑”右键断开。无盘符UNC没有等价的普通驱动器图标，不能靠关窗口断言会话已清除。用已有诊断方式核对该服务器旧连接；若无法确认，用宿主机独立标准测试用户会话完成，保存其工作后注销该测试用户、重新登录再测，不强制注销日常开发会话。若既不能隔离又无法验证清理，就记阻塞。不能全局删除，也不能换地址绕过缓存。
3. 用LD-PROJ-VM\ld_denied重新访问：APS000的marker可读，Denied的marker应拒绝。这证明同一服务器网络可达且账户可认证，拒绝发生在目标目录权限上。记录实际错误码/事件；仅有资源管理器提示时标明原生码未采集，不伪造错误5。
4. **坏密码与缺失路径。** 再按上一步清理本轮会话，进行一次坏密码连接；确认失败后不反复重试。另在有效账户下访问不存在的MissingShare和APS000\Missing目录，各保存原始反馈。不要用缓存成功会话冒充坏密码验证。
5. **固定Ready基线。** 恢复两账户、目录和规则的预期状态，清理本轮活动会话/句柄，关闭VM后建立Ready快照，记录ID。下一轮恢复Ready还要清理客户端会话，因为恢复VM不恢复宿主机。独立保存②参数、权限表、规则截图和本轮结果；快照不是备份。VM快照包含来宾系统账户状态，仅作本机实验恢复，不放进项目Git或当作无秘密配置导出。[VMware备份边界](https://knowledge.broadcom.com/external/article/304707/best-practices-when-backing-up-a-vmware.html)

Windows基线通过条件：同一客户端身份下，两账户权限矩阵与④一致；读取成功、写入拒绝、指定子目录拒绝、坏凭据失败、缺失目录失败可复现。每项必须有时间和实际账户上下文。仅TCP445通或ping通不算通过；不对生产账户做失败尝试。

<a id="acceptance"></a>

## ⑥ 再按功能验收：成功路径之后逐类注入故障

| 顺序/门槛 | 验收动作 | 前置与通过条件 |
|---|---|---|
| G0 登记与隔离 | 完成②③ | 无地址冲突；VM只有本轮Host-only连接；尚未开放生产数据 |
| G1 OS连接与权限 | 完成④⑤ | reader/denied矩阵实测一致，Ready基线与客户端清理可重复 |
| G2 产品远程预检 | VM-01、02、05、12 | **待实现后**：输入→协调器→具体C#脚本→Windows后勤→类型结果；可读不等于注册匹配 |
| G3 认证与失败 | VM-03、04、06、07、08、09、10、11、13 | 每次只变一个条件；失败不创建厂商进程、不丢草稿、不自动修复系统 |
| G4 授权与迁移 | VM-14、15、16，另TL-01–08 | **待实现后**：精确选择、撤销、导入未绑定、故障保留旧代，有脱敏证据 |
| G5 界面与真实工程 | FigCraft/WPF对照、实际AVEVA项目 | **本轮不实施**：三档DPI实际截图另验；真实注册/读写/许可/锁/并发/登录另验 |

G0/G1已按[执行记录](xref:remote-project-lab-results)通过，产品层G2–G5未执行，其中G2–G4还依赖未来产品实现。下列VM场景的部分操作系统前置已覆盖，但不能把十项OS基线当作完整产品场景通过。Windows基线的测试密码不进入AVEVA登录槽位，Windows成功也不自动生成LaunchDesk的授权。

| ID | 场景构造（均待执行） | 预期观察 |
|---|---|---|
| VM-01 | Ready、reader读取APS | Ready只表示读取；安全ProcessFixture可继续，非真实AVEVA登录证明 |
| VM-02 | 清理本轮会话且无连接资料 | 先试当前身份，失败才给手动配置；不自动建立连接 |
| VM-03 | 无缓存会话，错误测试密码一次 | AuthenticationFailed或实际系统策略结果；保留原生诊断，不泄密 |
| VM-04 | denied访问Denied下marker | AccessDenied；不改ACL，不称数据库损坏 |
| VM-05 | 缺失共享、缺失子目录分别测试 | PathUnavailable，能看清失败位置 |
| VM-06 | reader会话保持，再显式请求denied | 记录1219等实际结果；不自动断开既有会话或换地址。若缓存/系统行为未产生1219，则该码待故障注入验证 |
| VM-07 | 运行期间断开该VM网卡或停止VM | 8秒期限内返回PathUnavailable/ProbeTimedOut；仅清理自己Worker，失败标CleanupPending |
| VM-08 | 改路径/切页并延迟旧回包 | 需确定性故障注入；旧OperationId/Revision不回显、不放行 |
| VM-09 | 事先显式建立本轮P:映射 | DriveInUse，不覆盖。只有该盘符空闲且归本轮时才构造夹具 |
| VM-10 | 预检后、创建前断开网络 | 重查失败则不创建；末次检查之后的竞态如实反馈，不自动重复启动 |
| VM-11 | 建立连接时取消/延迟确认回包 | 确定性注入配合实际会话查询；OutcomeUnknown，不声称未连接 |
| VM-12 | 假init内APS指向不同目录 | ProjectRegistrationMismatch；夹具位于实验目录，不修改厂商init |
| VM-13 | C#夹具独占打开marker | 保留OS结果；单纯目录枚举可能成功，实际文件读检查才能观察锁；非AVEVA锁验收 |
| VM-14 | AVEVA槽位授权3项后撤销1项 | 只改勾选ID；保留其余项，不混用网络凭据，不关闭已运行程序 |
| VM-15 | 导入同名/同ID，并改Project/Module/账户 | 不继承本机秘密；安全指纹失效，重新预览确认 |
| VM-16 | 新秘密写入成功后注入提交失败 | 旧绑定可用，仅新无引用条目待清理，不误删其他秘密 |

每项结束先恢复Ready及客户端基线，再开始下一项；故障注入工具未来使用C#，不引入PowerShell产品脚本。坏TOML、磁盘满、日志失败、generation指针中断等用隔离故障注入，不能为构造错误损坏开发盘。TOML验收见[选型TL清单](xref:toml-selection)。

<a id="evidence"></a>

## ⑦ 证据和清理：环境通过与产品通过分别签字

| 证据 | 必须记录 | 排除内容 |
|---|---|---|
| 环境登记 | LabRunId、VMware/Windows版本、VMnet/IP、配置文件、规则范围、ACL、快照ID、宿主机普通用户上下文 | 密码、真实工程数据 |
| OS基线结果 | 操作顺序、实际账户、目标、时间、预期/实际、原生错误或未采集说明、截图 | 凭据窗口的秘密、完整敏感日志 |
| 产品验证（未来） | 构建哈希、ConfigRevision、OperationId、List<Record>、脱敏JSONL、阶段状态、Worker PID/退出/清理、进程创建计数 | 原始秘密、IPC载荷、凭据blob |
| 结论 | 每一G门槛及VM/TL项：通过/失败/阻塞/未执行；证据位置和复跑结果 | 用“网络通”替代产品或真实AVEVA结论 |

环境操作者负责G0/G1；产品验证者在实现后负责G2–G4；真实AVEVA和视觉验收另行登记。当前只交付步骤，不能预填通过。

清理顺序：结束本轮文件/进程句柄→确认并清理本轮客户端连接→由VM管理者关闭本轮VM会话→移除仅本轮创建且无引用的测试凭据→恢复测试VM或保留Ready→按登记决定是否保留专用VMnet。恢复VM不能替代宿主机清理。只处理登记清单，禁止全局net use删除、清空凭据管理器、Restore Defaults或递归删除计算出的不明目录。若曾意外保存凭据，只删除本轮准确目标；仍被使用则记录待清理，不强制断开其他工作。

<a id="vmware-loopback"></a>

## ⑧ 回环与匿名的范围，以及后续真实验收

VMware Host-only + 专用账户已选为主验证路线。本机`\\127.0.0.1\测试共享`仅可选UNC冒烟；整个127/8属于回环，宿主机和VM各自指向自己，不能冒充跨机认证或断网验证。[IANA回环定义](https://www.iana.org/assignments/iana-ipv4-special-registry)

“不弹密码框”可能用了当前身份或已有会话，并非匿名。匿名/null-session与Guest也不同，不作为本轮前置；不放宽Guest/匿名/SMB签名，不使用地址别名绕过冲突。[微软Guest说明](https://learn.microsoft.com/en-us/windows-server/storage/file-server/enable-insecure-guest-logons-smb2-and-smb3)

后续真实案例另验：Project注册、可写副本所需权限、MDB/用户、模块、许可签出、文件锁、并发及真实启动结果。没有真实案例时这一层保持未验证；测试夹具通过不变更该结论。前端继续按[FigCraft交付契约](xref:remote-portable-figcraft)规划；G0/G1环境执行不扩展为安装Tomlyn或实现WPF的授权。
