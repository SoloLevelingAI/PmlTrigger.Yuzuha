# 平台基础切片验收 · 2026-10-31

这是候选分支的阶段交付，不是整个平台完成或正式安装包发布。整体 OpenSpec 本次由 10/46 推进至 17/46；未完成项仍在原任务清单中，不归档。来源、契约与 Feature 接手入口见 [平台交接](../../../openspec/changes/integrate-pmltwin-product-platform/features/platform-foundation-handoff.md)。

## 新实现与复用

- Manager 增加 Feature 唯一目录、显式禁用与版本门禁；四个函数管理 ID 通过原通信 CLI 执行，未复制函数后端。
- 独立 Database.Contracts / Database DLL 实现按产品、版本、工作区和用途隔离的修订存储。并发 CAS、事务回滚、应用身份/结构门禁、历史和哈希校验已验证；未切换活动 JSON 存储。
- Ribbon 增加18个 HTTP v1 薄入口，沿用原服务的规范化、文档历史和部署规则；完整路由覆盖、Database接线和编辑UI仍未完成。
- CAF 三个实际 SDK 的 NET48 只读生命周期入口已编译，未部署到真实宿主。AM net48 编译不构成旧运行时兼容承诺。
- 最新 CLI 目录为管理80项、通信46项。源码入口、参数和权限通过 `op list` / `op describe` 读取，避免手抄旧操作总数。

## 本次证据

| 检查 | 结果 | 边界 |
|---|---|---|
| Manager完整回归 | 197通过、0失败、2跳过 | 最新源重新构建；两个可选7zr依赖用例跳过 |
| 原函数后端/镜像契约 | 9/9 | 源变化、撤权、重新登记、schema与授权一致 |
| Manager→真实通信CLI | 11/11 | 不存在目标PID严格拒绝；真实AVEVA派发0、重试0 |
| Database | 14/14 | 含真实SQLite并发、事务故障、进程终止恢复、外库拒绝 |
| CAF隔离测试 | 7/7 | 原生接口元数据与自写生命周期，不是宿主加载 |
| CAF真实SDK构建 | 3/3 | E3D21/E3D31/AM121，XML文档输出，无厂商DLL复制 |
| 架构 | 21/21 | 编译引用与完整源码项目依赖图 |
| DocFX metadata/build | 0警告、0错误 | Database与Manager API及手册已接入；不等于全部视觉验收 |
| 知识库 | AM/E3D各自检查通过 | 计数、哈希、FTS与逐文件来源复验，见knowledge-checks.json |
| OpenSpec | strict有效 | 保留候选状态，不同步或归档未完成变更 |

补测本机 `7z.exe` 时，14项安全测试通过、2项实际格式用例因必须独立 `7zr.exe` 被拒绝，失败TRX保留。没有把7z改名或降低白名单；7zr格式验收仍 unavailable。

独立审查修复了三个实质问题：SQLite只看版本号可能误接管外库；Ribbon回执未知时缺失mayHaveExecuted；HTTP响应头后正文读取缺少总时限。还修复了Ribbon schema字段缺失与Marine退出后目录释放的测试清理竞态。最终管理器全量回归发生在这些修复之后。

## 实机与交接

本片未新增APS登录或PML执行。此前 E3D2.1 APS 的374ms CLI返回、严格PID/启动时间/DESIGN核验、Host真实协议1和PML只读检查保留在 [Host验收](../cli-host-compatibility-2026-10-31.md)。没有把旧实机结果扩大到AM、E3D3.1或CAF/UIC。

下一阶段仍需：统一操作事实/资产索引、显式迁移与恢复、rewrite分享/环境变量/健康、Ribbon完整适配与UIC编辑部署、CAF真实加载、WPF/Feature FigCraft和安装升级验收。用户视觉认可也不能由自动测试代替。

推送目标为 `origin/codex/openspec-contract-review-20261027`，保留既有修改，不强推、不合并到主分支。只提交源码、设计与脱敏报告；本机知识库、凭据、厂商SDK和备份DLL保持忽略。
