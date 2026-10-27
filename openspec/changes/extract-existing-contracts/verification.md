# 本轮验证

取证基线为 `codex/quality-baseline-20261026` @ `abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143`。本轮只新增 OpenSpec 配置、Codex 生成技能和审查材料，未修改运行逻辑或原规范。

## 已运行

| 检查 | 结果 | 适用范围 |
| --- | --- | --- |
| `dotnet test YuzuhaToolkit.Development.slnx -c Release` | 业务 205 / 架构 14，通过 219，失败 0，跳过 0 | 隔离文件、合成 Host、编译依赖规则；不是 AVEVA 实机 |
| `npm ci` | 成功，锁定依赖不变 | 既有文档工具依赖；npm 报告 1 项 moderate advisory，未进行依赖升级 |
| `npm run docs:build` | 成功，DocFX 0 warning / 0 error | OpenAPI、LikeC4、Roslyn、测试索引、原生原型契约及链接完整性 |
| 文档构建内的 `tests/handoff-specifications.test.mjs` | 8 / 8 通过 | 原规范 ID/操作闭合、压缩往返、状态机、FigCraft 内容及源码哈希 |
| 文档完整性检查 | 2702 个 HTML 页面，70589 个本地链接，errors=[] | 既有生成文档；不将这些数量解释为产品功能验收 |
| 基线原生 CLI `op list` | 46 项，ID 唯一，contractVersion=1 | 只导出注册表，不 invoke 业务操作 |
| OpenSpec 与提取完整性 | 结果见 `verification.json` | strict validation、需求逐字段比较、源文件/操作目录哈希、引用存在性 |

机器可读摘要见 [verification.json](verification.json)，输入快照见 [evidence.json](evidence.json)。本地原始回执保存在忽略的 `artifacts/tests/openspec-contract-review/*.trx`、`artifacts/handoff-validation/`、`artifacts/docfx/integrity.json`；公开材料不复制本机绝对路径或运行用户数据。

## 边界

- .NET SDK 为 `10.0.400-preview.0.26322.102`；已有测试在该环境通过，不声称已验证所有 SDK 版本。
- Host NET35 / NET48 没有配置本地 AVEVA SDK，未作相应编译语义追溯或实机运行。28 个现有绑定中 27 个为 Roslyn-semantic，B31 为 Roslyn-declaration。
- 本轮没有调用 AVEVA、安装产品、执行 PML、写生产配置、加载插件或重建个人知识库。
- 历史实机证据来自原仓库 `docs/validation/2026-10-24-launch-cli.summary.json`，限其中列明的 E3D 2.1 环境；不冒充本轮回执，不扩展到 AM/PDMS/E3D3.1。
- 本轮没有验证真人可用性、完整结果 Schema、二进制 RPC 兼容或安装升级协议；这些缺口在审查稿中列出。
- OpenSpec planning complete / strict validation passed 只说明规划结构和格式合规，不等于契约内容获得批准。

## 再现

在同一 Git 基线、Windows / .NET 10 / Node 环境中执行：

```powershell
npm ci
dotnet test YuzuhaToolkit.Development.slnx -c Release
npm run docs:build
openspec validate extract-existing-contracts --strict
```

文档构建需 Python 标准库；本机使用已提供的 Python 运行时。CLI 清单来自已编译的 `src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.dll op list`，取证时以 `YUZUHA_STATE_DIR`、`YUZUHA_USER_DIR`、`YUZUHA_KNOWLEDGE_DIR`、`YUZUHA_WEBAPPS_DIR` 指向 `artifacts/openspec-contract-review/` 的独立子目录。

需求快照可用 `contract.requirementFields` 与每一行 `contract.requirements` 按字段顺序展开，再与 `evidence.requirements` 深比较。`sources[].sha256` 核对固定提交的 Git blob 字节；`workingTreeSha256` 是本次 checkout 原始字节，可能因 Windows CRLF 与另一平台不同。操作清单由 `openspec/.gitattributes` 固定 LF，可直接核对其 SHA-256。不要手工修改冻结快照来使校验通过；基线变化时重新取证并明确其差异。
