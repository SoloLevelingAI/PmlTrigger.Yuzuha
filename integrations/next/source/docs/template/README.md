# DocFX阅读主题实现

当前主题与验收边界统一维护在[八节主题说明](../design/docfx-reading-theme.md)，本文件仅为模板维护入口。

- layout/_master.tmpl：基于已安装DocFX2.81.0 modern模板的有限覆盖，保留MIT声明；提供三栏阅读、快捷入口和元信息。升级时对照上游，不重新生成覆盖人工作业。
- public/main.css、reading.js：阅读布局、明暗/小屏、表格与元信息呈现。目录、搜索、锚点和主题选择继续使用DocFX组件。
- public/theme-tokens.css：FigCraft导出的共享配色；public/theme-type.css：原生标题属性读回，不手工维护第二套值。
- tools/docs/sync_theme.py：同步变量与资源指纹，避免旧CSS缓存；不安装依赖或运行产品。tools/docs/export_type.py读回原稿标题。

模板顺序仍为default → modern → template；流程图和时序图的Markdown是唯一业务图源。尚未取得用户视觉验收，不将构建通过当作美观度通过。
