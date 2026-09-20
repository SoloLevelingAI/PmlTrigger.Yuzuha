# v0.3.3 — Windows Setup 与 PML 更新 / Windows Setup and PML update

## 中文

- 原生 Inno Setup 中英文向导：启动提权、明确安装目标、修改前后预览、备份及结果报告，不再弹出独立 WinForms 配置窗口。
- Agent 压缩包包含安装 Skill、运行期 Skill、后台工具及同一份 EXE；在预览前确认 MCP/Skill 接入与真实客户端路径。
- `plan-auto` 由程序探测 AVEVA 环境并生成候选计划；接入模式下拒绝空环境，配置缺失或歧义会停止。明确选择“不接入 AVEVA”才能只安装文件/客户端。
- EVARS.INIT/BAT 保留原编码和原脚本，在末尾更新不带双引号的 PMLLIB、PMLUI/PDMSUI、Yuzuha 接入行；报告区分配置写入与实际加载验证。
- 补齐维护者的 PML 窗体监控、视图、截图和剪贴板相关源文件。便携启动函数保持仓库版本，不带入本机调试路径。PML 注释整理延后到下次更新。
- 安装器源码及测试位于 `installer/`，UTC/SHA256 元数据集中在 `docs/semantic/`。

### 下载与验证

- 人工安装：`Yuzuha-0.3.3-Windows-Setup.exe`。
- Agent 安装：`Yuzuha-0.3.3-Windows-Agent.zip`，完整解压后先读 `START-HERE.md`。
- `Yuzuha-Windows-Setup-Sources.zip` 是安装器源码补充包；GitHub 自动生成的源码包包含完整仓库源码。
- 维护者已反馈 preview11 安装测试成功；v0.3.3 基于该版本调整发布标识与说明，环境处理逻辑不变。自动化覆盖构建、配置回归、整包哈希及不执行安装的预览测试。并非所有 AVEVA 版本、客户端或 UAC 场景均已验证。
- 本次发布保留 Host 0.3.2 与 NET10 Native AOT 0.3.1 二进制，不要求安装 .NET 10 Runtime。安装辅助工具需要 .NET Framework 4.8；支持 Windows 10/11 x64。EXE 尚未代码签名。
- 旧 PS1/非受管安装不能直接覆盖；请先阅读 `installer/INSTALL.md`。不分发 AVEVA 私有 SDK、本地知识库或用户录制数据。

## English

Native bilingual Inno Setup now provides startup elevation, explicit installation
scope, before/after preview, backups and result reports. The Agent archive ships
the same EXE, a dedicated installation Skill and runtime Skill. Client MCP/Skill
choices and actual target paths are confirmed before preview.

Program-driven `plan-auto` discovers AVEVA configurations; empty integration
requests are rejected and unresolved candidates require review. Vendor EVARS
content/encoding is preserved, with unquoted managed SET assignments appended.
Results distinguish configuration writes from actual application loading.

The release includes the maintainer's previously unpublished PML form/view/capture
and clipboard sources. The portable bootstrap is retained; PML comment cleanup
is deferred. Installer sources/tests and centralized UTC/SHA256 metadata are included.

The maintainer reported successful preview11 installation testing. This release
changes publication identifiers/documentation, not its environment-editing logic.
Build, regression, archive integrity and plan-only checks supplement that report;
it is not universal AVEVA/client/UAC certification. Existing Host 0.3.2 and NET10
Native AOT 0.3.1 binaries are retained. Windows 10/11 x64 and .NET Framework 4.8
are required for Setup; .NET 10 Runtime is not. The installer is unsigned.

Download the EXE for manual setup, or the Agent ZIP and read START-HERE.md first.
Review INSTALL.md before updating an older PS1/unmanaged installation. Installer
implementation/tests are attributed to OpenAI Codex; PML prototypes to the maintainer.
