# 知识导入与检查

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](05-knowledge.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

检查报告保留数据库身份与片段位置。两套资料分别发布和检索。缺少原来源时说明可搜索范围与新鲜度；同名内容冲突需要先修正。

后端归属：Manager CLI / Manager.Core。

- `knowledge.import.preview`
- `knowledge.import.apply`
- `knowledge.inspect`
- `knowledge.libraries.list`
- `knowledge.library.search`
- `knowledge.library.chunk`

## 必须验收

1. 未配置：明确选择资料源或已有库。
2. 处理中：构建、检查、发布分阶段。
3. 同名冲突：保留旧库，选择新名称。
4. 结构失败：禁止启用并展开具体检查。
5. 缺少来源：保留可搜索与新鲜度提示。
6. 活动写入：取得一致快照后重试。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
