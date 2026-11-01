# 只读迁移预览切片 · 候选实施契约

映射平台4.4，依据 continuation-contracts.md 第3节。此入口提供净化后的来源材料，不写数据库、不保存计划、不激活配置，不替代4.3或4.5。既有 launchdesk.import.* 兼容入口不改。

## 输入与支持矩阵

`migration.preview` 输入为 `format`（rewrite/next/ribbon）及本机绝对 `source`。只读所选文件或已约定层次的目录，不跟随portable重定向、链接、网络路径或任意嵌套目录。

| 格式 | 支持的实际来源 | 限制 |
|---|---|---|
| rewrite | schema2含Applications/Profiles的旧JSON；schema3 workspace指针及含Snapshot/History的generation文件 | 指针仅允许本根`.control/generations/<GUID-N>.json`，核验原字节SHA256，不跟随任意ControlRoot；分享包不是工作区格式 |
| next | profiles.json的LaunchRequest数组；schema1 draft.json LaunchModel；snapshots/*.json ModelSnapshot；logs/records.jsonl | 目录仅选profiles.json、draft.json、snapshots一层、logs/records.jsonl；记录/请求没有文档schema时报告unversioned，不把配置Version当schema |
| ribbon | 工作区documents/<GUID-D>/document.json与versions/<GUID-D>.json | 元数据/版本外壳无schema；每个版本project.schemaVersion必须1；只读JSON，不读artifacts链接/图标/XML，不创建WorkspaceFiles |

来源依据：rewrite JsonWorkspaceStore.Envelope/Pointer、LegacyWorkspaceReader；Next SavedProfiles、LaunchModel、LaunchWorkspace.ModelSnapshot、Infrastructure.Contracts.Record；Ribbon WorkspaceFiles.Get/Versions/Save。

## 输出与拒绝语义

输出包含预览schemaVersion=1、format、activate=false、完整性complete、每份材料的相对文件名/原字节SHA256/来源版本/净化JSON，以及code/file/path诊断。原始身份、未知非秘密字段、对象数组顺序、历史顺序及原始值类型保留；不转成LaunchProfile、不分配活动身份。Direct值保留并加DIRECT_UNSUPPORTED诊断，不转换成Monitor。存在拒绝或零材料时操作返回success=false、退出码2和MIGRATION_PREVIEW_INCOMPLETE；data仍保留可审查材料及稳定诊断，不能以部分材料冒充完整成功。

字段路径采用JSON Pointer转义。递归剔除密码、秘密、token、凭据/credential引用、认证/授权、privateKey等敏感键；Environment、命令参数、输入快照、自由文本日志/异常原因属于可能夹带秘密的容器，整字段剔除并报告路径。未知字段只有可判定非秘密时才保留；该机制不是对任意恶意隐写的秘密识别承诺。避免错误消息包含源正文或解析异常中的片段。

每次最多256个文件、每文件2MiB、合计16MiB、JSON深度64。文件超预算、链接、非法层次/路径、重复JSON键、损坏JSON、未知或未来schema使complete=false并返回稳定诊断；被拒文件不输出正文。零材料也不成功。枚举到预算即停止，不递归任意目录。读取不写回来源、不创建输出目录；SHA256是所读取文件的快照证据，不声称跨文件全局一致。

## 验证计划

使用临时合成夹具覆盖三类真实结构、rewrite v3历史、Next无版本请求/快照/JSONL、Ribbon历史；验证未知字段保留、敏感引用/嵌套环境剔除、Direct原值及诊断、未来版本/损坏/重复键/预算/越界和链接拒绝、源字节不变、没有目标创建。隔离SDK就绪后运行专用Manager测试；未执行前不声称通过。

## 本轮实现与验证 · 2026-10-31

新增 `MigrationPreview.cs`、`MigrationPreviewOperations.cs`、`MigrationPreviewTests.cs`；产品装配由主代理串行接入 `ManagerOperationCatalog` 与 Feature 注册归属。独立JSON source-generation context兼容现有无反射序列化路径，未修改旧 `launchdesk.import.*`、Database写者或活动Workspace。

使用本仓 `artifacts/toolchain/dotnet/dotnet.exe` SDK10.0.302，Debug、`--no-restore -m:1 -nr:false -p:UseSharedCompilation=false` 构建通过。首次夹具原始字符串编译错误已修正。专测过滤 `FullyQualifiedName~MigrationPreviewTests|FullyQualifiedName~FeatureRegistryTests`：首次20通过/1失败，既有manager.status读取默认用户状态受环境权限影响；保留 `artifacts/tests/continuation-migration/migration-preview.trx`。显式隔离 `YUZUHA_STATE_DIR`、`YUZUHA_USER_DIR`、`YUZUHA_KNOWLEDGE_DIR`、`YUZUHA_TOOLKIT_ROOT` 后，使用同一产物 `--no-build --no-restore`，迁移14/14及FeatureRegistry7/7通过，无跳过，证据 `artifacts/tests/continuation-migration/migration-preview-isolated.trx`。Windows junction真实创建并拒绝读取测试通过，不以静默跳过替代。

测试只操作临时合成来源，不读取真实用户工作区、不部署、不登录、不激活。按平台4.4原文重新核对：该项要求只读导入适配/迁移预览以及字段/历史保留、秘密剔除、Direct诊断和不自动激活，本切片的支持矩阵与上述14项专测已经满足。此前将Database字段级写入/绑定作为4.4未完成理由，混入了后续任务职责，现纠正为4.4只读阶段完成；数据库事实/资产存储归4.3，显式写入切换/恢复归4.5，Ribbon Database接线归5.5，仍未完成。本段为既有2026-10-31证据的职责复核，没有声称此次文档修订又运行了测试；共享tasks由主代理按最终判断更新。

同一Debug产物另执行Manager全量（`--no-build --no-restore --filter 'Category!=Optional7Zip'`）：总218，213通过、3失败、2可选7z跳过，持续1分2秒，证据 `artifacts/tests/continuation-migration/manager-all-isolated.trx`。失败分别是既有LaunchWorkbenchServer的HttpListener.Start“句柄无效”环境错误，以及当时Ribbon v2两项不确定写回执抛InvalidDataException未映射unknown；已通知主代理/Ribbon负责人，不修改他人文件或把全量称通过。后续统一构建/正常权限复验由主代理记录，本段保留当时事实。
