# 原生工作台、PML 开发与日志

`YuzuhaToolkit.Desktop.exe` 是 Windows C# / WPF 程序，日常启动、复制和状态查看不需要 Python。它复用 LaunchDesk 的 Monitor / Marine 启动协议、安装目录发现和 Windows 凭据 API，把实际操作接到同一 GUI。

## 启动工程

从本机注册表和受限安装目录遍历发现产品入口，包括 Marine 的 OH 版本子目录及旧 PDMS 批处理入口；也可以手动选择程序。安装入口与正在运行的工程会话分别列出，标题包含 AVEVA 的 LaunchDesk 不作为工程会话。

保存产品代码、初始化文件、工作目录、项目、用户、MDB、模块与许可。默认勾选“保存密码到本机凭据”：保存配置或保存并启动后，密码进入当前 Windows 账户的凭据管理器，JSON 只存本应用引用；下次启动密码框留空会使用已存凭据。输入新密码可更新，取消保存或清除已存密码解除关联。不会读取、迁移或删除 LaunchDesk 的凭据。预览和日志不包含密码正文。Monitor 通过原生参数列表处理空格，Marine 按厂商批处理参数协议启动。

许可和用户自定义环境只传给子进程，不改系统环境或 EVAR。启动前检查选定 PMLLIB；达到清理预算时显示提醒，由用户决定先清理还是继续。创建启动入口进程不等于工程已登录；MCP 仍通过发现和选择实际会话工作。

## 插件目录与清理提醒

“已注册 PMLLIB”应指向 EVAR 中实际注册的目录。工作台读取已安装产品对应的 EVAR 字面赋值和当前进程 PMLLIB，不执行这些脚本；只找到一个有效候选时会填入供查看，多个候选由用户选择。不能解析的变量表达式不猜测为已注册路径。

目录扫描统计全部文件、PML 数量、总大小、大目录和扩展名，不跟随联接；时间、访问条目和深度都有上限。扫描不完整时明确显示下限。默认提醒预算为 **3,000 文件或 200 MiB**，可在界面调整；`.git`、`node_modules`、编译输出、知识库、日志或备份混在注册目录中也会提醒。这些是开发工作区预算，不是厂商规定的上限，不能保证 `PML REHASH ALL` 完成时间。

把开发副本放在 PMLLIB 外，再选择源目录、单文件或 ZIP，预览后复制到注册目录中的插件子目录。源和注册目录不能相互包含。复制跳过开发产物与旧 `pml.index`；同名 ZIP 根目录会展开一层。覆盖前备份位于本机状态目录 `Deployments`，复制失败时恢复本次已应用内容；并发修改会停止并报告需要核对的路径。旧插件不会自动删除，“打开目录清理”供用户整理。

复制不自动执行 REHASH 或启动 AVEVA。开发流程可以在确认目录内容后通过原生 PML 工具进行必要的重载和验证。

## AI 运行状态与调用日志

两个 MCP 服务的所有实际工具调用都经过同一记录管线，CLI 使用同一服务管线。每次记录开始、关联 ID、服务 PID、选定 AVEVA PID、有界参数、耗时、完成或失败。业务 JSON 失败、`PML RPC failed:` 以及 MCP 错误分别被识别；取消或超时标为结果待核验，不触发重试。

默认目录：`%LOCALAPPDATA%\YuzuhaToolkit\Activity\<服务进程会话>`。每进程最多保留八段、每段约 1 MiB 的滚动 JSONL，以及每两秒更新的状态快照。`YUZUHA_STATE_DIR` 可统一更改桌面端和服务端的状态根目录。命名的密码/token 字段会脱敏；任意 PML 文本仍按开发代码记录，可能包含工程信息。完整元素数组不写入调用日志。

工作台区分执行中、空闲、停止、心跳过期及进程异常退出后的结果待核验，并显示日志写入故障。这里观察的是 Yuzuha 工具执行；AI 在客户端分析或生成代码时可能没有工具活动。原有 Host `BeginLog` / `EndLog`、截图与 JSONL 继续保留，二者并未自动关联成逐帧 AI 分析历史。

AI 也可调用 `get_ai_runtime_status`、`get_recent_tool_calls`、`inspect_registered_pmllib` 读取这些信息。

## 原生 PML 与撤销分组

`run_pml_command` 直接把 PML 交给已核验 Host 的主线程；`run_pml_command_list` 处理返回数组。开发任务授权覆盖范围内的连续编辑、加载和验证，不要求每条命令重新审批。保留会话身份核验和超时不自动重试。

先查询当前执行授权：

```json
[
  {"name":"get_execution_authorization","arguments":{}}
]
```

先按 [PML 开发文档](pml-development-plan.md) 配置明确的开发会话和三档授权。更新后的 Host 对每次非只读执行自动建立并结束 MARKDB 分组，失败不会重放，不再用手工 `markdb/endmarkdb` 嵌套包裹。MARKDB 是撤销分组，不是保存或回滚；参见 [AVEVA Undo and Redo](https://help.aveva.com/AVEVA_Everything3D/2.1/PMLPUG/PMLCG9.09.10.html)。

旧 `YuzuhaExcuter` 对象作为兼容路径保留，原生执行无需先调用它或使用危险命令绕过接口。当前元素、DBREF 和全局对象查询继续优先使用已有内置查询方法。

## 构建

```powershell
dotnet build src/YuzuhaToolkit.Desktop -c Release
./scripts/Build-Desktop.ps1
```

发布的 Windows x64 工作台为自包含 C# 程序；两个 MCP 程序仍使用 Native AOT。测试和历史构建脚本按维护需要保留，不成为产品运行依赖。
