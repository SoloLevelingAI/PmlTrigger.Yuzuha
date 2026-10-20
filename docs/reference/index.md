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

<div class="docs-grid">
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
  <a class="doc-card" href="documentation.md">
    <span class="card-icon" aria-hidden="true">⌘</span>
    <strong>开发与维护</strong>
    <span>配置本地环境，生成接口文档，运行行为测试与架构规则，了解验证范围。</span>
    <span class="card-link">开始本地开发 →</span>
  </a>
</div>

## 从这里开始

[沿六条流程阅读](flows.md) · [CLI / MCP 双入口](cli.md) · [原生工作台与运行日志](workspace.md) · [全部 PML / PMLUI](../../artifacts/docfx/navigation/pml/index.md) · [完整使用文档](../../artifacts/docfx/navigation/repository/index.md)

## 探索全项目与功能

<div id="workspace-explorer" data-view="projects">正在加载项目与功能索引…</div>

1. [了解架构边界](architecture.md)：区分编译依赖、MCP 与 Named Pipe 的运行关系。
2. [按项目阅读 API](api/index.md)：从工具入口到会话桥、查询规划与 RPC 客户端。
3. [运行测试与验证](testing.md)：检查行为、HTTP 契约和依赖方向。

<div class="docs-note">开发 HTTP 接口提供内置指南与计算预览。AVEVA 会话选择和模型执行通过 MCP 的身份核验流程。<a href="../../artifacts/openapi/v1.json">下载 OpenAPI 契约 ↗</a></div>

## 文档范围

覆盖全部生产项目的手写源码、现有使用指南和 PML/PMLUI 文件。C# API 由 DocFX 元数据与 Roslyn 源码声明共同生成；Host 和安装器的源码页明确标明验证方式。具体文件、类型、成员与文档数量见[覆盖报告](../../artifacts/docfx/navigation/coverage.md)。
