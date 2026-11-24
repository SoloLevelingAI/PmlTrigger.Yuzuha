# Phayton：从这里开始 / Start here

独立 PmlTwin 的新电脑配置步骤见 [PmlTwin 首次安装与配置](PmlTwin-Configuration.zh-CN.md)。程序安装、AVEVA Host 接入和 SAG 分开核对。

## 人工测试 EXE

运行 `Phayton-*-Windows-Setup.exe`，选择程序目录并确认安装。随后在 PmlTwin 中部署随包的 PMLLIB 和 PMLUI 源码，再确认 EVARS 切换。

## Agent 测试压缩包

先完整解压到独立目录，不要解压进现有安装目录。把下面这句话连同解压目录交给 Agent：

> 请先读取此目录的 agent-skills/yuzuha-setup/SKILL.md 和它引用的安装说明，核对压缩包校验值，生成程序安装预览。等我确认后再执行安装。PML 部署和 EVARS 切换在管理器中确认。

读取 Markdown 是本次任务的使用说明，不代表客户端已经永久注册/加载 Skill。
`agent-skills/yuzuha-setup` 用于安装指导；`skill` 是安装后可选复制到客户端的
运行期 yuzuha-toolkit Skill。两者用途不同，不要把安装指导目录当作运行期 Skill 目标。

本包包含与单独交付完全相同的 EXE、后台工具、安装 Skill、运行期 Skill、
INSTALL.md、VALIDATION.md、示例请求和 SHA256 校验清单；无需先启动 AVEVA 或后台服务。

本版使用 `integrationMode: "none"` 和空的环境列表生成程序安装计划。PMLLIB 和 PMLUI 的源码随 EXE 进入程序目录下的版本化 `deployment` 目录。

先测 EXE、再测压缩包时，建议恢复 VM 快照以测试全新安装；不恢复则属于更新测试，
应保留安装标记并重新生成计划，不能删除标记强行覆盖。

## English

Extract fully outside any existing installation. Ask the agent to read
`agent-skills/yuzuha-setup/SKILL.md` and its installation reference before
running tools, verify `BUNDLE-SHA256SUMS.txt`, preview the program installation,
and wait for approval. PmlTwin deploys the PML archives and confirms the EVARS switch.
The same EXE serves manual and agent-driven installation. UAC still applies.
