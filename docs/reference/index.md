---
_disableToc: true
_disableAffix: true
_disableBreadcrumb: true
_disableNextArticle: true
_layout: landing
---

<div class="eyebrow">PMLTRIGGER · DEVELOPER REFERENCE</div>

# Yuzuha 开发文档

<p class="docs-lead">从接口、架构与运行流程，理解 AVEVA 工程会话如何与 AI 工具连接。</p>

<div class="onboarding-grid">
  <p><strong>我要用</strong><br>安装、选中工程会话、跑通第一条 PML 调用。<br><a href="flows.md">一条命令跑通 →</a></p>
  <p><strong>我要查</strong><br>某个功能由哪些脚本实现，某个脚本干什么、怎么验证。<br><a href="../../artifacts/docfx/navigation/features/index.md">按功能翻 →</a></p>
  <p><strong>我要改</strong><br>依赖方向、契约、测试分层与文档规则。<br><a href="documentation.md">本地开发与维护 →</a></p>
</div>

## 常用入口

<div class="docs-grid">
  <a class="doc-card" href="../../artifacts/docfx/navigation/scripts/index.md">
    <span class="card-icon" aria-hidden="true">ƒ</span>
    <strong>Script 目录</strong>
    <span>搜索 PML 与 C# 脚本的名称、用途、符号和路径，查看人工说明与真实声明。</span>
    <span class="card-link">打开 Script 目录 →</span>
  </a>
  <a class="doc-card" href="api/index.md">
    <span class="card-icon" aria-hidden="true">{ }</span>
    <strong>C# API 参考</strong>
    <span>MCP 工具、知识检索、Named Pipe RPC 与开发接口。按项目阅读真实类型和成员。</span>
    <span class="card-link">浏览全部项目与类型 →</span>
  </a>
  <a class="doc-card" href="../../artifacts/architecture/index.html">
    <span class="card-icon" aria-hidden="true">↗</span>
    <strong>架构与依赖</strong>
    <span>探索项目引用、MCP 通信与 AVEVA Host 边界，在交互式视图中查看系统关系。</span>
    <span class="card-link">打开 LikeC4 架构图 →</span>
  </a>
  <a class="doc-card" href="http://127.0.0.1:5276/scalar/v1" data-site-path="../scalar/v1">
    <span class="card-icon" aria-hidden="true">⇄</span>
    <strong>HTTP API 试用</strong>
    <span>查看完整内置指南，预览 PML 调用和知识检索计划，直接核对请求与响应。</span>
    <span class="card-link">进入 Scalar →</span>
  </a>
</div>

## 从这里开始

[用户目录与知识库管理](user-data-and-knowledge.md)：推荐位置、可预览的目录切换、笔记修订与回收站、升级数据保护。

[设计约定](pmltwin-design-contract.md) · [原始基线](../../artifacts/docfx/navigation/design-baselines/index.md) · [项目适配记录](design-adaptation.md) · [怎么增删改目录](catalog-maintenance.md) · [待分类的 C# 类型](../../artifacts/docfx/navigation/scripts/candidates.md) · [全部文件清单](../../artifacts/docfx/navigation/scripts/inventory.md)

[按层看：CLI / MCP 双入口](cli.md) · [原生工作台与运行日志](workspace.md) · [全部 PML / PMLUI](../../artifacts/docfx/navigation/pml/index.md) · [完整使用文档](../../artifacts/docfx/navigation/repository/index.md)

[后端调用链](backend-tracing.md)：按输入、计算、判断和结果理解业务实现。[迭代记录](iteration-log.md)保留已采纳意见及未完成验收项；[排查笔记](understanding-notes.md)按问题选择合适的解释方法。

## 探索全项目与功能

<div id="workspace-explorer" data-view="projects">正在加载项目与功能索引…</div>

1. [了解架构边界](architecture.md)：区分编译依赖、MCP 与 Named Pipe 的运行关系。
2. [按项目阅读 API](api/index.md)：从工具入口到会话桥、查询规划与 RPC 客户端。
3. [运行测试与验证](testing.md)：检查行为、HTTP 契约和依赖方向。

<div class="docs-note">开发 HTTP 接口提供内置指南与计算预览。AVEVA 会话选择和模型执行通过 MCP 的身份核验流程。<a href="../../artifacts/openapi/v1.json">下载 OpenAPI 契约 ↗</a></div>

## 文档范围

覆盖全部生产项目的手写源码、现有使用指南和 PML/PMLUI 文件。C# API 由 DocFX 元数据与 Roslyn 源码声明共同生成；Host 和安装器的源码页明确标明验证方式。具体文件、类型、成员与文档数量见[覆盖报告](../../artifacts/docfx/navigation/coverage.md)。
