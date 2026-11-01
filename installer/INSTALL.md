# Phayton Windows 安装与 CLI 接入 / Installation and CLI integration

0.3.4.2 起使用 `runtime/cli/PmlTwin.Phayton.Cli.exe` 原生 CLI。新包不分发独立 MCP 服务，不注册或改写客户端 MCP 配置；请求中的 `mcpJson` 必须为空，非空及旧 MCP 计划会拒绝。可选的 Skill 安装仍保留。CLI 的接口、JSON 输入与测试由随包使用文档说明。

本文件是安装说明，不是自动执行授权。人工和 AI 使用同一个计划引擎与安装 EXE。任何配置写入前必须让用户检查预览并确认；不能把发现注册表条目视为修改授权。

## 当前默认：安装应用，由管理器准备环境

新交互安装默认使用明确的`integrationMode: "manager"`。它与`none`不同：安装器交付应用、管理CLI、清单、快捷方式和安装所有权，结果为`manager-preparation-required`；不发现后自动修改EVARS，不声称AVEVA已接入。使用示例请求的manager模式时，调用`plan`生成安装预览，环境列表必须为空；`plan-auto`只服务于下文保留的旧显式aveva模式。

安装完成页可勾选以原交互用户打开管理器，参数为`--prepare-installation --handoff "<安装根>"`。静默安装不自动启动管理器，也不后台启动CLI宿主。管理器核验安装清单和所有权后，再选择产品环境、预览EVARS/运行部署、明确应用及查看回执。应用根归Inno所有；独立复制部署须使用另一个管理器拥有的运行目录，不能覆盖应用根。

旧v2安装器已有EVARS所有权时，先用管理器只读预览，再明确移交。受支持的移交保留旧标记备份和回执，只从当前安装标记的`managed`及请求环境列表中移除指定目标，EVARS原字节不变；旧卸载随后不再恢复该目标，其他托管条目保留。来源漂移、未知旧格式或正在运行的Setup/helper/卸载程序会拒绝。旧程序不遵守新的管理器租约，因此该操作不承诺跨旧进程的全局事务；交接后复验不符时报告unknown并人工核对，不自动重放。真实安装或真实所有权移交仍需先批准具体预览。

部署与直接EVARS操作在预览及应用时都核对明确的包/运行根和HKCU已登记安装的当前标记；仍由v2安装器拥有的目标返回`SETUP_OWNERSHIP_TRANSFER_REQUIRED`，不能绕过移交直接改写。损坏、未知或无法读取的已知记录返回`SETUP_OWNERSHIP_UNRESOLVED`并停止。直接EVARS入口可选`packageRoot`保留到冻结计划；本轮前缺少该包装的旧冻结编辑计划须重新预览，旧恢复回执仍兼容。未登记、且未作为包/运行根提供的任意旧目录不在自动发现范围；不会执行脚本或全盘搜索来推测所有权，这些来源须先人工定位核对。

以下旧式EVARS直接接入说明仅适用于用户明确选择的兼容`aveva`计划；`none`继续表示明确仅文件/客户端，不得用它伪装manager准备完成。旧显式计划、备份及卸载语义继续保留。

## 人工安装

新增接入行按维护者约定使用不带双引号的 `set 名称=值`。原文件中的引号不变；安装路径的批处理特殊字符检查仍保留。

正式文件名为 EVARS.INIT / EVARS.BAT；旧 EVAR 拼写仅兼容。原脚本先用单一路径派生 reports/MDS 等目录、执行短路径转换及自定义配置调用。安装器保留这些原文，在文件末尾追加受管理的 pmllib、pmlui（旧版 pdmsui）、Yuzuha 三行，不提前把原路径变成分号列表。预览逐行显示新增内容和位置，更新替换已有受管理块，不重复追加。安装器不会执行环境脚本。

支持已验证的 PDMS FOR 短目录转换形式；未知目标写入、提前退出、无法解析的跳转或未闭合块会要求审查。外部项目/自定义脚本不由安装器执行或验证。预览仅证明文件改动，不等于验证完整 AVEVA 启动链。

YuzuhaFramework 是可选的 NET35/NET48 覆盖值；标准 Profile 从 Yuzuha 推断，不额外新增。原文件已有此变量时在末尾块补入匹配框架的覆盖值，并列入预览。

1. 运行 `Phayton-0.3.4.2-Windows-Setup.exe`，先批准管理员权限，再选择简体中文或 English。所有页面均为 Inno 原生向导，不再弹出 WinForms 配置窗口。若使用其他管理员账户提权，默认用户目录及 HKCU 属于该账户，请核对目标目录。
2. 选择名称包含 `PmlTrigger` 的本机安装目录。旧 PS1/preview1 安装不能直接覆盖迁移。随后默认“安装应用，随后由管理器准备环境与部署”；只有明确选择“旧版兼容：安装并接入AVEVA”才进入环境页；“仅安装文件/客户端”也继续保留。
3. 在“选择 AVEVA 环境”向导页查看发现的产品；点击一行可查看或编辑文件、Host、编码和来源。所有环境默认不勾选；可手动添加注册表未登记的 INIT/BAT。
4. 勾选产品。安装器自动推荐已支持的 Host 和环境文件：E3D 仅 INIT，PDMS/AM 优先 INIT、没有时推荐 BAT。未知版本、多个候选或扫描不完整时才需要人工选择/确认。推荐不表示已经启动 AVEVA 验证。
5. 点击“下一步”进入“可选：CLI 使用 Skill”。CLI 随软件安装；如需为 AI 提供使用说明，填写客户端实际读取的完整 yuzuha-toolkit 目录，否则留空。先核对客户端、Windows 账户和真实目录。该页没有 MCP 注册选项；安装器不会修改已存在的 MCP 服务配置。
6. 再点“下一步”生成修改预览，查看每个文件的修改前后内容、SHA256 和动作。返回修改选项会重新生成计划；确认后继续点击“安装”。
7. 重启 AI 客户端和受影响的 AVEVA。修改 PML 后按需执行 `PML REHASH ALL`；安装器不会启动 AVEVA 或执行该命令。

## AI 协助安装：先计划，后确认

### 生成预览前：先确认客户端接入选择

若用户未明确 Skill 选择，先询问是否复制 CLI 使用 Skill，并等待回答；用户已经明确的选择不重复询问。核对目标客户端、Windows 账户及客户端实际读取的 Skill 目录。CLI 可直接调用，不依赖注册后台服务。

把选定的 AVEVA 和 Skill 修改纳入同一份预览，确认后执行。结束时分别汇报软件/CLI 文件、AVEVA 配置、Skill 文件及实际加载验证。`mcpJson` 必须留空；安装器不会重新启用已禁用的 MCP，也不会改动其他客户端服务。

### 高级选项：显式接管已有的 CLI Skill

默认`adoptSkill`为`false`（省略也相同），已有未托管Skill仍拒绝直接覆盖。用户明确要更新这个现有Skill时，可在全新请求中设置严格布尔值`"adoptSkill": true`，同时给出完整的现有`skillTarget`。向导对应复选框默认不勾选。没有目标、非布尔类型、链接/逃逸、损坏文本或损坏所有权记录均拒绝，不能用它绕过具体预览批准。

接管预览冻结整个Skill的文件与目录成员、SHA256和长度，并绑定当前安装标记的原始SHA与完整`priorState`。边界为最多2048个成员、单文件4MiB、总文件16MiB；包内文本须能按UTF-8或带BOM的UTF-16审阅。预览后新增、编辑、删除、空目录或链接变化都需要重新生成。执行前、配置提交前和标记替换前复验来源；这不是对不遵守约定的旧程序承诺全局跨进程原子性。

只有包内Skill目标进入`managed`。内容相同的包文件也明确记录接管，并保存首次原始字节；有改动的文件使用同一既有备份/条件替换/回滚引擎。更新保留首次原文，不把上次安装内容冒充原文。包外额外文件只记录库存指纹，保留且不归安装器所有；卸载只按受管文件的摘要恢复首次原文或删除本安装创建的目标，不删除整个Skill目录。额外未托管文件仍存在时，后续更新继续需要明确接管选项和新快照。

`managed.original`必须明确存在，且只能是有效base64字符串或显式`null`；缺失、错误类型和坏base64均停止操作，不能把“缺失”推导为允许删除。所有新安装计划都冻结`markerBeforeSha256`与`priorState`，旧计划缺少这些上下文时须重新预览，旧合法安装标记及首次原文恢复语义保留。

接管的完整before/after、首次原文和备份信息只留在本机计划/Markdown预览中，不上传、提交或放进公开日志。安装器仍不自动声明客户端已重新加载Skill。示例仅展示选项，路径须采用已核验的当前客户端目录：

```json
{"integrationMode":"manager","root":"C:\\Review\\PmlTrigger.Yuzuha","mcpJson":"","skillTarget":"C:\\Users\\YOUR-USER\\.codex\\skills\\yuzuha-toolkit","adoptSkill":true,"environments":[]}
```

### 自动查找并生成接入计划（旧显式aveva兼容入口）

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

解压 VM/AI 安装包。只修改 `request.example.json` 的副本，不直接改用户环境文件。`root` 必须是明确的本机绝对路径。`mcpJson` 必须为空，非空时明确拒绝生成计划。`skillTarget` 留空则不复制 Skill，非空目录必须由用户选择并由目标客户端支持。安装器不改写 Codex TOML 或其他 MCP 配置。

同装 PDMS 和 E3D 的请求示例（路径及 Profile 需实际核对）：

```json
{
  "root": "D:\\PmlTrigger.Yuzuha",
  "mcpJson": "",
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
& '.\Phayton-0.3.4.2-Windows-Setup.exe' /LANG=chinesesimp /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NOICONS '/DIR=D:\PmlTrigger.Yuzuha' '/PLAN=D:\Review\plan.json' '/PLANHASH=PASTE_APPROVED_SHA256' '/LOG=D:\Review\inno.log'
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

在安装目录中使用 `setup\Yuzuha.SetupBridge.exe plan <request> .setup-payload.json skill <新plan>` 生成接入计划，确认后运行 `setup\Yuzuha.SetupBridge.exe attach <plan> <批准SHA256> .setup-payload.json`。`attach` 共用同一个预览/配置引擎，要求所有软件文件均为 unchanged，不重新复制程序；用于后续 Skill 接入。不要在客户端不支持 Skill 时复制一份并宣称已启用。

## 预览与实际结果

预览先逐项显示目标变量及相邻的“修改前 / 修改后”行，再列软件文件明细与 SHA256。完整原始文件和结果字节仍保存在计划中，备份及并发校验不变。执行报告保存到 `%LOCALAPPDATA%\YuzuhaToolkit\SetupReports\<plan-id>`：

- `plan.json`、`preview.md`：批准计划。
- `result.json`、`result.md`：实际结果、UTC、校验值、备份路径及错误。
- `*.bak`：修改前原始文件。
- `recovery.json`：失败时的软件文件恢复结果（如发生）。

受影响外部文件逐一备份，多文件配置失败会尝试逆序恢复；并发修改时保留备份并报告，不覆盖新内容。不是断电级事务：中断升级后 Windows 卸载元数据仍需检查。软件文件、Skill 和环境文件预览均不等于已验证其实际启动链生效。

## 更新与卸载

更新仍使用先预览后执行。已有受管理外部文件出现变化时先停下检查。卸载前检查这些文件仍与安装结果一致，才恢复首次接入前的字节；冲突会停止。卸载不会递归删除知识库、信任记录、录屏/截图日志和自定义 Profile。所有备份和报告保留。

## English

This CLI-only distribution installs `runtime/cli/PmlTwin.Phayton.Cli.exe` and does not ship or register standalone MCP servers. Nonempty `mcpJson` requests and frozen plans containing MCP changes are rejected before external writes. Existing client settings, including disabled servers and unrelated services, are not changed. Optional Skill copying remains available.

Before planning, resolve the user's Skill choice if not already stated. Confirm the client, Windows account and supported Skill directory; include selected AVEVA and Skill changes in the same review. Report file installation, configuration writes and actual loading verification separately.

The default native scope is explicit `manager`: install application/Manager CLI,
then open the preparation task as the original interactive user. It never writes
EVARS or claims a connected Host. Use `plan` with empty environments; the result
is `manager-preparation-required`. Silent Setup never opens Manager automatically.
Legacy AVEVA integration and explicit files/client-only modes remain available.
For AVEVA, use `integrationMode: "aveva"` and `plan-auto` with the same arguments
as `plan`; the program discovers environments and produces a preview only.
Run it inside the Windows system containing AVEVA. Missing, ambiguous, unsupported
or incompletely scanned candidates stop automatic planning and are recorded in
`<plan>.discovery.json`; read it and obtain an explicit selection before using
`plan`. Do not silently drop products. An empty AVEVA request is rejected.
Use `integrationMode: "none"` with empty environments only when no AVEVA changes
are intended; this still permits separately approved Skill integration.
Results distinguish `not-requested` from `configured-not-launch-tested` through
`avevaIntegration` and `environmentCount`; inspect per-file hashes as well.

All pages are native Inno wizard controls, with no WinForms configuration window.
Setup requests administrator privileges before the wizard. Credentials for a
different administrator change the meaning of default user folders and HKCU;
review explicit target paths. The headless C# helper handles discovery and plans.

Choose products, review automatically recommended supported Host profiles and files, then preview and approve. E3D requires INIT; legacy profiles prefer INIT, then BAT if none is found. Ambiguity requires manual review; discovery is not live launch verification. AI integration is on the next native wizard page; encoding is editable below the selected environment. Auto encoding preserves BOM and original bytes; non-ASCII installation paths with no-BOM files require an explicit encoding. Original scalar assignments, derived directories, verified short-path conversions and returning custom-config calls are preserved. Three integration assignments are added at the end, not before normalization. The managed block is replaced on update. Unknown target writes, early exits and unresolved jumps require review. Supplied scripts and external calls are never executed by Setup. Standard profiles infer the framework from Yuzuha.

Optional Skill copying uses a user-selected directory; no MCP JSON merging is available, and Codex TOML is not modified. Generate a plan, review before/after content and hashes, obtain user approval, then run Setup with `/PLAN`, `/PLANHASH` and matching `/DIR`. Silent installs without a plan are rejected. An installed toolkit can later be found through the documented HKCU registry key; read this guide and use the same `plan`/`attach` engine to add client integration without reinstalling binaries. `attach` requires unchanged payload files.

Existing unowned Skill files are refused by default. Advanced explicit `adoptSkill: true` requires the exact existing `skillTarget`, freezes the complete bounded directory inventory and current installation ownership, and presents full local before/after content. Only packaged targets become managed; unchanged matching files also retain their first original bytes. Extra files remain unowned and are preserved. Missing/invalid original records, inventory drift or links stop verification. All plans bind the raw prior marker hash and complete prior state independently of this option; old frozen plans without that context require a new preview. Keep raw previews local, and obtain approval of the concrete plan before execution.

Actual reports and backups are retained in LocalAppData/YuzuhaToolkit/SetupReports. Concurrent changes cause refusal or explicit recovery errors. Power-loss recovery and every Windows/AVEVA variation are not guaranteed. The installer is unsigned. Product-specific NET35/NET48 Hosts remain separate from the Native AOT CLI. Neither installation nor detection opens or saves AVEVA models.
## 可选的每次启动动作

所选 `environments` 项可显式加入 `"startupActions": "rehash-all"` 或 `"none"`。程序将该值作为 `set YUZUHA_STARTUP_ACTIONS=...` 纳入环境文件的逐行安装预览；未批准预览不写入。新安装省略时不添加特殊启动变量，更新省略时保留此前托管块内的明确策略。未知值拒绝生成计划。

该设置不是安装期间执行 PML。新 AM/E3D 客户端初始化 PML 后，原生 Addin 在 Host 建立前执行一次并写入 `%TEMP%/YuzuhaStartup.log`（桌面启动时使用独立关联文件），失败不重试。关闭已有 AM/E3D 后才替换安装，重新启动才验证该设置；安装成功不能代替真实启动验证。桌面显式选择可用 `YUZUHA_STARTUP_OVERRIDE` 覆盖 INIT/BAT 的默认策略。当前不支持任意命令列表。

桌面和 HTML 发布文件分别位于 `runtime/desktop` 与 `runtime/webapps`。桌面为自包含 Windows 发布；本批 HTML 宿主为框架依赖发布，目标机需有 ASP.NET Core 10。CLI 的 `--cli web open` 和桌面按钮都会打开独立页面；安装器不自动打开服务或调用 AVEVA。

0.3.4.1 安装包将 HTML 宿主也改为自包含发布，无需另装 ASP.NET Core 10。桌面、完整 DocFX、中文视频与只读文档宿主一并交付；上述历史版本依赖说明保留用于识别旧包。

构建时将桌面、Manager CLI与DevHost发布到独立目录，再用 `installer/build.py --version 0.3.4.2 --runtime <含四profile和webapps的独立runtime目录> --cli <原生CLI发布目录> --manager <Manager CLI发布目录> --desktop <桌面发布目录> --documentation <已校验DocFX站点> --documentation-host <DevHost发布目录> --iscc <ISCC.exe> --output <新目录>` 纳入包。`--runtime`省略时兼容旧的仓库runtime来源；独立输入避免覆盖开发或已安装运行目录。构建缺少四个Host、`Yuzuha.Manager.Cli.exe`或桌面入口时拒绝，不发布无效manager入口；所有发布文件进入同一散列清单。这些操作不执行安装。


## 用户数据与升级保护（TRACE32）

用户工作目录在桌面设置中推荐为 Documents/YuzuhaWorkspace，可显式修改并复制核验后切换。升级不自动迁移用户数据，不自动重建知识库。

新安装计划拒绝 Knowledge、Apps、Projects、UserPmlLib、Exports、trust 和内部状态目录的载荷，以及 SQLite/DB 数据文件；含 `.yuzuha-user-data.json` 的目录及其子目录不能作为程序文件目标。程序同名文件若已被用户修改，或不属于上版发布清单，预览停止并保留文件；先复制修改到用户目录，处理冲突后重新预览。原有备份和冻结计划检查继续有效。保持干净构建输入，禁止把个人资料混进发布源码树。

升级前关闭 AM、E3D、Yuzuha 工作台和相关 HTML/知识宿主，再确认具体更新预览。`pml.index` 属于 AVEVA 在本机生成的索引，不进入安装载荷。0.3.4.2 增加 `YuzuhaStartupPolicy.pmlobj`：受管理的旧安装若已有 `PMLLIB/pml.index`，计划仅在 `/Bootstrap/` 节补入缺失的这一行，保留原编码、BOM、其他字节和条目顺序；若没有该节则在末尾补节。已有该项不重复添加，缺少索引的新安装不生成索引。目标名在其他节或节重复等歧义会拒绝并保留原文件。

这项 `pml-index` 改动会显示前后内容与 SHA256，批准后和环境配置处于同一事务：写前备份、冻结哈希校验，失败时恢复原字节；期间如又被修改则保留备份并报告冲突。只针对选定安装根的这个文件，不扫描其他用户 PMLLIB，不执行 REHASH。生成索引不加入长期 `managed` 所有权记录，后续合法 REHASH 不会因此阻止更新或卸载；安装成功不等于已验证 AVEVA 重启。

TRACE32 的源码批次未安装，后续 TRACE33 已完成经授权的本机更新，详见仓库 `docs/validation/2026-10-23-installer-ui-preview.md`；真实 AVEVA 重启与业务复验仍单独记录。


## Phayton 0.3.4 / R2

新包展示名为 Phayton，安装身份与 PmlTrigger.Yuzuha 路径保留。旧快捷方式继续使用，新入口位于 Phayton 分组。完整 DocFX 位于 runtime/documentation，工作台的开发文档按钮启动随包自包含只读文档宿主；退出工作台后结束本次宿主。构建时必须提供 --documentation 和 --documentation-host，分别指定已经校验的 DocFX 站点与自包含 DevHost 发布目录。


## CLI 发行版构建与 Host 选择

`--cli` 必须指向已验证的独立发布目录，至少包含 PmlTwin.Phayton.Cli.exe 和 e_sqlite3.dll。构建器将其复制到 runtime/cli，排除旧 runtime/net10 MCP 程序。`--version` 同时控制载荷清单、Inno 显示版本、Setup EXE 和 Agent ZIP 文件名。构建不执行安装。

Host 策略按维护者约定：E3D 2.1 使用 E3D2.1；E3D 3.1 系列统一使用 E3D3.1.6；AM/PDMS 使用各自已编译的 NET35 Host。发现版本只选择交付 Profile，不代替真实登录、Host 握手和操作测试。
