# PMLTWIN v0.4 Form / Gadget 查询批次

日期：2026-10-21（Asia/Shanghai）。接续基线 `b3f4350f05fbf42de6c53395e472872522d5a557`；独立分支 `codex/pmltwin-v04-phase1`，PR #5 保持草稿供人工审查，目标仍为 `codex/yuzuha-native-workspace`。

本机结果针对提交前工作区，`dirty=true`，不把基线提交当作已包含本次功能。提交后的 CI 单独记录检出 SHA、Windows 环境、SDK 和运行地址；静态测试详情不把本机或历史结果自动显示为当前已通过。

## 实现与复用

新增 `list_pml_forms`、`list_pml_gadgets`、`get_pml_gadget`，沿用原生 PMLFORMS 的 FMSYS / attributes / attribute / objectType / set 路径。继续使用现有 C# Host 主线程、PID/启动时间/模块核验、RPC、共享预算验证与 LaunchDesk 文档测试机制，没有新增生产项目或窗体管理器。测试加载与读取继续使用 YuzuhaExcuter / YuzuhaReadGlobal；自有测试文件通过已有 C# PluginDeployment 预览/回执方式放入独立子目录。

查询不调用 Show/Hide/Kill/Load/Refresh，不装饰或执行回调，不读取 val 或 CONTAINER/WPF 内部对象。旧 Host 在发送查询前拒绝；新 Host 拒绝与原始命令、数组删除混用。原生稀疏空索引、普通成员、未设置 GADGET 仍占扫描预算，但不返回为控件。

先加入查询测试，再实现；新增非 FORM、临时 ARRAY 容器保护也先出现断言失败。实机发现变量类型冲突、变量名大小写和稀疏属性数组后，分别补回归或实机断言再修复。原生 STRING、REAL、BOOLEAN 临时变量分开存放，名称大写，结束时全部清理；有界数组读取的两个 Host 同步修正大写变量名。证据保留在本机 `artifacts/tests/phase2-{red-build,query,array-container-red,native-temporary-red,native-sparse-red}.log` 和 `artifacts/live-v04/array-red/`，失败没有改写成成功。

## 本机验证

| 检查 | 结果 | 证据与范围 |
| --- | --- | --- |
| xUnit 产品 / 真实 MCP / CLI | 103 passed，0 failed，0 skipped | `artifacts/tests/phase2-development.log`、development TRX、run-metadata.json；本批次新增 24 次执行、17 个方法 |
| ArchUnitNET 与架构守卫 | 11 passed，0 failed，0 skipped | 同上；开发 HTTP 处理器不得依赖新增查询执行入口 |
| 文档关联 / Roslyn | 8 passed；TRX 正例接受、4 个反例拒绝 | 完整 Test-Development；测试身份、源码、目的与失效关联检查 |
| NET35 生产 Host 编译 | passed，0 warning / error | `artifacts/tests/phase2-host35.log`；Marine 12.1.SP4 引用，显式 AM profile |
| NET48 生产 Host 编译 | passed | `artifacts/tests/phase2-host48.log`；E3D 2.1 引用，显式 E3D2.1 profile |
| Native AOT / Framework Smoke | 六项全部 passed | 下列按 ID 执行回执；协议合成夹具不代替 AVEVA 实机 |
| 完整 DocFX / OpenAPI / LikeC4 | passed，0 warning / error | `artifacts/tests/phase2-documentation.log`、`artifacts/docfx/integrity.json`；96 个测试入口、81 个 xUnit 方法、556 页、5169 个本地链接 |
| E3D 2.1 合成实机 Smoke | passed，范围见下文 | `artifacts/live-v04/e3d-final2/run.json`，2026-10-21 08:00:09–08:00:48 UTC |

环境：Windows build 26200 x64、.NET SDK `10.0.400-preview.0.26322.102` / Runtime `10.0.9`、PowerShell `7.6.5`、Node.js `24.18.1`、MSVC `14.51.36231`、Windows SDK `10.0.26100.0`。本批次使用本机依赖缓存，没有通过 Clash 下载或更改代理。Native AOT 采用独立本机构建目录避开已占用输出；录制器合成窗口测试在有桌面句柄的执行环境复测通过，先前的句柄失败仍留有独立 Failed 回执。

六项通过回执均为当前工作区运行，`dirty=true`：

| 测试 ID | artifacts/dt 下的回执目录 |
| --- | --- |
| smoke:native-cli | b3ec4f83dfa6864f/2433520661c2410ca2c2e613ac743fbd |
| smoke:native-builtin | 3d78b5e4e467f5e4/74f88b024f324f348924e32d2c1cb037 |
| smoke:rpc-net35 | d1572be8ceb08954/a8febe8194f44c22b5f299bbd0bc8f8b |
| smoke:rpc-net48 | 806f96243c635776/8960f2502c3e4fe6a203e0a4739db8a8 |
| smoke:formrecord-net35 | 2cd20125de363ddd/0f77928be17b461b9717d22525917761 |
| smoke:formrecord-net48 | eab77887ea4b93b8/c2e5c5fe5c8e45b99ca7aa19054345f2 |

每个目录包含 documented-run.json，注明测试 ID、源码 SHA256、环境、提交、dirty、时间和证据文件。RPC 使用生产 DTO 和实际 Framework 上的共享读取策略，原生返回值由合成夹具提供。可通过 `scripts/run-documented-test.ps1 -TestId <ID>` 独立复刻，不将零匹配视为成功。DocFX 本机运行完整原脚本，仅把工具还原命令指向本机缓存配置；临时副本在 `artifacts/Build-Documentation.Offline.ps1`，仓库脚本未改。

## E3D 2.1 实机范围

按用户指定启动 MAR、SYSTEM、`/ALL_NO_MDS`、design。先前错误启动使用了不适合 Monitor 的初始化文件、临时工作目录和受限进程链。恢复原始 launch.init、USERDATA 工作目录并复用 LaunchDesk 的独立进程启动代码后，正常进入 `mar | ALL_NO_MDS | Model`；隔离测试副本保留相同工作目录与启动方式，也正常进入。不能把此前通信文件错误归因于用户截图中的原配置或 MDB 无效。凭据只在本机启动边界使用，不写入报告或运行日志。

最终测试会话 PID `27540`，启动 ticks `639281661774392000`，NET48 Host，FormQueryVersion=1、BoundedArrayReadVersion=1。隔离镜像 SHA256 为 `370940F41F9366456986D7EFD048E8508253BE7E6FD6FBB1280DF387A65BC761`；Host 版本仍为开发基线 `0.3.2.0`。Host 模块字段仍报告 Unknown，窗口与入口为 E3D Design；这是既有身份信息限制，没有改写为已验证的模块名称。

Git 管理的纯计算 PMLFNC 实际返回 `2+3=5`；专用 PMLFRM 在自己的开发目录编辑，再复制到隔离 PMLLIB 子目录。脚本先比较源码与部署文件 SHA256，使用内置执行器执行 REHASH ALL 和 RELOAD FORM，再检查执行器 Query()。生产 MCP 实际验证：

- 数组三行 alpha/beta/gamma 分两页读取，内容、上界、NextIndex 与末端一致。
- 已加载窗体清单包含测试窗体；shown 清单不包含未显示测试窗体。
- 属性上界 32，完整枚举返回 LIST、BUTTON、TOGGLE、FRAME、TEXT 共五个已设置控件，跳过稀疏索引、MENU、普通成员与未设置 KEYBOARDFOCUS；两槽位分页保持原始索引。
- BUTTON/TEXT/TOGGLE/LIST 的类型、shown、active、visible、callback 文本可读；这四个控件的 tooltip 均明确 unavailable，保留原生“Object does not have a member TOOLTIP”、命令和行位置。
- 普通 STRING 成员的详情请求和首项超字符预算均明确失败。查询前后计数器仍为 0，文本、开关值、回调文本和显示状态一致。

可复刻入口：`tests/live/form-query/Invoke-FormQuerySmoke.ps1`，参数与准备步骤见同目录 README。它保留每次请求、响应、RequestId、stderr、宏、身份和源码/镜像哈希，只写本机 artifacts。首次测试脚本将 YuzuhaReadGlobal 的根 ARRAY 行也计入成员数量，断言失败；核对原始响应后按 Depth=1 的五个成员修正，重新完整运行通过，没有修改内置读取函数。

所有操作限于自有测试文件、窗体和内存变量，没有执行工程数据库修改或保存命令。正式安装未替换，PMLLIB/PMLUI 原生源文件未改；单独测试进程退出时不保存工程。临时镜像与证据保留本机供检查，不上传用户工程内容。

## 仍未完成的验收

NET35 新查询实机、CONTAINER/WPF、显示后交互、所有 PMLFORMS 人工对照、跨请求动态变化、完整临时变量残留核对等尚未完整执行。`manual:aveva-form-query`、`manual:aveva-gadget-query` 保持 **NotRun / 未完整验收**；此前六个 AVEVA 人工案例不改变状态。单次 E3D 合成 Smoke 不扩展成所有产品或属性均已通过。

后续 HTML/Web 应用管理按用户确认由 C# 记录目录、配置、版本/状态、部署回执与日志；页面通过现有 RPC 调用原生 FORM/PML Object。本批次尚未实现管理页。正式更新继续沿用已有 Inno Setup EXE / SetupBridge，发布审核与产物清单属于后续交付，不以开发脚本冒充已完成的 CI/CD 更新渠道。

额外 Hash 指纹检查仍有 17 项基线差异（安装器 12、PML 5），没有新增类别；本批次仅更新改动的两个 Host 项目指纹。`git diff -- PMLLIB PMLUI` 为空，不重写原生源文件或历史指纹掩盖差异。该额外检查不登记通过，本机明细为 `artifacts/tests/phase2-semantic-status.json`。
