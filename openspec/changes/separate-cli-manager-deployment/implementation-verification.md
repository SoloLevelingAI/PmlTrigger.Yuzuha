# 后端实施验证

本文件追加实施证据，原 verification.md / evidence.json 保留 2026-10-29 规划时快照。此前用户要求先做后端；2026-10-30 又授权当前模型自行设计、按 Feature 拆分及子 Agent 并行验收。设计不再等待 DeepSeek 凭据；已有独立 FigCraft 草稿，新管理 WPF 接线与任务级交互验收仍未完成。最新复审见 [作品集验收](../../../docs/validation/portfolio-review-2026-10-30/README.md)。下表保留上一轮384项基线，本轮修复与复跑另记该验收报告。

## 工具链及边界

已在用户 `.dotnet` 目录找到稳定 SDK 10.0.302；此前“未发现 .NET 10 SDK”只反映 PATH 检查，现由 global.json、当前进程工具链脚本及 CI 固定。隔离测试在允许 testhost 本机通信的环境正常完成，早先沙箱超时不是业务测试通过或失败的证据。

已建立 Manager.Core / Manager.Cli / Communication.Contracts / Plugins.Contracts。管理内核不引用 RPC/Mcp Core；通信 Core 不引用 Manager.Core、Knowledge.Core、SQLite 或旧 CommandLine。旧 NativeCliHost/NativeOperationCatalog 移到 Cli.Compatibility 组合层，原生 CLI 保留兼容操作；独立进程协议只组合通信与诊断。无存储依赖的 Builtins 被单独抽取，知识程序集保留原公开指南类型的转发。

WPF 既有会话查询、对象读取、注册函数调用和开发流程已改走通信 CLI，不再构造或调用 RPC 实现。初次只读选择冻结发现的 PID/启动时间及 Host 返回模块，后续每次调用携带完整目标；异步发现有共用 busy 状态。选择/状态改变清除目标，丢失回执不重发。注册函数在父进程与 pml.function.invoke 子进程分别核对权限/源码；四种 runtime 子目录共用安装根下的人工信任路径。此处仅为后台接线，新的管理界面和视觉验收仍待设计阶段。

## 当前可复刻结果

运行 `scripts/Test-Manager.ps1 -FullOffline -SevenZipTool <明确路径>`，随后对桌面函数转接及信任路径修正重跑既有全集、桌面编译与架构测试：

| 矩阵 | 结果 | 范围 |
| --- | --- | --- |
| Manager.Tests | 142/142 通过，0 跳过 | 子进程、配置、EVARS、复制/部署恢复、双路知识、迁移、发现、Monitor/BAT、灯态、原型资料、native 7z、插件与 VM 合成任务 |
| 既有测试全集 | 221/221 通过，0 跳过 | 原有知识/HTTP/CLI/工作区/桌面服务，以及新的 CLI 目标绑定、后台发现、对象/函数、unknown、开发流程和包根兼容 |
| 架构测试 | 21/21 通过，0 跳过 | 项目依赖白名单/无环、编译边界、管理与通信互不引用实现、桌面源码禁用 RPC 实现 |
| CLI 示例 | 20/20 通过 | 解析实际 stdout JSON，核对 0/2/3/4、冻结引用、授权、环境恢复及复制闭环 |
| 两个 CLI 与 WPF 构建 | 通过，0 警告/0 错误 | net10.0 / net10.0-windows Release；不代表视觉验收 |
| 操作导出 | 原生 46 / 管理器 52 | Core 分离前后原生 46 个 descriptor 均无变化 |

安装器 NET48 测试重跑通过 9 个测试组。生产环境文件未被写入。隔离部署覆盖复制后文件占用、阶段故障、来源/目标变化、条件恢复和未解决的外部修改。安装计划冻结唯一入口及有效环境文件内容；多版本、多入口、不可读 INIT 和预览后入口替换均有拒绝验证。管理 CLI stdin 保存/启动隐藏合成 BAT 的闭环回执始终为 entry-process-created-host-not-verified。

具体本机产物位于 `artifacts/manager/tests/*.trx`、`operation-inventory.json`、`cli-examples.json`、`compatibility-after-core-split.json` 和 `offline-run.json`。384 个测试均通过；65 项旧兼容子集包含在既有全集中，不重复计数。它们是忽略的验证产物，不进入程序发行包。OpenSpec 严格验证通过；四份新操作文档的本地链接无断链，git diff --check 通过。

本次明确选择已有 `D:/VirtualMachines/LaunchDesk/Tools/7zr.exe` 仅处理合成压缩包，未启动 VM，也未改变原二进制。测试含实际 CRC 损坏、空根、依赖缺失、包/工具变化和管理 CLI 闭环；没有依赖的 CI 会明确跳过两个 native-format 用例。复制事务进一步修正了部分恢复向上层传播及成功后清理警告，原 PML 入口保持扩展名选择，管理器明确工具策略允许 EXE/脚本并拒绝数据库目录与路径别名。

## 可选包与可审阅程序包

许可包只做 TCP connect 和只读 Windows 服务查询；可达/运行与 checkoutState=unverified 分层。基础启动无插件依赖；显式 required 策略下，缺失/停用/损坏、端口/服务失败或要求无法提供的 checkout 证明均在创建入口前拒绝，预检后再次核对配置修订。旧编辑器保存不会悄悄清除策略。

VM 包迁移 RemoteProjectLab 的允许根、UUID/MAC、Host-only、快照与输出绑定；发现/计划/模拟不加载 SDK。有状态执行需三个可信授权和完整确认指纹，先持久化意图再执行。合成 VMX/VMSD/假 DLL 与注入适配器覆盖身份/网络/快照/输出/厂商库变化、超时 unknown、同 attemptId 不重发和本插件 DPAPI 凭据绑定。实际 VMware/VIX 行为尚未实机验收。

`Build-ManagerPlugins.ps1 -Plugin license,vm` 已发布独立验证包到 `artifacts/manager/plugins/20261029T180909-a5f2bda5`（许可 3 个、VM 6 个操作）。独立许可进程经临时回环端口诊断，独立 VM 工作包经临时登记运行 simulation；均不代表实际许可或 VM 任务通过。

`Build-Manager.ps1` 已发布管理 CLI 到 `artifacts/manager/packages/20261029T183413-92292efa/manager`，含 40 个清单文件、52 个操作，要求 Windows x64/.NET 10 运行时。基础包依赖检查拒绝 RPC、兼容组合层、许可/VM 工作包以及用户库/镜像，逐文件 SHA256 已生成，发布后 list 和空插件登记读取通过。它不是包含完整 Host/PML 载荷的新安装包，尚未替换本机安装。

## 双版本真实资料

用户选定两套分别导入；只读已安装产品的明确资料目录，在工作区 `artifacts/manager/local-knowledge` 发布本机验证库。默认用户目录和安装程序未切换。

| 产品版本 | 所选资料 | 候选文件 | 文本来源 | 片段 | 跳过二进制 |
| --- | --- | ---: | ---: | ---: | ---: |
| E3D 2.1 | Everything3D2.10/PMLLIB、PMLUI | 14,052 | 11,392 | 50,620 | 2,660 |
| AM 12.1.SP4 | OH12.1.SP4/PMLLIB、PDMSUI | 15,287 | 12,627 | 51,300 | 2,660 |

库身份分别为 `00c4b527893f464ba91a8ab8ec2403d6` 与 `4a9390c7f9314cc9a44ef784c27198c6`。各自 SQLite 完整性、v1 结构、关系、FTS 行/正文/内部一致性、MATCH、manifest 计数和逐源字节 SHA256 均通过。独立复查各实际命中 5 个抽样 chunk；独立 `define` 检索各返回 3 条带本库身份的结果，默认来源状态明确为 not-rechecked。

真实检查/检索及候选计划保存在 `artifacts/manager/real-import`；发布库在 `local-knowledge/Libraries/E3D/2.1/installed-pml` 和 `local-knowledge/Libraries/AM/12.1.SP4/installed-pml`。本机原文、索引正文未上传或加入 Git。AM Documentation 中的 CHM 不在本次 HTML/HTM 支持格式内，没有声称它已导入。

## 尚未验收

未启动真实 AM/E3D 工程、执行 PML、修改 AVEVA 数据、运行 VM 或应用真实安装更新。连接绿色只在合成 Host 夹具验证，不能作为产品实机结论。本轮已生成00共享基础与9个独立 Feature 草稿，并经独立抽查；完整函数任务链、逐按钮输入/授权/回执映射、新管理页面、实际产品验收、完整安装包/更新预览及人工合同审阅仍未完成。contract.json 未据合成测试批量变更状态。
