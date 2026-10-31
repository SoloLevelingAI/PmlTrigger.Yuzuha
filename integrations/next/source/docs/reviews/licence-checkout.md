---
document_kind: historical-evidence
implementation_status: historical
reviewed_on: 2026-10-29
uid: licence-checkout-test
audience: developer
---
# 开发验证：FNF 7与原生启动契约

> 历史记录：保留当时结果，不代表当前进程、机器或许可状态。当前交付边界见[状态页](../status.md)。

2026-10-28，最新实验`20261028-fnf7`。本页为开发维护记录；产品中的远程工程不显示VM或模拟标记。普通功能入口见[远程工程方案](remote-portable.md)，脚手架见[开发索引](../development/INDEX.md)。

## ① 最新结论

**按原LaunchDesk的原生标准句柄方式启动，E3D3.1已正常进入MAR / ALL_NO_MDS / Model。** FNF7来自实验器改变厂商启动方式，不应被当成产品或数据库故障。两次原生方式均成功，并有许可授予、正常退出和归还记录。

## ② LaunchDesk原来怎么做

原项目`LaunchDesk.Core/LaunchService.cs`的Build读取保存配置：UseShellExecute=false、原工作目录、原launch.init，Monitor参数使用ArgumentList；没有接管stdin/stdout/stderr。现项目[WindowsProcessHost.Start](../../src/LaunchDesk.Next.Platform/Processes/WindowsProcessHost.cs)也保持这个原生边界。

前次实验器改用了实验目录、全量引号、标准流重定向及隐藏控制台，未先完整复现原启动契约，这是验证方法的遗漏。本轮只改独立实验工具，没有改原项目、Rewrite或产品启动代码。

## ③ 路径核对结果

| 项目 | 实际值与来源 |
|---|---|
| 工作目录 | C:\Users\Public\Documents\AVEVA\USERDATA，来自保存的E3D3.1应用配置 |
| 初始化 | Everything3D3.1/launch.init → evars.init → 工程custom_evars.bat → mar/evarsmarine.bat；再调用set_aveva_design.bat设置兼容名称 |
| 工作临时目录 | evars.init设置AVEVA_DESIGN_WORK=%TEMP%\，PDMSWK沿用；实际测试TEMP/TMP都是当前用户Local/Temp |
| 用户目录 | AVEVA_DESIGN_USER为上述USERDATA，PDMSUSER沿用；不是通信文件不存在的证据 |
| 通信文件 | 失败对照中已观察TEMP/comms…文件生成并达到58367字节；只收集路径/大小/时间，不读取内容 |

没有改全局TEMP/TMP、初始化文件、目录ACL或许可配置。[原契约与当前文件哈希](../../artifacts/vmware-lab/20261028-fnf7/launch-contract.json)记录读取范围；这些主机初始化文件没有配置前哈希，不补造前后比较。

## ④ 四组对照如何缩小原因

| 组 | 条件 | 实际结果 |
|---|---|---|
| A | 原USERDATA；仍重定向标准流、全量引号 | 仍FNF7；通信文件实际存在 |
| B | A再恢复原生等价引号 | 厂商运行日志仍记通信文件读取失败 |
| C | 原路径/参数；原生标准句柄、不重定向；CreateNoWindow=false | 成功进入MAR/ALL_NO_MDS/Model，PID48540 |
| D | C只改CreateNoWindow=true | 同样成功，PID22140；排除隐藏控制台本身 |

故障收敛到**实验器重定向标准流／继承管道句柄的启动模式**。没有再逐项拆分stdin、stdout、stderr内部协议，不能声称某一个流单独负责。厂商程序不是任意控制台工具，采集日志不能擅自改其通信通道。[对照元数据](../../artifacts/vmware-lab/20261028-fnf7/comparison.json)｜[厂商运行事件](../../artifacts/vmware-lab/20261028-fnf7/vendor-runtime-events.txt)。

## ⑤ 调用链与成功证据

```text
普通输入快照 → 既有启动协调器 → StartMonitor → WindowsProcessHost
  → 原工作目录 + 原init + 原生参数/标准句柄
  → mon.exe → des.exe → 远程许可服务 → MAR / ALL_NO_MDS / Model
开发观察：进程/窗口/元数据 + 厂商运行记录 + 服务端租约 → 脱敏Record
```

本轮使用用户指定SYSTEM测试凭据，仅内存传入专用进程，不写配置、日志或凭据文件；只指定测试服务4545@192.168.244.10，无备用服务器。C组14:00:41授予、14:06:25归还；D组14:06:44授予、14:12:00归还，均为AVEVA201。

窗口显示项目和MDB；YuzuhaToolkit验证PID、启动时间和管道身份。插件Model字段返回Unknown，这一限制保留；未执行PML来替换它，也未修改模型。成功依据是实际Model界面、厂商运行及租约证据共同成立。[会话核验](../../artifacts/vmware-lab/20261028-fnf7/session-verification.json)｜[本轮全部授予/归还](../../artifacts/vmware-lab/20261028-fnf7/server-leases.json)。

## ⑥ 失败与日志该怎样表达

Monitor退出码0、模块短暂存活或4546连通，都不能替代项目界面成功。A/B记录为通信失败；C/D记录为原生启动成功。最初“仅改工作目录后模块存活”的中间观察已被A组最终失败覆盖，不再当作修复结论。

日志继续使用List<Record>与现有NLog记录请求、版本、阶段、进程和原生错误；厂商服务日志按字段投影，不靠接管厂商标准流凑日志。后续如需重定向必须有明确协议适配和真实回归，不能直接替换默认进程方式。

## ⑦ 开发脚手架与收尾

当前维护代码集中在dev/RemoteProjectLab的Host、Guest和本机开关；产品不引用、不初始化、不自动发现或开启它。只在开发机显式执行enable --local后开放实验动作，绑定机器与用户；本轮收尾后默认关闭。产品只呈现普通远程工程，来源事实留在开发验证记录。

用户报告睡眠并重新登录后，只读复查确认4546恢复；临时SetThreadExecutionState防睡眠不改电源计划。按用户要求，测试会话正常退出、四次租约均归还、临时规则撤销、克隆正常关机，现有三份快照保留，原许可机未启动。[最终保护核对](../../artifacts/vmware-lab/20261028-fnf7/protection.json)。

## ⑧ 查源码、规格和边界

[开发源码索引](../development/INDEX.md)｜[逐条Record](../../artifacts/vmware-lab/20261028-fnf7/records.json)｜[本机开关与48项架构测试](../../artifacts/vmware-lab/20261028-fnf7/development-gate-validation.json)｜[LC-06、DV-01需求](../../openspec/requirements.md)。

历史证据保留：[主机名失配](../../artifacts/vmware-lab/20261028-rms-identity/binding-comparison.json)、[恢复前快照及配置保留](../../artifacts/vmware-lab/20261028-hostname-restore/snapshots.json)、[此前签出通过但应用FNF](../../artifacts/vmware-lab/20261028-hostname-restore/e3d-result.json)。原VMX早期克隆重写例外仍见[G0/G1记录](remote-project-lab-results.md#protection)。

本次证明现有厂商启动链与实验环境可用，不代表尚未实现的产品远程连接、TOML、凭据授权、G2–G5或WPF新界面已通过。普通用户无需启用开发脚手架来使用已实现功能。
