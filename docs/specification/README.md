# 规格源与生成入口

唯一规范源为 `contract.json`；手写文档位于 `docs/reference/specs`。BRD/PRD 共用，SRS/FRD/SDD/SDS 按前后端划分。FigCraft 位于 `docs/design/Phayton-Workbench-PRD.figcraft`。

新增行为：先增加 R/验收与 S/跳转，再绑定操作、B/真实符号和 T/测试。产品未实现写 planned/partial。接口改名应同步 B；签名与源码位置由 Roslyn 重新生成。

完整构建：`pwsh -NoProfile -File scripts/Build-Documentation.ps1`。它刷新 Roslyn 编译输入、DocFX API、规格投影，并运行元数据/原型合同检查。生成输出在 `artifacts/docfx/site/specs/index.html`。

仅更新规格投影：先确保现有 Roslyn 结果与源码一致，再运行 `node scripts/build-handoff.mjs`。旧哈希会拒绝构建。元数据检查：`node scripts/run-handoff-checks.mjs`，需要 Node 与 Python 标准库。

重建设计：`python scripts/build-handoff-prototype.py`；本机 FigCraft CLI 通过登记发现，或设置 `FIGCRAFT_CLI`/传入 `--cli`。无需在 CI 安装 FigCraft；提交的原生文件由 ZIP 内容、SHA 和交互结构校验。

可选浏览器验证：`python scripts/test-handoff-browser.py`；依赖已安装 Edge 与 `websocket-client`。它只操作合成原型，按独立目录输出结果/截图到 `artifacts/handoff-browser`，不验证真实 AVEVA 或真人易用性。不存在依赖时先按环境管理方式配置，不在测试中自动下载。
