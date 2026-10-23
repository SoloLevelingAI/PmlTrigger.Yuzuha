# 2026-10-23 PML 部署重载链路实测

基线 `833fc9aa3bb4a45a6aec8dd59466984cfce8d971` 已推送到 `codex/sync-phayton-object-walker`。本批新增独立开发流程、合成 Form/Object/Function 夹具和 IPC 结果的 HTML 表格导出；现有 `YuzuhaRead*`、两个 Walker、Host 和 C# 调用均未改动。

## AVEVA 实测

环境为 E3D 2.1 的 Model 会话，NET48 Host 0.3.2.0；通过已有 CLI 与命名管道执行，Host 回执 ExecutionThreadId=1。当前 Host 不提供新版 FormQuery/BoundedArrayRead 能力，因此本批只用既有命令和数组读取，不更新运行中的 Host。

| 检查 | 结果 |
| --- | --- |
| 会话与目录 | 从实际发现结果选择 PID，核验启动时间；从 AVEVA 查询并匹配实际 PMLLIB |
| 复制 | 复用 PluginDeployment，4 个独立合成载荷逐项核对来源/目标 SHA256，备份在 PMLLIB 外 |
| REHASH ALL | 成功，单次发送，原始 Host 回执已保存 |
| Object/Form 重载 | 成功；随后新建对象、读取 Form 构造状态并运行验证 |
| 修正版 v1 | 10 步通过，8 项结果精确匹配 |
| v1 → v2 | ObjectRevision=`object-v2`、FormRevision=`form-v2`；两个覆盖文件的备份哈希均与旧目标一致 |
| 最终脚本及含空格路径 | 最终版完整 10 步通过；宏路径使用引号，8 行、0 未解析行、8 项匹配 |
| PMLFNC | `YuzuhaDevAdd(2,3)` 返回 REAL 5 |
| PMLOBJ | 返回版本及 alpha/beta/gamma 三项数据 |
| 原生 Form/Button | 构造后验证计数为 1；实际点击 Run probe 后界面与 IPC 均为 `object-v2 / sum=5 / run=2`，counter=2 |
| 原版本与模型 | 实测 Walker 安装文件仍为原已验证 SHA256；没有写入或保存模型，没有切换旧调用 |

首次验证宏在属性循环中把 GADGET、MENU 等不同类型赋给同一个局部变量，报 `(2,761) Cannot assign variable to result - incompatible types (GADGET=MENU)`。流程停在 execute-validation，没有继续报告成功。按 pmlforms 的直接属性访问方式修正后通过，失败回执保留。

含空格宏路径的独立探针确认：不加引号失败，加引号成功。最终流程随后用含空格的证据目录重新完整实测。错误定位后的修正验证是独立运行，未对失败或未知请求自动重发。

验证数组通过 IPC 读取后删除；额外创建的三个诊断数组已清理。独立测试文件及测试窗体保留供复刻，用户原有窗体未关闭。公开记录不含现场 DBREF、工程名称、模型值、机器路径或完整 RPC 日志。[公开摘要](2026-10-23-pml-development.summary.json)记录最终载荷指纹及计数，本机保留完整 run/rpc 证据。

## HTML 与验证范围

新增 `pml-data-table.mjs` 把 IPC 返回的 Items 组织为 HTML 表格，提供搜索、导入 JSON、导出当前行 CSV。生成了本次 8 行合成结果和前批 2412 行 ELDATA 结果的本地表格；后者使用前批已保存的 IPC 快照，本批没有重新读取大 ELDATA，也没有把模型数据提交到仓库。

HTML 数据映射、失败状态、转义、脚本语法、筛选和 CSV 引号/换行/公式前缀处理通过离线测试。浏览器工具拒绝 file 协议，未绕过该限制，因此没有完成 HTML 的浏览器可视预览或点击下载验证。原生 Form 的真实按钮交互已另行通过。

本批交付开发脚本中的完整串联，未接入现有桌面“一键部署”按钮，未实现生产 HTML ↔ 本地桥 ↔ AVEVA 的持续交互，也不声称跨产品验证。HTML 展示/导出和浏览器直接访问命名管道不同；后续界面需由本地宿主桥接。

离线六项流程回归覆盖：正常链路、无授权不执行、错误 PMLLIB 不复制、REHASH 失败/未知后不重发不重载、旧版本结果不通过、进程身份变化即停止。执行与源码复刻见[测试说明](../../tests/live/pml-development/README.md)。

| 仓库检查 | 结果 |
| --- | --- |
| 开发流程离线回归 | 6 项通过；不连接 AVEVA |
| 文档和表格 Node 回归 | 48 项通过，含 4 项表格数据/导出测试 |
| 文档测试执行器 | 真实 TRX 身份、正执行数、全通过且无跳过的守卫通过 |
| DocFX | 元数据及站点构建通过，0 warning / 0 error；1977 页、超过 3.7 万个本地链接、0 完整性错误 |
| 源码 XML | Walker 36 个声明及新夹具 6 个声明均发布；41 个 PML/PMLUI/草稿文件纳入站点 |
| Script 目录 | 43 个 Script、13 个功能；新流程、导出器及合成夹具已登记 |
| 本批 7 项指纹 | 全部 HashMatch；全库既有 25 Changed / 8 Missing 仍单列，不改写旧来源记录 |
| 范围及格式 | 相对 833fc9a 的 src、PMLLIB、PMLUI 均无差异；`git diff --check` 通过 |

没有构建或部署新的 AVEVA Host、MCP 安装包或生产 HTML 宿主。C# 生产源码及既有 CLI 操作契约保持不变。
