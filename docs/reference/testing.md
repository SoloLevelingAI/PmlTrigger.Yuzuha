# 测试

| 层 | 入口 | 证据 |
| --- | --- | --- |
| 代码/架构/文档 | `npm test` | artifacts/tests、docfx |
| 单项复刻 | `scripts/run-documented-test.ps1 -TestId <ID>` | artifacts/dt |
| 启动生命周期 | `-TestId smoke:workbench-launch-lifecycle` | TOML/JSON 各15步，逐步输入/输出、run.json、HTML |
| 文档版式 | `python scripts/test-handoff-browser.py` | artifacts/handoff-browser |

场景源：`tests/scenarios/launch-lifecycle.toml` / `.json`；执行器 `scripts/run-workbench-scenario.py` 共用真实 CLI。解析失败/超时也保存部分结果；每次新目录，不覆盖旧报告。

[测试目录](../../artifacts/docfx/navigation/tests/index.md)由 Roslyn 发现并关联类型/PML；按源码身份匹配 TRX。零匹配、跳过、错配不算通过。

离线夹具、CLI 进程、GUI、安装、AVEVA 实机分别验收；关联≠覆盖≠本轮通过。旧 MCP 拒绝启动须保留测试。当前文档/原型结果见[本轮验证](specs/verification.md)。
