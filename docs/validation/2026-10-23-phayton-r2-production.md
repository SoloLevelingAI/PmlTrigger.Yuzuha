# Phayton R2 正式落地与安装预览 — 2026-10-23

需求：TRACE39。基线 `da5b5ec`，分支 `codex/sync-phayton-object-walker`。本批把 R2 接入 WPF 和 DocFX，保持现有调用与用户数据；测试通过后生成 0.3.4 Inno 安装包，先预览，关闭客户端并确认后才更新。

## 变更范围

原生标题栏、六组主导航、分组任务导航与窄屏替代入口；14 个已有主页面保留。启动模块卡片接既有字段，修复可编辑下拉框缺失文本编辑器导致 MDB/模块自填内容不显示的问题。知识库拆分四页并保留原增删改、修订和恢复处理器；设置拆成六页，密码清除仍单独确认。DocFX 使用 R2 首页、阅读布局、Phayton 图标和默认浅色，兼容旧阅读偏好。安装包交付完整站点和只读本地文档宿主。

## 验证过程

以下验证通过，使用隔离工作区、知识库与合成凭据，不启动 AVEVA：

| 项目 | 结果与证据 |
| --- | --- |
| Release 编译、自包含桌面与文档宿主发布 | 0 警告、0 错误；`artifacts/phayton-r2-publish.log` 与 `phayton-r2-docs-host-publish.log` |
| 业务单元测试 | 173 通过，0 失败；`artifacts/tests/phayton-r2-unit/phayton-r2.trx` |
| 架构测试 | 14 通过，0 失败；`artifacts/tests/phayton-r2-unit/r2-architecture.trx` |
| Node 文档、XML、目录、UI 溯源、HTML 应用回归 | 51 通过；`artifacts/tests/phayton-r2-node.log` |
| 发布后的 WPF 隔离自检 | 14 主页面、两主题、三种尺寸全部通过，绑定错误为 0；新增六设置页、四知识页、窄屏导航、模块卡片和 MDB 实际输入进入既有配置对象验证；笔记 CRUD/修订恢复与凭据回归通过 |
| 安装器 NET48 回归 | 用户文件保护、分发文件本地修改拒绝、目标确认、编码、环境受管块、回滚及并发检查通过 |
| 文档与 PML 开发流程脚本 | TRX 精确匹配、正执行数、禁止跳过通过；模拟传输的成功、未授权、库不匹配、REHASH 失败、旧结果和会话变化六种场景通过 |
| DocFX 构建 | 全量构建零警告/错误；2,484 页、53,416 个本地链接检查通过。最终发布复核以 `artifacts/docfx/integrity.json` 实际统计为准 |
| 随包只读文档宿主 | 真实发布 EXE 启动；首页 SHA256 一致、CSS 可读、写请求拒绝、非回环 Host 拒绝、开发 API 不暴露；`artifacts/tests/phayton-r2-docs-host/documentation-host.json` |
| 浏览器检查 | 真实生成首页、文章阅读和全文搜索可用；`PhaytonObjectWalker` 搜索返回 21 条，打开对应正文；保留 API 与源码入口 |

发布桌面截图和自检原始回执保存在本任务 `work/r2-ui-test`，使用缩短的隔离路径避免测试夹具的 SQLite 路径超限。截图不是模拟 HTML。此阶段文档宿主单独隔离运行，尚未替换本机已安装工作台；后续授权安装结果见下文。

环境记录：Windows、本机 .NET SDK 10.0.400-preview.0.26322.102、既有 Inno Setup 6.7.3。沙箱内 Windows 凭据/临时备份操作受限、原始单元测试 testhost 连接超时，均不计通过；获准后在隔离环境重跑通过。最初较长的发布自检目录发生 SQLite Error 14；缩短测试目录后实际发布 EXE 全部通过，未调整生产数据路径。

## 打包与更新预览

Inno 交付版本为 0.3.4，沿用既有 AppId 和安装目录。包包含本批发布桌面、完整生成文档和只读宿主；既有 Native AOT、五套 AVEVA Host、CLI/MCP/IPC 与 PML 调用保持。包哈希、交付清单核验及本机计划摘要随本任务更新预览交付，精确哈希不写入包内文档以避免循环依赖。生成计划只读取已选择的 AM/E3D 配置，不执行安装或环境文件。

## 当前边界

本机已按下述批准计划更新；AM/E3D 未在本批启动或执行。原始 ELDATA 大数组回读的无响应/结果未知仍未关闭。原生窗体手工键盘、Windows Snap 和其他电脑 DPI/UAC 仍需人工验收。本机包未签名，签名在指定的其他电脑进行。

## 经授权的本机更新 — 2026-10-23 23:19 CST

用户明确回复“已关闭，同意按预览更新”。执行前重新核验整个 Agent 包清单、安装 EXE 和冻结计划；计划目标无漂移。AM/E3D 与工作台已退出，停止安装目录中四个旧 MCP/Knowledge 后台进程以释放文件占用，其他预览服务保留。按原路径、原账户运行批准包并正常请求 Windows 提权，不绕过 UAC，不重新注册 MCP/Skill。

- 安装包 SHA256：`88bd83aa0ff0a397a46e62954b13edc87614c9bddf657531ebfa0b32cac5bdf4`。
- 批准计划 SHA256：`ce1d7a108ea35466f8b1e55c180f031c12196bfba7abe697f643d72d7f813275`。
- 安装退出 0，实际报告 `success`，环境数 2，状态 `configured-not-launch-tested`。
- 全部 3,247 个载荷文件匹配批准后的 SHA256；桌面 ProductName 为 Phayton，FileVersion 为 0.3.4.0。开始菜单 Phayton 分组已生成。
- 137 个原有用户/配置文件在安装前复制备份，安装后及隔离界面检查后均字节不变；包含本机配置、既有资料和生成索引。两份 AM/E3D 环境文件哈希不变，每次启动 REHASH ALL 策略保留。未读取真实密码。
- 直接运行安装位置的桌面 EXE，隔离状态、用户/应用/知识目录；14 页、两主题、三尺寸、笔记 CRUD 与修订恢复、模块卡片、MDB 编辑、导航及合成凭据验证通过，绑定错误 0。原始回执位于任务 `work/r2-installed-ui`。
- 安装位置的只读文档宿主通过首页散列、CSS、写入拒绝及 Host 检查，原始回执位于 `work/r2-installed-docs-test`。
- 安装位置执行 CLI 的 `get_builtin_usage` 和知识 CLI 的 `list` 正常返回；后者列出 8 个工具。全部使用隔离日志目录，未调用 AVEVA。当前聊天的旧 MCP 连接返回 `Transport closed`，需重新启动 AI 客户端恢复；不能把离线 CLI 通过记为当前 MCP 连接已恢复。

完整本机安装报告保留在 `LocalAppData/YuzuhaToolkit/SetupReports/bdcc1f1416a547d38a94646f75668957`，私有备份和原计划留在本机任务目录，不提交仓库。本节是在不可变发行包生成后追加的安装记录，不修改已安装的受管文档文件或包哈希。
