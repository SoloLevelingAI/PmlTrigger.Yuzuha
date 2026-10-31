# 用户数据目录

状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。

只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](07-user-data.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。

## 行为与边界

程序更新保留用户资料。资料目录与程序安装目录分开。迁移有独立计划、发布检查和切换记录，可定位每项冲突。

后端归属：Manager CLI / Manager.Core。

- `manager.status`
- `data.migration.preview`
- `data.migration.apply`

## 必须验收

1. 未配置：显示推荐目录及现有旧位置。
2. 处理中：复制、校验、切换分阶段。
3. 路径冲突：选择独立目标。
4. 活动库占用：关闭写入后再迁移。
5. 复制失败：保留旧源和原设置。
6. 环境覆盖：说明当前最终生效来源。

主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。

## 上下文预算

每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。
