# 总览与连接

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](01-overview-connections.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

每次测试都有目标与时间。发现工程窗口后，先选择准确会话。测试结果只说明当前目标；切换产品后需要重新测试。

后端归属：Manager CLI / Manager.Core。

- `connection.sessions`
- `connection.select`
- `connection.status`
- `connection.test`

## 必须验收

1. 无会话：转到启动工程。
2. 处理中：显示目标，禁用重复测试。
3. 目标改变：清除旧灯态并重新选择。
4. 管道缺失：显示产品和诊断入口。
5. 协议不足：保留各项独立结果。
6. 测试过期：回到待复测。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
