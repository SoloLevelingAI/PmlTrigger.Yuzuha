# 云补丁合并、YAML 与 Dashboard 交接

2026-10-31；承接 `pr13-acceptance-ci-review.md`，用户明确授权合并、升级、更新 PR 和回归后推送。

## 改动与原因

合并 `origin/fix/pr13-acceptance-ci@f89f2fa6c10b1b9835ce6f655dea5873b5d6e5f2`，保留本地复核提交 af53497。合并前代码已有 Git 提交备份，旧设计原件保留；本次没有修改安装目录、启动文件或 AVEVA 工程。

YAML 从2.8.1精确升级2.8.3（package.json及lock），修复 GHSA-48c2-rrv3-qjmp；联网审计0漏洞，没有关闭审计或放宽依赖检查。

WPF 默认首页改为 Dashboard：六个主导航收敛成总览、工程、开发、知识、设置五组。会话、函数、Form 收入工程；AI运行和日志收入总览。总览保留实时摘要，将趋势/近期记录和会话/AI明细折叠，1280×720无需滚动查看摘要。12个原功能视图仍保留，实例和草稿不重建，所有原校验保持。

设计入口：`docs/design/compact-workspace/index.html`。原生 `Phayton-R3.figcraft` 一个画布页、五个工作区及853×480变体、5个双主题颜色变量、30个导航跳转；CSS由变量导出。原九个Feature和R2作为历史详情保留，旧索引新增当前版本入口。新稿是导航/变量设计基线，不冒充WPF逐像素导出或实机状态。

## 回归证据

- `scripts/Test-Development.ps1`：470 .NET通过，2个可选7zr测试跳过（197 Manager + 238 general + 21 architecture + 14 Database）；51 Node文档测试；TRX精确身份/非空执行/全部通过/拒绝跳过校验通过。
- 生命周期解析8测试、JSON15及TOML15场景、6个离线PML场景全部通过。
- `tests/Phayton/frontends.py`：合并后64项CLI实进程检查通过，隔离状态；本轮未重新发布AOT，前轮f89f2fa6的64项AOT证据保留。
- `scripts/Test-Desktop.ps1`：12功能视图×双主题×3尺寸、4错误状态通过；0绑定错误。新增断言：首页为总览、五入口顺序、1280×720摘要无滚动、两组明细可展开、跨页草稿保留。没有减少原12视图覆盖。853×480仍允许纵向滚动。
- `scripts/Build-Documentation.ps1 -Port 5289`：完整构建通过，0警告/错误；3168HTML、78675本地链接检查通过；8项交接契约检查通过。
- FigCraft节点检查：6画板均无超出画板范围的子节点；亮/暗/窄窗导出目检。原生几何检查与WPF可用性证据分别记录。
- `npm audit --json`：0漏洞。

本机原始证据在 artifacts/pr13-* 与 artifacts/tests/desktop，摘要及SHA256在同目录 `pr13-merge-dashboard-evidence.json`。沙箱首次 npm DNS失败、WPF构建无诊断失败、dotnet test停滞；正常权限下重跑成功，失败不计为通过。

## 接手与边界

入口代码：MainWindow.Navigation.xaml.cs（分组与草稿保持）、MainWindow.xaml.cs（构造后首页、响应布局、隔离smoke）、MainWindow.xaml（摘要及折叠），都在src/YuzuhaToolkit.Desktop。UI变化没有修改通信协议、Host版本兼容、授权策略或回执成功判定。继续按原校验调试，不把发现候选等同于已核验。

`Use-YuzuhaDotnet.ps1`选SDK；Windows运行Development和Desktop脚本，随后文档构建。Python必须是实际解释器，不使用WindowsApps占位程序；设置YUZUHA_TEST_PYTHON并把其目录放PATH。新电脑不要复用本机凭据或安装路径。

本轮无APS/AVEVA实机操作；旧E3D2.1证据不扩展至AM或其他版本。NET35/48 vendor SDK未提供的文档追踪明确省略。CAF、真实知识库导入、其他平台未完成任务继续留在tasks，不sync/archive。PR #13标题与正文已按单仓集成、实际回归和未完成边界重写，保持draft。

推送结果将在最终交接提交中追加；不得把尚未观察到的远端CI写成通过。
