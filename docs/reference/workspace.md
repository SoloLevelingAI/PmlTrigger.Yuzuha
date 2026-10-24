# 原生工作台、PML 开发与日志

当前分支为 **0.3.4.2**。正式 WPF 启动工作台已接入同一配置用例服务，支持产品模块联动、组合筛选、预览、保存、重新读取、历史与恢复。下面说明实际界面流程；[工作台生命周期](workbench-lifecycle.md)说明接口、场景测试和证据。原有 [R2](phayton-r2-production.md)页面属于此前界面记录。

`YuzuhaToolkit.Desktop.exe` 是 Windows C# / WPF 程序，日常启动、复制和状态查看不需要 Python。它使用 AVEVA Monitor / Marine 启动协议、安装目录发现和 Windows 凭据 API。用户不需要安装 LaunchDesk 或导入其配置。AI 与自动化使用 `runtime/cli/PmlTwin.Phayton.Cli.exe`，直接调用底层库及 Host；独立 MCP 服务已禁用。

## 启动工程

1. 在“工程 → 启动工程”新建配置，选择本机安装入口，核对产品和版本，再填写名称、项目、用户名与 MDB。程序入口、初始化文件及工作目录可由安装发现填入，也能在“安装路径与入口类型”中修改。
2. 产品决定模块目录。E3D 使用 Design / Draw 等；AM 使用 Outfitting / HullDesign / OutfittingDraft / MarineDrafting 等，不包含 Design；PDMS 使用 Design / Draft 等。输入不属于产品的模块会在预览或保存时被拒绝。目录会区分安装元数据证实和仅产品映射的选项，列出模块不代表许可或登录已验证。
3. 点击“预览变更”查看字段差异和校验结果，再点击“保存配置”。配置立即持久化，重开程序或“重新读取”后仍可使用。保存不会启动 AVEVA；配置只在下次启动时生效，当前会话保持原样。
4. 左侧通过产品下拉、全部 / 收藏 / 最近下拉和关键词共同筛选，可查名称、工程、用户、模块或入口路径。收藏、复制和设为默认使用同一修订存储；复制配置不复制已存密码引用。
5. 在“配置历史与恢复”选择修订，先“预览恢复差异”，再“恢复所选修订”并确认。恢复创建新修订，保留后续历史和其他配置；不会启动工程，也不会找回已经删除的凭据。
6. 如果 CLI 或另一窗口先修改了文件，保存会拒绝覆盖并保留编辑草稿。先核对提示，再重新读取；放弃未保存的修改需要确认。关闭窗口和切换配置也会检查未保存内容。

上述流程已通过 0.3.4.2 的实际控件与文件服务验证，使用隔离状态目录和合成入口，未启动 AVEVA。模块的实机可用性与 Host 验证另行记录。Host 分配规则是 E3D 2.1 → E3D2.1、E3D 3.1 系列 → E3D3.1.6、AM / PDMS → NET35。

高级区保留许可、环境覆盖和启动动作。MDB 可输入或选择已有值。当前工作台不要求 LaunchDesk 数据；已有 Phayton 配置由版本存储兼容读取，后续修改保存为可回溯修订。开发、JSON/TOML 场景、运行报告以及代码接口见[生命周期契约](workbench-lifecycle.md)和[原生 CLI](cli.md)。

清除已存密码只在“设置 → 已保存的登录密码”中操作，选配置后再次确认，弹窗默认取消。配置本身保留；保存失败恢复原关联，共用凭据不会因清除单个关联而误删。启动页取消“记住本次输入的密码”只影响本次新密码的保存，不暗中清除已有凭据。

“Web 应用”页的“打开 HTML 管理器”会启动或复用独立的 `127.0.0.1:5288` 本地服务并用浏览器打开；桌面不内嵌 WebView。同一个入口也可从原生 CLI 的兼容命令使用：`PmlTwin.Phayton.Cli.exe --cli web open`，追加 `--no-browser` 时只输出地址。打开入口不执行 PML，网页调用仍需明确选择并核验 AVEVA 会话。

“部署”页保留原有仅复制操作，并新增“验证清单”与“预览 / 复制并验证”。选择已核验会话、源码目录、该会话实际 PMLLIB 及 JSON 清单后，预览列出来源/目标散列和预期；执行依次检查会话/PMLLIB、备份复制、`PML REHASH ALL`、Object/Form 重载、运行自有验证宏、读取格式化数组并精确核对结果。失败或未知即停止，不自动重发。证据和备份位于 PMLLIB 外。示例使用 `tests/ai-pml/development-probe` 和 `tests/live/pml-development/manifest.json`；不需要 PMLCMD。

启动高级配置可继承环境、每次 REHASH ALL 或明确关闭特殊动作。`YUZUHA_STARTUP_ACTIONS=rehash-all` 在原生 Addin 的 PML 初始化之后、Host 建立之前生效；未设置或 `none` 保持默认。桌面显式选择通过 `YUZUHA_STARTUP_OVERRIDE` 优先于 INIT/BAT 默认值。环境变量只在新进程中生效；已经打开的客户端不会回溯执行。这里的“启动前”指 Host 建立前，PML 命令无法在 AVEVA 进程启动前运行。

特殊启动先写开始记录，再执行动作和记录结果；每个 PML 会话最多执行一次。桌面启动日志保存策略、关联 ID 和日志路径，原生日志保存请求、阶段及实际结果。桌面发起时使用 `%TEMP%/YuzuhaStartup-<关联ID>.log`，外部启动且未指定路径时追加 `%TEMP%/YuzuhaStartup.log`；`!!YuzuhaStartupReport` 可核对本次结果。日志无法开始写入时不执行 REHASH；动作失败不自动重试，不保存模型。未知值不解释为任意 PML。

从本机注册表和受限安装目录遍历发现产品入口，包括 Marine 的 OH 版本子目录及旧 PDMS 批处理入口；也可以手动选择程序。安装入口与正在运行的工程会话分别列出，标题包含 AVEVA 的 LaunchDesk 不作为工程会话。

保存产品代码、初始化文件、工作目录、项目、用户、MDB、模块与许可。默认勾选“在本机凭据库记住密码”：保存配置或保存并启动后，密码进入当前 Windows 账户的凭据管理器，JSON 只存本应用引用；下次启动密码框留空会使用已存凭据。输入新密码可更新，取消勾选不删除之前的凭据，解除关联需在设置中明确清除。不会读取、迁移或删除 LaunchDesk 的凭据。预览和日志不包含密码正文。Monitor 通过原生参数列表处理空格，Marine 按厂商批处理参数协议启动。

许可和用户自定义环境只传给子进程，不改系统环境或 EVAR。启动前检查选定 PMLLIB；达到清理预算时显示提醒，由用户决定先清理还是继续。创建启动入口进程不等于工程已登录；工作台与 CLI 仍须发现并核验实际会话。

## 插件目录与清理提醒

“已注册 PMLLIB”应指向 EVAR 中实际注册的目录。工作台读取已安装产品对应的 EVAR 字面赋值和当前进程 PMLLIB，不执行这些脚本；只找到一个有效候选时会填入供查看，多个候选由用户选择。不能解析的变量表达式不猜测为已注册路径。

目录扫描分别统计全部文件与 PML 文件的数量、磁盘字节、按类型/直属文件夹的占用，以及忽略大小写的同文件名同扩展名重复候选组数和多出文件数。索引、日志和备份不计入 PML 源文件占用；同名候选不等于内容相同或实际加载冲突。桌面以“PMLLIB 目录统计”替代健康度圆环，显示最多 20 个目录和 20 组重复候选（每组 10 个路径），完整已扫描结构可通过 CLI 操作 `workspace.pmllib.inspect` 取得。源码字节不代表方法运行内存。统计口径见[能力核对](runtime-capability-audit.md)。

扫描不跟随联接；时间、访问条目和深度都有上限。扫描不完整时明确显示下限。默认提醒预算为 **3,000 文件或 200 MiB**，可在界面调整；`.git`、`node_modules`、编译输出、知识库、日志或备份混在注册目录中也会提醒。这些是开发工作区预算，不是厂商规定的上限，不能保证 `PML REHASH ALL` 完成时间。

把开发副本放在 PMLLIB 外，再选择源目录、单文件或 ZIP，预览后复制到注册目录中的插件子目录。源和注册目录不能相互包含。复制跳过开发产物与旧 `pml.index`；同名 ZIP 根目录会展开一层。覆盖前备份位于本机状态目录 `Deployments`，复制失败时恢复本次已应用内容；并发修改会停止并报告需要核对的路径。旧插件不会自动删除，“打开目录清理”供用户整理。

桌面原有“仅复制”动作不执行 REHASH；新增“复制并验证”流程以及独立[开发脚本](pml-development-flow.md)将复制、REHASH ALL、Form/Object 重载和结果核验串联，并在 E3D 合成夹具上实测。二者使用不同按钮，执行前均可查看具体文件计划。

## AI 运行状态与调用日志

原生 CLI 与工作台记录实际操作的开始、关联 ID、本机进程 PID、目标 AVEVA PID、有界参数、耗时、完成或失败。CLI 直接调用本地操作服务，不启动 MCP 子进程；业务失败、授权拒绝、存储冲突与 Host 失败返回结构化结果。取消或超时标为结果待核验，不触发重试。

默认目录：`%LOCALAPPDATA%\YuzuhaToolkit\Activity\<服务进程会话>`。每进程最多保留八段、每段约 1 MiB 的滚动 JSONL，以及每两秒更新的状态快照。`YUZUHA_STATE_DIR` 可统一更改桌面端和服务端的状态根目录。命名的密码/token 字段会脱敏；任意 PML 文本仍按开发代码记录，可能包含工程信息。完整元素数组不写入调用日志。

工作台区分执行中、空闲、停止、心跳过期及进程异常退出后的结果待核验，并显示日志写入故障。这里观察的是 Yuzuha 工具执行；AI 在客户端分析或生成代码时可能没有工具活动。原有 Host `BeginLog` / `EndLog`、截图与 JSONL 继续保留，二者并未自动关联成逐帧 AI 分析历史。

AI 也可通过 CLI 操作 `runtime.status`、`runtime.calls`、`workspace.pmllib.inspect` 读取这些信息；先用 `op describe <操作 ID>` 查看输入契约。

## 原生 PML 与撤销分组

CLI 的 `pml.command.execute` 直接把 PML 交给已核验 Host 的主线程；`pml.array.read` 处理返回数组。执行入口要求相应权限和实际目标 PID；开发任务授权覆盖范围内的连续编辑、加载和验证，不要求每条命令重新审批。保留会话身份核验和超时不自动重试。

需要撤销分组时，可在已授权的 CLI 批处理中使用以下操作；输入示意中的 PML 需替换为具体任务代码：

```json
[
  {"operationId":"pml.undo.begin","input":{"description":"MyPlugin development"}},
  {"operationId":"pml.command.execute","input":{"pmlCommand":"<本次开发需要的 PML>"}},
  {"operationId":"pml.undo.end","input":{}}
]
```

先发现并核验实际 AVEVA PID，批处理通过 `--pid` 和 `--allow-execution` 指定目标与执行许可。`pml.undo.begin` 发送 `MARKDB '描述'`，`pml.undo.end` 发送 `ENDMARKDB`；描述中的引号会转义，控制字符被拒绝。这是 AVEVA 的撤销分组，不会自动保存、撤销或在失败后重发结束命令。批处理在首个失败处停止。语义参见 [AVEVA Undo and Redo](https://help.aveva.com/AVEVA_Everything3D/2.1/PMLPUG/PMLCG9.09.10.html)。

旧 `YuzuhaExcuter` 对象作为兼容路径保留，原生执行无需先调用它或使用危险命令绕过接口。当前元素、DBREF 和全局对象查询继续优先使用已有内置查询方法。

## 构建

```powershell
dotnet build src/YuzuhaToolkit.Desktop -c Release
./scripts/Build-Desktop.ps1
```

发布的 Windows x64 工作台为自包含 C# 程序；`PmlTwin.Phayton.Cli.exe` 使用 Native AOT。新包不分发独立 MCP 程序，安装器也不注册 MCP。测试和历史构建脚本按维护需要保留，不成为产品运行依赖。
