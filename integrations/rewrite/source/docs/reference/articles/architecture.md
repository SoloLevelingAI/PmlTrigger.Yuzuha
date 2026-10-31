# 分层与功能职责

[文件索引](~/artifacts/docfx/navigation/source-index/index.md)列全源码；依赖以 `rewrite/architecture.json` 为准：`Desktop / UseCases / Windows → Contracts → Kernel`，Bootstrap 装配三层，Worker／DevHost→Windows，AppHost 独立可选。

职责见索引；页面／用例不直接访问文件、进程或注册表。[三种协议](flows/index.md)分开处理，旧身份／修订或过期证据不得覆盖新状态。

`npm run test:rewrite` 验引用白名单／无环、编译依赖、ViewModel 归属、副作用和 Aspire 隔离；真实违规反例验证守卫。场景动态发现、独立 STA／子进程，保留 Job／交付检查。旧 Core／Update 按套件运行，内部 JSON 不冒充逐项结果；TRX 及明细在 `artifacts/architecture-tests/`。

C# 参考来自程序集＋XML。静态关系不证明装配或功能完成，LikeC4／Archon 历史不回写。[术语](glossary.md)
