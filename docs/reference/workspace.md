# 原生工作台、PML 开发与日志

当前 0.3.4 的正式界面为 [Phayton R2](phayton-r2-production.md)：启动工程、知识库和设置按任务分组；以下调用、配置和数据约定继续适用。

`YuzuhaToolkit.Desktop.exe` 是 Windows C# / WPF 程序，日常启动、复制和状态查看不需要 Python。它复用 LaunchDesk 的 Monitor / Marine 启动协议、安装目录发现和 Windows 凭据 API，把实际操作接到同一 GUI。

## 启动工程

日常启动保留浅色默认，只需选择产品与模块，再填写项目、用户名、密码和 MDB。模块可选择 DESIGN、PARAGON、DRAW、Outfitting 或 OutfittingDraft，也保留手动输入；MDB 可填写或选择已有配置的历史值。程序入口、初始化文件和工作目录由产品选择自动填入；名称可自动生成。路径、产品代码、许可和启动参数收在折叠的高级设置/诊断区。

“从 LaunchDesk 带入”只读用户选择的 `Data/workspace.json`，选择一项带入未保存草稿，沿用其产品路径、项目、用户名、MDB 和模块。不读取 LaunchDesk 密码或凭据，不执行工具部署或导入其环境变量；密码需在 Yuzuha 首次输入，许可沿用环境，可在高级设置修改。保存或启动前仍可编辑，没有点击保存不会写入配置库。

清除已存密码只在“设置 → 已保存的登录密码”中操作，选配置后再次确认，弹窗默认取消。配置本身保留；保存失败恢复原关联，共用凭据不会因清除单个关联而误删。启动页取消“记住本次输入的密码”只影响本次新密码的保存，不暗中清除已有凭据。

“Web 应用”页的“打开 HTML 管理器”会启动或复用独立的 `127.0.0.1:5288` 本地服务并用浏览器打开；桌面不内嵌 WebView。同一个入口也可从 CLI 使用：`YuzuhaToolkit.Mcp.exe --cli web open`，追加 `--no-browser` 时只输出地址。打开入口不执行 PML，网页调用仍需明确选择并核验 AVEVA 会话。

“部署”页保留原有仅复制操作，并新增“验证清单”与“预览 / 复制并验证”。选择已核验会话、源码目录、该会话实际 PMLLIB 及 JSON 清单后，预览列出来源/目标散列和预期；执行依次检查会话/PMLLIB、备份复制、`PML REHASH ALL`、Object/Form 重载、运行自有验证宏、读取格式化数组并精确核对结果。失败或未知即停止，不自动重发。证据和备份位于 PMLLIB 外。示例使用 `tests/ai-pml/development-probe` 和 `tests/live/pml-development/manifest.json`；不需要 PMLCMD。

启动高级配置可继承环境、每次 REHASH ALL 或明确关闭特殊动作。`YUZUHA_STARTUP_ACTIONS=rehash-all` 在原生 Addin 的 PML 初始化之后、Host 建立之前生效；未设置或 `none` 保持默认。桌面显式选择通过 `YUZUHA_STARTUP_OVERRIDE` 优先于 INIT/BAT 默认值。环境变量只在新进程中生效；已经打开的客户端不会回溯执行。这里的“启动前”指 Host 建立前，PML 命令无法在 AVEVA 进程启动前运行。

特殊启动先写开始记录，再执行动作和记录结果；每个 PML 会话最多执行一次。桌面启动日志保存策略、关联 ID 和日志路径，原生日志保存请求、阶段及实际结果。桌面发起时使用 `%TEMP%/YuzuhaStartup-<关联ID>.log`，外部启动且未指定路径时追加 `%TEMP%/YuzuhaStartup.log`；`!!YuzuhaStartupReport` 可核对本次结果。日志无法开始写入时不执行 REHASH；动作失败不自动重试，不保存模型。未知值不解释为任意 PML。

从本机注册表和受限安装目录遍历发现产品入口，包括 Marine 的 OH 版本子目录及旧 PDMS 批处理入口；也可以手动选择程序。安装入口与正在运行的工程会话分别列出，标题包含 AVEVA 的 LaunchDesk 不作为工程会话。

保存产品代码、初始化文件、工作目录、项目、用户、MDB、模块与许可。默认勾选“保存密码到本机凭据”：保存配置或保存并启动后，密码进入当前 Windows 账户的凭据管理器，JSON 只存本应用引用；下次启动密码框留空会使用已存凭据。输入新密码可更新，取消保存或清除已存密码解除关联。不会读取、迁移或删除 LaunchDesk 的凭据。预览和日志不包含密码正文。Monitor 通过原生参数列表处理空格，Marine 按厂商批处理参数协议启动。

许可和用户自定义环境只传给子进程，不改系统环境或 EVAR。启动前检查选定 PMLLIB；达到清理预算时显示提醒，由用户决定先清理还是继续。创建启动入口进程不等于工程已登录；MCP 仍通过发现和选择实际会话工作。

## 插件目录与清理提醒

“已注册 PMLLIB”应指向 EVAR 中实际注册的目录。工作台读取已安装产品对应的 EVAR 字面赋值和当前进程 PMLLIB，不执行这些脚本；只找到一个有效候选时会填入供查看，多个候选由用户选择。不能解析的变量表达式不猜测为已注册路径。

目录扫描分别统计全部文件与 PML 文件的数量、磁盘字节、按类型/直属文件夹的占用，以及忽略大小写的同文件名同扩展名重复候选组数和多出文件数。索引、日志和备份不计入 PML 源文件占用；同名候选不等于内容相同或实际加载冲突。桌面以“PMLLIB 目录统计”替代健康度圆环，显示最多 20 个目录和 20 组重复候选（每组 10 个路径），完整已扫描结构可通过目录检查 CLI/MCP JSON 取得。源码字节不代表方法运行内存。统计口径见[能力核对](runtime-capability-audit.md)。

扫描不跟随联接；时间、访问条目和深度都有上限。扫描不完整时明确显示下限。默认提醒预算为 **3,000 文件或 200 MiB**，可在界面调整；`.git`、`node_modules`、编译输出、知识库、日志或备份混在注册目录中也会提醒。这些是开发工作区预算，不是厂商规定的上限，不能保证 `PML REHASH ALL` 完成时间。

把开发副本放在 PMLLIB 外，再选择源目录、单文件或 ZIP，预览后复制到注册目录中的插件子目录。源和注册目录不能相互包含。复制跳过开发产物与旧 `pml.index`；同名 ZIP 根目录会展开一层。覆盖前备份位于本机状态目录 `Deployments`，复制失败时恢复本次已应用内容；并发修改会停止并报告需要核对的路径。旧插件不会自动删除，“打开目录清理”供用户整理。

桌面原有“仅复制”动作不执行 REHASH；新增“复制并验证”流程以及独立[开发脚本](pml-development-flow.md)将复制、REHASH ALL、Form/Object 重载和结果核验串联，并在 E3D 合成夹具上实测。二者使用不同按钮，执行前均可查看具体文件计划。

## AI 运行状态与调用日志

两个 MCP 服务的所有实际工具调用都经过同一记录管线，CLI 使用同一服务管线。每次记录开始、关联 ID、服务 PID、选定 AVEVA PID、有界参数、耗时、完成或失败。业务 JSON 失败、`PML RPC failed:` 以及 MCP 错误分别被识别；取消或超时标为结果待核验，不触发重试。

默认目录：`%LOCALAPPDATA%\YuzuhaToolkit\Activity\<服务进程会话>`。每进程最多保留八段、每段约 1 MiB 的滚动 JSONL，以及每两秒更新的状态快照。`YUZUHA_STATE_DIR` 可统一更改桌面端和服务端的状态根目录。命名的密码/token 字段会脱敏；任意 PML 文本仍按开发代码记录，可能包含工程信息。完整元素数组不写入调用日志。

工作台区分执行中、空闲、停止、心跳过期及进程异常退出后的结果待核验，并显示日志写入故障。这里观察的是 Yuzuha 工具执行；AI 在客户端分析或生成代码时可能没有工具活动。原有 Host `BeginLog` / `EndLog`、截图与 JSONL 继续保留，二者并未自动关联成逐帧 AI 分析历史。

AI 也可调用 `get_ai_runtime_status`、`get_recent_tool_calls`、`inspect_registered_pmllib` 读取这些信息。

## 原生 PML 与撤销分组

`run_pml_command` 直接把 PML 交给已核验 Host 的主线程；`run_pml_command_list` 处理返回数组。开发任务授权覆盖范围内的连续编辑、加载和验证，不要求每条命令重新审批。保留会话身份核验和超时不自动重试。

两个专用 MCP 工具：

```json
[
  {"name":"markdb","arguments":{"description":"MyPlugin development"}},
  {"name":"run_pml_command","arguments":{"pmlCommand":"<本次开发需要的 PML>"}},
  {"name":"endmarkdb","arguments":{}}
]
```

先在同一 MCP 会话选定实际 AVEVA PID，再调用这些工具。`markdb` 发送 `MARKDB '描述'`，`endmarkdb` 发送 `ENDMARKDB`；描述中的引号会转义，控制字符被拒绝。这是 AVEVA 的撤销分组，不会自动保存、撤销或在失败后重发结束命令。语义参见 [AVEVA Undo and Redo](https://help.aveva.com/AVEVA_Everything3D/2.1/PMLPUG/PMLCG9.09.10.html)。

旧 `YuzuhaExcuter` 对象作为兼容路径保留，原生执行无需先调用它或使用危险命令绕过接口。当前元素、DBREF 和全局对象查询继续优先使用已有内置查询方法。

## 构建

```powershell
dotnet build src/YuzuhaToolkit.Desktop -c Release
./scripts/Build-Desktop.ps1
```

发布的 Windows x64 工作台为自包含 C# 程序；两个 MCP 程序仍使用 Native AOT。测试和历史构建脚本按维护需要保留，不成为产品运行依赖。
