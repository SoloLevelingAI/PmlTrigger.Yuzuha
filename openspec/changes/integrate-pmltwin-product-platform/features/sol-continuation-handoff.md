# Sol 并行续作交接 · 2026-10-31

基线 PR #13 `39e12f7261891003dfe99594fe5c5e481fe7ac4d`；当前分支 `codex/aps-contract-implementation`。用户授权 Sol 子代理并行，主代理先写续作契约，再分别实施、交叉审阅及集成。本批尚未提交或推送，不 archive/sync，不代表剩余平台全部完成。

## 交付

- [WPF 启动](wpf-launch-continuation.md)：桌面复用既有持久启动协调者，提供原编号恢复观察；unknown 禁止重放，页内及页头启动按钮同步。入口创建、登录与 Host 核验保持分离。
- [Ribbon v2](ribbon-v2-continuation.md)：四个严格冻结部署操作，服务端拥有预览输入，检查修订/目标/期限，写后核对哈希，重复应用复用回执。只报告待加载，不自动重启。保留 v1，现有编辑 UI 尚未切换到 v2。
- [迁移预览](migration-preview-continuation.md)：`migration.preview` 只读三种明确来源，保留净化后的字段/历史，剔除秘密和凭据引用；不完整返回退出码2及材料，始终不激活、不切换数据库。

独立审阅发现 Ribbon 显式选择新安装时可能绕过原生 UIC 保护。新增预览检查只信服务端文件及安装绑定；无 preferredBinding 和伪造 preferredBinding 均拒绝。移除该检查的临时构建得到200而非403，修复后历史、绑定、UIC及XML不变断言通过。

## 本机验证

使用工作区隔离 SDK10.0.302（Windows x64），未改 global.json 或系统 PATH。构建/测试使用隔离 YUZUHA 状态、用户与知识目录，不读真实用户工作区进行迁移。

| 检查 | 结果 | 原始证据 |
|---|---|---|
| Manager 全量 | 224通过、2可选7z跳过、0失败 | artifacts/continuation/final-tests/manager-final.trx |
| Desktop 相关测试 | 56通过、0跳过 | artifacts/continuation/wpf-launch-tests.log |
| WPF 构建 | 0警告/错误 | artifacts/continuation/wpf-launch-build.log |
| 正式 WPF 控件隔离验证 | Passed、绑定错误0字节；原编号观察/unknown禁启动/跨页按钮及可见性通过 | artifacts/continuation/wpf-launch-smoke-final/ |
| Ribbon 全量 | 44通过、退出码0 | artifacts/ribbon-native-after.log |
| Manager CLI 构建与实进程入口 | 0警告/错误；5个新操作schema可读，不完整迁移退出码2 | artifacts/continuation/manager-cli-build.log、registered-schemas.json、migration-cli-rejection.json |
| 来源完整性与规范 | check-integrations、OpenSpec strict通过 | 本批终端结果及验证摘要 |

首轮Manager全量213通过/3失败/2跳过保留在 artifacts/tests/continuation-migration/：两项Ribbon损坏回执异常已修复，HTTP句柄受限在正常权限下复验通过。Ribbon修复后的首次全量出现旧TS文件存储rename EPERM（43/44），原记录保留 artifacts/ribbon-native-after-first.log；相同代码最终完整44/44。不得只留下最后绿色数字而隐藏这些环境限制。

集成来源使用 `-text` 保留原始字节，manifest保持既有CRLF；空白核对使用 `git -c core.whitespace=cr-at-eol diff --check`，未放宽内容哈希验证。原来源sha256不变，新增实现标为 addedFiles，不能冒称原来源代码。

## 未完成范围

原平台29项及管理器13项未完成任务继续保留。尤其是持久跨Feature操作事实、Database写入迁移/切换恢复、CAF实际宿主入口、剩余WPF页面/设计验收、v2 Ribbon UI接线、完整发行与跨版本实机验证。本批5个子任务不替代这些整组。

仅执行过一次只读会话发现，返回0个AVEVA会话；当次默认日志目录受限，不作为登录证据。未启动APS、未部署真实安装、未执行Macro，也没有新的Ribbon实机加载证据。旧E3D2.1结果不得扩展到本机或其他产品。

复验入口：隔离SDK运行Manager/桌面对应测试，Ribbon source目录运行npm test/build，仓根运行node scripts/check-integrations.mjs及OpenSpec strict。详细命令和边界见三个Feature交接；最终来源/证据哈希见 docs/validation/sol-continuation-2026-10-31.json。
