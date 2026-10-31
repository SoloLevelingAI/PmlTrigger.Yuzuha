
## 2026-10-31 CLI / Host 最终实机补充

APS DESIGN 第五次启动及严格 Host/Form Query 验收通过；CLI 完整 JSON 捕获 374 ms，PID25624，Host0.3.4.2。最终实现恢复厂商原生标准句柄语义，不接管标准流、不使用 NUL、不继承 CLI stdin；Monitor DETACHED_PROCESS 与 Marine CREATE_NO_WINDOW 互斥。26 项 CLI 捕获、33 项 Host 协议测试通过。前四次失败及最终成功原始证据见 [中文验证报告](../../../../docs/validation/cli-host-compatibility-2026-10-31.md)。父终端退出后独立查询仍通过；APS已原生正常退出、无保存提示，截图已保存；工作台新PID19600沿用18642及原live-state，旧包保留。全局任务状态由根代理更新；不能由本轮结果推定其他 Feature 完成。
---
uid: pmltwin.launch-workflow-host-followup
title: 启动工作流：Host Unknown 身份跟进
---

# 启动工作流：Host Unknown 身份跟进

当前结论（2026-10-31 后续验收）：仅更新实装 resolver/Rpc 后，E3D 2.1 APS 的 PID 8408 在 expectedModel=DESIGN 下严格 TargetVerified=true，两次内置有界读取返回 OK；本次会话随后正常退出。协议能力版本仍为 0，未修改 DLL、EVARS 或模型。见 [严格验收与长期证据](../../../../docs/validation/host-pml-verification-2026-10-31.md)。下文保留此前 Unknown 的调查历史，不代表当前仍失败。

2026-10-31 只读调查。当前启动链成功打开 APS 工程窗口是主任务的实机证据；严格 `expectedModel=Design` 选择时 Host 报 Unknown，不能据窗口或模块进程宣称 Host 身份已核验。

## 结论

本机 E3D 2.1 注册的 Yuzuha 部署仍是较旧 PML 启动链：创建 `PmlCommandMethod` 后没有调用 `RefreshModel`。Host 构造器先将 Model 设为 Unknown 并启动 PID 管道，因此“pipe 可见但模块 Unknown”与现有源码行为一致。仓库已包含刷新模块身份的新版 PML，实装文件与仓库 SHA256 不同。

这是静态部署证据支持的根因解释。没有直接读取目标进程内已加载程序集或 PML 缓存，因此不能把磁盘版本直接等同于进程加载内容；后续修复仍需计划、部署后重验。

## 本次环境入口

文件：`C:\Program Files (x86)\AVEVA\Everything3D2.10\evars.init`。

| 行 | 与本调查相关的内容 |
|---|---|
| 9–10 | 基础 PMLLIB/PMLUI 包含 `C:\AVEVA\PMLLIB`、`C:\AVEVA\PMLUI` 及产品原目录 |
| 131 | 在 PMLLIB 前加入 `C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha\PMLLIB` |
| 132 | 在 PMLUI 前加入 `C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha\PMLUI` |
| 133 | Yuzuha profile 为 E3D2.1 |
| 134 | YUZUHA_STARTUP_ACTIONS 为 rehash-all |

只输出了 Yuzuha/PMLLIB/PMLUI 相关行，没有收集其他环境变量或凭据。静态 INIT 内容不作为运行时环境已生效的证明。

下文实装根为 `C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha`，仓库根为 `C:\Users\26648\Documents\ChatGPT\Example\PmlTrigger.Yuzuha`。

## 实装与仓库比较

| 相对路径 | 实装 SHA256 | 仓库 SHA256 |
|---|---|---|
| PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc | E134D87C9DDDB7864D56AEB617CEE3239D64162FFD0B0827B5AD55ABDE873950 | D38A4FDE4BFD8CE723E49EFDEF4856ACBA1138AC85950783541F020131F78BF2 |
| PMLLIB/Addins/YuzuhaRpc.pmlobj | 4D65EBBFF161EB71C9EFC49B7E2EE3C2A3F9F4F0AA3A88B83746CE859C153677 | 8E4486A10BEE89545783F85F90B6B29BCEB447DEDD1360A41881E0452785F560 |
| PMLLIB/Addins/YuzuhaAddin.pmlobj | 687E64D96A1C7A7F8A16517C2BFC284A2B7DEC6394646215B9EC49CE5A9F28E3 | 8A96389F9020CFB63FD215998A766719C9D23FA3FA8DF891C04319D7E8DA9F22 |

实装 resolver/Rpc 文件最后写入时间为 2026-10-23 05:36:40 UTC，仓库相应文件为 2026-10-27 01:16:07 UTC 左右；时间仅为线索，内容以 SHA256 和逐行比较为准。

- 实装 `PMLLIB/Addins/YuzuhaRpc.pmlobj:73` 创建 Host，`:78` 直接调用 GetRpcServerStatus；整个文件没有 RefreshModel。仓库同文件 `:78` 调用 `!!YuzuhaRpcHost.RefreshModel(!!YuzuhaModel)`，然后才查监听状态。
- 实装 `PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc:46–47` 使用 FMINFO 和局部 `!MODEL`，没有全局 `!!YuzuhaModel`。仓库 `:10–11` 从 Current Session 和 `.module()` 获取并保存模块身份。
- 实装 `PMLLIB/pml.index:1–5` 包含 `/Addins/` 下 YuzuhaRpc.pmlobj 和 `/Bootstrap/` 下 YuzuhaResolveRuntimePath.pmlfnc，支持这些磁盘文件在已注册索引中的判断，但不证明进程缓存已刷新。
- 实装 `runtime/profiles/E3D2.1/net48/YuzuhaToolkit.PmlHost.Net48.dll` 文件版本 0.3.2.0，最后写入 2026-09-19 15:10:46 UTC，SHA256 `EF6CDB934D7F69BD429B66F6C83F52928B3A14A66BF49B1EF3ABF28046C48987`。仓库根没有对应 runtime 发布文件，本次没有编译/加载 DLL，也没有借放宽模块约束读取 HostVersion。

## 源码责任与后续契约

- `src/YuzuhaToolkit.PmlHost.Net48/PmlCommandMethod.cs:42–45`：构造时 Model=Unknown 并 Attach。`:63–66`：RefreshModel 才更新身份。
- `src/YuzuhaToolkit.PmlHost.Net48/PmlCommandRpcHost.cs:42` 起：Attach 可在监听存在时替换目标；再次创建未刷新对象也可能把身份重新变为 Unknown。
- `src/YuzuhaToolkit.PmlHost.Net48/PmlCommandRpcService.cs:25`：GetHostIdentity 直接回传 target.Model，不从窗口标题或 exe 猜测。
- `src/YuzuhaToolkit.Mcp.Core/YuzuhaRpcBridge.cs:314–320`：expectedModel 严格匹配，Unknown 正确拒绝 Design。应保留这条约束。
- `src/YuzuhaToolkit.Manager.Core/ConnectionEvidence.cs:90` 起：现有部署指纹冻结 Host DLL、resolver 及可选环境文件，但未包含 Rpc/Addin PML。建议下一修复把这些影响模块初始化的启动文件与索引纳入指纹，防止旧启动链配新 DLL 被当成同一部署。
- 历史 `docs/validation/2026-09-19-e3d.md:36` 已记录 bootstrap 未调用 RefreshModel 导致 Unknown；本次实装比较提供同类原因的当前证据，不把旧报告当本次实机验收。

后续建议先通过既有部署 preview/plan 比较完整运行载荷，记录来源与目标摘要、备份及回退，再单独授权 apply。Host 身份契约可增加模块初始化状态/来源及实际加载程序集路径指纹，区分“管道已启动”与“模块身份已初始化”，避免只返回无原因 Unknown。部署后仍用同一 PID/创建时刻/pipe 与 expectedModel 做严格核验；不能把 PML 模块别名、产品版本和启动模块名称未经明确映射混为一谈。

本次没有执行 PML，没有调用 RefreshModel，没有加载或替换 Host，没有改安装、INIT、索引或部署文件，没有重启产品，也没有取消 expectedModel。只新增这份跟进文档；后续修复不在本次调查授权内。

## 后续仓库修复：部署证据覆盖启动链

2026-10-31 的后续代码修复扩展 `ConnectionEvidence.Fingerprint`：除所选 profile 的 Host DLL、resolver 与可选环境文件外，必需读取 `PMLLIB/Addins/YuzuhaRpc.pmlobj`、`PMLLIB/Addins/YuzuhaAddin.pmlobj`、`PMLLIB/pml.index`。这些文件共同决定 Host 创建、模块身份刷新及 PML 加载入口；任一内容改变会清除旧部署证据的全部绿色灯态，任一必需文件缺失或不可读仍失败关闭。路径、无链接检查及文件大小预算保持原约束。

这只是部署磁盘内容绑定，不证明进程已加载该内容，也不单独证明 Host 已就绪。PID、进程创建时刻、expectedModel、pipe 和协议核验保持严格；Host Model 为 Unknown 仍拒绝 Design。测试使用临时合成文件与通信 fixture，未触碰本机 AVEVA、实装目录或产品进程，不构成实机 Host 修复验收。
