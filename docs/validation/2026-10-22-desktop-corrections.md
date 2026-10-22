# WPF 功能纠偏验证 · 2026-10-22

本批次修正用户指出的五处功能偏差。分支为 `codex/pmltwin-v04-wpf-flows`，PR #6 保持草稿供人工审查；未合并主分支。以下是本次工作区验证，提交前存在修改，不把历史实机结果认作本次通过。

## 修正与复用

| 问题 | 实现与复用 | 边界 |
| --- | --- | --- |
| AM / PDMS 安装未发现 | 参考 LaunchDesk 的有界子目录扫描，识别 marine.bat / pdms.bat / mon.exe，复用现有安装与登录配置模型 | 安装发现不等于运行会话；不把 AM 自带 PDMS DLL 当成独立 PDMS 安装 |
| LaunchDesk 被列为工程会话 | 工程进程名与产品路径/标题联合检查，排除 LaunchDesk、mon 和控制台 | 发现窗口仍须通过既有专用管道核验才能查询 |
| 密码没有保存 | 参考 LaunchDesk 的 Windows Credential Manager 路径，配置仅保存本应用凭据引用，默认勾选保存 | 留空沿用已有凭据；可明确清除；未启动真实工程验证登录 |
| 对象读取只有 DBREF | 复用 YuzuhaReadCurrentElement / YuzuhaReadDbref / YuzuhaReadGlobal、已有列表解析和 RPC，增加当前对象、名称/路径/DBREF、全局变量及成员路径输入 | 成员路径只接受属性/索引，不接受任意调用表达式；真实工程读取待人工验证 |
| GUI 知识库只有提示 | 直接复用 KnowledgeRepository 搜索、内置指南和 GetChunk，GUI 展示正文、库/块/源码位置 | 检索可用；索引写入、经验迁移与持久审查记录尚未实现 |

## Form / Gadget 的准确范围

本轮只调整布局、状态和查询入口，没有重写原生窗体管理。已有查询仍通过 `list_pml_forms`、`list_pml_gadgets`、`get_pml_gadget`，复用 FMSYS、原生属性枚举和 C# Host。详情读取 shown / active / visible / tooltip / callback 文本，逐项报告不可读属性；不执行 callback。

`YuzuhaGetGadget`、`YuzuhaGetGadgetType`、`YuzuhaDecoratedCall`、`YuzuhaGadgetDecorated`、`YuzuhaChooseFormToTrack` 原生文件均未修改。Show/Hide、重载、装饰与事件执行不是本轮已交付功能；支持与人工步骤见 [FormUse 登记](../reference/formuse-audit.md) 和 [Form/Gadget 查询](../reference/form-gadget-query.md)。历史 E3D 测试不代表本次或全部版本通过。

## 本次结果

| 验证 | 结果与本机证据 |
| --- | --- |
| 测试先行 | `artifacts/desktop-corrections-red.log`；新增测试在实现前因缺少实现编译失败 |
| 聚焦测试 | 18/18；`artifacts/desktop-corrections-focused.log` |
| xUnit / ArchUnitNET | 142/142、11/11；`artifacts/desktop-corrections-development.log` 和 `artifacts/tests/*20261022085*.trx` |
| Native AOT | CLI 22 断言 / 8 工具及 schema 对照通过，内置指南 Smoke 通过；`artifacts/desktop-corrections-native-*.log` |
| WPF 发布包 | `artifacts/desktop-corrected-20261022`；108 张明暗/尺寸/状态图，绑定错误 0；`artifacts/ui-corrections-package/desktop-smoke.json` |
| 发布包功能夹具 | 临时 Windows 凭据写读、清除；对象两行绑定；独立 SQLite PML 夹具建库、GUI 检索并打开完整正文；内置指南全文；切换主题保留输入与结果 |
| 安装/窗口只读检查 | `artifacts/desktop-corrections-local-discovery.json`；发现 E3D 2.1、E3D 3.1、AM Marine OH12.1.SP4 三个入口；工程会话 0，LaunchDesk 未混入；未发现独立 PDMS 安装 |
| DocFX | 本批次含本记录完整构建通过：618 页、5681 本地链接、129 个测试目录条目；`artifacts/desktop-corrections-documentation-final.log` |
| AVEVA 实机 | **NotRun**；没有启动工程、没有保存用户工程、没有修改 PMLLIB/PMLUI |

测试输出保存在本机 artifacts，不进入 Git。人工复刻步骤见 [桌面验收](../../tests/manual/desktop-m1.md)；按 ID 入口为 `scripts/run-documented-test.ps1 -TestId 'smoke:desktop-flows'`。本机安装扫描通过不等于真实账号登录、原生 PML 执行或人工可用性验收。
