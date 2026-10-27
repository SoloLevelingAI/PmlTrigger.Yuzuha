# 文档维护

```powershell
pwsh -NoProfile -File scripts/Build-Documentation.ps1
```

顺序：架构校验→编译输入/Roslyn→API/源码/测试→规格/原型→DocFX→链接与清退检查。生成物只在 artifacts；源文档在 docs/reference，契约在 docs/specification，设计在 docs/design。

仅发布当前参考树与生成索引；不再扫描仓库全量 Markdown，不发布旧设计/视频/废弃 PML。删除页面须清输出并检查搜索、TOC、资源均无残留。

`capabilities.json` 维护用途/归属，不替代能力发现；签名/源码/测试由索引生成。新增/删除源声明同步绑定，失效 SHA/符号阻止构建。[规格维护约定](specs/AUTHORING.md)定义压缩口径。

[源码导读](source-reading.md)：首屏区分前端、功能、编排和后勤，给出源码行与 Roslyn 直接下游。目录按用途帮助查找，不要求套模式；基础功能保留具体方法。`workflow` 标记顺序流程，`Composition` 标记注册接线；均不混入后端功能列表。前后端筛选与模块、项目、输入查找组合使用。

模块目录 `modules.json`；测试意图及关联见 `test-map.json`。修改后跑文档回归。独立子目录 TOC 经目录引用，避免全站目录重复展开。
