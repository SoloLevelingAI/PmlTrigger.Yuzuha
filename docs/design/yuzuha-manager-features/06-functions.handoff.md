# 函数与调用权限

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](06-functions.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

本机登记与 AVEVA 实际加载分开。登记保存源码身份和参数签名。源码变化后拒绝调用，重新登记默认关闭许可；实际加载需独立核验。

后端归属：communication CLI / 旧GUI本机登记；Manager CLI统一入口待补齐。

- `pml.function.register`
- `pml.functions.list`
- `pml.function.permission`
- `pml.function.invoke`

## 必须验收

1. 未登记：选择源码并填写匹配函数名。
2. 处理中：核验源码与目标后调度一次。
3. 源码变化：拒绝调用并要求重新登记。
4. 许可关闭：检查后人工授予。
5. 会话加载未验证：展示独立提示。
6. 调用结果未知：保留回执，不自动重发。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
