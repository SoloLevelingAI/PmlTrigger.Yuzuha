---
uid: project-portfolio
document_kind: portfolio-guide
implementation_status: navigation
reviewed_on: 2026-10-30
implementation_baseline: 78b8dc4
---
# LaunchDesk.Next：项目作品集

## ① 打开作品

[进入八幕HTML作品集](index.html)｜[HTML↔FigCraft逐页证据对照](compare.html)。这是独立演示版式，不替换DocFX主题，也不改变产品UI。适合项目介绍、技术评审与开发交接。

## ② 看什么

价值 → 易用性 → 同一模型 → 执行链 → 可插拔交付 → 维护知识 → 验证证据 → 交付边界。用项目经理的视角说明收益、设计取舍、风险和下一步，不将测试数量当成客户价值。

## ③ 如何操作

方向键或底部箭头翻页，目录跳转，Home/End到首尾；可全屏、打印全部八幕。窄屏按当前一幕自然滚动。无自动播放，尊重减少动态效果设置。

## ④ 哪些是演示

Project输入、快照与Addin开关是内存中的交互叙事，不读写产品文件，不访问凭据，不创建进程，也不连接VMware。示意工作台不是新产品界面或真实运行截图。正式操作以[使用手册](../manuals/user.md)为准。

## ⑤ FigCraft设计交付

[可编辑原稿](design/launchdesk-portfolio.figcraft)现在由实际HTML的DOM、计算样式与换行位置导入，包含8张1600×900画板、475个可编辑图层及6个绑定颜色变量；不是上一版单独排版的示意稿。[封面](design/cover.png)与[架构页](design/architecture.png)由原稿直接导出，含底部导航。对齐过程修正了中文字体回退与旋转坐标方向，保留导入/回写前后证据。

## ⑥ 样式往返与事实如何追溯

实际HTML → [DOM采集器](../../tools/portfolio/capture-dom.js) → [原生图层导入](../../tools/portfolio/import_dom.py) → FigCraft原稿 → [CSS导出适配器](../../tools/portfolio/export_css.py) → [回写CSS](figcraft.css) → 实际HTML。HTML生成器只生成文档，不再另画一套FigCraft排版。

颜色由FigCraft自身variable.export导出到tokens.css；标题字体/字重/行高/字距与封面卡片位置/尺寸/旋转从原稿读回，[图层ID→CSS映射](design/css-map.json)逐项记录。11项映射已由[同尺寸往返校验](../../artifacts/portfolio-roundtrip/provenance.json)验证，包含文件SHA256与实测结果。样式资源带内容指纹，避免缓存造成旧实例与新设计混看。

当前回写范围是上述颜色、标题与封面卡片；响应式网格、其他CSS效果与交互仍由HTML维护，不声称任意DOM/CSS可无损往返。修改这些范围以外的设计必须扩展映射并重新采集/对照，不能只改设计后宣称实例同步。原生文字与控件渲染差异在[逐页对照](compare.html)保留，不以“共享配色”代替一致性验收。

主叙事仍在[content.json](content.json)，业务事实引用[功能八股](../features/launch-by-profile.md)，交付范围引用[状态](../status.md)。配色不再在叙事JSON里维护第二份。生成命令为可再生中间文件，导入输入与校验记录为历史证据。

## ⑦ 如何判断收益

已有源码路线、冻结输入、具体脚本和架构守卫支持技术与维护性说明；来源可见、预填可改、结果可核验支持易用性解释。目前没有新手/熟手计时研究，未宣称提升比例。测试分批展示，不累计重叠数；历史E3D案例不代表当前环境通过。

## ⑧ 交付范围

本轮只增加作品集与文档入口，不改产品代码或样式、不启动VM、不安装依赖。远程向导、TOML迁移、共享授权、新产品界面和CAF适配仍保留拟议状态。基础交互记录见[作品集验收](../../artifacts/portfolio-review/verification.json)，本轮设计一致性见[往返证据](../../artifacts/portfolio-roundtrip/provenance.json)；效果待用户视觉评审。
