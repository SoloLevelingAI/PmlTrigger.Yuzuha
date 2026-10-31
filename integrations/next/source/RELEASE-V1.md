# 第一版检查点 — 2026-10-27

> 历史阶段资料：保留当时行为和结果；当前实现与未完成项以[交付状态](docs/status.md)为准。

本版本是独立新项目的一条可运行功能切片：本机发现 → 预填与修改 → 保存配置 → 按保存内容启动 → 查看状态和历史快照。之后可从此检查点继续修改方案、界面和实现。

## 使用与阅读

运行 `artifacts/publish/LaunchDesk.Next.Desktop.exe`，或解压快照目录中的 `LaunchDesk.Next-v1-windows.zip` 后运行同名 EXE。发布包依赖 .NET 10 Windows Desktop Runtime，本机已具备；它不是安装程序。

首次运行自动扫描。选候选，核对项目、用户、许可地址和预设值，保存后启动。未保存的修改必须先保存。真实 AVEVA 项目登录与许可签出尚未验证，自动测试只启动无害测试进程。

配置及日志位于 `%LOCALAPPDATA%/LaunchDesk.Next`；`discovery.settings.json` 可配置扫描根、上限和预设。不会迁移旧项目数据。密码不写入普通配置或历史快照。

- [按角色查找文档和源码](docs/INDEX.md)
- [八股功能说明、前后端伪代码与设计理由](docs/features/launch-by-profile.md)
- [验证明细](docs/verification.md)
- [独立评审](artifacts/review/independent-review.md)
- [FigCraft 原稿与界面证据](docs/design/index.md)

## 封版证据

| 检查 | 结果 |
|---|---|
| Release 全 solution 构建 | 0 警告、0 错误；`artifacts/release/build-release.txt` |
| 产品 Release 发布 | `artifacts/publish/` 已生成；独立评审直接运行该 EXE |
| 最终单元 / 集成 / 架构 / 输入行为回归 | 22 / 9 / 43 / 2 全通过；`artifacts/release/results/` |
| 发布版实际窗口端到端 | 1 通过；`artifacts/review/results/ui-published-review.trx` |
| 去重后的用例总数 | 77；作者和独立评审重复运行不重复计数 |
| 本机 C# 只读发现 | 两个 E3D 和一个 Marine Monitor；包含在上述集成覆盖中 |
| DocFX 与 OpenSpec | 实际构建与严格校验，原始结果见 `artifacts/documentation/` |
| 原项目状态 | 两个原仓库 HEAD、工作区状态与开始前一致；`artifacts/release/original-repositories.json` |

独立评审发现并修复三项 P2：未保存输入仍可启动、重启历史不可见且编号重复、架构规则遗漏非 Features 的 Desktop 类型。修复均独立复验，封版时无未关闭 P1/P2。界面采用原项目视觉资源与配色，保留可编辑 FigCraft 原稿；设计预览与真实运行截图分别保存。

## 边界与后续修改

真实厂商登录、许可签出、本机凭据写入和专门的进程后代链测试仍未验证。进程创建或窗口出现不等于项目登录成功；未确认状态不自动重试。重启恢复历史记录，不恢复运行观察或跨重启去重。架构规则可复用，第二个 Feature 的复用收益尚未实证。

本次没有比较旧工程与新工程的全文压缩率：功能范围不同，不能用删除旧功能得到的字数下降证明压缩有效。文档通过单一事实正文、角色索引和 OpenSpec 单源投影减少重复维护；后续应在相同功能范围比较文本量和查找步骤。

## 快照与 Git

修改前设计快照见 [SNAPSHOT.md](SNAPSHOT.md)，其 ZIP 与 SHA256 已保留。最终源码、Git 历史包、发布包、DocFX 站点和相对初始设计提交的 Diff 存入相邻 `LaunchDesk.Next.Snapshots` 的独立 v1 目录，清单记录实际提交与文件 SHA256。后续修改从新分支继续，不覆盖这两个快照。
