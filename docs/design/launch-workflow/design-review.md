# 启动工作台视觉交接

本 Feature 的运行页面是 `index.html` + `launch-workflow.css` + `tokens.css` + `launch-workflow.js`。通过管理器 `workbench` 命令打开，直接双击 HTML 不会获得本机服务。原有 Yuzuha Feature 设计和用户正在编辑的 Ribbon 稿均未覆盖。

`launch-workflow.figcraft` 是从真实页面 DOM 几何生成的独立原生文件，包含五个可编辑画面、Theme 集合的十个颜色变量与一个圆角变量。用户字段在设计快照中脱敏。它记录实际执行后恢复观察的状态，不是预制成功演示。原生画面为完整内容高度，视口高度在名称及 `resolution-review.json` 中单独记录。

| 视口 | 内容高度 | 页面横向滚动 | 纵向滚动 |
|---|---:|---|---|
| 1440 × 1000 浅/深色 | 1463 | 无 | 需要 |
| 1180 × 800 | 1500 | 无 | 需要 |
| 960 × 800 | 2253 | 无 | 需要 |
| 390 × 844 | 2737 | 无 | 需要 |

窄屏按单列阅读；长下拉和原始回执有内部滚动，不能为了“无滚轮”压缩表单字号。实际浏览器完成截图检查；原生导出也已检查，字体/符号及阴影可能与浏览器渲染不同。FigCraft 不是布局引擎，不能替代浏览器测量，当前也没有把静态画面伪装成可执行业务原型。用户视觉认可仍待后续评审。

## 双向迭代

1. HTML/CSS 修改后，用 `source/capture-dom.js` 在浏览器只读采集，保存到 `source/captures`，保持口令和隐藏诊断不进入快照。
2. 运行 `node docs/design/launch-workflow/source/build-figcraft.mjs`，再用 FigCraft CLI `run artifacts/manager/launch-workflow/design/commands.json --out docs/design/launch-workflow/launch-workflow.figcraft`。重建会覆盖此生成稿；如有人手工修改，先另存修订，不能覆盖其未合并改动。
3. FigCraft 修改 Theme 后，运行 `node docs/design/launch-workflow/source/sync-tokens.mjs --write` 回写 `tokens.css`；省略 `--write` 只检查漂移。实测 11 个变量、两模式回写及复核通过，语义变化为零。
4. 回写只覆盖已有颜色/圆角 token；字体、阴影、布局和行为由源文件维护。重新跑浏览器分辨率检查后再固化，不自动推导布局代码。

本机预览证据见 `artifacts/manager/launch-workflow/design/final-desktop.png`、`final-mobile.png` 和 `figcraft-light.png`。可重建的脱敏 DOM 快照已随 Feature 保存，不依赖聊天记忆。
