# 安装、环境与运行时选择

正式安装、升级和 Skill 集成使用发行包内的 INSTALL.md 与 Windows Setup。先生成真实目标的修改预览并核对哈希，按已批准计划执行。安装保留现有知识库和用户配置，不自动索引资料，不启动 AVEVA 或运行模型操作。

## CLI 入口

主程序是 `runtime/cli/PmlTwin.Phayton.Cli.exe`，可直接使用 `op list`、`op describe`、`op invoke` 和 JSON 输入。独立 MCP 服务已禁用，不注册或启动旧 stdio 服务，不使用旧 Register-YuzuhaMcp.ps1 路线。已有禁用的 MCP 条目不能因安装而重新启用。

CLI/Skill 文件安装与 AVEVA 环境接入是不同状态。只有用户选定 AVEVA 环境时才改对应环境文件；不要因为发现了其他产品就同时接入。

## AVEVA 环境定位

优先读取目标安装的注册信息和实际启动入口，核对存在的 `evars.init` / INIT；PDMS/AM 仅使用 BAT 时才选择对应文件。修改前备份，安装器维护 PMLLIB/PMLUI 托管块。不要运行整个厂商 EVARS 脚本来猜环境，不改其他产品或 D 盘开发目录。

`PMLLIB` 包含发行包函数/对象，`PMLUI` 包含启动入口；安装目录保留 `PmlTrigger` 标识，供兼容引导定位。缺少目录、权限或 Host DLL 时返回明确错误，不能把连接失败标为成功。

## 初始化与 Host 映射

初始化执行 `!!PhaytonSession = Current Session`，从 `!!PhaytonSession.module()` 读取模块。产品版本通过原生 `VAR !banner BANNER` 获取，不调用未确认的 SESSION 版本方法。创建 Host 后刷新当前模块身份。

- E3D 2.1：E3D2.1 / NET48。
- E3D 3.1 的各小版本：统一 E3D3.1.6 / NET48。
- AM / PDMS：各自已编译的 NET35 DLL。

映射不代表所有平台已经实机验收。未知产品不默认为 E3D，所选 DLL 必须实际存在。AM 使用 Outfitting/HullDesign 等模块，不能套用 E3D DESIGN。

## 修改后的核验

PML 定义更新按开发流程复制、REHASH 和重载；Host DLL 或环境变量更新需要重新启动相应 AVEVA 会话。发现真实 PID 后用 `aveva.session.status` 核验模块、进程身份和 Host 版本。不能通过启动器退出码推断工程登录成功。

CLI 每次直接启动，无常驻 MCP 服务需要重启。Skill 更新后的客户端是否已重新读取应单独核验。用户的模型、资料、密码和个人知识库不进入发行包或公开测试报告。
