# 来源基线与独立推送材料审阅 · 2026-10-31

本报告只覆盖任务1.1/1.2，未改canonical contract、tasks、解决方案，未提交/推送。相邻来源只读；没有运行其构建/复制/提交脚本。根及上级未发现适用AGENTS；仓内installer/AGENTS与本次新增文件无作用域交叉。

执行：`node scripts/source-baseline.mjs`（仓根）。Archon原仓可用环境变量PMLTWIN_ARCHON_ROOT重定位，默认取source-inventory指定权威根，拒绝不存在来源。不取semantic-compression/rewrite等工作副本覆盖原仓。脚本通过Git已跟踪+未忽略未跟踪文件枚举；JSON保留原仓HEAD、分支、原始dirty状态、逐文件SHA256/大小、已跟踪差异行数与排除计数。HEAD与dirty文件哈希是两个维度，不能称所有文件已经提交。Ribbon无HEAD，输出null及失败诊断属预期，不捏造提交。

## 实际产物

- `baseline/source-manifest.json`：Archon/rewrite及其规格文档、Next、Ribbon、PMLTWIN文本来源清单。packages下契约源码纳入；排除bin/obj/node_modules/生成artifacts等目录、二进制、数据库、秘密命名文件；并非完整发行包/图稿清单。原生FigCraft/截图不在本次文本清单范围。
- `baseline/id-feature-map.json`：Archon catalog.records全部49条；Next台账全部39条（补足原概述漏写的BRD-005、PF-02等）；Yuzuha24 R、28 B、15 T、30 S，另保留3 G与3 M，共103条。合计191条。
- `baseline/push-material-review.json`：推送候选集合的路径级敏感/二进制检查；不写秘密值。生成目录不参与本身哈希，避免递归不稳定。

映射按来源+原ID为唯一键，逐ID显式分组。每条只有一个accountable owner；原record、scope、状态、依据引用原样保留，不把归属误称完整实现。跨Feature约束由该owner负责协调，不能另增重复主owner。检查拒绝重复原ID、重复owner配置、遗漏源ID和映射表中的不存在ID。Archon49/Next39/Yuzuha103与源数逐一相等，检查通过；未修改来源状态。

owner为具体Feature责任边界：environment、launch、configuration、tools-deployment、environment-variables、health、communication、knowledge、optional-diagnostics、composition、design-evidence。它们落实contracts.md候选所有者，不冒充已注册CLI operation/CAF Feature ID。

## 保留的缺口

本轮不是实现验收，原待确认/partial/planned不升级。映射不代表39项Next需求均已迁移，也不代表49项Archon需求获得业务批准。原Yuzuha B11/B13仍指旧Mcp.Core绑定；实际同名源码已移至Cli.Compatibility，应由后续canonical评审确认适配后绑定，不能因旧路径删除静默删除ID。测试关联存在不表示本轮重跑产品测试。Ribbon的来源清单不代替它原有OpenSpec任务验收；1.2指定的原ID集合不包含无显式ID的Ribbon场景自动编号。

同一工作区有其他协作者持续修改，清单是generatedAt时点快照，提交前须重跑脚本并复核变化。trackedDiffSummary为差异索引，不声称已逐行批准其他人的全部业务实现。

Archon位于原workspace外，默认沙箱读取Git status曾失败；已通过受控只读执行重新采集，证实仅有一份未跟踪规格草稿、无已跟踪diff。脚本现明确拒绝Git枚举或dirty状态不可读取的部分视图，避免覆盖有效基线。Ribbon无HEAD以diagnostics明确区分，非忽略错误。

## 独立材料审阅结论

在Git已跟踪+未忽略未跟踪候选中，发现9个既有已跟踪DLL：3套Newtonsoft.Json及6个PlantHost.Rpc。它们未进入来源哈希清单；`git diff --name-only -- '*.dll' '*.exe' '*.db' '*.sqlite'`为空，未发现本轮新增/修改上述二进制。文件名本身不能证明PlantHost为AVEVA专有组件；没有将其错误标为厂商SDK，也没有删除已跟踪依赖。若重发既有DLL，仍应由发布流程提供来源/许可证和构建证明。

扫描未发现私钥头或常见GitHub/AWS/OpenAI token格式；这是有限模式检查，不是“无任何秘密”的证明。未导出凭据库/本机秘密引用，未读取ignored运行数据库；未发现新增pfx/key/db候选。源码测试中的密码占位及真实凭据应由调用链红线继续区分，本报告不导出匹配原文。不含Archon/Next/Ribbon二进制复制。当前可以提交这些新增基线文本；整仓其他变更的最终暂存diff及发行依赖许可审阅仍归主代理，不用本报告替代发布批准。
