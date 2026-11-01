# Ribbon v2 冻结部署切片

本切片实现 continuation-contracts.md 第 2 节，仅 API/Manager 适配；v1、UI、Database 和真实加载验收独立保留。

## 精确请求与冻结

POST `/api/v2/deployment/file-preview` 仅接受 `{project: ProjectV1}`；`registration-preview` 仅接受 `{project: ProjectV1, monPath: string, manifestName: string, entryName: string}`。project 必须已有 documentId，沿用现有结构验证、规范化及权威来源/原生保护。未知顶层字段拒绝 422。

输出 `{contractVersion:2, planId, planDigest, kind, documentId, expectedVersionId, projectDigest, exportDigest, expiresAt, targetPlan}`。targetPlan 为原 v1 只读预览，含权威路径、旧/新哈希及登记信息。exportDigest 是规范化项目实际 UTF8 UIC 导出字节 SHA256；registration 同样冻结项目导出摘要，但只写清单。planDigest 是除自身外全部计划字段的 canonical JSON SHA256。

每个进程最多缓存 64 项（含已消费结果），TTL 五分钟；总缓存规范化请求不超过 32 MiB，单请求最多 4 MiB。达到预算返回 423；过期项清理。缓存拥有项目及模块选择副本，响应不能修改缓存。服务重启使计划失效，需重新预览，不能据此推断外部写入未发生。

两个 apply 只接受 `{planId: UUID, planDigest: lowercase SHA256}`，未知字段拒绝 422。缺失/过期/摘要或种类不符为 409。apply 在同一进程写锁内先检查源历史版本、重新解析权威绑定/保护，并比对完整 targetPlan、项目/导出摘要；任何上述拒绝先于 Save、备份、Bind 和目标写入。工作区写者共享该锁，防止进程内预览检查与保存之间插入历史变更。外部文件仍可能竞争，既有写前再次检查保留。

## 执行与证据

验证后消费计划，复用原文件/登记引擎。响应 `{contractVersion:2,planId,planDigest,state:'deployed'|'unchanged'|'unknown',workspace:'saved'|'unchanged'|'unknown',deployment:'deployed'|'unchanged'|'unknown',activation:{reloadRequired:boolean,runtimeVerified:false},result?,recoveryActions:[]}`。任何进入引擎后的异常保守记 unknown，人工核对原工作区、目标和备份，不能重新执行。同计划重试返回缓存的原结果；过期/服务重启后没有自动重放能力。内存回执不是持久操作协调器，断进程需人工核对，此限制不替代平台 2.2/2.3。

部署成功只证明磁盘动作；reloadRequired=true，runtimeVerified=false。unchanged 也不证明已加载。后端重启、AVEVA 重启均不升级真实加载状态。不执行宏或重启进程。

## Manager 与验证

显式注册 `ribbon.v2.deployment.file-preview/file-apply/registration-preview/registration-apply`，descriptor version=2；精确顶层 required/properties/additionalProperties=false，project 使用现有 ProjectV1 结构字段约束。apply 需原管理器写授权；预览不需写授权。HTTP v1 的路由、错误及旧 schema 保持。

HTTP 临时安装回归覆盖冻结项目、不接受 apply 偷换字段、缺失/篡改/跨种类计划、源版本及目标字节变化拒绝且历史不增、一次消费、后端重启失效、登记编码/未选清单及 UIC 字节保留、原生保护。缓存预算/期限使用独立 C# 夹具注入时钟验证。Manager 测试覆盖精确 schema、v2 路径、写授权及 unknown；全 Ribbon 离线测试保持。

## 实施与离线结果 · 2026-10-31

- FrozenDeploymentFeature 已实现独立 v2 四入口；WorkspaceFiles 与部署共享进程写锁。原 v1 路径及输入兼容保持。缓存返回计划副本，apply 不再接项目。
- v2 文件更新使用已冻结的规范化项目；不再次缩放图标，避免重复规范化改变导出字节。UIC/清单写后回读哈希后才返回 deployed；登记备份后再次核对目标，变化返回 unknown 并保留核对动作，不覆盖补偿外部变更。
- Manager 检查 HTTP200 回执的 contractVersion=2、计划编号/摘要与请求一致、有效 state 及 activation.runtimeVerified=false/reloadRequired 布尔类型。缺失、错身份、非 JSON 或不确定结果均为 RIBBON_RESULT_UNKNOWN，不重发。操作描述契约为 2，OperationResult 封装版本仍为 1。
- Node 全量离线测试最终 44/44 通过（原 35 项 + 新 9 项）；`npm run build`、`npm run check` 和 `node scripts/check-features.mjs` 通过。测试 HttpListener 需脱离受限沙箱，仅本机临时服务和模拟安装；未启动 AVEVA。Manager 测试由主代理统一构建运行，最终结果见其交接。
- 集成 manifest 保留原 sha256，更新变换后 integratedSha256；新增实现/测试放 addedFiles，明确无原 snapshot 来源。`node scripts/check-integrations.mjs` 通过。

限制：内存计划/回执不是持久协调事实，进程终止后必须人工核对外部文件和历史，再明确重新预览；不自动重放。文件写前核验不能提供对任意外部写者的全局原子事务。UI 仍调用 v1，v2 UI 接线、Database 和真实 Ribbon 加载不在本切片验收。

## 独立审阅 P1 修复

显式选定但未被 Discover 覆盖的安装，可能绕过旧 References 的原生目录检查。v2 Target 在加载登记预览前独立核对权威 sourcePath 是否位于显式安装目录；若无服务端持久绑定同时匹配该源文件与安装 monPath，返回 403。调用者 preferredBinding 不授予托管权。检查发生于计划入缓存及所有 Save/Bind/目标写入前；v1 不改。

新增回归构造未发现安装、空清单和 Native.uic，不提供 preferredBinding，再测试伪造 preferredBinding；均 403，历史、绑定、XML 与 UIC 字节保持。临时编译仅移除该检查的变体以证明测试修复前返回 200 而失败，日志 `artifacts/ribbon-native-before/regression.log`；修复后最终全量日志 `artifacts/ribbon-native-after.log` 为 44/44。首次全量因旧 TS store 的临时 rename EPERM 为 43/44，保留在 `artifacts/ribbon-native-after-first.log`；未改变该代码，重新完整执行通过。Manager 代码本轮不变。
