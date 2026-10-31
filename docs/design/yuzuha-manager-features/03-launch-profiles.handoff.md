# 启动工程

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](03-launch-profiles.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

入口已创建与工程就绪分别显示。启动回执记录入口进程。工程登录和 Host 连接在总览中单独核验；保留最近启动记录。

后端归属：Manager CLI / Manager.Core。

- `launch.profile.preview`
- `launch.profile.save`
- `launch.profile.history`
- `launch.profile.start`

## 必须验收

1. 未配置：新建或导入启动配置。
2. 处理中：禁止重复创建入口进程。
3. 修订冲突：保留草稿并比较当前版本。
4. 入口缺失：定位字段并重新选择。
5. 入口已创建：Host 仍待核验。
6. 结果未知：查看创建记录，避免重复启动。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
