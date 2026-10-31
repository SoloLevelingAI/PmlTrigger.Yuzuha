# 单仓来源集成交接 · 2026-10-31

用户纠正此前只交付来源清单/部分适配器不足以换电脑开发。本次目标是 rewrite、Next、Ribbon/UIC 必要源码、契约、测试、原生设计与作品集进入同一个仓库，并在全新克隆里验证。没有相邻仓库时仍应能还原、构建、运行离线测试和阅读设计。

## 入口与所有权

- `integrations/catalog.json`：统一源码/原稿/作品集/交接索引。
- `integrations/rewrite/`：原操作、交付、恢复、测试与架构作品集。旧协调者保留为迁移依据，当前 Manager 仍是产品权威。
- `integrations/next/`：Next 完整来源闭包、测试、可编辑设计、八幕作品集及文档资产；SDK对齐根10.0.302。
- `integrations/ribbon/`：原后端、C#/TS契约、Web/WPF、UIC和历史、模拟部署测试与设计作品集。本仓可构建启动，不要求另外运行相邻工程。
- `scripts/Start-IntegratedWorkbench.ps1`：启动本仓Ribbon并将动态端口传给Manager；保持原业务后端，不造第二套UIC存储。
- `scripts/Test-IntegratedWorkspace.ps1`：完整离线验证入口；`Test-IntegratedRibbon.ps1`实际经过Manager→HTTP→工作区与确定导出。
- `scripts/Serve-IntegratedPortfolio.mjs`：无npm依赖的本机作品集总入口，端口默认4176。

## 来源与变换

各模块manifest记录原SHA256和迁入后SHA256，原仓只读。`integrations/** -text`保持跨Git克隆字节一致。SDK、路径、长路径和测试隔离修复以transformation记录；旧产品需求ID/状态原样保留。新电脑步骤见 `docs/reference/portable-development.md`。

迁入时已修复：根Database测试固定dotnet路径；来源检查脚本依赖邻仓；Ribbon深路径下原子写临时名过长；rewrite测试固定Release Worker并触及真实用户注册。后者改为可信测试宿主注入私有注册根，不改变生产身份/nonce/摘要校验。曾由本轮旧测试产生的6条登记已在完整备份和原锁下精确移除，其余37条保持不变；备份仅在ignored artifacts内。

私有知识库、凭据、厂商SDK、bin/obj/node_modules、现场VM数据不迁入。NuGet/npm属于标准还原依赖。7zr、Aspire和真实AVEVA环境单独声明，不把这些外部软件等同于“缺本机项目”。

## 验证状态

来源副本预验证：rewrite默认71项与Node10项；Next四批122项；Ribbon34项及桌面验证通过。三模块1,204份原件双哈希/项目引用闭包通过；作品集真实HTTP 172个本地请求通过。Manager→本仓Ribbon六步链路通过，无安装UIC写入或Macro执行。

全新克隆验收尚待执行，执行结果将追加到本文件和独立报告。之前单机测试不能替代该验收，不提前推送。

## 保留的独立任务

本组9.x是单仓可接手性验收，不自动完成原1–7组中的数据库显式切换、完整平台业务融合、WPF/CAF/UIC实机验收或用户视觉认可。下一名维护者先读本文件、模块handoff和本组测试报告，再按Feature阅读原契约，避免重新加载所有项目上下文。
