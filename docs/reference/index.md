---
_disableToc: true
_disableAffix: true
_disableBreadcrumb: true
_disableNextArticle: true
_layout: landing
---

<section class="phayton-hero">
  <div>
    <div class="eyebrow">BUILD WITH PHAYTON</div>
    <h1>从会用，<br>到会创造。</h1>
    <p class="docs-lead">连接 AVEVA，做出自己的 PML 与 HTML 工具。<br>让每一步有解释，让每个结果可核对。</p>
    <div class="phayton-actions"><a class="primary" href="workspace.md">从这里开始 →</a><a href="pml-development-flow.md">开发第一个工具</a></div>
  </div>
  <div class="phayton-flow" aria-label="开发与验证流程">
    <p><span>FROM IDEA TO TOOL</span><span>开发闭环</span></p>
    <ol><li>01　描述需求 · AI 编写工具</li><li>02　Phayton · 参数与 IPC</li><li>03　PML / AVEVA · 执行与回读</li></ol>
    <small>核对结果 → 记录经验 → 继续改进</small>
  </div>
</section>

## 按你现在要做的事，找到下一步。

<div class="phayton-paths">
  <a class="doc-card" href="workspace.md"><span class="step">01</span><strong>连接与启动</strong><span class="description">工程配置、模块与会话。<br>先让每天的工作顺畅起来。</span><span class="card-link">阅读指南 →</span></a>
  <a class="doc-card" href="pml-development-flow.md"><span class="step">02</span><strong>开发一个工具</strong><span class="description">Function、Object、Form 与 HTML。<br>把想法变成可重复的操作。</span><span class="card-link">阅读指南 →</span></a>
  <a class="doc-card" href="backend-tracing.md"><span class="step">03</span><strong>理解运行结果</strong><span class="description">数据、回执、错误与验证。<br>知道发生了什么，再继续改进。</span><span class="card-link">阅读指南 →</span></a>
</div>

[Phayton R2 正式版与兼容约定](phayton-r2-production.md) · [知识库与用户目录](user-data-and-knowledge.md) · [更新记录](iteration-log.md)

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
  <a class="doc-card" href="documentation.md">
    <span class="card-icon" aria-hidden="true">⇄</span>
    <strong>文档与 HTTP API</strong>
    <span>了解离线文档与开发服务的使用方式，需要试用接口时再启动开发服务。</span>
    <span class="card-link">阅读使用说明 →</span>
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
