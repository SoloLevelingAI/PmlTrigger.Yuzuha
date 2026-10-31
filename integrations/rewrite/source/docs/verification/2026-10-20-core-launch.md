# AM / E3D 核心启动协议修复

## 问题与证据

用户确认的 Marine 命令是 `marine.bat Paragon noconsole SEA SYSTEM/****** /ALL`；E3D 2.1 Paragon 命令是 `mon.exe PROD CATALOGUE init catalogue.init SEA SYSTEM/****** /ALL paragon`，路径实际来自本机 Everything3D2.10 安装目录。密码均为文档遮蔽值。

只读核对本机 Marine 12.1 SP4 的入口脚本，确认它先以 `set moduleArg=%1` 读取原词元，再比较 Paragon 等名称；原网关给每个词元加双引号会改变比较内容。该脚本随后还通过 cmd/c 将参数转交原生进程，不能以仅 echo argv 的测试证明兼容。

Monitor 现有模型能够分别指定 ProductCode 和 InitializationPath。E3D 2.1 Paragon 需要明确的 CATALOGUE／catalogue.init 定义；不能把 Design 的 E3D／launch.init 定义当成所有模块的通用入口。本次没有自动覆盖用户的应用定义或产品安装文件。

## 实现

- Build 仅选择入口协议、调用环境构造并组合计划；Monitor 与 Marine 分别为公开的 BuildMonitorArguments、BuildMarineArguments，工具变量展开移入私有 BuildEnvironment。
- 两个公开方法补充 summary、remarks、example、参数与返回说明，保留唯一 SecretSlot；Monitor 不硬编码产品、版本或初始化路径。
- MarineBatchPolicy 校验能原样穿过多层 cmd 的词元。网关保留入口路径引号，五个参数不添加字面引号；运行时秘密不支持的字符在进程创建前明确拒绝，原生 EXE 的 ArgumentList 能力保持。
- 网关创建失败增加安全的系统错误码，清理父进程临时参数引用与环境；不输出命令行或秘密内容。
- 编辑器预览按 Marine 协议显示模块、noconsole、项目、登录和 MDB，避免展示错误顺序。
- 新增核心启动导读；阅读配置对核心类型和方法进行显式标记，经真实元数据解析校验，API 页显示核心标识并突出三个构造入口。

## 示例与回归

LaunchPlanExamplesTests 直接读取本次编译的 UseCases XML example，与真实参数数组的遮蔽渲染逐字比较。它还独立断言词元数量、顺序、MDB、完整带空格路径、产品／init 保留及唯一秘密插槽。

Marine 集成场景先用旧引用方式调用模拟脚本，断言模块比较返回 41；再走真实 WindowsProcessGateway，穿过同样的 %1 比较与嵌套 cmd，断言接收进程获得五个正确参数和独立许可环境。两个 Monitor 场景通过独立原生接收程序验证 E3D／launch.init 与 CATALOGUE／catalogue.init 的实际 argv。

进程场景必须通过原测试 EXE 执行。dotnet run 额外 Job 可能拒绝 CREATE_BREAKAWAY_FROM_JOB 并给出系统错误 5；统一 xUnit 入口保留独立 EXE 的既有方式，没有移除产品的进程生命周期边界来规避限制。

| 验证 | 结果 |
| --- | --- |
| 统一 xUnit / ArchUnitNET | 62 通过，0 失败，0 跳过；原 43 项加 19 项新核心检查 |
| 原 Rewrite 场景 | 30 项均通过，其中新增 Marine 1 项、Monitor 2 项 |
| 新参数、XML 示例及拒绝边界 | 16 项通过 |
| 既有旧版 Core / Update 套件 | 内部 30 / 20 项通过，仍以两个套件入口纳入 xUnit |
| 新发布 EXE 的 WPF 契约冒烟 | 40 项通过，隔离工作空间与虚构凭据 |
| DocFX metadata / build | 0 警告、0 错误；277 个手写方法，233 个产品类型 |
| 文档发布完整性 | 1,027 个成员节空摘要为 0；282 个 HTML 文件无重复 ID；18 个阅读入口、300 条链接通过 |
| 浏览器 | 43 项、26 页通过；无页面错误、失败响应及外网请求 |
| 文档完整性／缓存回归 | 5 项通过 |

## 产物和边界

修正版发布于 `artifacts/win-x64-rewrite-3.0.1-launchfix/LaunchDesk.Archon.exe`，同目录含 Worker、运行依赖与清单。已有发布目录和真实用户配置保留。

本次验证使用模拟厂商解析方式、虚构密码及原生接收程序。没有自动登录真实 AM／E3D 项目，也不以模拟进程或许可端口可达证明业务登录／许可签出成功。Marine 对不能保持字面的特殊字符明确拒绝；这项兼容范围在公开方法备注与核心导读中说明。

本机证据：`artifacts/architecture-tests/architecture.trx`、`artifacts/documentation/core-launch-suite.log`、`core-launch-green.log`、`core-launch-docs-build.log`、`core-launch-browser.log`、`core-launch-doc-regressions.log`、`core-launch-publish.log`、`artifacts/core-launch-release-smoke/report.json` 及 `artifacts/docfx/verification.json`。生成证据不纳入 Git。
