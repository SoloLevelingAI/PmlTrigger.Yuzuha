# 可选插件

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](08-plugins.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

插件的状态不会替代基础功能状态。安装后默认停用，操作能力由包清单与实际 worker 握手核对。取消登记保留原包和用户证据。

后端归属：Manager CLI / Manager.Core。

- `plugin.list`
- `plugin.describe`
- `plugin.install.preview`
- `plugin.install.apply`
- `plugin.enable`
- `plugin.invoke`
- `plugin.unregister`

## 必须验收

1. 未安装：选择明确的本机包。
2. 处理中：校验、复制、登记分阶段。
3. 包变化：重新预览身份与文件。
4. 不兼容：保留原因并禁止调用。
5. 停用：基础功能继续可用。
6. VM 模拟：不点亮真实连接成功灯。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
