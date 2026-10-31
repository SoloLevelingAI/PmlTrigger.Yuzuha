## ADDED Requirements

### Requirement: 可拉伸与可收起的侧栏
主窗 SHALL 提供左右两条可拖动的分隔边界（6 px），并 SHALL 允许把任一侧栏收起为 48 px 窄轨。左侧栏宽度 SHALL 限制在 **148–320 px**（默认 232、紧凑 168），右侧栏 SHALL 限制在 **216–380 px**（默认 300、紧凑 246），收起宽度 SHALL 为 48 px。中央工作区 SHALL 保持至少 480 px。所有状态帧、说明文字与文档中的宽度范围 SHALL 完全一致。

#### Scenario: 拖动边界调整侧栏
- **WHEN** 用户拖动左右任意一条 6 px 分隔条
- **THEN** 侧栏 SHALL 在 148–320 / 216–380 内改变宽度，中央 SHALL 不窄于 480 px，双击 SHALL 复位默认宽度

#### Scenario: 收起与恢复侧栏
- **WHEN** 用户点击侧栏标题处的收起入口
- **THEN** SHALL 收起为 48 px 竖轨并保留展开箭头与竖排标题；点击展开箭头 SHALL 回到此前的宽度

#### Scenario: 窗口变窄
- **WHEN** 主窗缩到 1100×740
- **THEN** SHALL 使用紧凑宽度（左 168 / 右 246），主要控件与字段保持 12 px；树、代码和次要信息可用 11 px，并通过重排或必要滚动容纳，不得整体缩放或使用极小字号

### Requirement: 应用命令 Ribbon 默认完整展开
主窗的**默认状态** SHALL 是顶部应用命令 Ribbon 完整展开（106 px）：文件 / 设计 / 撤销·重做 / 工具四组，保留全部原有命令与大/小图标分组，SHALL NOT 压成一条文字菜单。正常 1280×800 与紧凑 1100×740 两个默认态 SHALL 都完整展开。

#### Scenario: 默认启动
- **WHEN** 用户启动应用且布局中没有 Ribbon 折叠字段
- **THEN** SHALL 显示完整展开的 Ribbon（四组、14 个命令入口），中央 UIC Ribbon 预览同时可见

#### Scenario: 紧凑窗口默认态
- **WHEN** 主窗为 1100×740 且用户未手动收起
- **THEN** Ribbon SHALL 仍完整展开并保留全部命令，SHALL NOT 以折叠态代替紧凑默认态

### Requirement: Ribbon 可手动收起且同尺寸往返
用户 SHALL 能把 Ribbon 手动收起到 44 px（常见表格编辑工况），收起态 SHALL 保留 Ribbon 页签、5 个常用命令（加载文件、保存快照、撤销、重做、部署 UIC）与常驻「展开 Ribbon」入口，并提供 Ctrl+F1。展开 / 收起 SHALL 在同一窗口尺寸内往返，SHALL NOT 因收起改变窗口尺寸。收起 SHALL NOT 隐藏中央 UIC Ribbon 预览或左右侧栏。

#### Scenario: 1280×800 往返
- **WHEN** 用户在 1280×800 主窗点击收起，再点击「展开 Ribbon」
- **THEN** 窗口尺寸 SHALL 始终为 1280×800；收起后工作区增加 62 px 并用于表格，展开后恢复原布局

#### Scenario: 1100×740 往返
- **WHEN** 用户在 1100×740 主窗收起或展开 Ribbon
- **THEN** 窗口尺寸 SHALL 始终为 1100×740，且两种状态各自保留中央 UIC 预览

#### Scenario: 重启恢复手动状态
- **WHEN** 用户手动收起 Ribbon 后重启应用
- **THEN** SHALL 恢复为收起状态，并同时恢复两侧栏宽度与停靠布局

### Requirement: 中央 UIC Ribbon 预览常驻且每列最多 3 行
所有主窗状态（正常展开、紧凑展开、1280 收起、1100 收起、侧栏收起、停靠预览）SHALL 保留中央 UIC Ribbon 预览，SHALL NOT 缩放其字号。预览 SHALL 根据当前 UIC 的实际定义生成，SHALL NOT 添加源文件中不存在的按钮；本设计的 YtoolsAM 验收样例为 13 个按钮 / 3 个 Group。每个纵列 SHALL 最多 3 个按钮，第 4 个按钮 SHALL 另起新列；各 Group 标题 SHALL 在底部同一条基线对齐。空间不足时 SHALL 调整区域宽度或水平滚动，SHALL NOT 增加第四行或整体缩小内容。

#### Scenario: 13 个按钮的排布
- **WHEN** 预览渲染矢勤工具（6）、开发工具（3）、个人常用（4）
- **THEN** SHALL 排成 2 + 1 + 2 列，每列不超过 3 行，组标题底部对齐

#### Scenario: 收起状态下的预览
- **WHEN** Ribbon 处于手动收起态
- **THEN** 中央 UIC 预览 SHALL 仍显示当前 UIC 的全部按钮（本样例为 13 个），行数与展开态一致

### Requirement: 表格行内垂直居中
停靠表与浮动窗结果表 SHALL 把每行内的按钮名称、Group 副行、Macro、Tooltip、图标与勾选框按**整行垂直居中**：双行名称 SHALL 作为 30 px 整体居中于 32 px 行，单行 Macro / Tooltip SHALL 对齐行中心，SHALL NOT 贴靠双行名称的第一行。列内水平方向 SHALL 仍按列规则左对齐。图标列 SHALL 只显示图标，SHALL NOT 出现「16 · 32」之类占位文字。

#### Scenario: 双行名称行
- **WHEN** 表格行同时显示按钮名与 Group 副行
- **THEN** 名称块、图标、勾选框、Macro 与 Tooltip 的垂直中心 SHALL 与行中心一致

### Requirement: 计数与示例数据一致
文档标题、Ribbon 预览、结构树、停靠表、浮动窗与部署影响范围中的按钮数、Group 数、可见行数与总数 SHALL 互相对应，并 SHALL 标注为设计样例。可见行少于总数时 SHALL 显示「显示 N / 总数 项 · 滚动查看更多」。

#### Scenario: 停靠表页脚
- **WHEN** 停靠表显示 6 行、当前 UIC 共 13 个按钮
- **THEN** 页脚 SHALL 显示「显示 6 / 13 项 · 滚动查看更多」，SHALL NOT 显示「共 6 项」

### Requirement: 独立的浮动工具界面
被拖出的工具 SHALL 呈现为独立界面：单条精简标题栏（工具名 + 当前 UIC / 来源 + 停靠回主窗 + 并排 + 窗口控制）、搜索或筛选、主要命令、结果区、详情区与底部反馈。浮动窗 SHALL NOT 复制主窗整套 Ribbon，SHALL NOT 叠加嵌套大标题或拖出条。外壳 SHALL 使用细边与极浅投影，SHALL NOT 带深色背景、厚阴影或发光。

#### Scenario: 拖出按钮 / 查找
- **WHEN** 用户从停靠区拖出「按钮 / 查找」
- **THEN** SHALL 得到约 980×680 的独立窗口，包含结果表与选中项详情，并提供「停靠回主窗」「并排」

### Requirement: 浮动窗作用域与结果一致
浮动查找窗的作用域选择 SHALL 与结果一致：**当前 UIC** 模式 SHALL 只列出当前文件的按钮且允许批量修改；跨已打开 UIC SHALL 为独立的只读模式（仅查找与定位），SHALL NOT 出现隐含的跨文件编辑。

#### Scenario: 当前 UIC 模式
- **WHEN** 作用域为「当前 UIC」
- **THEN** 结果 SHALL 只包含当前所选 UIC 的按钮，计数与该文档模型一致（YtoolsAM 样例为 13 项），勾选后可批量修改

#### Scenario: 已打开 UIC 模式
- **WHEN** 用户切换到「已打开 3 个」
- **THEN** 结果 SHALL 标注来源与只读，勾选框与批量应用 SHALL 被禁用，仅提供定位

### Requirement: 批量操作主按钮与字段状态一致
仅勾选行而没有选择任何修改字段时，主按钮 SHALL 为「批量设置 N 项」或处于不可用状态，SHALL NOT 显示可直接写入的「应用到 N」。只有存在未应用字段修改时，应用动作 SHALL 才可用。

#### Scenario: 只勾选未选字段
- **WHEN** 用户勾选 3 行但没有设置任何字段
- **THEN** 主按钮 SHALL 显示「批量设置 3 项」，底部 SHALL 说明尚未选择修改字段

### Requirement: XML 登记对象是 UIC
UIC ↔ XML 工具 SHALL 表达 XML 与 **UIC 文件**之间的登记关系。详情 SHALL 显示该 XML 登记到的 UIC 名称与完整路径；仅在该 UIC 已打开时可附带 Group 与按钮数量，未读取时 SHALL 明确显示未读取，不得推断数量。SHALL NOT 把「被 N 个按钮引用」作为登记关系呈现，也 SHALL NOT 虚构按钮直接引用 XML 的关系。

#### Scenario: 查看单个 XML
- **WHEN** 用户选中 DesignCustomization.xml
- **THEN** 详情 SHALL 显示其完整路径、来源、状态与「登记对象 · UIC 文件：YtoolsAM.uic（3 Group · 13 按钮）」，并注明按钮通过所属 UIC 间接关联

### Requirement: 停靠与布局恢复
底部工具 SHALL 可停靠回主窗底部，SHALL 只占中央列底部且不覆盖中央 Ribbon 预览。停靠位置、高度、Ribbon 折叠状态与两侧栏宽度 SHALL 在重启后恢复；配置缺少这些字段时 SHALL 回退默认值（Ribbon 默认展开）。

#### Scenario: 停靠回去
- **WHEN** 用户在浮动窗点击「停靠回主窗」
- **THEN** 工具 SHALL 回到底部停靠区，中央 UIC 预览仍可见，选择与筛选保持不变

#### Scenario: 重启后恢复
- **WHEN** 用户关闭并重新启动应用
- **THEN** SHALL 恢复上次的停靠高度、Ribbon 折叠状态与两侧宽度；无字段时 SHALL 回到展开态

### Requirement: 轻盈 Office 视觉基调
界面 SHALL 保持原参考的轻盈 Office 风格：主体白底、浅灰分区、淡灰细线、低饱和浅选中，蓝色只用于主操作、焦点与短标记，并保留原稿的窄蓝标题栏（#254E7A）。SHALL NOT 使用深蓝外描边、厚阴影、发光、发黑的外部背景或多层蓝灰底。SHALL NOT 重绘或染色原 UIC 图标。

#### Scenario: 核对视觉
- **WHEN** 将导出稿与 `design/figcraft/ribbon-first-compare.png` 及原桌面截图对照
- **THEN** 主体、分区、细线与强调色 SHALL 与原稿一致，浮窗导出 SHALL 不带暗色背景或大范围投影

### Requirement: 查找、批量修改与写入边界
跨已打开 UIC 的查找结果 SHALL 为只读定位。当前 UIC 的批量修改 SHALL 可一步撤销。部署与登记 SHALL 保持两个独立步骤，均先预览、再明确确认后才写入。

#### Scenario: 部署与登记
- **WHEN** 用户在部署窗口执行主命令
- **THEN** SHALL 只生成预览，SHALL NOT 直接写盘；登记 SHALL 在「管理登记」中单独确认，部署完成后 SHALL NOT 自动登记

### Requirement: 多文件与 Ribbon 主视角保持
主窗 SHALL 支持多个 UIC 文件标签，文件 SHALL 按规范化绝对路径或文档 ID 识别；重复打开同一文件 SHALL 跳转到已有标签，同名但路径不同的文件 SHALL 保持独立标签和编辑状态。切换文件、切换停靠页、收起 Ribbon 或收起侧栏 SHALL NOT 使中央 UIC Ribbon 预览消失。

#### Scenario: 收起后切换文件
- **WHEN** Ribbon 处于收起态且用户切换文件标签
- **THEN** 中央 UIC 预览 SHALL 仍显示新文件的 Ribbon，Ribbon 收起状态保持不变

#### Scenario: 同名 UIC 的不同来源
- **WHEN** 用户打开两个名称相同但完整路径不同的 UIC
- **THEN** SHALL 分别保留标签、选择与撤销栈；再次打开其中同一路径时 SHALL 跳转到对应原标签

### Requirement: UIC 更新与 XML 登记目标分离
部署预览 SHALL 明确写入目标为所选 UIC 文件，并 SHALL 在确认后先备份再写入。已关联模块 XML SHALL 只读展示，其数量表示已有引用数，不得解释为本次写入 XML 数。UIC 的按钮定义 SHALL NOT 写入模块 Customization.xml；模块 XML 只登记 UIC 的 Name/Path。

#### Scenario: 更新被多个模块引用的 UIC
- **WHEN** 用户修改已被 N 个模块 XML 登记的 UIC 并执行部署
- **THEN** SHALL 备份更新该 UIC，保留已有 N 个登记，不重复改写各模块 XML；已有引用在 AVEVA 重新加载后使用新内容

#### Scenario: 首次生成 UIC
- **WHEN** 用户预览首次部署的新 UIC
- **THEN** SHALL 明确生成 UIC 的文件位置；增加模块登记 SHALL 在独立登记预览中明确确认，SHALL NOT 因首次生成自动新增登记
