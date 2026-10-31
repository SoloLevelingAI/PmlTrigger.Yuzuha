# 桌面端界面视觉规范

## 2026-10-29 决策

- 采用一套生成器输出多页设计稿，外壳（标题栏 / Ribbon / 标签栏 / 面包屑 / 状态栏）复用，页面只描述主体区域。这样规范只写一次，六页不会各自漂移。
- Ribbon 对齐以“内容块”为单位：先算每组内容块高度（大命令 76、行命令 28×n + 8×(n-1)），在 40–146 的内容带内垂直居中；组标题固定在 146 下方独立基线。行末留白允许存在，不拉伸、不居中。
- 每组右侧都画分隔线（含最后一组），使被两根线夹住的组与末组视觉中心一致；这是上一轮“末组不画边界”的修订。
- 图标统一为 32/16 双尺寸矢量族，1.8（32px）/1.4（16px）描边，主色 `#2F5FD0`；语义必须可区分（新建 = 文件+加号，导出 = 箭头出框，JSON = 大括号，部署 = 上传，检查 = 放大镜+勾）。
- 表格用三层信息密度：主信息 13px `#263248`，次要 13px `#5A6678`，元信息 11px `#8A94A6`；枚举值用 pill 承载，避免纯文本。
- 弹层遮罩从标签栏下方开始（y=206），标题栏与标签栏保持可辨识，符合 Windows 桌面弹窗习惯。

## 页面族

| 帧 | 内容 | 关键约束 |
| --- | --- | --- |
| 01 工作区 | 结构树 / Ribbon 预览画布 / 属性检查器三栏 | 三栏 240 / 676 / 316，间隔 12；树选中行 `#EDF3FE` |
| 02 功能表格 | 12 行按钮属性表 | 列宽 150/124/70/50/292/142/282；行高 30 |
| 03 批量设置弹层 | 覆盖在表格上的模态 | 600×424，6 个字段行高 38，底部说明 + 取消/应用 |
| 04 部署 UIC | 目标文件、模块关联、写入步骤、摘要 | 左卡 720 / 右卡 510，间隔 14 |
| 05 加载登记 | 筛选 chips + 登记表 | 6 列，状态用 pill（官方/自定义/已接管） |
| 06 紧凑布局 768 | 折叠分组 + 合列 | 仅保留 文件 / 设计 + 「更多」，表格合并为 4 列 |

## 风险

- 图标为矢量占位语义图形，落地时若要贴合产品实际，需要按最终命名重画。
- 表格与弹层中的数据（12 个按钮、3 个模块）为演示数据，不代表真实 UIC 内容。
- 未覆盖更高 DPI、深色主题、以及多层嵌套弹层；紧凑布局只验证 768 一档。
- 设计稿为静态状态，悬停/聚焦/禁用态需在实现阶段按规范补齐。

## 复现

```powershell
node scripts/design-desktop-screens.mjs artifacts/screens.json
figcraft-cli run artifacts/screens.json --out design/figcraft/desktop-screens-v2.figcraft
figcraft-cli export design/figcraft/desktop-screens-v2.figcraft -o artifacts/screens --scale 1
```

## 2026-10-29 WPF 原生对照

为避免 FigCraft 光栅与真实 WPF 渲染差异误导评审，新增独立预览工具 	ools/DesignPreview/Program.cs：读取同一份几何 JSON（frame / rectangle / ellipse / vector / text），用原生 WPF 逐帧渲染成 PNG。它不改动 pps/desktop，只作为设计对照。

对照结论（02 功能表格，1280×840）：

- 布局、间距、点缀色、图标位置在两端一致，说明设计稿的几何是可靠的。
- 文字度量有细微差异：WPF 下 13px 中文略宽、基线约低 1–2px，表头行高看起来略紧。评审应以 WPF 为准，FigCraft 只作为结构编辑源。
- 若后续 WPF 观感偏挤，优先调整行高（30 → 32）与表头 11px 字重，而不是放大字号。

复现：

```powershell
node scripts/design-desktop-screens.mjs artifacts/screens.json
artifacts/wpf/DesignPreview.exe --json artifacts/screens.json --out artifacts/wpf/frames
```