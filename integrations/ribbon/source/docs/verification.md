# 首版验证记录

日期：2026-10-28。范围为本地网页、独立 API 和生成文件；没有连接或修改 AVEVA 会话。

## 自动检查

- npm run build：类型检查和生产构建通过。
- npm test：8 项测试通过，覆盖 XML 结构、命名空间、定义引用、大小布局、图像解码和限制、字段错误、悬空引用、版本、稳定 Key 和确定性输出。
- 特殊字符包含中文、单双引号、&、<、原文实体字符串、CRLF 与类 HTML 提示；解析后原文保真。
- API 测试不启动前端，检查 400/404/405/413/415/422、有效文件及实际超过 20 MiB 请求。

## 浏览器检查

- 修改按钮名称，空 Macro 得到字段错误，修正后校验通过。
- 切换 Large/Small，移动至另一 Group，上移排序，预览和项目数据一致。
- 输入中文、特殊字符与换行 Macro/Tooltip，下载 UIC 后由 PowerShell XML 解析器读取确认原文。
- 保存项目、重新打开、再次导出；两份 UIC 的 SHA-256 均为 35B85FA58051F5EA4D8E2EC9A28AA1DACF9122BFF49F39FFBE96E593A013ABE9。
- 文件选择器上传有效 PNG 后图标更新。
- 新增 Tab、Group 和按钮，改名及排序，通过页面内确认删除临时 Tab，清理其不再使用的按钮定义。
- 检查窄屏和三栏桌面布局；窄屏保留打开、保存、新建和导出入口。

浏览器下载证据保存在 artifacts/e2e/edited-project.ribbon.json 和 artifacts/e2e/edited-ribbon.uic。Macro 仅为文本，未执行。

## 兼容边界

尚未实机验收 AVEVA 加载入口、Namespace 合并、PNG/JPEG 显示、Large/Small 布局、Tooltip 和按钮点击行为。页面与 README 明确说明此状态；OpenSpec 实机加载及兼容范围两项保持未完成，变更暂不归档。

## v0.2 图像与部署增量

新增测试后共 12 项通过。矩形大图压缩为透明补边 32×32 PNG，Large/Small 及混合引用导出为精确 32/16，派生标识避开冲突。测试覆盖 UTF-16 BOM、CRLF、注释保留、预览无写入、备份、更新、重复部署、过期预览和冲突文件拒绝。

浏览器上传 1024×512 的 2051.9 KiB 测试 PNG 后，显示约 0.1 KiB；DOM 图片自然尺寸为 32×32。真实 E3D 3.1 Design 模块预览显示生成 UIC 约 6.4 KiB，尚未点击实际安装的“确认部署”。

随后将目标改为项目内 .runtime/ui-install 的模拟 mon.exe，通过页面预览和确认，成功生成 UIC、追加单个加载条目并生成原清单备份。E3D 3.1 和 Marine 12.1 SP4 的实际 DesignCustomization.xml 前后 SHA-256 一致。

真实安装的只读预览截图为 artifacts/e2e/deployment-preview-v0.2.png。部署入口最终不预选安装，由用户自行选择。实际安装写入、真实加载和 Macro 运行仍未验收。

最终通过网页下载的 export-v0.2.uic 解码后，四个按钮图片尺寸依次为 32×32、16×16、32×32、16×16，均为 PNG。结果保存在 artifacts/e2e/icon-dimensions-v0.2.json；真实清单未修改的摘要核对在 real-installation-unchanged-v0.2.json。最终未选择目标的部署入口截图为 deployment-entry-v0.2.png，浏览器控制台没有警告或错误。

## v0.3 导入、管理和历史

构建、类型检查与 20 项测试通过；OpenSpec import-manage-uic-history 严格校验通过。覆盖导入快捷键/附加属性保留、未编辑富文本保留、不支持工具拒绝、持久草稿及重建存储后读取、恢复追加版本、原生保护、接管前编辑保留、原路径部署、加载移除/恢复、旧凭证核验及前缀伪装拒绝。

浏览器实际从 C:\AVEVA\PMLLIB\Other\UIC\YtoolsAM.uic 导入，读取 11 按钮、3 Group。临时修改小太阳 Macro 和 Tooltip 后自动生成草稿版本，再从导入版本恢复编辑器。源文件没有写回。

从历史界面下载的原始 UIC 与实际源文件 SHA-256 均为 F1E7235E6F83BF0AC04C3AB980C8DA1ADEB0F2EE02C6C3B12F2927E900963411。

实际 Marine Design 清单只读扫描出 6 条：Ytools 为既有自定义且接管前只读，其 Path 保持 C:\AVEVA\PMLLIB\Other\UIC\YtoolsAM.uic，其他条目受系统保护。名称筛选得到 1/6，组合系统保护筛选为 0/6，清空名称后系统保护为 5/6。真实安装未接管、未移除、未部署；文件写入测试仅使用模拟安装。

持久记录保存在正式 data/uic-workspace，当前文档已有导入、编辑草稿和恢复三个版本。截图见 artifacts/e2e/history-v0.3.png 与 manager-v0.3.png。
最终后端重启后，页面仍可从持久化库打开 3 个版本中的最新恢复版本。实际下载导出的 UIC 包含 11 个 ButtonTool、3 个 Group 和 2 个 ShortCut，原小太阳 Macro 保持正确。权限筛选“可管理”为 0/6，“只读/未接管”为 6/6。原始快照与源文件摘要一致；实际 E3D/Marine 清单摘要保持不变。详细核对 JSON 在 artifacts/e2e/*-v0.3*.json。

## v0.3.1 覆盖加备份流程修正

按用户修正，导入的既有自定义 UIC 可以直接编辑并部署，无须先接管。原模块中唯一的原 Name/Path 自动匹配，确认后完整备份原字节并覆盖原 UIC，保持原加载登记。管理表新增“编辑原文件”；系统保护项仍拒绝覆盖。

21 项测试、构建及 OpenSpec 严格校验通过。新增测试逐字节比较旧 UIC 备份与覆盖前内容，验证覆盖后 Macro 已更新、原清单字节不变、成功后才登记托管。真实 Ytools 的页面预览已显示“覆盖原 UIC · 自动备份”及 C:\AVEVA\PMLLIB\Other\UIC\YtoolsAM.uic，未点击真实“确认部署”。

## 共享 UIC 更新与模块关联（2026-10-28）

- 更新以持久文档的 UIC 路径为单位，无需模块参数；备份旧字节并记录前后快照。
- 模拟安装验证两个模块共用文件：更新一次 UIC，两份 XML 原字节保持不变；新增第三个模块只修改第三份 XML，UIC 原字节保持不变。
- 浏览器打开最新 YtoolsAM 草稿，预览收集到 Marine 的 Design、Draft、Outfitting、Paragon 四个 XML；验证筛选与独立收集模块入口。未点击真实文件覆盖或新增关联确认。
- 前后端已启动：http://127.0.0.1:4317/ 。构建与 OpenSpec 严格校验通过。截图 artifacts/e2e/shared-uic-preview.png。
- 最终验证：22 项测试全部通过，含零关联文件预览、原生文件更新拒绝、重复引用拒绝；构建通过。

## 草稿轮换与加入模块可见入口（2026-10-28）

- 每文档纯编辑草稿保留最近 5 次，后续保存时实际删除旧 JSON。手动保存、导入、部署及操作快照长期保留；版本序号独立于保留数量。相同草稿手动保存会建立长期快照。打开部署改为记录普通草稿。
- 新增顶部加入模块按钮及更新 UIC / 加入模块页签；加入表单常显并提供已发现安装下拉框。
- 23 项测试通过，包含草稿物理清理、相同内容手动保存、重建存储后读取及恢复。构建和 OpenSpec 严格校验通过。
- 浏览器验证：YtoolsAM 历史由 18 条变为 12 条，保留 5 条自动草稿及 7 条长期快照。顶部加入模块直达常显表单，选择 Marine 自动收集模块。仅选择 Spooler 展示，未确认新增加载。截图 artifacts/e2e/add-module-visible.png。

## 模块穿梭框（2026-10-28）

- 左侧未关联模块、右侧已有及待加入模块；已有加载按实际 XML 收集，无法读取条目禁选。支持双侧搜索、筛选结果全选、左右移动和数量显示，本次待加入优先显示。
- 批量预览全部待加入项，确认后逐模块备份登记并记录历史；部分失败报告成功数量并保留剩余待加入项。
- 24 项测试通过，含绝对/相对引用识别、空模块与损坏 XML 区分；构建和 OpenSpec 严格校验通过。
- 浏览器验证 Marine 原有 4 个关联，选择 Spooler、Tags 右移，搜索全选 HullDesign 后左右移动取消，恢复为 2 个待加入并完成只读批量预览。未确认写入实际 XML。

## 可核对的加载变更预览（2026-10-28）

- 加入预览进入独立确认步骤，显示每份 XML 实际路径、前后加载条数、Name/Path、绿色新增登记及完整前后 XML，支持返回调整。
- 后端返回实际清单原文与最终补丁原文，测试核对预览后源文件不变、确认写入与 afterXml 完全一致。24 项测试通过，OpenSpec 严格校验通过。
- 浏览器核对 Marine Spooler 原有 Module/Project 两条登记，修改后预览增加 Ytools 第三条登记；已展开核对完整 XML。仅预览，未确认实际写入。

## Ribbon 主视角 0.10

设计确认后集成到 WPF，独立后端未改。完整原生证据 .runtime/desktop-smoke-axBBKQ/：原有编辑、批量撤销、跨文件定位、真实 XML 正反查询和辅助浮动回归全部通过；desktop-result.ribbon-first.json 验证三底部页、顶层文件页签、1280/1050 窗口中的 Ribbon 最小 180px 与只读跨文件范围。layout-seed / layout-restore / layout-docked 为三个独立 WPF 进程，覆盖浮动位置大小、停靠、底部高度与收起恢复，以及只针对底部的非交互停靠预览。

实际窗口 .runtime/desktop-ribbon-first-live.png；程序 artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe。新版已打开 YtoolsAM.uic；真实文件不自动部署。架构边界检查与 TypeScript 检查通过，OpenSpec 使用缓存的 CLI 严格验证。
