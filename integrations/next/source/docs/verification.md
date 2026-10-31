---
document_kind: reference
implementation_status: mixed
reviewed_on: 2026-10-29
uid: verification
---
# 验证记录

<a id="current-evidence"></a>

## 当前实现所引用的证据

2026-10-29文档核对，未重跑测试。模型批次：47单元、52架构、4UI、9集成；Addin后续批次：64架构（含12项Addin）、4UI、9项分包缺清单检查。两批存在重叠，不相加为覆盖总数。[模型报告](../artifacts/model-execution/verification.json)、[Addin报告](../artifacts/addins/verification.json)、[最新包清单](../artifacts/addins/manifest.json)。新可配置VM参数真实回归未做；新远程连接/TOML/共享授权未实现。

此前真实E3D签出与原生启动是独立[历史实验](reviews/licence-checkout.md)，不属于这些自动测试。交付边界见[当前状态](status.md)。

<a id="discovery-baseline"></a>

## 历史：Project/Module发现批次

2026-10-28，该历史批次实施范围为Project/Module发现。远程连接、TOML及凭据授权只记录提案，不在本次完成范围。状态见[逐条需求台账](../openspec/requirements.md)。

| 检查 | 当前结果 | 证据 |
|---|---|---|
| 单元 | 40通过；包含12项Project/Module新回归 | [完整结果](../artifacts/project-modules/final-tests.txt) |
| 集成 | 9通过，包含本机安装盘点及无害进程参数交接 | 同上 |
| ArchUnitNET | 47通过，保留真实违规夹具 | 同上 |
| 界面 | 3通过；发布EXE查找、保存、失败反馈、安全创建、记录查看 | 同上 |
| Debug构建/Release发布 | 构建0警告0错误，发布成功 | artifacts/project-modules/build.txt、publish.txt |
| OpenSpec | 发现与待实现提案均严格校验通过 | [发现](../artifacts/project-modules/openspec-discovery.txt)、[提案](../artifacts/project-modules/openspec-proposals.txt) |
| DocFX | 实际构建0警告0错误 | artifacts/project-modules/docfx-build.txt |

去重后99个用例，重复运行不另算覆盖。新增用例验证嵌套引用、变量/参数、子目录、数据库目录不递归、模块来源、注释/秘密排除、循环、条件、编码/上限、取消、预填冲突、跨版本共享路径及不可访问目录线索。此次由主Agent实施验证，不把第一版独立评审当成本次独立复核。

## 本机只读发现

[完整扫描报告](../artifacts/project-modules/local-scan.json)直接映射进站点，不复制维护报告正文。Marine返回6个项目代码和5个模块名候选；两个E3D各返回9个项目代码和9个模块名候选。编码、目录或时间上限没有截断这次扫描。项目目录存在与否、声明/目录/可执行文件线索仍分别标注，不把候选数量说成成功登录数量。

本机E3D3.1的evars.init指向E3D2.1，属于用户主动共享；保留该来源，不改配置。备用evars.bat中的其他路径也作为静态线索显示，不把扫描器描述成完整的cmd运行环境解释器。复杂控制流/未知变量诊断保留。

## 使用边界

当前安装发现只查配置目录，不查注册表；Marine/AM与E3D已做本机发现，PDMS只有临时目录识别测试。Project/Module结果位于“快照与记录”的ProjectModules / StaticCandidates记录；输入框仍可改，多候选不自动选首项。

该发现批次没有执行实际AVEVA登录、许可签出、远程连接配置、TOML迁移或真实凭据写入；后来独立实验见上方历史证据链接。UI布局、资源与旧工程未改。UTF-8/BOM文本之外的编码会诊断，不静默猜测。OS阻塞的目录访问不能靠普通取消令牌保证按时停止；远程预检的隔离与超时方案另行设计。


模型与双包增量：47项单元、52项架构、4项UI、9项集成回归通过；另有6项开关与8项临时VMX夹具检查。原XAML未改，VM未启动，厂商程序未启动。输入/快照/冲突与同源包哈希见[模型批次验证](../artifacts/model-execution/verification.json)和[manifest](../artifacts/model-execution/manifest.json)。
