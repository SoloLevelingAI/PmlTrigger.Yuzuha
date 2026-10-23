# PMLTWIN 迭代与意见记录

这里集中记录用户意见、已采纳的决定、实现状态和验收入口。后续迭代沿用本页的编号，避免意见只留在聊天中。具体行为解释进入功能文档，运行结果进入批次验收报告；登记或采纳意见不表示功能已经实现。

[后端调用链与排查](backend-tracing.md) · [理解与排查笔记](understanding-notes.md) · [文档维护](documentation.md)

## 2026年10月23日 Walker、Form 信息与开发链路

本轮追加：TRACE17–TRACE23 经两份预览获批及客户端关闭后完成本机更新；E3D 自动 REHASH 和桌面共用后端通过，较早完整 ELDATA 通过，最终大数组回读发生 IPC 中断和无响应，尚不能验收稳定性。AM 发现缺口与当前未知结果见[桌面接入记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-desktop-pml-integration.md)。

| 编号 | 用户意见与采纳决定 | 当前状态 | 验收条件与去向 |
| --- | --- | --- | --- |
| TRACE39 | R2 正式落地到桌面与 DocFX，统一 Phayton，保留调用与用户数据；测试后打包，更新前预览并提醒关闭客户端 | 正式实现、回归与 0.3.4 打包通过；用户批准具体预览并关闭客户端后已完成本机更新，3,247 个发布文件及 137 个用户/配置文件核验通过；安装后桌面、只读文档与 CLI 检查通过 | [正式版说明](phayton-r2-production.md)与[本批记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-phayton-r2-production.md)；AM/E3D 环境文件原样保留，历史 AVEVA 待验项保持；当前 AI 客户端需重启加载已释放的旧 MCP 服务 |
| TRACE38 | 按 R2 补齐知识库、设置、DocFX，完整可操作，统一 Phayton，正式界面和调用先不改 | 独立 R2 交互预览、七画板原生设计页、知识库修订恢复、设置分组与确认、DocFX 搜索阅读与 API 入口 | [R2 说明](phayton-r2.md)和[浏览器验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-phayton-r2.md)；演示状态不等于后端接入或 AVEVA 复验 |
| TRACE37 | 新版逐步使用 Phayton 或 Phayton-Yuzuha 名称 | 设计、预览、演示和视频使用 Phayton；旧可执行文件、方法、环境变量、目录和注册名保留 | [名称过渡](phayton-demo.md)；运行标识迁移属于后续独立兼容性批次 |
| TRACE36 | 除开发指南还需演示和视频，突出 AI 连接 E3D 并自行开发的能力 | 已制作中文 1080p 介绍初剪、十幕演示、字幕和五分钟复刻脚本 | [演示手册](phayton-demo.md)；历史实测、设计画面和未验收能力分别标注 |
| TRACE35 | 打开桌面及 DocFX 完整宽屏预览，重点启动、知识库、设置；现有 FigCraft 变化不足 | 增加宽屏评审器和页面直达；R1.1 名称过渡，R2 工程优先布局另存 | [设计与演示](phayton-demo.md)及本批记录；正式界面不替换 |
| TRACE34 | 桌面与 DocFX 使用 FigCraft 重做信息组织、自绘窗口视觉、适量动效；用户选择先交付设计和可操作预览 | 独立 R1 设计、七画板与 HTML 交互预览；生产界面与现有调用未替换；未发现 EffectCraft 可用入口 | [设计约定](clear-workspace-design.md)及[本批验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-installer-ui-preview.md)；原生边框、全功能映射与真实运行状态留待落地验收 |
| TRACE33 | 打出新版 Inno 包、先给本机预览、保留全部现有配置数据；用户已关闭客户端并授权更新；签名在另一台电脑 | 已真实安装，352 载荷校验一致，31 静态配置/数据及 PML 索引、2 环境文件保持；安装版桌面隔离自检通过 | [安装验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-installer-ui-preview.md)与[签名交接](signing-handoff.md)；本地包未签名，未重启 AVEVA 复验 |
| TRACE32 | 先交付统一用户目录、推荐可改路径、知识库可视化增删查改；测试后提交，更新前提醒关闭客户端 | 源码实现目录预览/复制核验/切换，笔记版本与回收站、资料索引管理、升级用户数据保护；现有调用保留，尚未更新本机 | [操作说明](user-data-and-knowledge.md)与[验证记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-user-data-knowledge.md)；安装需另行预览及关闭客户端确认 |
| TRACE26 | 核对 AVEVA 读写、Attribute 无响应以及 Form 表格和自主开发链路 | 静态核对完成；原生 LIST/按钮及开发夹具有实测，数据库任意写入、大数组稳定性和 AM 全链路未验收 | 见[能力核对](runtime-capability-audit.md)，恢复后分段测量，不能按事件猜测根因 |
| TRACE27 | 健康度改为重复条目与 PML 类型/文件夹存储占用 | 源码增加同名候选组/多出文件数、类型/直属目录字节及明确部分扫描；未覆盖本机安装 | 同类型大小写重复、排除索引/备份、聚合总量及部分扫描测试；不冒充内容去重或运行内存 |
| TRACE28 | 更新保留用户数据，设置中提供推荐且可改的个人目录 | 已由 TRACE32 实现共用路径和显式复制迁移，安装器拒绝覆盖用户改动 | 用户数据独立，升级/回退哨兵验证，同名发布文件修改需冲突处置 |
| TRACE29 | 知识库需要可视化增删查改 | TRACE32 增加个人笔记增删查改与修订历史、资料索引管理和回收站；原文不自动修改 | 原文、索引、登记、经验版本分别管理，删除可恢复，GUI/CLI 使用同一路径 |
| TRACE30 | CLI 可独立用；XML/源码扫描登记能力，支持宽松或仅登记策略 | CLI 不依赖客户端 MCP 注册；当前函数登记范围有限，原始命令未统一受控；XML 自动登记与统一策略待实现 | DocFX 展示契约，运行时按本机策略执行；作者风险声明不自行授予权限，版本哈希及未知风险保留 |
| TRACE31 | 补全文档并以后用视频展示核心能力和辅助 UI | 原记录为四段提纲；TRACE36 已追加介绍初剪、分幕演示及复刻脚本 | 每项关联操作、版本、验收和故障记录，视频带版本日期与可复现夹具 |
| TRACE25 | 大数组回读后的无响应必须区分提取、传输与加载状态 | 最终 execute-macro 成功，read-results 断管且 mayHaveExecuted=true；用户确认无响应，后续只读采样仍显示单线程忙碌，已停止派发 | 当前轮 Unknown，根因未定，未自动重发或强制结束客户端；保留前一轮成功，不能据此覆盖最终失败/未知；恢复后分别测量宏、总量和少量回读 |
| TRACE24 | 新开 AM/E3D 后核验真实启动 | E3D 自身启动数组回读通过，二者日志及专用管道存在；AM marodes/mardra 不在现有发现器白名单 | AM 的 IPC 选择与全流程未验证；保留核验边界，不用进程枚举结果绕过发现器 |
| TRACE21 | 日常启动保持浅色，仅填写项目、用户、密码、MDB；模块选择避免反复手填 | 模块/MDB 改为可编辑下拉，安装路径和参数折叠；默认浅色保留 | 新版编译通过，现有配置与启动协议保留；完整新界面实测待后续操作 |
| TRACE22 | 清除密码易误触，移入设置并加二次确认 | 设置中选配置后确认，默认取消；取消记住本次密码不再暗中删旧凭据 | 确认前不写设置、不删凭据；设置写失败回退，共用引用保留 |
| TRACE23 | 简单原生 PMLFRM 可以接受，复杂表格优先 HTML | 原生用于简单输入、确认和少量按钮；HTML 用于复杂表格、筛选、排序和导出，仍由 IPC 调用 PML | E3D 原生 Form/Object 部署后端 v1→v2 的 10 步及 8 项结果通过；不扩大为全部控件或 AM 兼容性通过 |
| TRACE20 | 启动工程新增配置太麻烦 | 产品选择移至主表单，自动填写路径和生成名称；可从 LaunchDesk 读取非密码配置并选择一项带入草稿；高级字段折叠 | 编译及相关 18 项回归通过；尚未安装简化版本，用户停止桌面控制后未继续界面操作 |
| TRACE17 | 桌面接入 HTML 管理器、复制、REHASH、Object/Form 重载与结果核验；保留既有调用 | 桌面按钮打开独立本地 HTML；新增可预览的完整开发流程，复用现有 IPC；CLI 增加 web open，不内嵌 WebView | 完整运行必须核对固定会话、实际 PMLLIB、来源/目标散列、重载后业务值和证据；离线通过与更新后实机分开记录 |
| TRACE18 | Phayton 可能仍有逻辑问题，先复验，再提醒关闭 AM/E3D 后更新 | 更新前 40 项离线回归、既有 E3D Walker smoke 和 2410 项 ELDATA 链路复验通过；未改 Walker 源码 | 仅覆盖列明用例；不代表所有输入无缺陷。用户关闭客户端和批准预览前不覆盖安装文件 |
| TRACE19 | AM/E3D 每次启动可用环境变量控制启动动作，特殊启动需要记录 | 新增 YUZUHA_STARTUP_ACTIONS=none/rehash-all；原生 Addin 在 PML 就绪后、Host 建立前执行并记录；安装预览可显式选择，更新省略时保留已有设置 | 默认 none；桌面显式策略优先；每个 PML 会话仅一次，失败不重发。真实启动及 AM/E3D 分别验证，未运行不能写通过 |
| TRACE10 | 新版 Walker 保留 VAR SPLIT，接清取值、格式化和下标遍历；DocFX XML 注释，先不改旧调用 | 新对象独立同步，2410 项 ELDATA 实测通过，旧调用保留 | [对象说明](phayton-object-walker.md)与[实测报告](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-phayton-object-walker.md)记录哈希、2412 行解析及范围 |
| TRACE11 | Form/Button 和全量信息参考用户提供的 Marine pmlforms.pmlfrm | 已核对分类与展开；现有工具只有部分属性 | [链路说明](pml-development-flow.md)保留来源指纹、差距与验收；扩展未实现，Marine 未实测 |
| TRACE12 | 明确需求格式，核实编写、复制 PMLLIB、REHASH ALL、Form/Object reload 链路及文档 | 需求与决定用 Markdown，契约用源码 XML，目录关联用 JSON，DocFX 展示；已有复制和命令入口，通用自动串联待实现 | 每步关联目标、版本、回执和实际行为；见[开发与加载链路](pml-development-flow.md) |
| TRACE13 | HTML 作为界面，复用后端和 AVEVA 能力 | 方向已记录；应用管理与运行桥仍为计划，开发网页不代表生产接通 | 通过本地桥复用服务/Host，关联会话和结果，验收一条真实场景；见[HTML 范围](pml-development-flow.md#html-作为界面) |
| TRACE14 | 推送 Walker 提交，打通复制到 PMLLIB、REHASH ALL、Form/Object 重载和验证，保留现有调用 | 833fc9a 已推送；开发脚本已串联并在 E3D 完成 v1→v2、备份及 8 项值核对；旧桌面入口不变 | [本批实测](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-pml-development.md)记录失败诊断、原生按钮交互、版本/哈希和跨版本边界 |
| TRACE15 | PML 导出数据繁琐，改用 IPC 信道和 HTML 表格；从 pmlforms 借鉴编程思路 | PML 负责取值，IPC 返回 Items；已提供 HTML 快照的筛选/JSON 导入/CSV 导出。原生 Form 用属性分类方式实现，修正 GADGET/MENU 局部变量类型冲突 | 当前是已读数据的展示导出；持续 HTML 调用桥待接入，不把生成网页当作实时联通；见[链路与界面选择](pml-development-flow.md) |
| TRACE16 | 做本地 HTML 应用管理器，提供 Form 分类、按钮绑定、参数配置，经现有 IPC 实测 ELDATA 和结果表；接入由程序负责，兼容性优先且不用 PMLCMD | 独立 WebApps 已提供本地分类与编辑、对象/函数/Form 实时调用；E3D 2.1 的 2410 项、首中尾、预算、函数与 Form 通过；现有调用不改 | [使用及边界](html-app-manager.md)、[实测](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-html-app-manager.md)；远程市场、第三方 HTML 包及其他版本兼容性待完成；ELDATA 业务编码未解码 |

## 2026年10月22日 后端溯源方向

以下意见来自本次用户审阅调用链后的反馈。核对的代码基线为 `7050ac8a81cbc95cf7f7eaada5248c53aed4bbb3`。

| 编号 | 用户意见与采纳决定 | 当前状态 | 验收条件与去向 |
| --- | --- | --- | --- |
| TRACE01 | 重点看后端每一步调用了什么、执行什么计算和判断，能够快速理解和排除问题 | 已补实际函数体、逐级调用树及有依据的 RPC 到 Host 对应；运行值未采集 | 关键步骤能定位输入、处理规则、输出、失败分支、源码和证据；见[后端调用链](backend-tracing.md) |
| TRACE02 | 前端 ViewModel 优先关注用户所称的 Valid / Validate 部分，其他界面关系作为辅助 | 已采纳，确定阅读优先级；未改业务代码 | 保留校验、CanExecute、拒绝原因、状态变化和实际业务入口；不因名称猜测存在独立的 Valid 模块，也不删除已有关系 |
| TRACE03 | 图能展示、静态可达、测试通过和真实后端执行成功需要分别判断 | RPC 对应附路由常量、契约、注册三处证据；不声称实际分派或实机通过 | 接口候选、分支、委托、RPC 和 PML 动态边界明确标出；未知不能当作安全或通过 |
| TRACE04 | 迭代文档和审阅意见需要一个持续可查的位置 | 本页与 DocFX 导航已建立 | 新意见保留编号、决定、实现状态、验收入口；改变决定时保留原因与替代项 |
| TRACE05 | 引入便于理解的方法；5W2H、康奈尔笔记只是示例，不代表最佳或必选 | 已采纳，建立[按问题选择方法的说明](understanding-notes.md) | 每条笔记解决明确问题，保留来源、证据和结论；无帮助的模板字段不填、不引入额外系统 |
| TRACE06 | 从按钮/事件到后端命令，再逐级深入使用函数；现有网状图不清晰；用 Roslyn 和模板实现 | 默认改为逐级调用树；同一方法不同调用处分开，父级路径可点击；复用原有分析器、模板、源码和测试关联 | 13 个页面沿真实关系浏览；可定位调用实参、语法条件、包装器和独立回调；Form 查询可继续到两个 Host 的算法与 AVEVA 外部执行边界。见[本批验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-call-hierarchy.md) |
| TRACE07 | 依据脚本优先基线形成 PMLTWIN 设计约定，并说明 DocFX 的实际组织、自动发现和人工增删改查 | 已形成[项目设计约定](pmltwin-design-contract.md)、[目录维护说明](catalog-maintenance.md)与可搜索的功能/Script 目录；人工意图集中维护，真实签名与关联继续自动生成 | 原生源码自动发现，C# 候选经人工选择；增删改查可按真实文件与命令复刻，错误引用阻止发布，元数据不授予审核/执行或通过状态。见[本批验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-design-contract-catalog.md) |
| TRACE08 | DSH 调整 Web 美术与代码后无法运行，需要保留调整并修复 | 保留五组导航、首页、阅读样式及 Core 拆分；修复 API 元数据归属、Script 类型、源码与兼容夹具引用，并恢复正式预览宿主 | 新增失败回归测试，完整开发、Native AOT、NET35/NET48 离线兼容和 DocFX 验证；实机仍为 NotRun。见[恢复记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-web-repair.md) |
| TRACE09 | 原始软件设计要求需可在另一台电脑获取，并记录版本、来源、哈希、Draft 和项目适配 | 原字节 Word 版本归档、可检索原稿、[18 项适配及 D01–D13 处置](design-adaptation.md)接入 DocFX；原稿与当前约定分别维护 | 构建核对原稿/发布副本哈希、下载资源和适配定位，不把归档或历史样例当人工批准/当前通过。见[本批验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-design-baseline-archive.md) |

## 延续的开发约束

2026年10月22日补充：根据 PR #8 的 v08 设计更新 WPF，范围经用户确认为现有页面和已有后端支持的配置操作。明暗主题、14 个主页面、配置库的搜索/收藏/默认/复制/名称确认删除已完成离线验证；新增原型业务保持实际接入边界。见 [v08 WPF 验证记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-v08-wpf.md)。

| 决定 | 本轮如何落实 |
| --- | --- |
| 优先复用现有功能，不为文档或设计改接口 | 继续使用现有 Roslyn、DocFX、test-map、按测试 ID 执行器；调用树改动限于分析工具、模板、测试及文档，没有改生产接口 |
| 设计建议与已实现功能分开 | 人工核对示例、现有自动分析和后续验收条件分别标明；界面存在按钮不表示后端已接通 |
| 复用 PMLFORMS / FMSYS，不修改原生 PMLLIB / PMLUI | 示例沿现有查询路径解释；AI 编写的测试 PML 保持独立目录，人工审核后迁移 |
| 不保存用户 AVEVA 工程 | 本批不启动或操作 AVEVA；实机复刻继续使用自有夹具，关闭会话时不保存工程 |
| 测试通过后提交，PR 保留人工审查 | 新功能继续先加测试再实现；本批文档沿现有验证脚本检查，不增加生产接口、数据库或笔记服务 |

## 后续批次与完成条件

以下保留原定顺序，按实际状态验收；自动静态溯源不关闭实机或运行记录待验项。

| 顺序 | 批次 | 可独立验收的结果 |
| --- | --- | --- |
| 1 | Form/Gadget 后端链路 | 静态树已连接按钮、稳定路由、两个 Host、查询扫描与原生执行边界；可阅读分页、预算、错误和清理源码。FMSYS 内部和本次运行值仍需原生源码或实机证据 |
| 2 | 对象与注册函数链路 | 对象查询解释内置函数与结果解析；函数调用解释源码版本、授权、参数编码、目标核验和回执；超时不重放，权限不由图推断 |
| 3 | 其余后端与排查入口 | 按实际风险补充启动、凭据、部署、知识查询和日志；优先展示改变结果的计算、状态和写入边界，重复包装可折叠 |

每批验收报告记录提交、工作区状态、环境、测试 ID、实际结果和本机证据位置。只有与该范围对应的证据齐全才关闭条目；文档构建通过不能关闭 AVEVA 实机待验项。

## 已有批次报告

| 报告 | 已记录的范围 | 阅读时的边界 |
| --- | --- | --- |
| [v0.4 第一阶段](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-21-pmltwin-phase1.md) | 测试关联、函数注册与权限、有界数组读取 | 以报告中的提交、环境和实机状态为准 |
| [Form/Gadget 查询](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-21-form-gadget-query.md) | 查询实现、原生能力复用和验证 | 离线夹具不能代替 AVEVA 产品实测 |
| [桌面功能纠偏](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-desktop-corrections.md) | 安装发现、凭据、对象入口和知识查询 | 历史运行不自动成为当前通过 |
| [WPF 页面溯源](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-ui-pages-trace.md) | 页面发现、静态关系与可点击图 | 展示和静态关系基础已完成，后端完整执行链尚未验收 |
| [后端溯源方向与迭代笔记](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-backend-trace-notes.md) | 本次意见归档、人工核对示例和理解方法 | 文档批次；自动跨 Host 衔接仍待实现 |
| [逐级调用树与 Host 对应](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-call-hierarchy.md) | Roslyn 调用位置、函数体、条件、独立回调、真实 Host 输入和路由证据 | 静态分析；未采集实机每步运行值，不推断候选实际执行 |
| [设计约定与功能 Script 目录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-design-contract-catalog.md) | 项目适配约定、自动发现、人工维护、检索和双向入口 | 静态站点的增删改通过 Git 文件维护；GUI 评审迁移和网页写回仍是独立待实现功能 |

## 如何继续记录

收到新意见时，先判断它是需求、更正、疑问还是实现缺陷，再记录决定及理由。可以不同意或暂缓，但需要写清依据。后续批次引用本页编号，把功能说明、测试和验收报告连回对应条目。

条目至少保留日期、原意、采纳或暂缓的决定、当前实现状态、验收条件和相关链接。存在待确认事实时单列疑问；疑问不能变成实现前提。已完成条目被新意见替代时保留原记录和替代原因，不把历史验收报告改写成新结论。
