# 启动策略

初始化：读 `Current Session` → `YuzuhaStartupPolicy.Apply()` → Host。Object 留独立扩展点，不解析任意环境值为 PML。

| 环境值 | 行为 |
| --- | --- |
| YUZUHA_STARTUP_ACTIONS=none / 空 | 关闭 |
| YUZUHA_STARTUP_ACTIONS=rehash-all | 新 PML 会话一次 REHASH ALL |
| 其他值 | Rejected |

工作台选择经 `YUZUHA_STARTUP_OVERRIDE` 覆盖基础值；配置保存后下次进程生效，已开会话不变。托管环境文件通过安装预览改。

先成功写 Started 日志才执行；动作开始即置每会话标记，失败不自动重试。`!!YuzuhaStartupReport` 保留阶段/来源/编号/结果；默认日志 `%TEMP%/YuzuhaStartup.log`，桌面可指定关联文件。日志失败、禁用、拒绝、已执行、成功/失败分别报告。

显式 `.Apply(policy,logPath,launchId)` 供测试注入；不能冒充真实环境重启验收。旧索引升级仅补受控 Bootstrap 条目，备份并核对哈希；安装时不 REHASH。
