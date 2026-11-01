# 文档维护

```powershell
pwsh -NoProfile -File scripts/Build-Documentation.ps1
```

顺序：架构校验→编译输入/Roslyn→API/源码/测试→规格/原型→DocFX→链接与清退检查。生成物只在 artifacts；源文档在 docs/reference，契约在 docs/specification，设计在 docs/design。

[安装到工程操作](install-to-operation.zh-CN.md)的共享契约由 `ManagerContractCatalog` 提供；先构建Manager CLI，再运行 `npm run manager:contracts:build` 更新JSON、只读OpenAPI、TS/JS和七字段指南。文档构建从实际CLI重新导出并检查一致性，不以已提交JSON自证运行目录。`npm run manager:themes:check` 检查Web同源ION资源；六套原生主题及FigCraft变量不能当作真实环境验收。

仅发布当前参考树与生成索引；不再扫描仓库全量 Markdown，不发布旧设计/视频/废弃 PML。删除页面须清输出并检查搜索、TOC、资源均无残留。

`capabilities.json` 维护用途/归属，不替代能力发现；签名/源码/测试由索引生成。新增/删除源声明同步绑定，失效 SHA/符号阻止构建。[规格维护约定](specs/AUTHORING.md)定义压缩口径。

模块目录 `modules.json`；测试意图及关联见 `test-map.json`。修改后跑文档回归。独立子目录 TOC 经目录引用，避免全站目录重复展开。
