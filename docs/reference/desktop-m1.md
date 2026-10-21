# PMLTWIN WPF：三条 M1 流程

本批在已有 WPF 项目交付会话选择核验、全局函数登记授权调用、原生 Form/Gadget 与有界数组读取。视觉以 v02 为基础，恢复 232px 深色图标侧栏、琥珀选中标记、全局页头、圆角卡片、总览横幅与指标区、会话列表和身份双栏、函数登记列表和详情双栏。原有启动、插件复制预览与本机日志保留。评审、测试执行、Web 管理和知识库新界面显示后续里程碑，不填入虚构运行结果。

实现使用可读字号，窄窗口下双栏转为单栏；总览沿用原稿结构，缺失的长期趋势与目录健康分显示缺失状态。本机有界记录的完成项数、平均耗时和成功率注明分母，不冒充长期统计。页头搜索用于按页面名称导航，Ctrl+K 聚焦，Enter 前往。

## 重点复用

| 能力 | 桌面使用方式 |
| --- | --- |
| `AvevaSessionDiscovery` / `YuzuhaRpcBridge` | 发现不自动选择；连接后按 PID、启动时间、模块和专用管道核验，每次请求重新核验。 |
| `PmlFunctionRegistry` / `PmlFunctionTools` | 使用同一个本机注册表、源码哈希、默认拒绝、重新登记撤权、已有参数生成器及人工信任提示。 |
| PMLFORMS / FMSYS | 使用已有查询桥读取窗体、控件和固定元数据；不重写原生窗体管理，不修改 PMLLIB/PMLUI。 |
| 共享有界策略 | 使用原生 NextIndex 续读，空页、扫描预算、索引上界和返回数分别显示。 |
| `LaunchService` / `PluginDeployment` / `RuntimeJournal` | 使用实际启动预览、现有复制与备份、滚动日志；未新增 HTTP 执行端点、RPC 字段或通用工作流引擎。 |
| LaunchDesk 文档测试机制 | 新测试继续进入 test-map、DocFX 双向链接与按 ID 执行器。 |

`SessionWorkbench` 是本窗口的状态与测试接缝，编译到现有项目；没有另建生产库。Desktop 直接引用 MCP 的已有托管服务，依赖图据此新增 Desktop → MCP。MCP 的工具、CLI、RPC DTO、Host 以及原生 PML 均未修改。

## 使用

启动 `YuzuhaToolkit.Desktop.exe`。从总览进入工程会话，发现并明确选择目标。无 Host、身份失效和能力不足分别反馈；输入保留，旧查询清理。函数登记与授权支持离线操作，执行和原生查询要求有效目标。

托管安装使用现有包根下的人工信任记录；独立开发可沿用 `YUZUHA_TOOLKIT_ROOT` / `YUZUHA_TRUST_FILE` 指向已有记录。

函数只接受当前契约支持的全局名称和 STRING / REAL / BOOLEAN 参数。登记不会加载 PML，保存的许可不证明 AVEVA 当前加载版本。源码哈希变化时拒绝；重新登记关闭许可。注册表未保存旧源码正文，界面显示当前源码与完整哈希，历史差异需定位 Git 版本，不凭哈希伪造旧正文。

窗体、控件与属性依次选择和显式读取，不因选中行自动触发原生查询。继续读取沿用原索引；目标、名称或预算变化后不能拼接旧页。属性 unavailable 不推断为 false；callback 只读文本。数组读取已有变量，不执行生成表达式或删除数组。

通信失败、发送后的取消或不可解析回执保留结果未知，不自动重试。调度成功与业务返回值分开；文件复制、重加载和实测亦不互相代替。

## 复刻与范围

```powershell
./scripts/run-documented-test.ps1 -TestId 'smoke:desktop-flows'
./scripts/run-documented-test.ps1 -TestId 'manual:desktop-aveva-m1' -Describe
```

WPF Smoke 使用隔离目录与合成 Host，验证窗口创建、三条流程的数据绑定、拒绝与未知状态以及 1440×960、1280×720、853×480 逻辑视口。最后一项对应 1280×720 在 150% 缩放下的逻辑空间；它不是人工可用性验收。输出默认位于本机 artifacts，记录提交、dirty、环境和证据。CI 只保存离线回执与环境，不上传用户工程。

人工实机走查见仓库 `tests/manual/desktop-m1.md`，未运行保持 NotRun。设计文件为仓库 `design/figcraft/PMLTWIN-v03.figcraft`；说明、复用边界、预览和原型状态在其同目录。WPF 本批实现 Light 配色与系统缩放；设计中的 Dark / High Contrast 变量继续保留，桌面主题切换待后续实现。
