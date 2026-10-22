# Yuzuha 文档主题

适配自 [LaunchDesk 文档主题](https://github.com/SoloLevelingAI/LaunchDesk/tree/8f1f71dd07c2981683c8fb88a311abb6504b6029/docs/reference/template)。复用 CSS、DocFX 模板扩展、API 分表、主布局与中文标签；外观脚本改用 Yuzuha 独立偏好存储，首页导航和标识按当前项目调整。

`public/main.css` 维护系统字体、蓝色强调、卡片和深浅色；`appearance.js` 在首次绘制前恢复阅读设置；`main.js` 提供外观菜单和折叠成员深链接；`explorer.js` 适配完整项目、模块、类型筛选、分页和接口关联浏览。项目、模块及类型由生成的 `reference/graph.json` 驱动，跨项目类型使用复合标识，避免两个 Host 同名类型相互覆盖。

DocFX 衍生模板保留 `LICENSE.docfx.txt`。所有资源从本机提供。修改后运行 `dotnet docfx docfx.json --warningsAsErrors`，检查桌面、手机、搜索、深浅色和 API 锚点。
