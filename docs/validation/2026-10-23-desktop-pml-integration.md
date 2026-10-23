# 2026-10-23 桌面 PML 开发接入：阶段记录

先复验 Phayton，再经用户关闭 AM/E3D 并批准预览完成两次更新；修正版新 E3D 的自动 REHASH 及 ELDATA 链路已通过。以下“更新前”至“后续验收”为保留的初始阶段，末尾是最新结果。现有 Walker 逻辑和 Host 原字节保留，不改业务调用，不保存模型。AM 原生启动有记录和管道，但既有发现器漏识别实际进程，不能宣称 AM 的 IPC 全流程通过。

## 更新前真实验证

既有 E3D 2.1 / NET48 Host 0.3.2.0 会话：运行 Walker smoke，Failures=0、UnparsedCount=0。继续使用已部署 HTML 适配器复验大 ELDATA：2410 项完整提取、7 次 IPC 动作、首中尾 3 个下标核对及 4 项 HTTP 边界检查通过。VAR SPLIT 提取、格式化、缓存后的下标取值均完成；本轮没有重载 Walker，也没有复制安装文件、重建原生 Host 或保存模型。

现场 DBREF、会话身份和原始数组只留本机，公开仓库仅记录统计。离线 Phayton 40 项回归全部通过。这些结果只覆盖现有测试输入，不能证明对象对所有输入都没有逻辑缺陷；本轮未因怀疑而擅自重写 Walker。

## 已实现与隔离验证

桌面新增独立 HTML 打开按钮，CLI 新增 `--cli web open [--no-browser]`，复用同一个启动入口。CLI 对本机既有 HTML 服务健康核验后成功输出地址。打开入口不发送 PML。

桌面原生 C# 流程复用 PluginDeployment 与既有 YuzuhaRpcBridge：会话/PMLLIB 核验、备份复制、REHASH、Object/Form 重载、验证宏、数组读取和精确核对。新流程 10 项隔离测试通过，覆盖顺序、错误 PMLLIB、未知 REHASH 不重试、错误/未解析业务结果、预览变化、会话变化、无效清单、启动策略优先级和独立关联日志。测试使用临时目录及模拟 IPC，不代表 AVEVA 实机通过。

安装桥的离线测试通过：启动变量仅显式加入，更新保留已有策略、可显式关闭、未知值拒绝；原有编码/字节保留、托管块幂等、冲突拒绝和事务恢复测试继续通过。

工作台完整回归 155 项通过、0 失败、0 跳过（包含新增 10 项）；桌面与 Native AOT CLI 构建通过。自包含桌面在隔离配置下检查 14 个页面、2 个主题和 3 种视口通过，绑定错误为 0。这是程序化布局检查，不能代替真实按钮与 AVEVA 交互验收。HTML 宿主发布完成；已安装的 26 个 Host 发布文件按原包清单逐项核对后原字节暂存，未重新编译 Host。

## 后续验收

更新前向用户提供来源版本、包散列、安装路径、环境文件逐行差异、保留的客户端接入选择及恢复位置。用户关闭 AM/E3D 并确认后，使用同一包和计划安装；检查安装结果及文件散列。重新启动后分别验证特殊启动日志、REHASH 实际结果、桌面 HTML 打开、部署 v1→v2 的 Object/Form 版本与 8 项预期、ELDATA 全量与下标。AM 和 E3D 分别记录；未运行的项目保留 NotRun，不以旧会话结果替代。

## 首次安装与新 E3D 的实际结果

首次安装使用用户批准的同一 EXE、计划散列和路径，结果 success、两个环境 configured-not-launch-tested。348 个发布文件与两份环境配置的 SHA256 逐项匹配；已安装 CLI 的 web open 成功。客户端 MCP/Skill 注册保留。

包清单与 bundle SHA256 全通过。旧 verify_delivery.py 把 2026-09-20 D 盘历史同步指纹当作当前发布指纹，停在 pml.index；历史记录未改写，本轮以实际包清单校验，不冒充旧同步检查通过。

用户从 LaunchDesk 新开 E3D。用户按 Esc 停止 Computer Use 后，未继续界面操作；改用同一个 DesktopDevelopmentHost 与 PmlDevelopmentWorkflow 的独立控制台验证，不能称为新版按钮点击验收。

| 检查 | 结果 |
| --- | --- |
| v1 新载荷 | 会话/PMLLIB、复制、REHASH、Object/Form 重载、验证宏、结果核对共 10 步通过 |
| v1→v2 | ObjectRevision=object-v2、FormRevision=form-v2，8 项精确匹配、0 未解析行 |
| 覆盖备份 | 两个旧文件备份的 SHA256 与 v1 原目标相同；备份位于 PMLLIB 外 |
| 原生能力 | 函数结果 5、三行对象数据、Form 计数 1、类型 BUTTON；本轮无鼠标点击验证 |

独立类型名避免与已有业务对象/Form 重名。原始会话、DBREF 和模型数据只留本机。

## ELDATA 子元素前置条件

新 E3D 最初访问旧子元素引用报 `(2,118) ... reference to an illegal element`。用户明确指示先输入 `$T8+`；在固定会话执行一次后完整 HTML/IPC 回归通过：2410 项、7 次动作、3 个下标、4 项 HTTP 检查，VAR SPLIT、格式化与下标访问均通过。

该前置条件单列，最初的引用错误不能据此归为 Walker 缺陷，也不扩大为所有 ELDATA 的通用要求。本轮没有将 `$T8+` 自动加入每次启动策略。

## 启动读取缺陷与修正

最初将 EVAR 失败处理成 unset，误判为 LaunchDesk 没继承变量。实测 `EVAR 'YUZUHA_STARTUP_ACTIONS'` 返回 rehash-all，不带引号报 `(61,125) F&M: Gadget STARTUP not found`。变量实际存在，无需改写 LaunchDesk 配置。

四个含下划线的启动变量均改用引号，空关联 ID 回退 external-launch。独立 PMLOBJ 提取同一修正方法，在已就绪会话执行 REHASH 并写 Started/Succeeded、0 未解析行。日志阶段字段是启动约定；手动探针不能证明实际进程启动时序。修正版安装并新开客户端后的自动执行为 NotRun，AM 本轮亦 NotRun。

## 日常启动改进与验证边界

产品选择前置并自动填路径；日常保留项目、用户、密码、可编辑 MDB 和模块下拉，名称自动生成，高级路径/参数折叠，默认浅色保留。LaunchDesk 只读带入非密码草稿，不立即保存或启动。

清除密码移至设置，明确选配置后二次确认、默认取消；保存失败恢复关联，共用凭据保留，取消记住本次新密码不暗中删除旧凭据。

完整工作台回归现为 160 项通过、0 失败、0 跳过；桌面编译 0 warning/0 error。首轮 14 页隔离布局检查属于旧布局，新布局在用户停止界面操作后没有继续可视验收，不能借用旧截图冒充。新修正版仍须经预览和客户端关闭后安装。

界面分工：简单输入/确认/少量按钮可用原生 PMLFRM；复杂表格、筛选、排序和导出优先 HTML，继续复用 PML/IPC。这不等于所有控件或所有版本已通过。

## 修正版安装及真实启动复验

用户关闭 AM/E3D 与 Yuzuha 工作台并批准第二份具体预览后，使用同一 EXE/计划散列安装。349 个发布文件与两份环境配置逐项匹配；本次两份环境文件均 unchanged。覆盖备份及恢复报告由安装器保留，客户端注册继续不变。

用户从 LaunchDesk 新开 E3D 和 AM。未手动调用启动方法或先发 REHASH，E3D 自身 `!!YuzuhaStartupReport` 读回 Policy=REHASH-ALL、Phase=after-pml-init-before-host、LaunchId=external-launch、Status 包含 Started/Succeeded，6 行、0 未解析行。原生日志也出现对应新启动记录，确认引号修正后的真实自动启动已通过。

新会话再次按用户指定的 `$T8+` 前置条件完成 HTML/IPC 大数组复验：2410 项、7 次动作、3 个定向下标、4 项 HTTP 检查通过。现有 Walker 未改逻辑。

AM 此次为 Outfitting，实际进程名 marodes；原生启动后有第二份成功记录，PID 专用管道存在。但当前 AvevaSessionDiscovery 的工程进程白名单未包含 marodes/mardra，内置发现只列 E3D。AM 日志归属依据同轮两个新进程与两份记录，尚未通过 AM 内的结果数组回读；不绕过会话核验选择未被发现的 PID，也不把这个证据扩大为 AM IPC 通过。该兼容性缺口已记录，未在本轮悄悄扩展发现器。

最终 DocFX 0 warning/0 error，2272 页、42853 个本地链接，0 完整性错误；XML/源码目录缺失注释为 0。文档 Node 回归 38 项通过，工作台完整回归 160 项通过。新日常界面的人工可视操作与密码确认弹窗未在用户停止 Computer Use 后继续验证。
