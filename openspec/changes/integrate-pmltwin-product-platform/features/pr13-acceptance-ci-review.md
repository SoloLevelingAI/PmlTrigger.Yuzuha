# 云端补丁 Windows 复核 · 2026-10-31

## Scope

用户要求拉取 `fix/pr13-acceptance-ci`、对比当前版本、保持校验并运行相关回归。基线为 `bc8c7785b367ff062fc5761cdd7129ed4d0d8a03`，补丁为 `f89f2fa6c10b1b9835ce6f655dea5873b5d6e5f2`。共同祖先就是基线；补丁包含 `7a29e10`、`4a9f859`、`f89f2fa` 三个提交，共38个文件。

通过 fetch 获取精确提交，在独立克隆 `.portable-checks/pr13-acceptance-ci/pmltwin` detached HEAD 下检查。测试前后源码干净，未将补丁合并到当前产品分支。本次只在当前分支保存审查与证据，不修改云端提交、安装或用户工程。

## Findings

未发现本补丁新增的阻断性问题。建议保留这些修复，后续合入时保持完整三提交和本轮验收边界。

- **DocFX 归属修正合理。** Database.Contracts 与 Database 分离输出目录，Manager.Core 映射真实 manager 元数据。别名唯一性断言保留；B11/B13 仅修正实际 Cli.Compatibility 程序集身份，原需求内容不变。完整生成与交接检查通过。
- **14页改12页不是降低已实现功能验收。** 基线前的 `abd89de` 已移除没有操作后端的评审页和测试中心，XAML、导航、截图生产者及 planned-tools 记录一致。补丁修正旧消费者，并增加页面列表/回执/两组截图循环的交叉测试。真实WPF验证通过；双主题、三尺寸、错误状态等条件未删除。
- **敏感输入与执行授权没有放宽。** 批处理成功路径改为 stdin，新增 inline 输入拒绝且不得写 trust 文件的反例。原函数授权后“无目标”真实进程断言被计数的发现/连接替身用例替代；新用例要求授权前零发现/连接、授权后一次派发、源码改变后不增加任何派发。原真实进程拒绝和篡改用例保留，普通及AOT CLI进程检查均通过。
- **调用链边界更准确。** ReadObject/ReadForms 只追至 CommunicationCliClient 的 Process.Start，不伪造跨进程RPC边；候选实现关系保留。Host样例独立，配置Host时仍要求契约、路由、注册三份证据。discard构造归属修复通过真实Roslyn和反例测试。
- **生产行为与发布输入得到核对。** Roslyn比较195份源码，172份字节相同、23份仅注释/空白不同、0份语法变化（其中两份是检出换行差异，不能误称新增注释）。WebApps是项目文件的Content项修复；源码、构建输出、实际发布的catalog三份SHA256相同。复制恢复测试不再假定枚举顺序，仍要求保留外部修改、恢复另一文件、保存备份与错误细节。

仍有两个既有问题，未借本次检查自动改动：

1. **中危开发依赖告警：** 根 `package.json` / lock 中 `yaml 2.8.1` 受 [GHSA-48c2-rrv3-qjmp](https://github.com/advisories/GHSA-48c2-rrv3-qjmp) 影响。官方2.x修复版为2.8.3；应单独升级并复验文档解析。补丁未修改这两个文件，故不是新增回归。未使用强制升级或关闭审计。
2. **PR元数据陈旧：** [PR #13](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/pull/13) 的标题/正文仍描述早期“仅文档提取”，未反映后续Manager、通信和单仓来源集成。应按最终范围更新，不能让绿色CI替代准确的交接描述。本轮未向GitHub发送评论或修改PR。

## Verification

运行环境：Windows 10.0.26200、固定.NET SDK 10.0.302、PowerShell 7.6.5；Python使用本机已安装运行时。测试详情和日志SHA256见 [证据摘要](pr13-acceptance-ci-evidence.json)。

| 检查 | 本轮结果 |
|---|---|
| 文档Node回归、测试身份保护 | 51/51；精确TRX身份、非零执行及跳过拒绝检查通过 |
| Database / Manager / 通用 / Architecture | 14 / 197 / 238 / 21 通过，共470；2项可选7zr跳过 |
| 云端报告9项.NET失败 | Windows中全部通过，包括知识库写者锁、凭据存储和真实启动夹具 |
| 生命周期 | Python解析8项；JSON、TOML各15步通过 |
| 离线PML开发 | pass、unauthorized、wrong-library、rehash-failure、stale-version、identity-change六类通过 |
| 实际CLI进程 | 普通构建64项；重新发布的Windows Native AOT 64项通过 |
| WPF | 12页、2主题、3视口、4错误状态通过；150%缩放，绑定错误日志为空 |
| 完整文档脚本 | OpenAPI、LikeC4、真实编译输入追踪、元数据、导航、8项handoff、DocFX全通过；0警告/错误 |
| 站点完整性 | 3,167 HTML，78,633本地链接，无错误 |
| 导入资产 | rewrite310、Next602、Ribbon292，1,204份最终哈希及引用闭包通过 |
| WebApps发布 | 源/构建/发布catalog字节哈希一致 |

原始 `Test-Development.ps1` 在.NET全部通过后，因PATH中的WindowsApps Python占位程序返回9009而停止；设置既有 `YUZUHA_TEST_PYTHON` 后，后续生命周期与PML阶段分别通过。没有把首次聚合退出码改成成功。最初WPF `-NoBuild` 因尚未生成EXE未执行；真实Trace-Desktop构建后，同一验收脚本通过。以上均保留在证据摘要中。

精确提交的GitHub **push** 三工作流也已成功（不是仅查询PR工作流）：[build](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/actions/runs/41199678257)、[Phayton](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/actions/runs/41199678244)、[development-quality](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/actions/runs/41199678247)。

## Handoff

复现入口：固定SDK后运行 `scripts/Test-Development.ps1`、`scripts/Build-Documentation.ps1`、`scripts/Test-Desktop.ps1`；实际进程回归用 `tests/Phayton/frontends.py`，Native AOT先运行 `scripts/Build-NativeCli.ps1`。缺Python时设置已有环境覆盖，不修改校验。原始日志、TRX与截图在独立克隆的 `artifacts/pr13-review`、`artifacts/tests`、`artifacts/handoff-validation`。

本轮没有实机AVEVA/PML执行，也没有加载CAF/UIC或运行VM。未为追踪配置NET35/48供应商SDK，两个Host项目按现有规则明确列为未分析；合成Host映射反例通过不等同实机通过。2项可选7zr未执行，原平台未完成任务继续保留，未sync或archive。
