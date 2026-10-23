# 2026-10-23 PhaytonObjectWalker 同步与实测

## 基线和改动范围

仓库 main 基线为 `5efe62a1292ca936c76e5c08e230dcca85c74e4e`，同步分支为 `codex/sync-phayton-object-walker`。加入独立的新对象及加载索引、XML 注释转换、说明和实测记录。现有 `YuzuhaObjectWalker`、三个 `YuzuhaRead*` 函数以及 C# 调用均未改变。

实测日期为 2026-10-23（Asia/Shanghai）。安装目录实测源文件 SHA256：

`BC435A68F9DE90658B4D6A763259A2C0A529D2B7FD1AE78915D400AA320EA26B`

按 `.gitattributes` 统一 LF 后的仓库源文件 SHA256：

`AFC91E1E515C509D4EA89AC626CF1D8049CC9DE3D083032AD617FB3B1A8E5789`

同步时核对了仅换行不同，PML 语句和 XML 注释一致。本轮实测期间未修改 Walker 的可执行代码。原作者文件头保留，重构与文档同步由 OpenAI Codex 按用户指示完成。

## AVEVA 环境与结果

实际目标为 AVEVA Everything3D 2.1 的 Model 会话，NET48 Host 0.3.2.0。通过本基线的独立 `PmlTwin.Phayton.Cli` 完成会话发现、身份核验、PML 执行和结果读取。CLI Release 构建为 0 warning / 0 error；没有替换已安装 MCP/Host 程序。

用户给定元素为 ARCHIV，自身没有 ELDATA。通过既有 DBREF 查询发现其唯一子元素，实际大 ELDATA 位于该子元素。原始普通属性读取、独立 VAR SPLIT 基线、对象重载/实例化、缓存核对、完整遍历和 Host 解析均有本地 RPC 回执。

| 检查 | 实测结果 |
| --- | --- |
| `.attribute('ELDATA')` 普通读取 | `(2,136) Attribute ELDATA too large to be transferred in single array` |
| 原始 `VAR SPLIT` | 2410 项 |
| Walker 兜底来源与缓存 | `VAR SPLIT`、ARRAY、2410 项；逐项核对基线 0 差异 |
| 12 行预算 | 根、ELDATA 摘要、10 个子项；值核对 0 差异，PendingCount=0 |
| 完整读取（行数 2412、深度 2） | 2410 个子项、2412 行、0 值差异、0 节点错误、PendingCount=0 |
| 既有 Host 解析全部结果 | Count=2412，Summary.total=2412；DBREF=1、ARRAY=1、STRING=2410；UnparsedCount=0 |
| 下标探针 | 首项数字下标、中间第 1205 项竖线下标、末尾第 2410 项方括号下标均匹配基线；共用同一个提取节点 |
| 中间层大数组后继续下标 | 从 ARCHIV 读取 `members.1.ELDATA.2410`，行数 1、深度预算 0：成功输出显式目标，实际深度 4 |
| 普通数组回归 | 稀疏/嵌套/空数组、预算、目标优先、缓存重置、表达式模式及失败字面文本：0 失败 |

全部 2410 个格式化子项的路径、深度与 STRING 类型另行核对，末项为 ELDATA 的第 2410 个下标。Summary.total 是本次返回行数，不是源对象图总量。

首次小预算测试宏在完成缓存和子项核对后，使用了不支持的 `(表达式).int()`，产生 `(47,15) CP: Syntax error`。修正为中间 REAL 变量后，独立下标探针和完整遍历通过；没有为此改动 Walker。该失败保留在本地诊断历史中。

本次未写入模型数据；创建的 10 个临时 PML 全局变量已按现有 Host 清理约定删除。公开记录保留计数、版本和结论，不提交现场 DBREF、工程名称、模型值、完整 RPC JSON 或机器路径。

## 复刻与范围

先选择包含大 ELDATA 的自有测试元素为 CE，比较直接读取与独立基线：

```pml
!ob = !!ce
!lastfixArr = 'ELDATA'
var !baseline split $!lastfixArr of $!ob
!walker = object PhaytonObjectWalker()
!walker.InitializeFromCurrentElement()
!walker.startStr = 'ELDATA'
!walker.MaxArraySize = !baseline.size() + 2
!walker.MaxBFSDepth = 2
!rows = !walker.Read()
```

对 `NodeValues` 中的 ELDATA 和子节点逐项比较基线，同时核对 `NodeSources`、`NodeErrors`、`PendingCount`、行数、最后路径和 Host 的 UnparsedCount。[内存回归宏](../../tests/live/phayton-object-walker/README.md)默认分支已实测，其中可选大属性分支未启用；本报告的大数组结论来自独立完整测试宏。

通过结论仅覆盖上述 E3D 会话及 2410 项样本。没有扩展成 AM/Marine/PDMS 或其他 Host 版本验证，也没有实现分页、切换旧调用、Form 全属性读取、部署后自动重载或 HTML 运行桥。

## 仓库检查

| 检查 | 结果 |
| --- | --- |
| 新对象与已实测源内容比对 | 仅 CRLF → LF，规范化后完全一致 |
| XML 导出器与文档回归 | 44 项通过，含新 XML 正例、错误 XML、错误参数、缺少 returns、孤立注释、实体和示例转义 |
| `npm run docs:build` | 通过；DocFX metadata/build 均 0 warning / 0 error |
| 生成站点完整性 | 1959 页、超过 3.6 万个本地链接、0 错误；36 个 XML 声明已展示 |
| `node scripts/documentation-catalog.mjs check` | 通过，38 个 Script、13 个功能；新对象已关联对象/数组功能及人工回归案例 |
| 本次源文件指纹 | 新对象、XML 导出器和加载索引均 HashMatch |
| 全量历史指纹 | 仍有 25 个 Changed、8 个 Missing；涉及旧来源指纹和重构前路径，均未在本次修改。没有重写这些旧记录掩盖差异 |
| 原有调用与差异 | 旧 Walker、三个读取函数、C# Host/调用源码均无差异；`git diff --check` 通过 |

离线检查环境为 Windows x64、.NET SDK `10.0.400-preview.0.26322.102`、仓库锁定 DocFX 2.81.0。没有重建/部署正式安装器，也没有把文档测试结果扩展为其他 AVEVA 产品实测。

本次补充的 Form/Button、需求格式、开发部署链路与 HTML 界面问题登记为 TRACE11–TRACE13，见[迭代记录](../reference/iteration-log.md)和[链路说明](../reference/pml-development-flow.md)。这一补充只核对源码与文档，不声称实现 Form 全属性扩展、通用自动重载或 HTML 生产运行桥。
