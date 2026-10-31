# 保留原界面，接回核心启动修复

2026-10-20，用户澄清：后端和 ViewModel 可以重写，界面无需重写。原 View 没有因后端分层而必须重画的问题；此前将独立 Archon 外壳作为启动修复的验证入口扩大了范围。

## 本次结果

- 原 `LaunchDesk.App` 的 46 个受版本控制文件，与本次工作开始时的提交 `66f0339` 完全一致，包括 XAML、主题、图标、控件、代码后置与 ViewModel。此次不替换原 ViewModel，未来迁移保持其绑定与交互契约。
- 原 `LaunchDesk.Core.LaunchService` 通过适配调用新 UseCases 的 `BuildMonitorArguments` 和 `BuildMarineArguments`，复用同一套已验证的参数规则。原配置、凭据引用、工具注入、启动跟踪继续使用现有入口。
- Marine 调用只给脚本路径加引号，让厂商 `%1` 模块比较和嵌套 `cmd` 转交得到正确的五个参数；参数预览同步修正顺序。Monitor 保留配置指定的 CATALOGUE、初始化路径和模块大小写。
- 这一步是启动规则接入，不是整个协调器、Windows 适配器或 ViewModel 的迁移完成。后续架构替换遵循 IMPLEMENTATION.md 的原 View 边界。

## 验证

| 检查 | 结果 |
| --- | --- |
| 原 App 源码与本次开始时差异 | 46 个文件零差异；保留此前已加入的圆角与边框修正 |
| 统一 xUnit / ArchUnitNET | 62 通过，0 失败；包含原测试套件入口 |
| 原 Core 测试 | 32 通过；增加原桌面到新规则的 Monitor / Marine 兼容及运行时拒绝边界 |
| 原 Update 测试 | 20 通过 |
| 原界面布局 | 10 页、三种窗口尺寸、36 个页面／展开状态检查通过 |
| 原界面交互 | 70 项通过；绑定错误为空，生成 49 张截图 |
| 原界面独立发布 | Release / win-x64 单文件构建成功 |

启动模拟使用虚构密码和测试接收程序。Marine 场景实际经过 `%1` 比较和嵌套 `cmd`，验证最终五个参数及子进程许可环境；不能原样穿过多层 `cmd` 的字符明确拒绝。未登录真实 AVEVA 项目，不以窗口出现或端口连通证明项目登录与许可签出成功。

## 本机产物

`artifacts/win-x64-original-ui-launchfix/LaunchDesk.exe`，产品版本 `2.0.4-original-ui-launchfix`。

SHA-256：`B94452A227622B3C75AEAB2F4A5DAAEBEA1C38A998918E85F14C9B8213AAE2E2`。

本机独立产物的 portable.json 指向现有 `D:\VirtualMachines\LaunchDesk\Data`；没有复制、迁移或写入真实工作空间，没有覆盖原安装目录或修改快捷方式。正常使用该产物后仍按原程序规则保存所选数据目录。它与原程序共享单实例锁，运行前需从托盘退出原程序。

证据：`artifacts/original-ui-launchfix-tests.log`、`artifacts/architecture-tests/architecture.trx`、`artifacts/architecture-tests/LaunchDesk.Tests.json`、`artifacts/architecture-tests/LaunchDesk.Update.Tests.json`、`artifacts/original-ui-launchfix-publish.log`、`artifacts/original-ui-smoke-20261020-190538/report.json` 及其 `launchdesk-ui-previews/`。这些生成文件、截图和本机 portable.json 不进入 Git。
