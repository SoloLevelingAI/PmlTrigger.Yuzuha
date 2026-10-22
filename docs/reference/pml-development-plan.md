# PML 开发工作流与授权管理开发文档

更新日期：2026-10-22。本文是本轮需求、实现状态与验收证据的持续记录，后续实现必须同步更新，不能只保留在聊天中。

## 用户要求

1. 检查仓库中的 PR，按依赖顺序合并已满足条件的改动；不能把“无文本冲突”当作已经完成验证。
2. 盘点日志、能力/函数注册、CLI 与授权管理，补齐缺失功能。
3. 授权支持开发模式、项目内规则规避以及仅允许登记的保守模式。
4. CLI 可以明确连接开发环境、编写与调试 PML，产出供人检验的测试代码和执行证据。
5. 每个会话中的执行在开始前建立 MARKDB 撤销分组；只有已经确认只读的操作可以豁免。未知操作按可能写入处理。

MARKDB 是撤销分组边界，不是保存、事务回滚或执行成功证明。模式切换不能取消目标身份核验、日志、源码身份和 MARKDB 约束。项目规则规避必须有明确配置及可追溯原因，不能由源码、检索结果或报错隐式开启。

## 功能清单与验收

| 编号 | 功能与要求 | 本轮开始时的实际状态 | 完成条件 | 当前状态 |
| --- | --- | --- | --- | --- |
| DEV01 | PR 整理与集成 | #3–#7 为草稿；#5→#6→#7 是叠加关系；#8 已合并 | 记录每个 PR 的目标、SHA、检查和合并结果；冲突改动经过复验 | 集成中，#5/#3 等待检查 |
| DEV02 | 每次调用与每个会话的日志 | 已有本机 RuntimeJournal、工具审计和 Host 回执；筛选/导出未接入 WPF | CLI/WPF 可按会话、工具、结果查询并导出；记录授权模式、Markdb 与失败/未知结果 | 已实现，验证记录见下文 |
| DEV03 | 能力和函数注册 | 已有源码 SHA256、标量签名、独立 AllowInvoke 与源码改变拒绝 | 统一 CLI 查询、登记、撤销、校验和调用；测试关联和风险分类可审阅 | 已实现，验证记录见下文 |
| DEV04 | CLI 开发环境连接 | 已有 CLI/MCP 双模式与 batch；单次进程不保留会话连接 | 显式选择 PID/启动身份/模块，连接与执行同一会话；可复用受约束的开发环境配置 | 已实现，最终检查进行中 |
| DEV05 | 授权模式 | 已有逐函数许可和信任列表，尚无统一模式 | 保守模式仅登记；标准模式逐项授权；开发模式限定会话和源码目录；模式及规则规避可查询、撤销、记录 | 已实现三档，默认 Standard |
| DEV06 | 自动 MARKDB | 已有手工 markdb/endmarkdb 工具，未统一拦截执行 | 非只读执行在派发前 MARKDB，失败时不执行用户代码；记录分组和结束结果；不自动重试未知结果 | 已实现，最终检查进行中 |
| DEV07 | 只读豁免 | 原生 Form/Gadget 和数组窗口工具有类型化查询边界；原始 PML 尚无确认机制 | 只读声明与已核验源码/操作类型绑定，修改源码后失效；不能仅凭名称/关键词或调用方一句话豁免 | 已实现，最终检查进行中 |
| DEV08 | 编写 PML 与人工检验测试代码 | 已有函数登记和 tests/live/form-query 合成脚本 | 限定开发目录，保存源码/测试及哈希；分开记录未运行、通过、失败、未知与人工确认；禁止自动保存工程 | 已实现，最终检查进行中 |
| DEV09 | 自动化验证与发布说明 | 已有 xUnit、架构、Native AOT、NET35/48 合成 Host、WPF 和文档检查 | 覆盖模式拒绝、会话错配、源码变化、Markdb 失败/未知、只读豁免和 CLI 完整流程；实机结果独立记录 | 已实现，最终检查进行中 |

## 拟定授权模型

- **保守模式**：允许登记、查看、校验和管理能力；不派发 PML 执行。
- **标准模式**：沿用明确的逐函数许可；源码或目标身份变化要求重新核验。
- **开发模式**：仅面向明确选择的开发会话与源码根目录，允许编写、调试及显式规避项目内可配置规则；保留身份核验、审计和自动 Markdb。
- 规则规避应注明适用规则、范围、原因和有效期/撤销方式，不允许全局隐式绕过。

用户已要求上述能力进入开发文档；用户已确认按上述三档实现。开发模式只允许显式配置的项目规则规避，身份核验、源码核验、日志和自动 MARKDB 不可规避。

## 实施顺序

先审查和集成可合并 PR，保留已经完成的 v08 WPF 工作区；随后以合并后的代码盘点能力，统一执行策略与 Markdb，再补 CLI 开发入口、日志查询/导出和 WPF 授权管理。每完成一项更新上表并附实际测试证据。

## 执行与验证边界

任何开发执行都先明确目标会话。新建进程、连接成功、PML 执行回执、测试通过以及工程保存分别记录，不能互相替代。MARKDB 失败后不派发用户代码，传输超时可能表示远端已经执行，禁止自动重放。未经实机验证的项目保持 NotRun。

## 本轮记录

- 2026-10-22：建立本文，逐项保留用户需求。开始盘点 #3–#7；设计稿 #8 已合并。v08 WPF 和配置库改动已保留并推送 #5。

## 已实现的执行规则

- `ExecutionAuthorizationStore` 保存 `authorization.json`，默认 **Standard**。Conservative 禁止所有 PML 派发，仍可发现会话、登记、管理许可、查询日志。Standard 允许源码未变且已授权的注册函数，以及有类型边界的原生数组/Form 查询。
- Development 通过 `connect_development_session` / WPF 设置页明确绑定 PID、进程启动 ticks、模块、已有源码根目录、原因和 1–24 小时有效期（默认 8 小时）。`raw-command` 和 `function-permission` 分别控制原始命令与逐函数许可的项目规则规避。切回 Standard/Conservative 即撤销开发许可。
- 根目录限定 CLI 文件写入、执行快照及登记函数来源，拒绝越界、链接和重解析点。PML 本身仍有宿主进程权限，**这不是操作系统沙箱**；函数所调用的其他代码也需审阅。
- 所有客户端执行统一通过 `YuzuhaRpcBridge`，每次重新核验完整 Host 身份、授权和源码。旧 Host 缺少自动分组协议时，在正文执行前拒绝变更，需更新对应 NET35/48 Host。
- Host 在一个主线程 RPC 回调内执行 `MARKDB → 正文 → ENDMARKDB`，建立失败不执行正文，结束失败保留正文回执并报告失败。老客户端发送的普通命令也由新 Host 默认分组。手工 `markdb/endmarkdb` 兼容名称保留，但不能嵌套管理中的分组。
- 只有类型化的已有数组/Form 查询和人工确认的准确注册源码版本可免分组。只读确认通过 `confirm_pml_function_read_only` 绑定 SHA256 和审阅依据，重新登记会撤销；调用前和 Host 端再次核验文件。**本机源码哈希不能证明 AVEVA 内存中已加载的版本**，确认前由审阅人核对已加载版本及全部被调用函数。表达式赋值和结果数组清理仍分组。
- 传输失败、取消、超时可能表示已执行；日志记录 Unknown，不重放。同一连接在未知结果或分组未结束后拒绝继续派发，先检查 Host，再显式重连。

## CLI 开发工作流

执行入口仍是 `YuzuhaToolkit.Mcp.exe`；`--cli dev` 是同一 MCP 工具的简写，不维护第二套执行策略。完整参数也可使用 `--file arguments.json` 或 stdin；源码建议从 JSON 文件输入，避免 shell 展开 PML 的 `$` 和引号。

```powershell
# 发现，不自动选择 PID
.\YuzuhaToolkit.Mcp.exe --cli dev sessions
# 将下面的 PID、目录、模块替换为发现并确认的实际开发目标
.\YuzuhaToolkit.Mcp.exe --cli dev connect --arguments '{"processId":1234,"expectedModel":"Design","sourceRoot":"D:\\PmlDev","reason":"调试当前插件","bypassRules":"","validHours":8}'
.\YuzuhaToolkit.Mcp.exe --cli dev status
# write-args.json: relativePath / content / kind（source 或 test）；覆盖另需 expectedSha256
.\YuzuhaToolkit.Mcp.exe --cli dev write --file write-args.json
# run-args.json: relativePath / sourceSha256 / 可选 testOutcomeGlobal
.\YuzuhaToolkit.Mcp.exe --cli dev run --file run-args.json
.\YuzuhaToolkit.Mcp.exe --cli dev logs --arguments '{"targetPid":1234,"phase":"failed"}'
.\YuzuhaToolkit.Mcp.exe --cli dev export --arguments '{"outputFile":"D:\\PmlDev\\events-01.jsonl","targetPid":1234}'
.\YuzuhaToolkit.Mcp.exe --cli dev mode --arguments '{"mode":"Conservative"}'
```

其余简写：`register`、`functions`、`allow`、`readonly`、`invoke`、`evidence`。它们对应现有登记与新证据工具；`--cli list` 返回实际工具 Schema。Standard 下单次 CLI 调用不保留选择，使用 batch 中先 `select_aveva_session` 再调用注册函数；Development 的 `run` 使用持久身份并重新核验，不因 PID 重用改连其他工程。

### 源码、测试与人工检验

`write` 只创建 PML 文件；覆盖必须提交当前文件准确哈希。每次写入创建/重置 `<source>.evidence.json`，记录源码版本、source/test、NotRun 和 HumanReview=Pending。`run` 仅运行 `.pmlmac` 测试/加载脚本，复制到根目录内 `.yuzuha-runs` 的独立快照，并由 Host 再次检查快照哈希。函数和窗体加载动作必须写在可审阅的测试脚本中；不自动改 PMLLIB，也不自动 SAVEWORK。

没有断言协议时执行成功只记 **Dispatched**。可选 `testOutcomeGlobal` 指定测试专用 STRING 全局变量；Host 在同一分组内先置 `NotRun`、运行宏、读取结果、最后删除该变量。测试代码必须明确写入 `Passed` 或 `Failed: 原因`，没有报告或沿用旧值不会计为通过。HumanReview 始终保持 Pending，自动测试不会伪造人工审阅。示例见 `tests/live/development/arithmetic.pmlmac`，实机状态仍为 **NotRun**。

日志按 PID + 启动 ticks、工具、结果和文本筛选，导出新 JSONL 文件；不覆盖已有证据。WPF 工具日志页支持文本/PID/结果筛选和导出，设置页支持三档授权与开发连接。保留日志是有界滚动窗口，不当作全量历史。

### 云端继续开发的边界

代码、设计和本开发文档进入 GitHub PR；云端可以编写代码、审阅测试并运行离线检查。AVEVA 连接使用本机 PID 专用 Named Pipe，执行 CLI 必须运行在 AVEVA 所在 Windows 主机；当前没有把本机 RPC 暴露到公网的桥。云端测试通过不能替代本机 AVEVA 实机验收。

## PR 与验收进度

- #8、#9 已合并；#7 合入 #6（`53ee59a`），#6 合入 #5（`a65188a`），#4 合入 main（`778c155`）。
- #5 已整合主分支的 Phayton、v08 WPF 和配置库，处理 Core 拆分与 AOT 元数据冲突。修正了独立 MCP 构建和文档测试入口定位，等待最新云端检查后合并。
- #3 启动 REHASH 修复仍需解决主分支文档冲突并复验。
- 新授权、自动分组、CLI 开发、日志及证据会推送独立 PR，保留供下次云端继续工作的入口。
- 已通过：开发解决方案编译；13 项新增执行/授权回归；现有实际 MCP 注册权限回归；40 项 Phayton 回归。完整 xUnit/架构、Native AOT、NET35/48 实际服务合成测试、WPF 与完整文档构建还在最终复验，结果进入批次验证文档。
- 本轮没有选定或执行任何真实 AVEVA 会话；没有保存工程，实机验收保持 NotRun。
