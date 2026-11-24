# 独立 PmlTwin 首次安装与配置

适用于已安装 AVEVA E3D、第一次使用 PmlTwin 的 Windows x64 电脑或虚拟机。本指南对应独立 PmlTwin WPF 0.2.7，由 Phayton 0.3.5.6 安装包交付。安装后从开始菜单打开「Phayton Workspace」。SAG 用于正文检索，可以在登录和会话核验完成后再安装。独立 PmlTwin 可以直接手填登录字段，无需安装工程 Reader。

## 01 选择安装包和测试环境

- 打开 [PmlTwin v0.3.5.6 GitHub 发布页](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/releases/tag/v0.3.5.6)。页面实际显示该版本和资产后再下载。自己操作下载 `Phayton-0.3.5.6-Windows-Setup.exe`。让 AI 协助时下载 `Phayton-0.3.5.6-Windows-Agent.zip`。同时获取该版本的 `SHA256SUMS.txt`。GitHub 自动生成的 Source code 压缩包供开发使用。
- 本测试版以 GitHub 发布页的 SHA-256 核验完整性。安装程序未签名，Windows 可能提示未知发布者。按所在组织或系统规则处理。
- 对照发布的 SHA-256 核对刚下载的文件。若更新已有版本，使用有安装记录的原目录走更新预览。已有文件与安装清单不符时，查看计划列出的差异和备份路径。名称含 `PmlTrigger` 的本机新目录可用于独立测试。
- 在实际运行 E3D 的 Windows 内操作。若 E3D 在虚拟机，将已核对的安装包拷入客户机并在客户机内复核摘要，运行安装器、PmlTwin 和 CLI。需要做两次全新安装实验时，可在首次安装前创建 VM 快照，两个实验之间各自还原。先确认当前 Windows 用户可合法登录目标 E3D 工程。
- 要从空白配置开始，在同一安装中使用一个全新工作区目录即可。旧工作区、系统凭据、AVEVA 用户文件、已有 PMLLIB 和 EVARS 都可以继续保留。需要确认完全无既有 PmlTwin 和 SAG 时，用独立的测试 VM 或 Windows 用户。

```powershell
Get-FileHash -LiteralPath '.\Phayton-0.3.5.6-Windows-Agent.zip' -Algorithm SHA256
```

结果应与 `SHA256SUMS.txt` 中同名资产的记录一致。手动下载 EXE 时对 EXE 执行相同检查。

## 02 安装 PmlTwin 程序

### 人工操作

运行已核对的 Setup EXE，选择程序目录，阅读许可并确认。默认目录为当前 Windows 用户的 `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha`。安装时先正常退出 AVEVA 和正在使用目标目录的旧 PmlTwin。Windows 如提示提权，用受支持的方式批准。安装完成后从向导完成页或开始菜单打开「Phayton Workspace」。

### 交给 AI 操作

将以下整段、下载位置和发布页的 `SHA256SUMS.txt` 一同交给可以在目标 Windows 运行本机终端的 Agent。

> 请在运行 AVEVA 的目标 Windows 内安装独立 PmlTwin。使用我提供的 GitHub v0.3.5.6 发布页、`Phayton-0.3.5.6-Windows-Agent.zip` 和发布 SHA-256。先核对 ZIP 摘要，在新的本机临时目录完整解压。读包内 `START-HERE.md`、`agent-skills/yuzuha-setup/SKILL.md` 及它引用的 `INSTALL.md`，核对 `BUNDLE-SHA256SUMS.txt` 的文件路径及哈希，然后按随包工具生成仅程序安装的计划。先给我看版本、安装路径、已发现的旧安装、变更和备份位置，等我确认。按原计划和摘要执行并核对实际安装回执、桌面和原生 CLI。安装后帮我打开一个新的 PmlTwin 工作区。E3D 登录由我在实际 Windows 窗口里输入密码。需要部署 PMLLIB/PMLUI、切换 EVARS 或安装 SAG 时，每个操作先提供具体预览和目标，由我分别确认。记录每项是未做、已预览、已执行还是已核验。保留现有 AVEVA 工程、配置和 AI 客户端设置。

以下命令由操作方在解压后的包根目录运行。先验证 ZIP 和包内摘要。包内各项路径必须是归属于解压目录的相对文件路径。发现绝对路径、越界项、不匹配的摘要或不同版本的资产，重新从指定发行页取得对应包。

1. 用 `locate` 查已有程序。查看匹配的版本与目录。若升级，先由用户确认选用的安装记录。为本次安装选择路径并在新目录保存计划。

```powershell
$bundle = (Get-Location).Path
$review = Join-Path $bundle 'review'
New-Item -ItemType Directory -Path $review -ErrorAction Stop | Out-Null
$bridge = Join-Path $bundle 'setup\Yuzuha.SetupBridge.exe'
$request = Join-Path $review 'request.json'
$plan = Join-Path $review 'plan.json'
$installDir = Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit\PmlTrigger.Yuzuha'
& $bridge locate (Join-Path $review 'existing.json')
if ($LASTEXITCODE -ne 0) { throw '安装目录定位失败，请读取错误。' }
```

检查 `existing.json` 并确定 `$installDir` 后，继续生成仅程序安装计划。

```powershell
[ordered]@{
    integrationMode = 'none'
    root = $installDir
    mcpJson = ''
    skillTarget = ''
    environments = @()
} | ConvertTo-Json | Set-Content -LiteralPath $request -Encoding utf8
& $bridge plan $request (Join-Path $bundle '.setup-payload.json') `
    (Join-Path $bundle 'skill') $plan
if ($LASTEXITCODE -ne 0) { throw '未生成可执行安装计划，请查看错误。' }
Get-Content -LiteralPath ($plan + '.md')
```

2. 用户确认这份 `plan.json.md` 后，使用同一份 EXE、计划和 SHA-256。若程序、计划或路径有变化，重新出计划。`$setup` 应指向本包唯一且已验证的 Setup EXE。可在交互式窗口中执行这一步，以便处理 UAC 和选择打开工作台。

```powershell
$assets = @(Get-ChildItem -LiteralPath $bundle -File -Filter 'Phayton-*-Windows-Setup.exe')
if ($assets.Count -ne 1) { throw '请使用所选版本的唯一安装程序。' }
$approvedDigest = (Get-Content -LiteralPath ($plan + '.sha256') -Raw).Trim()
if ((Get-FileHash -LiteralPath $plan -Algorithm SHA256).Hash -ne $approvedDigest) {
    throw '计划已改动，请重新生成和确认。'
}
$setupLog = Join-Path $review 'setup.log'
$setupArgs = @(
    '/LANG=chinesesimp'
    "/DIR=`"$installDir`""
    "/PLAN=`"$plan`""
    "/PLANHASH=$approvedDigest"
    "/LOG=`"$setupLog`""
)
$process = Start-Process -FilePath $assets[0].FullName -ArgumentList $setupArgs -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "安装退出码 $($process.ExitCode)，请查本机安装日志。" }
```

3. 读取 `%LOCALAPPDATA%\YuzuhaToolkit\SetupReports\<本次 plan.json 中的 id>\result.json`。确认 `status=success`、实际 `InstallLocation` 和 `runtime\desktop\PmlTwin.Workbench.exe` 是否可启动。只装程序时，`avevaIntegration` 为 `not-requested`。保留报告在本机，外发前去掉内部目录、配置和其他敏感数据。

```powershell
$planId = (Get-Content -LiteralPath $plan -Raw | ConvertFrom-Json).id
if ($planId -notmatch '^[0-9a-fA-F]{32}$') { throw '计划 ID 无效，请核对包和计划。' }
$reports = Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit\SetupReports'
$resultFile = Join-Path (Join-Path $reports $planId) 'result.json'
$receipt = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
$receipt | Select-Object status, avevaIntegration, environmentCount
if ($receipt.status -ne 'success') { throw '安装回执未成功，请留存日志供本机核对。' }
```

## 03 开新工作区并保存第一个登录配置

开始菜单默认打开 `%LOCALAPPDATA%\PmlTwin\Core30Workbench`。要测新配置，可用已安装 EXE 的 `--workspace` 参数指定一个尚未创建的本机目录。以下用到的 `$installDir` 是上一步实际确认的目录。

```powershell
$desktop = Join-Path $installDir 'runtime\desktop\PmlTwin.Workbench.exe'
$workspace = Join-Path $env:LOCALAPPDATA ('PmlTwin\Validation-' + (Get-Date -Format yyyyMMdd-HHmmss))
& $desktop --workspace $workspace
```

1. 等首页检测完成。核对「AVEVA 安装」所示产品和版本。没找到时，到「EVARS」用「选择软件目录…」选 E3D 安装根目录，回来点「重新检测」。可以先保存配置，再处理许可或 Host 接入。
2. 在首页「快捷登录」点「新建配置」。选实际软件安装，填写工程、启动模块、用户和 MDB。已确认的 APS 测试可以用界面的「填入示例」，再逐项核对 `APS`、`SYSTEM`、`ALL` 和该版本允许使用的模块。例如本机 E3D 2.1 实测用 Design。其他机器以本机安装、工程与许可为准。
3. 点「保存配置」。同一工作区重新打开后确认可选中该条配置。工作目录与 AVEVA 密码在「显示名称和高级选项」中。保存密码只写当前 Windows 用户的凭据，按工作区、配置、安装和登录身份核对。换电脑或目录后在本机重新输入。可留空，登录时在实际密码框中输入。
4. 从首页点「一键登录」对应的模块。发生工程或模块提示时核对本机值。密码由用户在 WPF 密码框中输入。记录 PmlTwin 回执和实际 E3D 窗口。若 E3D 已经由其他方式登录，可以直接到「会话与 CLI → CLI 状态」刷新会话。

## 04 需要 Host 时再接入 AVEVA

安装 PmlTwin 后，若 CLI 可以列出工程窗口，但提示 Host 尚未连接，先核对 AVEVA 实际启动的版本和 EVARS。需要给该 E3D 接入 Host 时使用以下 WPF 流程。

1. 打开「会话与 CLI → 文件部署」，点「部署并接入所有版本…」。核对 Phayton 程序目录和共用资源根。随包源码位于 `deployment\<版本>\PMLLIB` 和 `deployment\<版本>\PMLUI`。
2. 点「预览部署与接入」。逐项检查待写入的目标版本、Host、EVARS、PMLLIB/PMLUI 路径和行级变更。该按钮可能匹配多个安装。若预览包含此次未同意变更的版本，从预览取消并先厘清范围。目标目录或 EVARS 无法写入时，按实际权限处理再重出预览。
3. 确认后应用，保存「共享部署回执」中的实际结果与备份位置。正常退出并重新打开受影响的 AVEVA。回「CLI 状态」点「刷新会话」，核对具体工程窗口及 Host。单独看到文件路径或启动 PID 只能证明相应阶段已完成。
4. 如需解除托管接入，从 PmlTwin 的「EVARS」选准当前版本，检查「解除 Phayton 接入…」的变更再确认。解除后重开 AVEVA。保留回执中的备份与现有业务配置。

## 05 没有 SAG 时安装，已有 SAG 时绑定

没有 SAG 也可完成上面的 E3D 登录与 Host 验收。需要正文检索时，在「PmlTwin 设置 → 更新」的「SAG 正文检索组件」卡开始。

- 全新安装点击「检查并预览安装…」。它会取得对应的官方 Windows x64 发行包并校验，首次可能需要等待下载。核对页面的版本、来源、SHA-256、安装位置及当前工作区索引位置，才点「确认安装」。安装程序返回后，页面需要核对 `sag.exe` 版本并绑定当前工作区。默认组件目录为 `%LOCALAPPDATA%\AVEVA-Toolbox\SAG-Wiki`，其中 `packages\<版本>\<SHA-256>.exe` 是缓存路径。名称中的 `AVEVA-Toolbox` 是目录约定，独立 PmlTwin 也使用该路径。
- 已有 SAG 时用「选择已有 CLI…」，或到「知识库 → 正文检索」点「选择 CLI…」，选 Windows 下实际可运行的 `sag.exe`。预览显示「使用此组件」时可以直接绑定已经安装好的版本。组件绑定成功的依据是实际 CLI 返回版本，页面能打开正文检索。
- VM 访问 GitHub 缓慢或离线时，可先在有网络的 Windows 选择 [SAG-Wiki 官方发行版](https://github.com/Zleap-AI/SAG-Wiki/releases)，核对同版 Windows x64 安装资产的 SHA-256 后拷入 VM。若用官方安装器另行安装，完成后在 PmlTwin 选择其实际 `sag.exe`。未获确认时可先跳过 SAG。下载结束、命中缓存、正式安装和索引可用分别记录。

到「知识库 → 正文检索」，选择导入类型并导入一个只含测试资料的目录。可先在目录中准备 `sample.txt`，正文写入 `APS-PMLTWIN-SEARCH`。待索引就绪后搜索该唯一词并打开原文。也可导入 HTML、Markdown/TXT 或未加密 PML 目录。当前「资料部署」接收 PDF，正文索引以「正文检索」页列出的导入类型为准。该工作区的 PmlTwin 索引位于 `.core30\sag-knowledge\data`。

## 06 AI 日常接入与移机验收

原生 Phayton CLI 随程序安装。可以从「会话与 CLI → CLI 状态 → Agent 接入…」核实实际程序路径。AI 在 AVEVA 所在的 Windows 用户下运行终端。手册优先以原生 CLI 接入，运行期 Skill 可单独配置。安装端 Skill `agent-skills/yuzuha-setup` 用于步骤 02，运行期 `yuzuha-toolkit` 用于调用帮助。本次安装计划将 AI 客户端与 MCP 设置留空。

```powershell
$cli = Join-Path $installDir 'runtime\cli\PmlTwin.Phayton.Cli.exe'
& $cli op list
& $cli op invoke aveva.sessions.list
```

先确认 `success`。`data.Count=0` 表示当前没有发现工程窗口；已登录后再刷新。选择本次发现中的确切 PID 和对应启动时刻，从 `op describe aveva.session.status` 核对参数，使用目标模块严格验证。

```powershell
# 参数值取自现场会话发现和已经核实的目标模块。
& $cli op invoke aveva.session.status --pid $sessionPid `
    --expected-start-time-utc-ticks $sessionStartTicks --expected-model $expectedModule
```

检查 `data.TargetVerified` 和 `data.Host.Model`。更换会话后重新查 PID，不能复用旧回执。需要查询保存的资料时，先跑 `op invoke launch.workspace.read`，把 CLI 报告的 `data.filePath` 和 PmlTwin 使用的工作区核对。两者可能读不同的配置库。`knowledge.*` 与 PmlTwin 的 SAG 索引也各有存储位置。

### 发回验收时只记录这些结果

| 项目 | 通过所需的现场结果 |
| --- | --- |
| 安装 | 发行版与摘要对应，安装回执成功，能打开独立 PmlTwin |
| 新工作区 | 能保存并重开读回登录配置，密码由当前 Windows 用户在本机输入 |
| E3D 登录 | 对应工程窗口已经打开，版本、工程和模块符合目标。提交启动只是过程状态 |
| Host | 同一窗口的 PID、启动时刻、Host 核验及实际模块吻合 |
| SAG | 实际 CLI 返回版本，测试文件可索引、检索并打开原文 |

请提供在哪台机器或 VM 做的测试、实际发行版和 E3D 版本、通过到哪个阶段及第一个失败步骤。保留关键错误文案，去掉密码、账户秘密和不相关的安装报告内容。本机 0.2.7 在隔离新工作区已验证 E3D 发现、无 Reader 手动录入和无 SAG 的安装入口。先前的 APS/Host 实测以及 SAG 缓存、样本检索证据可作参照。另一台机器和虚拟机是否通过，应以它们自己的结果为准。
