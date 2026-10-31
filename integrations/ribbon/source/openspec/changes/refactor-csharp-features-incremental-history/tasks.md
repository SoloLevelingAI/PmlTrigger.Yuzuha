## 1. 实现
- [x] 1.1 独立 C# API 及 Projects/Images/Uic/Workspace/Deployment/Management Feature。
- [x] 1.2 WPF Feature 拆分，共享 DTO 与 UI 显示逻辑分离，启动脚本切到 C#。
- [x] 1.3 兼容旧持久记录、原路径覆盖、模块关联和来源保护。
- [x] 1.4 增量撤销、局部刷新、图像缓存及渲染索引。
- [x] 1.5 顶部 Tab 条、＋入口、大/小按钮列分隔。
- [x] 1.6 记录功能、脚本及选型说明，移除默认运行的旧 TS API。
## 2. 验证与交付
- [x] 2.1 C# API、参考回归与完整 WPF+C# 隔离链路通过。
- [x] 2.2 性能数据、架构边界、构建、OpenSpec 严格校验通过。
- [x] 2.3 备份已有工作区，启动正式 C# 后端及桌面并核对持久文档。

最终验证：32 项 API/参考回归通过；隔离 WPF+C# 全链路通过（.runtime/desktop-smoke-4fYE6T）；一万按钮中 200 次增量属性记账 0.9836ms，不含重绘/网络。架构边界、完整构建与 OpenSpec strict 通过。正式数据已备份到 .runtime/feature-migration-backup-20261029-144846，桌面与独立 C# 后端已启动，Ytools 持久文档正常加载；网页代理返回 implementation=csharp。证据位于 artifacts/desktop/csharp-features。未写真实 UIC/XML；外部真实鼠标/键盘、任意 DPI 与 AVEVA 实机加载仍需人工验收。
