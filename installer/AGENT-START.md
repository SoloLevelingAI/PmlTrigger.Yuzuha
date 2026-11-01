# Phayton：从这里开始 / Start here

## 人工测试 EXE

运行本目录唯一的 `Phayton-*-Windows-Setup.exe`，按原生向导操作。
安装前先预览；UAC 由 Windows 请求。不需要先安装 Skill。

## Agent 测试压缩包

先完整解压到独立目录，不要解压进现有安装目录。把下面这句话连同解压目录交给 Agent：

> 请先完整读取此目录的 agent-skills/yuzuha-setup/SKILL.md 和它引用的安装说明，核对压缩包校验值、探测环境。在生成预览前，若我还没说明，请先询问是否安装 CLI 使用 Skill，并核对目标客户端和路径；等我确认完整预览后再安装。不要先执行 EXE，也不要修改未选择的环境。

读取 Markdown 是本次任务的使用说明，不代表客户端已经永久注册/加载 Skill。
`agent-skills/yuzuha-setup` 用于安装指导；`skill` 是安装后可选复制到客户端的
运行期 yuzuha-toolkit Skill。两者用途不同，不要把安装指导目录当作运行期 Skill 目标。

本包包含与单独交付完全相同的 EXE、后台工具、安装 Skill、运行期 Skill、
INSTALL.md、VALIDATION.md、示例请求和 SHA256 校验清单；无需先启动 AVEVA 或后台服务。

新默认使用`integrationMode: "manager"`和空环境列表，通过`plan`生成应用安装预览，
之后在管理器准备EVARS、部署和启动；这不是none，也不表示已接入AVEVA。
只有用户明确要求兼容式直接接入时才使用aveva和`plan-auto`，由程序查找路径；
明确只装文件/客户端时仍使用`integrationMode: "none"`。

先测 EXE、再测压缩包时，建议恢复 VM 快照以测试全新安装；不恢复则属于更新测试，
应保留安装标记并重新生成计划，不能删除标记强行覆盖。

## English

Extract fully outside any existing installation. Ask the agent to read
`agent-skills/yuzuha-setup/SKILL.md` and its installation reference before
running tools, verify `BUNDLE-SHA256SUMS.txt`, ask about CLI usage Skill installation
before planning if not already decided, confirm client paths, generate a preview,
and wait for approval. Reading instructions is not permanent client skill registration.
The same EXE serves manual and agent-driven installation. UAC still applies.
