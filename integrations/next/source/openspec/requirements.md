# 逐条需求台账

2026-10-28，直接来自用户的需求。每条以唯一ID追踪，正文和场景只维护在对应规格中；本页维护状态和入口。“已记录”不等于“已实现”，选型与系统操作另行评审。

| ID | 用户诉求与判断 | 当前状态 | 唯一规格 |
|---|---|---|---|
| PM-01 | E3D3.1主动共用E3D2.1项目，保留配置，不按版本替换Proj路径 | 已实现并通过验证 | [发现规格](changes/project-module-discovery/specs/project-module-discovery/spec.md) |
| PM-02 | 从evars.bat、evars.init、launch.init及引用、下级目录查找Project | 已实现并通过验证 | 同上 |
| PM-03 | Module也要实际查找，并显示声明、启动分支或文件线索的来源 | 已实现并通过验证 | 同上 |
| PM-04 | 多候选和未知结果不乱选；预设可改，保留手动修改 | 已实现并通过验证 | 同上 |
| PM-05 | 明示AM/Marine、E3D、PDMS的发现来源和覆盖；注册表是否参与要如实说明 | 已实现并通过验证；当前只查目录 | 同上 |
| RP-01 | 远程Proj先检查配置与路径可访问性，不把连接问题说成数据库损坏 | 方案已补齐，待评审/实现 | [远程与持久化规格](changes/portable-profiles-and-credential-grants/specs/portable-profiles-and-credential-grants/spec.md) |
| RP-02 | 未配置连接时，软件给出手动连接配置、重新检查和取消入口 | 方案已补齐，待评审/实现 | 同上 |
| RP-03 | 连接检查失败或未配置时，阻止本次启动并保留配置，提示可操作原因 | 方案已补齐，待评审/实现 | 同上 |
| PF-01 | 前后端分离后，登录数据可持久化为TOML等可读格式 | 用户已批准Tomlyn 2.10.1；明确暂不安装 | 同上 |
| PF-02 | 登录项及关联关系可预览导出/导入，支持版本与路径修订 | 方案已补齐，待评审/实现 | 同上 |
| CR-01 | 密码只在本机凭据管理器，不随配置、日志、快照或导出迁移 | 基础能力已有；导出边界方案待评审/实现 | 同上 |
| CR-02 | 一个密码供同Proj多个Module使用，用共享引用，不复制密码到各项 | 共享引用与授权图稿已设计；待评审/实现 | 同上 |
| CR-03 | 弹列表预览，用户勾选后批量授权；允许显式跨Module、跨Proj | 方案已补齐，待评审/实现 | 同上 |
| CR-04 | 从发现的Project/Module生成登录项，先预览修改，授权只作用于选中项 | 方案已补齐，待评审/实现 | 同上 |
| CR-05 | 可撤销单项授权；更新/删除凭据前能看见受影响登录项 | 方案已补齐，待评审/实现 | 同上 |
| CR-06 | 导入其他机器后先绑定本机凭据，不按同名自动授权 | 方案已补齐，待评审/实现 | 同上 |
| CR-07 | 说明Windows当前用户/本机边界；应用内授权不是独立操作系统安全边界 | 边界已具体化，待评审/落实 | 同上 |
| VM-01 | 无真实远程Proj案例时用隔离VM验证；通用连接和真实AVEVA分层验收 | G0/G1环境与10项OS检查通过；16项产品场景仍待实现后分层验证 | 同上 |
| VM-02 | 采用VMware Host-only与专用账户，写清网络、SMB/NTFS配置、基线恢复及验收顺序 | G0/G1通过，配置前和Ready快照保留；原VMX哈希例外未掩盖，见[实际记录](xref:remote-project-lab-results) | 同上 |
| LC-01 | 分开说明E3D客户端、许可服务、许可文件和实际签出；不把端口通过当有效许可 | 只读清单已核对；恢复主机名后AVEVA201签出/归还通过；现有厂商启动链已进入Model，未改许可密钥 | [规格](changes/portable-profiles-and-credential-grants/specs/portable-profiles-and-credential-grants/spec.md)、[证据](xref:remote-project-lab-results#licence) |
| UX-01 | 新前端先使用FigCraft，交付可编辑图稿、操作状态和组件 | 图稿与8张预览已交付，待评审 | 同上 |
| LC-02 | 查清4546超时，区分RULES监听、RMS明细、防火墙来源与Clash TUN | 已查明：克隆改名使0008主机名绑定失配，RMS拒绝加载许可 | [许可测试](xref:licence-checkout-test)及LC-02规格 |
| LC-03 | 宿主机E3D连接测试VM，验证实际许可签出，保护原机与工作会话 | 首次失败；恢复主机名后签出/归还通过，后续已定位实验器重定向模式并用原生启动进入Model；清理完成 | [许可测试](xref:licence-checkout-test)及LC-03规格 |
| LC-04 | 用厂商工具只读核对克隆身份与许可绑定，不猜改许可或时间，不影响原机 | 已确认失配；后续按LC-05授权恢复名称，指纹重新匹配 | [根因与证据](xref:licence-checkout-test)及LC-04规格 |
| LC-05 | 先快照再恢复测试克隆主机名，保留隔离/账户/共享，复测4546与真实签出；不改许可或时间 | UID3保留；配置与10项SMB通过，4546及AVEVA201签出通过；应用FNF7，克隆关机 | [过程与证据](xref:licence-checkout-test)及LC-05规格 |
| LC-06 | 核对原工作目录、init、TEMP并用SYSTEM测试凭据复查FNF；不记录密码、保护实验环境 | A/B失败、C/D原生句柄启动成功；并非目录缺失，详见开发对照 | [证据](xref:licence-checkout-test)及LC-06规格 |
| DV-01 | VM远程模拟作为隐藏开发功能，代码独立保留；仅开发机显式启用，产品初始化不加载，普通远程呈现不标VM | dev/RemoteProjectLab已拆分；机器/用户绑定开关默认关闭，6项开关检查及48项架构测试通过 | [开发索引](xref:remote-project-development)及DV-01规格 |
| DV-02 | 本机开关仅控制脚手架，普通远程不依赖；八节索引列明每步源码、失败返回和图示，参考Yuzuha改进DocFX阅读主题 | 调用链与图示已补齐；主题仅构建通过，视觉验收重开，见UX-03 | [八节调用链](xref:remote-project-development)及DV-02规格 |
| MD-01 | 前端双向编辑同一模型并保存文件；执行时持久化冻结快照，后端仅收快照提取参数与执行业务 | 已实现LaunchModel/LaunchWorkspace；原UI不变，草稿版本冲突、快照与记录失败均阻止执行 | [八节实现说明](xref:model-driven-development-review)及MD-01规格 |
| DV-03 | 实验VM路径/预期身份、远程地址与许可服务参数可调，前端不承载实验业务；开关不随模型导入启用 | 用户已确认双包；LabSettings可配置VM，LaunchModel统一许可参数，默认关闭；真实VM参数回归未执行 | 同上及DV-03规格 |
| FM-01 | 像CAF的Feature一样由宿主装配/移除，分包只隔离交付和依赖，不复制模型或后端事实 | WorkspaceHost为唯一login.launch装配入口；两包比较共用DLL哈希，具体CAF插件API适配未实施 | [开发索引](xref:remote-project-development)及FM-01规格 |
| AD-01 | 用TOML/XML Addin控制启用：代码存在而不加Addin，Feature仍不可见且不装配 | 已采用无新依赖的XML清单，工厂与入口受清单约束；启动时装配，VM动作仍需本机授权；TOML解析适配/热卸载未实现 | [八节说明](xref:addin-registration)及AD-01规格 |
| DG-01 | 查原LaunchDesk部署复制失败为何不显示原因；不猜测、不修改实际SeaGend和原程序 | 已发现三份空Error；同包校验/隔离复制通过，历史底层原因因证据丢失仍未确定，原程序未修复 | [调查记录](xref:original-deploy-diagnostic)及DG-01规格 |
| UX-04 | 同时修改DocFX，与作品集使用统一风格；改善导航、阅读层级、代码、表格和图示，保留真实页面与样式来源 | 新主题已实现；实际呈现与功能检查，视觉仍待评审 | [主题说明](xref:docfx-reading-theme)、UX-04规格 |
| UX-03 | 当前DocFX主题未获认可，重新对照Yuzuha完整导航和阅读层级评审，不以构建通过代替主题通过 | 2026-10-31新主题已实现并检查，仍待用户视觉评审 | 同上及UX-03规格 |
| DOC-03 | 先从实际HTML导入FigCraft，再将原稿样式回写CSS；保留同尺寸截图、图层映射、哈希和限制，不能两份独立排版冒充一致 | 8幕导入、6变量/11项回写验证及逐页对照已落地，待用户视觉评审 | [证据对照](xref:project-portfolio)、规格DOC-03 |
| DOC-02 | FigCraft＋HTML八幕作品集，以项目经理视角解释技术、易用性、内部逻辑与维护性；同源叙事并保留已实现/拟议/历史边界 | 八幕HTML与8张FigCraft画板已交付；交互/窄屏/链接校验通过，展示不操作产品或VM | [作品集](xref:project-portfolio)、远程与持久化规格DOC-02 |
| DOC-01 | 统一已实现/拟议/历史及证据日期，以同一八股正文组织使用、开发、演示入口；评估相对原版的可理解性，不虚报提升数值 | 文档入口与元信息已整理；理解成本未做计时实测 | [状态](xref:delivery-status)、[规格](changes/portable-profiles-and-credential-grants/specs/portable-profiles-and-credential-grants/spec.md) |
| UX-02 | 防止WPF默认样式偏离设计；显式模板及三档DPI实际截图校验 | 验收契约已写；尚未实施WPF | 同上 |

当前文档核对基线78b8dc4（2026-10-29）：已包含模型执行、双包与XML Addin；本轮只整理文档，未重跑产品或VM。fee897f的99项发现回归为历史批次，不代表最新总覆盖。[状态与证据边界](xref:delivery-status)区分当前、拟议和历史。设计正文见[八节评审稿](xref:remote-portable-review)，库研究见[TOML选型](xref:toml-selection)，执行与批准状态见[清单](changes/portable-profiles-and-credential-grants/tasks.md)。
