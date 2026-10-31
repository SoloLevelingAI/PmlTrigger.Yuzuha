# 环境部署

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](02-deployment.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

先核对目标，再确认应用。预览固定所选安装、环境文件与程序内容。预览后文件改变时，重新生成计划。原文件备份可定位。

后端归属：Manager CLI / Manager.Core。

- `installation.discover`
- `deployment.preview`
- `deployment.apply`
- `deployment.restore`

## 必须验收

1. 未配置：选择程序包与产品安装。
2. 处理中：按复制、环境、回读显示阶段。
3. 计划过期：保留输入并重新预览。
4. 写入失败：展示错误和备份位置。
5. 部分恢复：列出未恢复项供人工处理。
6. 回执未知：先检查记录，不自动重发。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
