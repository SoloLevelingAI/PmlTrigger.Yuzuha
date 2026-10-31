## 1. 视觉规范

- [x] 1.1 Ribbon 分组：组宽贴合内容、组间 12px、每组右侧闭合分隔线、行末保留自然留白（不做整行居中）。
- [x] 1.2 组内内容按内容块高度在 106px 内容带内垂直居中；组标题固定底部基线，距组底 24px。
- [x] 1.3 图标双尺寸族：大命令 32px、行命令 16px，统一 1.8/1.4 描边与 `#2F5FD0` 主色；禁止不同语义复用同一图形。
- [x] 1.4 标签栏区分文档标签（可关闭）与固定页面（无关闭叉）；激活态白底 + 顶部 2px 主色条；脏标记为 6px 圆点。
- [x] 1.5 表格：表头 11px `#6B7688`、单元格 13px、行高 30、行分隔线 `#F1F4F9`、选中行 `#EDF3FE`。
- [x] 1.6 控件形态：输入 / 筛选 / 主操作统一 30px 高与 6px 圆角；主操作实心、次要操作描边、弱操作纯文本。

## 2. 设计稿

- [x] 2.1 生成器 `scripts/design-desktop-screens.mjs`：一套外壳 + 6 个页面主体，输出 1351 条 FigCraft 命令。
- [x] 2.2 六页：`01 工作区`、`02 功能表格`、`03 批量设置弹层`、`04 部署 UIC`、`05 加载登记`、`06 紧凑布局 768`。
- [x] 2.3 产物：`design/figcraft/desktop-screens-v2.figcraft`、各帧 PNG（`artifacts/screens/`）、总览图 `desktop-screens-v2-overview.png`。
- [x] 2.4 校验：各帧导出尺寸与帧一致（1280×840 / 768×840），无内容溢出（bbox 检查通过）。
- [x] 2.5 新增 WPF 原生预览工具 	ools/DesignPreview，渲染同一份几何数据用于对照（产物在 rtifacts/wpf/frames）。

## 3. 待办（实现与验证）

- [ ] 3.1 按 1.1–1.6 改造 `apps/desktop/Features/Editing/FunctionTable.cs`（列宽、行高、pill 与图标列模板）。
- [ ] 3.2 统一 `Views/Ui.cs` 的颜色、圆角、内边距与字号 token，替换散落的内联值。
- [ ] 3.3 替换 `Resources/OfficeRibbon.xaml` 图标为用户确认的 32/16 矢量族，修正导出/部署/检查图形。
- [ ] 3.4 落地后以 1440 / 1280 / 768 三档截图与本设计稿逐帧比对，记录差异与取舍。
- [ ] 3.5 OpenSpec 严格校验（`openspec validate --strict`）并归档本变更。
