# 启动工作台与原生 CLI 验收

> 安装后已完成新Host的11项实机验收，见[安装后追加验收](#post-install-acceptance)。前面的表格与旧Host结论保留打包前快照语境；已安装文档未随本次追加重建。

2026-10-24 · 0.3.4.2。启动配置已完成真实服务、代码测试、JSON/TOML 场景与正式 WPF 的连接。**离线验收通过；AVEVA 实机验收仍有明确边界。**

调用与扩展方式见[开发、测试与使用约定](workbench-lifecycle.md)、[CLI 使用](cli.md)和[桌面工作台](workspace.md)。可下载[脱敏能力报告 JSON](../validation/2026-10-24-launch-cli.summary.json)。

## 已实际运行的证据

| 范围 | 结果 | 验证内容 |
| --- | --- | --- |
| 完整 solution | 217/217，零失败 | 14 架构 + 203 应用测试；专项重跑不重复计入 |
| 正式 Native AOT CLI | 63/63 | 46项契约逐项核对、UTF-8 inline/file/stdin、显式目标与权限、错误及批处理首错停止 |
| AOT 知识路径 | 10次调用通过 | 无数据库内置指南、隔离 SQLite 导入与分层查询 |
| JSON / TOML 最终场景 | 各15/15 | 每一步启动一个新 CLI 进程；保存、重开、冲突拒绝、AM模块、历史、恢复与筛选 |
| 场景作者与错误语义 | 8/8 | 格式等价、非法输入、授权边界、失败、中断和超时部分输出保存 |
| WPF业务基线 | 20/20 | 此前发布版实际控件、真实服务、隔离配置；重开、恢复与组合筛选 |
| 最终WPF/Core补丁 | 2/2 | 两类Host能力异常可见、窗口继续交互、0绑定错误；未重跑20项业务基线 |
| 安装器离线测试 | 9组 + 3用例 | 安装计划与CLI-only载荷；打包编译器使用替身，未实际执行安装 |
| 发布文档宿主 | HTTP 200 / 403 / 404 | 隔离HTML可读、POST拒绝、开发API不存在；不是DocFX页面视觉验收 |

代码能力用例逐个调用真实适配器或本地服务。AOT目录核验不能替代下面的逐项执行记录。Host错误分类补丁专项70/70通过，并进入最终217项完整suite。WPF均不启动AVEVA；20项业务基线与最终补丁2项烟测分别记录对应二进制SHA，不把旧版本的20项声称为最终版本重测。

## 46项操作逐项核对

下表每行均有独立正向调用记录。授权列中的PID表示每次必须提供 `--pid`；执行、写入、启动分别表示 `--allow-execution`、`--allow-write`、`--allow-launch`。注册函数调用还要求源码未变化且已授予该版本调用许可。元命令不计入46项业务操作。

环境说明：

- **合成发现**：发现器返回测试会话，未证明本机AVEVA发现。
- **合成Host**：真实原生适配器构造请求、核验身份/协议并分派到测试替身；未执行AVEVA/PML。
- **真实本地服务**：真实配置、文件、源码解析、SQLite或纯计算/内置指南；有存储的操作使用隔离测试资料。
- **注入进程工厂**：真实启动服务和权限检查，创建进程的边界由替身替代。

这四类离线环境均为 **noAveva**。表中的“通过”只适用于相应环境。

实机列的“前版通过”指补丁前冻结CLI的测试，最终版没有重复这些调用；“状态通过”与“旧Host不支持”来自最终CLI的5项定向复验。未实测的操作保留未实测。

| 操作ID | 旧工具 | 必要授权 | 离线环境 | 离线结果 | E3D 2.1实机证据 |
| --- | --- | --- | --- | --- | --- |
| `aveva.session.select` | `select_aveva_session` | PID | 合成Host | 通过 | 前版通过，未重跑 |
| `aveva.session.status` | `get_connection_status` | PID | 合成Host | 通过 | 最终版状态通过 |
| `aveva.sessions.list` | `list_aveva_sessions` | 无额外授权 | 合成发现 | 通过 | 前版通过，未重跑 |
| `builtin.usage` | `get_builtin_usage` | 无额外授权 | 真实本地服务 | 通过 | 前版通过，未重跑 |
| `knowledge.chunk.read` | `get_knowledge_chunk` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.database.build` | `build_knowledge_database` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.database.check` | `check_knowledge_database` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.databases.list` | `list_knowledge_databases` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.experience.record` | `record_local_experience` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.layers.search` | `search_knowledge_layers` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.search` | `search_knowledge` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `knowledge.source.register` | `register_knowledge_source` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.modules.list` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.default` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.delete` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.duplicate` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.favorite` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.history` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.list` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.preview` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.read` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.restore` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.restore.preview` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.save` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.start` | 新增 | 启动 | 注入进程工厂 | 通过 | 未实测 |
| `launch.profile.update` | 新增 | 写入 | 真实本地服务 | 通过 | 未实测 |
| `launch.profile.validate` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `launch.workspace.read` | 新增 | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `pml.array.read` | `run_pml_command_list` | PID + 执行 | 合成Host | 通过 | 前版通过，未重跑 |
| `pml.array.window` | `read_pml_array` | PID + 执行 | 合成Host | 通过 | 最终版确认旧Host不支持 |
| `pml.call.generate` | `generate_pml_call` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `pml.command.execute` | `run_pml_command` | PID + 执行 | 合成Host | 通过 | 前版通过，未重跑 |
| `pml.forms.list` | `list_pml_forms` | PID + 执行 | 合成Host | 通过 | 最终版确认旧Host不支持 |
| `pml.function.invoke` | `invoke_pml_function` | PID + 执行 + 函数许可 | 合成Host | 通过 | 未实测 |
| `pml.function.permission` | `set_pml_function_permission` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `pml.function.register` | `register_pml_function` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `pml.functions.list` | `list_registered_pml_functions` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `pml.gadget.read` | `get_pml_gadget` | PID + 执行 | 合成Host | 通过 | 最终版确认旧Host不支持 |
| `pml.gadgets.list` | `list_pml_gadgets` | PID + 执行 | 合成Host | 通过 | 最终版确认旧Host不支持 |
| `pml.trust.list` | `list_pml_function_trust` | 无额外授权 | 真实本地服务 | 通过 | 未实测 |
| `pml.trust.set` | `set_pml_function_trust` | 写入 | 真实本地服务 | 通过 | 未实测 |
| `pml.undo.begin` | `markdb` | PID + 执行 | 合成Host | 通过 | 未实测 |
| `pml.undo.end` | `endmarkdb` | PID + 执行 | 合成Host | 通过 | 未实测 |
| `runtime.calls` | `get_recent_tool_calls` | 无额外授权 | 真实本地服务 | 通过 | 前版通过，未重跑 |
| `runtime.status` | `get_ai_runtime_status` | 无额外授权 | 真实本地服务 | 通过 | 前版通过，未重跑 |
| `workspace.pmllib.inspect` | `inspect_registered_pmllib` | 无额外授权 | 真实本地服务 | 通过 | 前版通过，未重跑 |

旧33个工具中的30个业务入口已经映射到上表，其余3个元命令对应如下。没有启动MCP服务来执行这些调用；旧stdio入口返回 `MCP_DISABLED`。

| 旧元工具 | 原生CLI |
| --- | --- |
| `op_list` | `op list` |
| `op_describe` | `op describe <id>` |
| `op_invoke` | `op invoke <id>` |

需要写入、执行或启动的操作均实际验证了缺少对应授权时拒绝；拒绝码逐项保存在JSON。Host失败若可能已执行，会保留 `mayHaveExecuted`，不会自动重试，也不把批处理当作可回滚事务。

## 打包前实机记录与当时边界

E3D 2.1 已确认 `Current Session` / 当前模块、原生 `BANNER`，以及原 `YuzuhaAddin` 构造和 `YuzuhaVersion` 缺失错误的修复。该会话仍加载旧Host，自报版本 **0.3.2.0**，数组窗口与Form/Gadget协议版本均为0。新Host文件版本 **0.3.4.2** 已构建，待部署、重启及新协议实机复验；不能把合成Host通过写成新版Host实机通过。

补丁前冻结CLI执行18次调用，得到 **14通过、4不支持、0失败**，覆盖13个操作ID。自有内存变量、当前元素与DBREF回读通过，blank/unset/zero/emptyArray/FALSE保留，未解析行0，DBREF读取后恢复CE，自有探针已清理，没有数据库模型写入。这14次通过保留为前一CLI哈希的证据，未重复冒充最终版结果。

实机发现的四项旧Host拒绝曾被外层错归为 `INVALID_INPUT`；已用 `HostCapabilityException` 修复。最终冻结CLI只定向复核 **状态1项通过 + 旧Host不支持4项，0失败**。四项均返回退出码6、`PML_BOUNDED_ARRAY_UNSUPPORTED`或`PML_UI_HOST_UNSUPPORTED`，`mayHaveExecuted=false`，任何PML请求发送前即拒绝。正确拒绝已验证；数组窗口和Form/Gadget功能仍未在该旧Host上通过。

| 平台 | 本次状态 |
| --- | --- |
| E3D 2.1 | 上述局部实机通过；新Host和完整能力尚未实机验收 |
| E3D 3.1 | 统一映射到E3D3.1.6/NET48；只有映射，没有实机兼容性结论 |
| AM | 模块区分与配置生命周期离线通过；未实机启动 |
| PDMS | 配置/映射离线覆盖；未实机启动 |

StartupPolicy Object 在**已运行的真实 E3D 2.1.0.6 会话**完成7项验证。旧索引仅精确补一条Bootstrap记录并执行 `PML INDEX`，没有用发现性REHASH掩盖对象加载问题；原始Addin构造成功读取实际环境中的空配置，结果为 `SkippedDisabled`。

| 策略用例 | 原生结果 | 结果 |
| --- | --- | --- |
| 空策略 | `SkippedDisabled` | 通过 |
| `none` | `SkippedDisabled` | 通过 |
| 未知策略 | `Rejected` | 通过 |
| 开始日志写入失败 | `LogFailedBeforeExecution` | 通过 |
| 启用一次 | `Started` → `Succeeded` | 通过，实际REHASH一次 |
| 相同策略重复调用 | `SkippedAlreadyApplied` | 通过，未再次执行 |
| 启用后关闭 | `SkippedDisabled` | 通过，未再次执行 |

启用和异常条件通过 `Apply(policy, logPath, launchId)` 显式注入；没有修改真实EVAR。日志中Started与Succeeded各1次，临时安装文件、索引与guard已恢复。**这不是新进程启动测试**，也不扩展为AM、PDMS或E3D3.1通过。实际创建启动入口进程同样不等于项目登录、模块许可和Host连接成功。

本次完成的是启动配置切面。官方HTML/源码默认优先知识流程、其他配置页面和完整工作台业务仍需分别实现和验收；已有知识操作可调用不代表这一默认体验已经完成。

## 复核与交付标识

正式入口为 `runtime/cli/PmlTwin.Phayton.Cli.exe`，Native AOT 文件版本 `0.3.4.2`。对应冻结产物位于 `artifacts/native-cli-release-final`，SHA256：

```
4f704ac26cc532906d23188c7b1c9e52522f00cb6dc9c088d56fe5eeda291988
```

公开JSON逐项保存操作ID、旧工具、授权、环境、结果，以及下列证据的仓库相对路径与SHA256。原始回执保留在本机；不会把用户名、用户路径、模型内容或知识库复制进公开文档。

| 证据 | 相对路径 |
| --- | --- |
| business | `artifacts/native-cli-migration/capabilities.json` |
| launch | `artifacts/native-cli-migration/launch-capabilities.json` |
| legacy | `artifacts/native-cli-migration/legacy-tool-coverage-final.json` |
| aot | `artifacts/native-cli-migration/native-aot-processes-final.json` |
| knowledge | `artifacts/native-cli-migration/native-aot-builtin-final.json` |
| failurePaths | `artifacts/native-cli-migration/failure-paths.json` |
| serviceFailures | `artifacts/native-cli-migration/service-failures.json` |
| json | `artifacts/launch-lifecycle-scenarios/final-host-errors-json/run.json` |
| toml | `artifacts/launch-lifecycle-scenarios/final-host-errors-toml-retry/run.json` |
| ui | `artifacts/launch-lifecycle-ui/published-20/launch-lifecycle-ui.json` |
| uiFinal | `artifacts/launch-lifecycle-ui/final-core-verified/final-published-ui-smoke.json` |
| installerFinal | `artifacts/install-0342-plan/installer-index-tests-final-reviewed.log` |
| runner | `artifacts/native-cli-migration/scenario-runner-audit-tests.log` |
| startupPolicy | `artifacts/native-cli-live/startup-policy.summary.json` |
| liveBeforePatch | `artifacts/native-cli-live/capabilities-release-02/summary.json` |
| liveFinal | `artifacts/native-cli-live/capabilities-final/summary.json` |

完整测试TRX位于 `artifacts/workbench-complete-tests/release-final`。复刻命令与场景格式见[工作台开发、测试与使用约定](workbench-lifecycle.md)。每次使用新输出目录；失败或中断保留独立回执，不覆盖成通过。

最终TOML首轮曾因工作区报告文件原子替换遇WinError5中止；该目录与中止说明保留，不计通过。相同冻结程序和案例在新目录重跑15/15通过，公开证据使用该重跑目录。

<a id="post-install-acceptance"></a>

## 安装后追加验收 · E3D 2.1 / Host 0.3.4.2

正式安装完成后，**真实新进程中的11项检查全部通过，0项不支持、0项失败**。已安装CLI仍为上文冻结SHA，Host自报 `0.3.4.2 / NET48 / DESIGN`，数组窗口与Form/Gadget协议均为1。下面的结果补充打包前旧Host记录，不覆盖或否定旧Host当时的拒绝。

### 安装与接入

- 正式安装核验4738个文件，0失败；仅接入1个AVEVA环境（E3D 2.1），环境配置与PML索引均匹配计划。
- Skill已应用并核验14个目标文件；本次4个文件更新并备份，未删除用户文件，受保护配置保持不变。
- 2个旧独立MCP程序的原入口已备份并停用；Skill应用没有重写MCP配置或安装器所有权。
- 安装收据当时只记录 `configured-not-launch-tested`；随后新进程的11项实机记录才构成本次启动与Host验证。

### 新Host的11项实读

| 检查 | 操作ID | 结果 |
| --- | --- | --- |
| 已安装Host身份与协议核验 | `aveva.session.status` | 通过，退出码0 |
| 新进程自动启动策略回读 | `pml.array.read` | 通过，退出码0 |
| 初始化后的Current Session | `pml.array.read` | 通过，退出码0 |
| 准备自有内存数组探针 | `pml.command.execute` | 通过，退出码0 |
| 原生BANNER与产品模块核验 | `pml.array.read` | 通过，退出码0 |
| 有界数组第一页与空字符串 | `pml.array.window` | 通过，退出码0 |
| 有界数组续读页 | `pml.array.window` | 通过，退出码0 |
| 现有官方窗体第一页 | `pml.forms.list` | 通过，退出码0 |
| 现有官方窗体Gadget筛选 | `pml.gadgets.list` | 通过，退出码0 |
| BUTTON固定元数据与unavailable | `pml.gadget.read` | 通过，退出码0 |
| 清理自有全局探针 | `pml.command.execute` | 通过，退出码0 |

此前不支持的4项能力现在已实读通过：数组窗口验证两页、续读与空字符串保留；Form查询读取现有官方窗体的第一页25项（上界270、下页26）；Gadget查询扫描35个成员、返回6个Gadget并排除普通成员。BUTTON的shown、active、visible、callback可读；**tooltip在该平台没有可读成员，明确返回unavailable并保留原生错误**，没有伪造空值或false，也不宣称支持全部属性。

新进程从真实环境读取 `YUZUHA_STARTUP_ACTIONS=NONE`，在人工探针前已自动得到 `SkippedDisabled`；此次没有手动调用Apply。启用、重复防护等其他策略仍对应此前7项参数注入验证，不能混为新进程实测。没有模型写入、回调执行或窗体生命周期变更，自有全局变量已清理。AM、PDMS及E3D3.1仍未实机验收。

### 文档快照边界

**这是一份安装后追加记录。安装包及已安装的DocFX仍是打包时快照，没有重打包，也没有修改已安装文档。** 打包时公开JSON的SHA256为：

```
643d201d32cf9895167af821ea3fd45a262708bccda22dc072a36c888e05babd
```

完整旧JSON已保存在 `artifacts/install-0342-plan/validation-packaged.snapshot.json`；本页前面的操作表、旧Host和7项注入测试保留打包前语境。当前仓库JSON的7个相关操作已补最新实机状态并保留此前记录。

脱敏独立 HTML 报告仅在维护者本机留存，不随仓库发布：`artifacts/install-0342-plan/acceptance-final.html`。安装后证据分别为 `artifacts/native-cli-live/new-host-acceptance/summary.json`、`artifacts/install-0342-plan/installed-verification.json`、`skill-final-01-apply-result.json`、`skill-final-01-installed-verification.json` 与 `legacy-mcp-retirement-result.json`（后三项位于同一安装证据目录）。这些原始收据也仅在本机保留；公开的[逐项 JSON 摘要](../validation/2026-10-24-launch-cli.summary.json)不包含用户名、安装绝对路径或用户知识库。
