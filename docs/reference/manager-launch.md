# 管理器启动发现与 LaunchDesk 原型

管理器提供 `installation.discover`（必填 roots，可设扫描预算）和 `installation.projects`（必填 installationRoot）。发现只读，不运行 INIT/BAT；扫描产品根后不继续爬入数据库与 PML 树。报告包含是否完整、入口/环境文件指纹、厂商文件版本和目录提示。目录提示不等于版本核验，安装身份不会代表许可、登录或 Host 已连接。

静态工程/模块读取器迁自 `LaunchDesk.Next.Application/Features/LoginLaunch/Scripts/FindAvevaProjectsAndModules.cs`，同时迁入该原型的报告类型和最小文件适配边界。源原型保留原样，发布无需 LaunchDesk 进程或程序集。调用、变量展开和工程目录提示仅是静态候选；无法解析的条件、流程及编码有单独诊断，不能用来宣称运行时生效变量。

Monitor 的八个原生参数和 Marine 的 module/noconsole/project/user/mdb 顺序继续复用 `LaunchService`，与原型行为相同。进程环境和工作目录在单个 ProcessStartInfo 内配置，不修改管理器或系统环境。基础启动没有许可插件硬依赖；可选许可检查不替代厂商启动链的实际 checkout。

`launch.profile.start` 仍需要当前 expectedRevision、password 和 `--allow-launch`。密码通过 stdin 或本机输入文件，管理 CLI 拒绝此操作的内联 `--input`；批处理同样使用 stdin/文件。回执 `entry-process-created-host-not-verified` 只代表创建入口进程，不能亮 Host 成功灯。

可选 profile.licensePrecheck 例如 `{"mode":"required","host":"127.0.0.1","rmsPort":5093,"timeoutMilliseconds":1500,"requireCheckout":false}`；端口是用户明确指定的策略值，不能作为产品通用默认值。未设置或 mode=off 时不调用插件。required 只通过已启用的独立 license 包做只读预检，可选 rulesPort/serviceName 也必须通过；requireCheckout=true 在当前无签出证明的实现中拒绝启动。预检后重新核对配置修订，冲突不启动。成功回执附带原始诊断和 checkoutState=unverified；厂商初始化和实际签出继续由正常入口完成。

隔离验证使用实际测试 EXE 和自建 BAT，核对参数顺序、中文/空格路径、工作目录及子进程环境；不会启动真实产品。

## 原型资料迁移

`launchdesk.import.preview` 的 `source` 为明确选择的应用/数据根，或 portable.json/workspace.json/profiles.json/draft.json。portable 的 DataDirectory 依据应用目录解析，配置损坏不回退到其他 AppData 目录。旧版读取 Applications/Profiles；Next 读取 profiles、draft、snapshots 和 logs/records.jsonl。预览展示最终根目录、逐源指纹、净化草稿和历史坐标。

`launchdesk.import.apply --allow-write` 使用 planId/sha256，再次核对来源后发布净化快照到 StateDirectory/LaunchDeskImports。`launchdesk.import.read` 使用返回的 id 读取，核对发布内容指纹。原资料保留，草稿未自动写入正式配置；检查并修正本机路径后，使用现有 launch.profile.preview/save 提交。

不复制密码、原应用凭据引用、环境、嵌套 InputSnapshot、ArgumentPlan 或自由文本 Reason。Next 历史只带入结构坐标及明确的工程/模块等字段；损坏行明确计数，原文件保留。导入操作不会启动原 LaunchDesk、创建工程或继承其强制许可预检。

## 文件、目录与 ZIP

`copy.preview` 输入 source、registeredRoot、name，使用明确的工具载荷策略，支持 EXE/脚本等程序文件并保留旧 PML 复制入口的扩展名规则；随后 copy.apply/copy.restore 使用冻结引用和 `--allow-write`。覆盖前备份到状态目录，来源/目标变化要求重新预览，相同文件跳过。工程数据库目录与活动知识库不进入普通工具复制；ZIP/7z 的路径越界、Windows 别名/设备名和链接在写入前拒绝。恢复保留外部新改动并列出未恢复项。失败的诊断包含 code、phase、非空 message、exceptionType、logPath 和 rollbackStatus；成功后的清理警告附在复制回执，不伪装成未执行。

## 可选 7z 格式

`copy.formats` 显示内置文件/目录/ZIP 和可选 7z 的依赖状态。7z 使用独立 Tools/7zr.exe；开发覆盖为 YUZUHA_7ZR_PATH，或仅本次 preview 的 sevenZipTool。没有该依赖时在计划阶段拒绝 7z，基础复制保持可用。依赖声明见 installer/optional-formats.json；本次没有自动下载、安装或从其他应用打包二进制。

7z 预览依次运行受控 list/test/extract，在状态目录的私有暂存区检查列举/实际展开、路径、链接、体积和完整性；目标保持不写。包和工具内容指纹冻结，apply 再次展开验证，之后进入与文件/ZIP 相同的备份/条件恢复事务。不同的包根不会被猜测抹掉，只有与 name 一致的单层根目录会展开到目标下。

`scripts/Test-Manager.ps1 -SevenZipTool <明确选定的 7zr.exe>` 运行真实工具的合成 CRC 损坏、空根、包/工具变更及 CLI 闭环；无依赖的 CI 明确跳过两个 native-format 用例，基础矩阵仍运行。真实工程/工具没有被更新。可选包说明见 [manager-plugins.md](manager-plugins.md)，实机矩阵仍按未勾选任务推进。
