# Verification

后续已获用户批准实施。实施工具链纠正、测试及双版本真实资料导入见 [implementation-verification.md](implementation-verification.md)；下文保留原规划时记录。

日期：2026-10-29，Asia/Shanghai。本轮为规划和只读检查，不是产品实施或实机验收。

## Source inspection

- 已核对原生 CLI 入口/操作目录、桌面直接依赖、用户数据迁移、插件目录部署、EVARS 写入器、知识仓库检查及 freshness 规则。
- 已核对 LaunchDesk.Next 只有 LoginLaunch 主功能，许可预检在启动协调器内；旧 LaunchDesk 有文件复制/7z/worker，空 Message 可能形成空 Error。
- 已核对本机 E3D 2.1/3.1 INIT 与 AM BAT 文件存在且部分已登记 Yuzuha，未执行厂商脚本或写入环境文件。
- 连接工具报告 Count=0、Sessions=[]、Error=null；没有选会话，没有 PML/模型/写库调用。
- 知识工具列库返回 []；所检查默认/旧知识目录没有发现可用 sqlite3。不是全盘知识库盘点，没有真实库可执行导入验收。

## Local commands

| 检查 | 本次结果 | 结论范围 |
| --- | --- | --- |
| `dsh --version` | 0.2.1-rc.3 | 本机 CLI 存在 |
| `dsh --help` | 成功 | 支持 headless 与 profile/plugin 入口 |
| 默认位置 `dsh headless --help` | EPERM，试图创建用户 profile | 沙箱目录不可写；不是模型服务失败 |
| 隔离 DSH_HOME 的 `dsh headless --help` | 成功 | 只确认命令可用；未验证鉴权、网络、生成或 FigCraft MCP 连接 |
| 插件目录精确搜索 DeepSeek | [] | 本目录未找到；不证明其他目录没有 |
| `dotnet test ... --no-restore` | 首次遥测注册表访问失败；禁用遥测后重试退出 1，无充分诊断 | 没有获得新构建通过结果 |
| `dotnet test ... --no-build --no-restore` | 90 秒未连接 testhost，运行中止 | 只使用已有程序集；没有测试通过或业务失败结论 |
| `openspec list/context` | 成功解析本仓库根 | 使用缓存安装的 CLI，未安装新全局工具 |

本机发现 .NET 11.0.100-rc.3 SDK 和 .NET 10 runtime；没有发现 .NET 10 SDK。测试不能运行的准确原因尚未确定，不能只据超时归因于源码缺陷或机器性能。

测试过滤范围：UserDataManagementTests、LaunchOperationTests、LaunchDraftTests、DualModeCommandLineTests、DesktopSessionWorkflowTests、NativeCliCapabilityTests、WorkspaceBehaviorTests。

中止报告：`artifacts/planning/2026-10-29/yuzuha-manager-prebuilt-baseline.trx`（本机产物，不作为通过证据）。原有历史验证文档仅作源码背景，不能替代新测试。

## Planning checks

最终 OpenSpec 严格校验、文件/链接检查及工作树变更范围记录在 `evidence.json` 的 planningValidation 中。规划完整不代表任务完成或审阅已批准；tasks.md 的未来实施项全部保持未勾选。

## Unexecuted work

尚未调用 DeepSeek 模型、生成新 FigCraft 设计、修改生产代码、启动工程/VM、部署本机程序、写 EVARS、导入或重建真实知识库。新工作明确列在 tasks.md。
