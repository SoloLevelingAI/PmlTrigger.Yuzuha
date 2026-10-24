# 用环境配置控制启动动作

启动时先读取当前会话与模块，再调用独立的 `YuzuhaStartupPolicy` Object，最后建立 Host。改变环境值即可开启或关闭预设动作，不必编辑 Addin 的主流程。

## 开启和关闭

在目标 AVEVA 实际使用的环境文件中选择一个值，并重新启动该 AVEVA 进程：

```bat
set YUZUHA_STARTUP_ACTIONS=rehash-all
```

上面的值表示每个新会话启动时执行一次 `PML REHASH ALL`。关闭时改为：

```bat
set YUZUHA_STARTUP_ACTIONS=none
```

留空同样关闭。当前实现不把任意环境变量内容当成 PML 命令执行；未知值记录为 `Rejected`。如果环境文件由安装器管理，通过更新计划预览修改托管块，避免手工改动后与下一次升级的并发保护冲突。

正式工作台“许可与启动动作”提供“沿用进程环境”“每次启动后 PML REHASH ALL”“不执行特殊动作”。保存配置只持久化选择；下次启动时才传入对应环境。桌面明确选择通过 `YUZUHA_STARTUP_OVERRIDE` 覆盖基础环境值。

环境变量属于进程启动快照。修改 INIT/BAT 不会改变已经运行的 AVEVA 进程；这里的“关闭”在下一次启动生效。

## 代码接点与测试接点

`PMLLIB/Bootstrap/YuzuhaStartupPolicy.pmlobj` 提供这些接点：

| 接点 | 用途 |
| --- | --- |
| `.ReadConfiguration()` | 读取基础环境与桌面覆盖，确定日志位置和关联 ID |
| `.Apply()` | 正常启动入口，读取配置后调用统一调度 |
| `.Apply(policy, logPath, launchId)` | 显式配置入口；测试和未来适配器复用相同调度，不改真实 EVAR |
| `.ApplyConfigured()` | 校验支持的动作、处理关闭状态和每会话执行标记 |
| `.ExecuteAction()` | 内部动作实现接点，新增动作在这里扩展并同步前置校验 |
| `.BeginReport()` / `.Record()` | 生成可查询的启动报告 |

`YuzuhaAddin.ApplyStartupPolicy()` 仅构造对象并调用 `.Apply()`。未开启时不占用执行标记；开启后必须先成功写入 `Started` 日志才执行动作。动作开始后设置每会话标记，即使结果失败也不自动重试。

## 结果与首次升级

`!!YuzuhaStartupReport` 保留 `CommandSessionStartup`、配置来源、关联 ID、阶段及实际状态。结果区分 `SkippedDisabled`、`Rejected`、`Started`、`Succeeded`、`Failed`、`SkippedAlreadyApplied` 和日志失败。动作日志默认使用 `%TEMP%/YuzuhaStartup.log`，桌面启动可指定独立关联文件。

旧安装的 `pml.index` 可能不认识新增 Object。升级预览只为受控安装目录的现有索引补缺失的 `/Bootstrap/` 对象条目，先备份并检查前后哈希；不扫描用户其他 PMLLIB，不在安装期间运行 REHASH。索引是 AVEVA 生成文件，不纳入长期文件所有权检查，后续正常 REHASH 可以更新它。新安装没有旧索引时沿用 AVEVA 首次索引流程。

环境读取实测、显式策略注入测试和安装后新进程验收分别记录；注入测试不能冒充已修改并重新读取真实环境文件。见[本次验收报告](workbench-validation.md)。
