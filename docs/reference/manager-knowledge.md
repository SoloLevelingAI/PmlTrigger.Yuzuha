# 管理器知识导入与检查

`knowledge.import.preview` 只读取用户选择的来源、检查已有库并保存本机冻结计划，不建索引。`knowledge.import.apply --allow-write` 核对计划引用、来源指纹和目标冲突，在私有暂存目录建库或获取 SQLite 一致备份，再检查和一次性发布。目标已存在时请改名；导入不覆盖同名库，不重建个人、项目或经验库。

## 资料源建库

把以下 JSON 保存为输入文件，执行 `op invoke knowledge.import.preview --input-file <文件>`：

```json
{"product":"E3D","version":"2.1","role":"official","name":"reference","pmlLib":"C:\\Fixtures\\E3D\\PMLLIB","pmlUi":"C:\\Fixtures\\E3D\\PMLUI"}
```

可选 `webHelp` 仅支持 HTML/HTM 原文；CHM/PDF 不在本次索引格式内。PMLLIB/PMLUI 沿用仓库现有文本选择及解析规则，过滤二进制、version.dat、pml.index；计划与建库共用同一枚举。访问失败及链接路径明确拒绝。源内容摘要涵盖相对名称及文件字节，索引期间变化不发布。

AM 可将其原生 PDMSUI 目录明确选作 `pmlUi` 来源。AM 12.1.SP4 和 E3D 2.1 分别登记，版本字段不用来推断另一产品的兼容性。

## 已有 SQLite 导入

```json
{"product":"E3D","version":"2.1","role":"custom","name":"imported-reference","database":"C:\\Fixtures\\sample.sqlite3","manifest":"C:\\Fixtures\\sample.manifest.json","mappings":[{"recordedRoot":"C:\\OriginalMachine\\PMLLIB","localRoot":"C:\\Fixtures\\E3D\\PMLLIB"}]}
```

manifest 可省略；提供时必须是支持的 v1 清单且来源/片段/FTS 计数吻合。历史来源不改写，映射只用于本机定位和内容比对。数据库以只读句柄取得一致 SQLite 备份；拒绝写占用、应用写锁及 WAL/SHM/journal。绝不裸复制这些活动文件。原库及清单保持原字节，登记同时保留导入前的 SHA256 与发布库 SHA256。

应用输入仅为预览输出中的引用：

```json
{"planId":"<本次预览的 planId>","sha256":"<本次预览的 sha256>"}
```

## 报告与检索

`knowledge.inspect` 输入 `database`，可选 `manifest` 和 `mappings`。检查原库只读，FTS 自校验只在临时快照上运行。报告独立列出 SQLite 完整性、必要列、支持版本、关系、FTS 行与正文一致、FTS 内部完整性、MATCH 查询、清单及逐文件来源。

`canEnable=false` 表示结构/版本/检索检查有错误，不能发布。缺源、无法定位或旧指纹是降级警告，可保留索引正文检索；它们不构成来源内容已验证。新建库记录 `sha256-bytes-v1`，同长度内容变化也会显示 `changed`。旧库的路径/长度摘要标为 `unverified-legacy-fingerprint`，不会自动重建。

发布位置为 `Knowledge/Libraries/<product>/<version>/<name>/`，包含 library.sqlite3、登记、检查报告及适用的来源清单。隐藏暂存目录不能被发现；最后一步目录重命名完成才算启用。失败只清除本次暂存资料，保留既有库。

用 `knowledge.libraries.list` 筛选 product/version；`knowledge.library.search` 必填 product/version/name/query；`knowledge.library.chunk` 必填 product/version/name/chunkId。结果同时返回库 GUID、数据库 SHA256、数据库路径及库内 chunk ID，不能把 chunk ID 当成跨库编号。片段保留 recordedPath，另显示映射后的 resolvedPath。资料仅作补充，既有内置指南优先规则保持。

检索先核对启用时的数据库身份；外部修改必须重新检查并另名导入。默认 `sourceEvidenceState=not-rechecked`，不把历史检查当作本次新鲜度证明；设置 `checkSources=true` 才重新做内容比对并返回分层报告。来源 `changed`/`missing` 会随报告保留，不会被相关性分数隐藏。

## 验证

`scripts/Test-Manager.ps1` 运行合成矩阵；`scripts/Test-ManagerExamples.ps1` 运行 CLI 实际命令。矩阵含未来版本、缺表、损坏、孤立关系、FTS 失配、同长度变更、跨机器映射、manifest 矛盾、写锁及四个发布前故障点。真实 AVEVA 资料仅保存在本机忽略的 artifacts 目录，不能作为程序包或设计任务的分发内容。
