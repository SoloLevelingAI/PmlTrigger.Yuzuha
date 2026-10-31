# PMLTWIN / Yuzuha 契约演示

打开 `index.html` 即可离线播放；无需服务器、CDN、字体下载或构建。

- 21 页，每页一个核心主张。方向键 / PageUp / PageDown / Space 翻页，Home / End 跳转。
- I 打开可筛选索引，包含 24 条需求原文、13 个 Feature 与 Script 登记；Esc 关闭。
- 每页“边界与依据”展开实现限制；底部链接定位仓库原始来源。
- F 全屏；P 或打印按钮可导出横向逐页 PDF。窄屏内容允许自然纵向滚动。

`content.js` 是 2026-10-31 的只读快照：保留 contract.json 原始状态（15 verified、5 partial、4 planned），没有升级任何完成状态。source-manifest.json 记录所引用源文件的 SHA-256；源文件变化后须重新审阅快照。快照与来源链接可以互相核对；哈希不代表测试通过。

规范来源为 docs/specification/contract.json；Feature/Script 来源为 docs/reference/capabilities.json。当前不存在 docs/reference/catalog 目录，目录呈现逻辑在 docs/reference/template/public/catalog.js。OpenSpec 集成草案不替代生效规范。历史报告、静态分析、离线测试和当前实机验证分开陈述。

本演示没有执行 PML、写配置、启动工程或调用网络的能力。第 19 页的 GitHub 链接只供人工打开原失败运行，不是在线依赖。

本地验证（非云端、非 AVEVA 实机）：

运行 `node docs/presentation/contracts/verify.mjs`，需要安装 Playwright 后，配合本机 Microsoft Edge。验证器在 file:// 打开页面，启用 reduced-motion 避免捕获动画初态；对 1440×900、1280×720、390×844 的全部 21 页共 63 个组合核对水平溢出、桌面垂直溢出、可读主内容（图、表、实例或信息卡）和最终 opacity=1。窄屏允许纵向滚动。另核对键盘翻页、索引24条、R19筛选、打印21页与脚本错误；来源哈希不匹配严格失败。

本轮本地结果见 verification.json，零失败、零脚本错误。previews/ 保留全部21页，contact.png 为联系图；两张单页预览位于 preview-cover.png / preview-script.png。该验证不声称云端、Host 或真实工程已经通过。

可复现前置条件：Node.js（支持ES模块）、标准安装的 `playwright` npm包、Microsoft Edge 稳定版（verify使用 `channel: 'msedge'`，不依赖用户缓存路径）。安装 Playwright 后可直接运行；独立副本可在演示目录执行 `npm install --no-save --package-lock=false playwright`，再执行 `node verify.mjs`。此操作无需更改仓库根package或锁文件。若没有Edge，可先安装Microsoft Edge，或由维护者将channel改成已安装的Chromium；缺少浏览器时验证器明确失败。

补充打印导出：`node export-print.mjs` 输出到仓库 artifacts/contracts-print.pdf，需同样的Playwright/Edge前置条件。PDF已用pdfinfo确认21页A4横向，并经Poppler渲染关键流程/表格/JSON页目检。Script索引新增5类手段契约（启动发现、CLI有界捕获、冻结复制、知识导入、验收），原目录所有字段可无损展开；未登记的输入/恢复细节明确标未单列，避免补造保证。
