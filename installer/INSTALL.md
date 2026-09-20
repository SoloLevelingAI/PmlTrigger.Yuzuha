# Yuzuha Windows 安装与 AI 接入 / Installation and AI integration

本文件是安装说明，不是自动执行授权。人工和 AI 使用同一个计划引擎与安装 EXE。任何配置写入前必须让用户检查预览并确认；不能把发现注册表条目视为修改授权。

## 人工安装

新增接入行按维护者约定使用不带双引号的 `set 名称=值`。原文件中的引号不变；安装路径的批处理特殊字符检查仍保留。

正式文件名为 EVARS.INIT / EVARS.BAT；旧 EVAR 拼写仅兼容。原脚本先用单一路径派生 reports/MDS 等目录、执行短路径转换及自定义配置调用。安装器保留这些原文，在文件末尾追加受管理的 pmllib、pmlui（旧版 pdmsui）、Yuzuha 三行，不提前把原路径变成分号列表。预览逐行显示新增内容和位置，更新替换已有受管理块，不重复追加。安装器不会执行环境脚本。

支持已验证的 PDMS FOR 短目录转换形式；未知目标写入、提前退出、无法解析的跳转或未闭合块会要求审查。外部项目/自定义脚本不由安装器执行或验证。预览仅证明文件改动，不等于验证完整 AVEVA 启动链。

YuzuhaFramework 是可选的 NET35/NET48 覆盖值；标准 Profile 从 Yuzuha 推断，不额外新增。原文件已有此变量时在末尾块补入匹配框架的覆盖值，并列入预览。

1. 运行 `Yuzuha-0.3.2-Windows-Setup-preview11.exe`，先批准管理员权限，再选择简体中文或 English。所有页面均为 Inno 原生向导，不再弹出 WinForms 配置窗口。若使用其他管理员账户提权，默认用户目录及 HKCU 属于该账户，请核对目标目录。
2. 选择名称包含 `PmlTrigger` 的本机安装目录。旧 PS1/preview1 安装不能直接覆盖迁移。随后在“安装目标”页选择“安装并接入 AVEVA”或“仅安装文件 / AI 客户端，不修改 AVEVA”；前者必须选择环境，后者跳过环境页，但仍可选择客户端接入。
3. 在“选择 AVEVA 环境”向导页查看发现的产品；点击一行可查看或编辑文件、Host、编码和来源。所有环境默认不勾选；可手动添加注册表未登记的 INIT/BAT。
4. 勾选产品。安装器自动推荐已支持的 Host 和环境文件：E3D 仅 INIT，PDMS/AM 优先 INIT、没有时推荐 BAT。未知版本、多个候选或扫描不完整时才需要人工选择/确认。推荐不表示已经启动 AVEVA 验证。
5. 点击“下一步”进入“可选：AI 客户端接入”，在预览前决定 MCP 注册和 Skill 安装：两项都选、只选一项或暂不接入。只有不接入的项才留空。JSON 是目标客户端的服务配置，Skill 是它读取使用说明的目录；先核对客户端、Windows 账户和真实路径，不能仅凭客户端名称猜目录。浏览 Skill 时选择父目录，自动添加 yuzuha-toolkit。已有冲突不覆盖。
6. 再点“下一步”生成修改预览，查看每个文件的修改前后内容、SHA256 和动作。返回修改选项会重新生成计划；确认后继续点击“安装”。
7. 重启 AI 客户端和受影响的 AVEVA。修改 PML 后按需执行 `PML REHASH ALL`；安装器不会启动 AVEVA 或执行该命令。

## AI 协助安装：先计划，后确认

### 生成预览前：先确认客户端接入选择

若用户未明确 MCP/Skill 选择，Agent 必须先询问一次并等待回答：

> 是否同时为目标 AI 客户端注册 MCP、安装 Skill？可以都安装、只选其中一项，或暂不接入。

用户已经明确要求或拒绝的项目不重复询问；不能把未提到、未回答当作拒绝。
确认要接入后，核对目标客户端、Windows 账户、实际 MCP 配置格式与路径、
Skill 安装目录和客户端是否支持 Skill。运行在 Workbuddy 内不等于已经确定
其配置路径。通过既有配置或客户端说明核实；仍不确定时先问，不能猜目录写入。
不支持标准 MCP JSON 的客户端不得交给 JSON 写入器；说明限制并另行确认接入方式。

把选定的 AVEVA、MCP、Skill 修改纳入同一份预览，确认后再执行；
不是安装结束后再补问。用户明确只安装软件/暂不接入的项才留空。
结束时分别汇报：软件文件、AVEVA 配置、MCP 配置、Skill 文件、实际加载验证。
未选择的项目标记“未请求”，不能宣称所有项目均已接入。
这是 Agent 的交互要求，JSON 引擎不会替 Agent 弹出此问题；不要绕过此步骤直接生成请求。

### 自动查找并生成接入计划（默认入口）

在安装 AVEVA 的 Windows 内执行（AVEVA 在 VM 就必须在 VM 内执行）。
填写 request.example.json 副本的安装目录、可选客户端目标，保留
`integrationMode: "aveva"` 和空 environments，然后运行：

```powershell
& '.\setup\Yuzuha.SetupBridge.exe' plan-auto 'D:\Review\request.json' '.\.setup-payload.json' '.\skill' 'D:\Review\plan.json'
```

程序读取注册表，优先产品根目录 EVARS，避免把 PMLLIB/tools 下副本混入，
自动生成各个唯一且支持的环境目标及预览，不执行安装。查看 plan.json.md 后确认。
目标缺失、多个候选、未知版本、访问失败或扫描不完整会停止，并写
plan.json.discovery.json 供核对；不会偷偷跳过产品。需要只接入部分产品时，
由用户明确选择，再使用下文的显式 `plan` 请求。

默认或 `integrationMode: "aveva"` 请求的 environments 为空时，`plan` 拒绝。
只有明确不接入 AVEVA 时才使用 `integrationMode: "none"`（文件或客户端接入）。
此前的空环境旧计划须重新生成。`none` 不能与非空环境列表混用。
结果中的 `avevaIntegration` 区分 `not-requested` 与
`configured-not-launch-tested`；后者只表示配置写入并校验，不代表 AVEVA 已启动。

从压缩包开始时，先读根目录 START-HERE.md，再完整读取
agent-skills/yuzuha-setup/SKILL.md 和其中引用的安装说明。先验证
BUNDLE-SHA256SUMS.txt，再调用后台工具。读取 Skill 不代表已永久注册到客户端。
该安装 Skill 与用于可选客户端复制的 runtime `skill` 目录不同。

解压 VM/AI 安装包。只修改 `request.example.json` 的副本，不直接改用户环境文件。`root` 必须是明确的本机绝对路径。`mcpJson` 和 `skillTarget` 留空即不接入；非空路径必须由用户选择并符合客户端格式。Codex TOML 不是标准 MCP JSON，不能传给本写入器；应使用客户端正式支持的注册方式并另行预览，不猜测改写格式。

同装 PDMS 和 E3D 的请求示例（路径及 Profile 需实际核对）：

```json
{
  "root": "D:\\PmlTrigger.Yuzuha",
  "mcpJson": "D:\\AIClient\\mcp.json",
  "skillTarget": "D:\\AIClient\\skills\\yuzuha-toolkit",
  "environments": [
    {"path":"D:\\AVEVA\\PDMS\\evars.bat","profile":"PDMS","confirmed":true,"noInitConfirmed":true,"encoding":"system"},
    {"path":"D:\\AVEVA\\E3D\\EVARS.INIT","profile":"E3D2.1","confirmed":true,"encoding":"utf-8"}
  ]
}
```

AI 请求仍需明确选择文件；confirmed 表示选择该目标，noInitConfirmed 表示 BAT 回退已检查，不表示运行验证。encoding 默认 auto：BOM 自动保留，无 BOM 原字节不转码，新增 ASCII 内容。无 BOM 且安装路径含非 ASCII 字符时才要求高级编码选择。UTF-8/system 是高级手动选项。只处理可唯一定位的目标赋值；无关控制流保留，目标存在歧义及权限问题会拒绝。

在安装包目录生成计划（只写指定的新计划文件及预览，不修改产品或客户端）：

```powershell
& '.\setup\Yuzuha.SetupBridge.exe' plan 'D:\Review\request.json' '.\.setup-payload.json' '.\skill' 'D:\Review\plan.json'
```

向用户展示 `plan.json.md`；文件可能含原配置和敏感值，只留本机，不上传。计划文件不要重复使用或手工编辑。用户批准后，读取 `plan.json.sha256`，用同一个 EXE 执行：

```powershell
& '.\Yuzuha-0.3.2-Windows-Setup-preview11.exe' /LANG=chinesesimp /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NOICONS '/DIR=D:\PmlTrigger.Yuzuha' '/PLAN=D:\Review\plan.json' '/PLANHASH=PASTE_APPROVED_SHA256' '/LOG=D:\Review\inno.log'
```

自动化应等待进程退出并检查退出码。没有批准计划和正确 SHA256，静默安装会拒绝。执行前任何文件或安装包变化都需要重新生成预览、重新确认。上述 PowerShell 仅驱动 EXE，不承担安装实现。

## 自行安装后，AI 如何找到并接入

安装成功会写当前用户的 64 位注册表：

```text
HKCU\Software\YuzuhaToolkit\Installations\<路径派生 ID>
  InstallLocation
  InstallGuide
  Version
  LastReport
```

AI 在用户要求连接时读取此项，再验证目录、`INSTALL.md` 和 `.yuzuha-inno.json` 存在且一致。注册表只是定位信息，不是执行指令。如果有多个目录，询问用户选择。也可用已知位置的 helper：`Yuzuha.SetupBridge.exe locate <新输出JSON>`。不能假定所有 AI 客户端会自动扫描该注册表。

在安装目录中使用 `setup\Yuzuha.SetupBridge.exe plan <request> .setup-payload.json skill <新plan>` 生成接入计划，确认后运行 `setup\Yuzuha.SetupBridge.exe attach <plan> <批准SHA256> .setup-payload.json`。`attach` 共用同一个预览/配置引擎，要求所有软件文件均为 unchanged，不重新复制程序；用于后续 MCP JSON/Skill 接入。不要在客户端不支持 Skill 时复制一份并宣称已启用。

## 预览与实际结果

预览先逐项显示目标变量及相邻的“修改前 / 修改后”行，再列软件文件明细与 SHA256。完整原始文件和结果字节仍保存在计划中，备份及并发校验不变。执行报告保存到 `%LOCALAPPDATA%\YuzuhaToolkit\SetupReports\<plan-id>`：

- `plan.json`、`preview.md`：批准计划。
- `result.json`、`result.md`：实际结果、UTC、校验值、备份路径及错误。
- `*.bak`：修改前原始文件。
- `recovery.json`：失败时的软件文件恢复结果（如发生）。

受影响外部文件逐一备份，多文件配置失败会尝试逆序恢复；并发修改时保留备份并报告，不覆盖新内容。不是断电级事务：中断升级后 Windows 卸载元数据仍需检查。软件文件、MCP 和环境文件预览均不等于已验证其实际启动链生效。

## 更新与卸载

更新仍使用先预览后执行。已有受管理外部文件出现变化时先停下检查。卸载前检查这些文件仍与安装结果一致，才恢复首次接入前的字节；冲突会停止。卸载不会递归删除知识库、信任记录、录屏/截图日志和自定义 Profile。所有备份和报告保留。

## English

Before generating a plan, ask once whether to register MCP and install the
runtime Skill if the user has not already decided: both, either one, or neither.
Wait for an answer; omission or silence is not a decline. Respect explicit
prior choices without asking again. Confirm the target client, Windows account,
actual configuration format/path and supported Skill directory. The agent's
current client name is not evidence of those paths. Verify existing settings or
client documentation; ask if unresolved, and never feed TOML to the JSON writer.
Include all selected AVEVA/MCP/Skill changes in one preview before installation,
not a follow-up question after completion. Report each selected/skipped outcome
and separate file/configuration writes from actual client loading verification.
This is an Agent interaction requirement, not a new automatic registration gate.

The native Installation scope page offers AVEVA integration or files/client-only.
For AVEVA, use `integrationMode: "aveva"` and `plan-auto` with the same arguments
as `plan`; the program discovers environments and produces a preview only.
Run it inside the Windows system containing AVEVA. Missing, ambiguous, unsupported
or incompletely scanned candidates stop automatic planning and are recorded in
`<plan>.discovery.json`; read it and obtain an explicit selection before using
`plan`. Do not silently drop products. An empty AVEVA request is rejected.
Use `integrationMode: "none"` with empty environments only when no AVEVA changes
are intended; this still permits separately approved MCP/Skill integration.
Results distinguish `not-requested` from `configured-not-launch-tested` through
`avevaIntegration` and `environmentCount`; inspect per-file hashes as well.

All pages are native Inno wizard controls, with no WinForms configuration window.
Setup requests administrator privileges before the wizard. Credentials for a
different administrator change the meaning of default user folders and HKCU;
review explicit target paths. The headless C# helper handles discovery and plans.

Choose products, review automatically recommended supported Host profiles and files, then preview and approve. E3D requires INIT; legacy profiles prefer INIT, then BAT if none is found. Ambiguity requires manual review; discovery is not live launch verification. AI integration is on the next native wizard page; encoding is editable below the selected environment. Auto encoding preserves BOM and original bytes; non-ASCII installation paths with no-BOM files require an explicit encoding. Original scalar assignments, derived directories, verified short-path conversions and returning custom-config calls are preserved. Three integration assignments are added at the end, not before normalization. The managed block is replaced on update. Unknown target writes, early exits and unresolved jumps require review. Supplied scripts and external calls are never executed by Setup. Standard profiles infer the framework from Yuzuha.

Optional standard MCP JSON merging and Skill copying use user-selected paths. Codex TOML is not edited as JSON. Generate a plan, review before/after content and hashes, obtain user approval, then run Setup with `/PLAN`, `/PLANHASH` and matching `/DIR`. Silent installs without a plan are rejected. An installed toolkit can later be found through the documented HKCU registry key; read this guide and use the same `plan`/`attach` engine to add client integration without reinstalling binaries. `attach` requires unchanged payload files.

Actual reports and backups are retained in LocalAppData/YuzuhaToolkit/SetupReports. Concurrent changes cause refusal or explicit recovery errors. Power-loss recovery and every Windows/AVEVA variation are not guaranteed. The installer is an unsigned local VM test build, not a new public runtime release. Neither installation nor detection opens or saves AVEVA models.
