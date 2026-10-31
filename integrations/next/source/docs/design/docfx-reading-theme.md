---
uid: docfx-reading-theme
document_kind: design-reference
implementation_status: implemented
reviewed_on: 2026-10-31
---
# 文档阅读主题：与作品集使用同一套视觉语言

## ① 统一什么

DocFX现采用暖白、墨黑与信号黄，沿用作品集的六个FigCraft颜色变量；统一品牌、导航、标题、表格、代码与图示。正文保持文档阅读密度，不直接套用演示稿的大字号。实现与技术校验不代表用户已经认可视觉效果。

## ② 怎么找内容

桌面左侧是文件目录与中文标题筛选，中间是正文，右侧是本页小节；顶栏直达文档、作品集与交付状态。小屏改为文件目录抽屉与可展开顶栏。原源码、证据和锚点链接保留；全文搜索使用现有DocFX组件与本地索引，没有引入搜索服务。

## ③ 怎么读层级

文档标题与小节通过字重、留白和分隔线区分；选中目录使用信号黄。页头展示既有元信息：阅读入口、已实现范围、拟议、历史或分项状态，以及文档核对日期。页面状态只限定正文范围，不能代替逐条需求台账；核对日期也不等于产品重新测试日期。

## ④ 代码、表格与流程图

代码保留语法着色与原始换行；宽表和Mermaid在局部区域滚动，避免撑宽整页。流程图与时序图仍由Markdown代码生成，明暗模式都检查标签、连线和注释框。没有维护第二份手绘业务流程。

## ⑤ 实际页面与原稿

[浅色使用手册](../../artifacts/docfx-theme/light-reading.jpg)、[深色流程图](../../artifacts/docfx-theme/dark-flow.jpg)、[手机阅读](../../artifacts/docfx-theme/mobile-reading.jpg)、[手机目录](../../artifacts/docfx-theme/mobile-navigation.jpg)均为浏览器截图。[DocFX可编辑快照](docfx-reading-theme.figcraft)由真实1600×900页面DOM导入，[原生导出](previews/docfx-reading-theme.png)用于对照。它不是独立重画的样稿；浏览器滚动条、占位文字及字体栅格化存在投影限制。

## ⑥ 样式来源与回写边界

原生颜色导出 → [同步工具](../../tools/docs/sync_theme.py) → theme-tokens.css；真实DocFX HTML → 共用DOM采集器（docfx模式）→ 共用原稿导入器 → [标题读回工具](../../tools/docs/export_type.py) → theme-type.css → 实际页面复核。六个颜色变量与作品集逐字节一致；标题映射和文件哈希见[来源清单](../../artifacts/docfx-theme/theme-source.json)与[标题映射](../../artifacts/docfx-theme/type-map.json)。

文档导航布局、响应式、表格适配和交互保留在实际模板及CSS，不宣称全部CSS均由FigCraft自动反推。模板覆盖基于已安装DocFX2.81.0的modern主模板，保留MIT声明；升级DocFX时需要核对主模板变化，不能直接覆盖本地定制。

## ⑦ 怎么验收

检查使用手册、开发索引、架构表格和实际图示的桌面/窄屏/明暗呈现，验证目录筛选、抽屉、锚点和搜索。已有产品测试不因主题构建而重算。实际检查范围及限制见[主题验证记录](../../artifacts/docfx-theme/verification.json)；DocFX与OpenSpec通过只表示技术检查通过。

## ⑧ 本轮边界

本轮修改的是DocFX主题与文档元信息，未改LaunchDesk产品/WPF界面、未安装依赖、未启动VM。独立作品集沿用既有文件；本轮没有把旧主题或新主题标为用户视觉验收通过。
