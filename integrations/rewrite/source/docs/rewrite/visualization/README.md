> 本图保留已审阅设计投影。后续用户已授权实现，源码在 `rewrite/`，实际依赖与位置见 [实施说明](../IMPLEMENTATION.md)；历史审阅状态与模型证据保持原样。

# Archon → LikeC4 架构审阅入口

这是 `architecture/archon` 的可视化投影。原生审阅契约是类型职责、方法与依赖的来源；没有合并独立的 LikeC4 / OpenSpec 候选。所有应用类型仍是 Proposed，业务实现未获批准。

## 打开与更新

在仓库根目录执行：

```powershell
npm ci --ignore-scripts
npm run evidence:check
npm run architecture:generate
npm run architecture:check
npm run architecture:validate
npm run architecture:build
npm run architecture:serve
```

打开 `http://127.0.0.1:5187/`。默认只绑定本机。静态构建位于 `artifacts/architecture-review/`，不提交到 Git，不对外发布；LikeC4 使用 hash 路由和相对资源路径。该目录需经 HTTP 提供，不能用双击 HTML 代替服务器。

也可运行 `./tools/archon/Open-ArchitectureReview.ps1`，在后台启动本机预览并输出地址和 PID，默认端口 5189；可指定 `-Port`。之后在浏览器打开输出地址。

可以从六个入口阅读：总览、模块、类型契约、场景验收、问题修订、证据意见。支持从模块进入类型，查看公开方法、错误约定、生命周期、依赖、职责禁区及初稿差异；需求 R01–R14 与全部 AR01–AR27 场景互相追踪。三个重点场景提供 LikeC4 动态图和逐步说明。F01–F05 展示契约层复核，F06 保留原始“不接受”意见及后续勘误，明确未再次复核。

意见保存在本浏览器，可导出 JSON。导出包含当前运行与契约 SHA-256，供下一轮人工选择并作为 Archon 输入；当前不自动触发模型、不回写原生记录，也不批准架构或实施。

## 唯一来源与展示配方

- `source.json`：选定归档运行、中文阅读分组、场景展示步骤、审阅范围及 19 个缺少模块字段类型的展示归属建议。没有复制职责、方法或类型依赖。
- `../runs/<runId>/reviewed-contracts.json`：当前类型与公开方法的权威来源。
- 同一运行下的 `scope/challenge/revision/recheck/native-run/native-events`：需求、发现、修订、复核与真实阶段状态。
- 顶层 `review-contract.json` 与 `SCENARIOS.md`：原先 27 个验收场景的索引及定义；其中超时次数等数值是初稿样例，当前策略阈值仍待审阅。
- `scripts/lib/architecture-projection.mjs`：确定性投影和规则校验；场景调用关系与构造依赖分别处理，不把通过端口或工厂的间接调用写成构造依赖。
- `generated/`：生成的 `model.c4`、LikeC4 配置和 `review.json`，提交便于代码审阅。不要手工修改；输入变化后未重新生成会检查失败。
- `portal/`：本地审阅界面源码。构建清单绑定输入与门户源码哈希，过期构建拒绝启动预览。

未来换用另一次归档运行时，先更新 `source.json` 的 runId，显式补充新增类型展示归属及方法引用，再重新生成。缺失类型、未知依赖、失效方法、来源已批准或缺少真实节点完成记录都会拒绝生成。

## Archon 工作流

新增 `launchdesk-architecture-visualize`，顺序为：校验原生证据 → 生成投影 → 投影测试 → LikeC4 校验 → 构建门户。五个确定性节点，没有 AI，也没有批准节点。它使用单独隔离的 SQLite，不接续或批准之前暂停的六阶段审阅。

```powershell
./tools/archon/Invoke-ArchitectureVisualization.ps1 `
  -ArchonSource '<Archon v0.12.1 源码目录>' `
  -BunExecutable '<bun.exe>' `
  -RunRoot '<新的隔离运行目录>'
```

依赖已经由 `npm ci --ignore-scripts` 安装后运行。源码与运行器来源沿用 `tools/archon/runtime-provenance.json`；实际执行记录另存 `evidence/`，与原始架构审阅证据分开。

最终原生运行 `4d4454ff-c281-48cb-b57b-0d178b3fb3ca` 的五个 exec 节点全部完成。第一次使用 Bash 的运行被 Windows 沙箱的 MSYS 对象权限拒绝；改为 Bun 脚本直接启动 Node 后成功，失败日志保留。官方 CLI 的运行时发现曾报告 Bun 不在 PATH，但实际五个节点均通过 Bun 执行；该警告未隐藏。`npm run architecture:evidence-check` 校验归档日志和哈希。原生执行发生于记录了变更清单的工作目录，没有冒称基于干净提交。

## 检查范围

`architecture:check` 检查投影时效和 16 项正反例，包括依赖缺失、展示归属覆盖、方法失效、场景缺失、复核证据缺失、批准状态和原生节点记录缺失。

`npm run architecture:ui-check` 使用独立 Playwright 浏览器及临时本机预览，检查嵌入图、类型筛选、场景和需求跳转、原始资料、意见保存/导出和窗口宽度。优先使用已有 Playwright Chromium，缺少时使用本机 Edge；可通过 `ARCHITECTURE_BROWSER` 指定路径。检查结果和实际截图在 `previews/`。

这些检查覆盖架构工具、图和追踪关系。它们不执行 WPF、真实许可、VM 或业务故障注入，也不证明未来 C# 实现满足模型规则。实施后仍需程序集依赖及场景行为测试。

参考：[LikeC4 模型与视图](https://likec4.dev/dsl/views/)、[动态图](https://likec4.dev/dsl/views/dynamic/)、[CLI 构建](https://likec4.dev/tooling/cli/)、[Archon 工作流](https://archon.diy/guides/authoring-workflows/)。
