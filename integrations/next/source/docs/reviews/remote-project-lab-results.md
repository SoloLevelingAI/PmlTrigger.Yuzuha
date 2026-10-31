---
document_kind: historical-evidence
implementation_status: historical
reviewed_on: 2026-10-29
uid: remote-project-lab-results
---
# G0/G1执行结果：已通过，保留原机VMX保护例外

> 历史记录：保留当时结果，不代表当前进程、机器或许可状态。当前交付边界见[状态页](../status.md)。

2026-10-28，LabRunId：20261028-g0-g1。**G0网络隔离、G1账户与共享权限验收已通过；10/10项真实SMB检查通过。** 已保留配置前及Ready两份关机态快照。当前所有VM关机；原许可机没有启动。随后只启动测试克隆核对许可组件，已再次正常关机，结果见②。原机VMX被VMware在克隆时重写的保护例外见⑥，不能把它说成所有原文件字节均未改变。

## ① 结论与范围

Host-only、专用账户、假Project目录和受限共享已验收，依据[手册](xref:remote-project-lab)。这是环境基线，不是产品远程功能；G2–G5、真实AVEVA数据库/登录/锁/许可仍待验证。十项OS检查未施加产品拟议的8秒截止，也不与现有99项产品测试混算。未安装依赖或实现WPF。

## ② 环境、E3D与许可在哪里

| 查找对象 | 实际位置与状态 |
|---|---|
| 测试VM | VMware库中LD-PROJ-VM；Windows 11，独立完整克隆，文件在`LaunchDesk.Next.Lab/LD-PROJ-VM` |
| 管理账户 | `LD-PROJ-VM\ZHANGSH1`；口令只在本机保护存储，不写入文档 |
| 网络 | 单网卡Host-only VMnet1；宿主机192.168.244.1，VM192.168.244.10/24；无网关、DNS或外部IPv4路由 |
| SMB | `\\192.168.244.10\AVEVAProjects\E3D2.1\AvevaPlantSample\APS000`，只有marker.txt假工程 |
| 防火墙 | 三配置文件开启；实验TCP445仅允许宿主机244.1访问VM244.10 |
| 原许可机 | `D:/VirtualMachines/E3D-License/E3D-License.vmx`，保留原NAT，本轮未启动 |

复用现有VMware 26.0.1和VMnet1；DHCP范围128–254不改，静态地址10在范围外。没有修改全局DHCP/NAT。宿主机接口Forwarding为0；Internet共享查询失败，不能据此宣称禁用。隔离结论限于现有网卡与路由，不保证管理员以后不会改网。

<a id="licence"></a>

**你看到VM没有E3D，是有依据的：E3D客户端在宿主机，VM装的是许可组件。** 2026-10-28只读核对如下：

| 位置 | 找到什么 | 可以说明什么 |
|---|---|---|
| 宿主机 | `C:\Program Files (x86)\AVEVA\Everything3D3.1\mon.exe`、`des.exe`；`Everything3D2.10\mon.exe` | 有E3D3.1及2.1启动文件 |
| 测试VM | 安装记录只有AVEVA Licensing System 4.1；所查AVEVA目录未找到mon.exe/des.exe | 当前证据支持“许可服务器”，没有发现E3D客户端；不是全盘穷举 |
| 测试VM | 已确认克隆改名导致主机名许可绑定失配，RMS拒绝加载，RULES停止 | 后续按授权恢复ZHANGSH后签出AVEVA201通过；后续原生启动复测已进入Model，见[复测](licence-checkout.md) |
| 许可文件 | 宿主机`D:\VirtualMachines\E3D-Setup\LicenseFile.xml`为12102字节；VM的Flex/licenses.lic为0字节 | 找到文件线索；未导出内容，空Flex文件也不能代表RMS无许可 |

**能找到许可系统和文件，但尚不能确认有效许可。** 克隆改过身份/网络，不能假定继承原机许可有效；原许可机仍未启动，未安装E3D或改许可密钥。首次签出因RULES4546超时失败；后续恢复克隆主机名后签出已通过，后续原生句柄复测已进入MAR/ALL_NO_MDS/Model。宿主机RMS服务也在运行，RULES停止，不能与VM结果混为一谈。旧Monitor文档指向原机`4545@192.168.173.10`，那是历史配置，本轮没有验证该端点。

LaunchDesk现有`CheckAvevaLicence`只检查服务/端口，不验证功能许可签出。后续恢复名称后签出通过但原生启动已通过，见[结果](licence-checkout.md)；SMB夹具无需安装E3D。客户端向许可服务器签出的分工参见[AVEVA官方说明](https://docs.aveva.com/bundle/licensing-system/page/1046971.html)。

证据：[VM只读清单](../../artifacts/remote-review-edit/guest-licence-inventory.json)、[宿主机与保护核对](../../artifacts/remote-review-edit/host-and-protection.json)、[扫描器C#源码](../../artifacts/remote-review-edit/GuestLicenceInventory.cs)。清单只含安装/服务/路径/大小/哈希与探测结果，不含许可正文；VM扫描范围与深度限制也记录在内。

## ③ 账户、共享与目录权限

| 账户 | 共享 | APS000 | Denied |
|---|---|---|---|
| ld_reader | Read | Read & Execute | Read & Execute |
| ld_denied | Read | Read & Execute | 无读取授权 |
| SYSTEM/Administrators | Full Control | Full Control | Full Control |

两测试账户只有Users组，使用不同非空随机密码；验收未借管理员身份。共享无Everyone，目录移除宽泛继承；Denied独立DACL只给管理员与reader。克隆内一条较宽SMB规则已禁用，原机/宿主机防火墙未改，见[配置记录](../../artifacts/vmware-lab/20261028-g0-g1/guest-setup.json)。

秘密经加密交接、DPAPI保护，不进入Git、日志或CLI参数；未启用Guest/匿名/SMB1或放宽签名。本只读夹具不证明真实工程所需的写入、锁权限。

## ④ G1实际验收：十项均通过

从宿主机C#检查器发起，使用LOGON_NEW_CREDENTIALS(9)，四组不同AuthenticationId/LUID隔离账户和坏密码情况。不是先用管理员连通再换界面标签，也没有借同名凭据或已有连接放行。

| 检查 | 实际结果/原生错误 | 判定 |
|---|---|---|
| reader读取APS marker | 成功，内容属于本轮夹具 | 通过 |
| reader读取Denied marker | 成功 | 通过 |
| reader写入APS临时文件 | 拒绝，5 | 通过 |
| 缺失Project子目录 | 路径不存在，3 | 通过 |
| 缺失共享 | 网络名不存在，67 | 通过 |
| denied读取APS marker | 成功 | 通过 |
| denied读取Denied marker | 拒绝，5 | 通过 |
| denied写入APS临时文件 | 拒绝，5 | 通过 |
| reader一次错误密码 | 登录失败，1326 | 通过 |
| 错误密码后换独立会话用正确密码 | 再次成功，未误判锁定或缓存成功 | 通过 |

[List<Record>完整结果](../../artifacts/vmware-lab/20261028-g0-g1/host-smb-results.json)与[逐条JSONL日志](../../artifacts/vmware-lab/20261028-g0-g1/host-smb-records.jsonl)保留时间、OperationId、账户、LUID、实际错误及判定；没有秘密。复核又从来宾侧读取了[实际网络、路由、共享DACL、NTFS和防火墙](../../artifacts/vmware-lab/20261028-g0-g1/guest-after.json)，与预期一致。

## ⑤ 重启、会话与快照

配置后正常重启，再完成十项检查，证明网络、账户、共享与权限已持久化。检查结束未强断缓存句柄；正常关机终止测试会话。未创建持久盘符或自动重连。

| 回滚点 | 快照名 | UID |
|---|---|---|
| 配置前 | LD-Proj-Before-G0-G1-20261028 | 1 |
| 已验收 | LD-Proj-Ready-G0-G1-20261028 | 2 |

[快照与哈希](../../artifacts/vmware-lab/20261028-g0-g1/snapshots-final.json)已保留；本轮许可核对后两份哈希仍一致。常规复跑用Ready；Before会恢复旧账户/网络，不能假定当前口令有效。快照不还原宿主机连接，镜像未进Git。

<a id="protection"></a>

## ⑥ 原环境保护与例外

原VM始终未启动。原快照表、NVRAM、VMXF及全局DHCP/NAT哈希未变；原磁盘大小/修改时间未变，**没有磁盘内容前后哈希**。

**例外：原E3D-License.vmx在VMware完整克隆时被重写，SHA256改变，大小仍10703字节。** 没有配置前原文，无法完整字段比较；不推定“仅无害元数据”，不声称原机所有文件字节未变，也未猜测重建加密配置。可观察NAT、磁盘路径与硬件身份保留。本轮只读核对未再改变这些保护基线。

证据：[配置前](../../artifacts/vmware-lab/20261028-g0-g1/before-hashes.json)、[G0/G1后](../../artifacts/vmware-lab/20261028-g0-g1/protection-checks.json)、[磁盘元数据](../../artifacts/vmware-lab/20261028-g0-g1/source-disk-check.json)、[本轮复核](../../artifacts/remote-review-edit/host-and-protection.json)。以后克隆前须另存原VMX私有备份，勿将加密元数据公开。

## ⑦ 实际工具链与中途失败

初期完整克隆/快照使用界面；随后用现有VMware VIX API与C#，不需在VM安装Cursor或其他工具。

```text
受保护输入 → LabVix固定克隆 → GuestInspect确认身份
→ GuestSetup配置 → 正常重启 → SmbCheck独立会话验收
→ GuestAudit复核 → 正常关机 → Ready快照
```

[早期访问错误](../../artifacts/vmware-lab/20261028-g0-g1/access-validation.json)17005/3015在确认实际账户与口令后解决；未离线重置、移除加密/vTPM或放宽策略。GuestAudit曾因SESSION_INFO_1多字段崩溃0xC0000005，按[微软定义](https://learn.microsoft.com/en-us/windows/win32/api/lmshare/ns-lmshare-session_info_1)修正后成功；失败历史保留。PowerShell交接曾被策略阻止，未改策略，实际使用C#。

## ⑧ 证据索引与下一步

状态入口：[验收汇总](../../artifacts/vmware-lab/20261028-g0-g1/acceptance.json)。[配置前](../../artifacts/vmware-lab/20261028-g0-g1/guest-before.json)→[配置](../../artifacts/vmware-lab/20261028-g0-g1/guest-setup.json)→[复核](../../artifacts/vmware-lab/20261028-g0-g1/guest-after.json)；十项结果见④，许可证据见②。

C#源码：[工具索引](../../artifacts/vmware-lab/20261028-g0-g1/tools/README.md)、[LabVix](../../artifacts/vmware-lab/20261028-g0-g1/tools/LabVix.cs)、[GuestSetup](../../artifacts/vmware-lab/20261028-g0-g1/tools/GuestSetup.cs)、[GuestAudit](../../artifacts/vmware-lab/20261028-g0-g1/tools/GuestAudit.cs)、[SmbCheck](../../artifacts/vmware-lab/20261028-g0-g1/tools/SmbCheck.cs)、[版本哈希](../../artifacts/vmware-lab/20261028-g0-g1/toolchain.json)。这是环境工具，非产品实现；二进制、秘密和VM镜像不公开。

后续产品实现另行批准，再做G2–G5及真实AVEVA验证。
