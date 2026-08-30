# 为 Agent 安装 YuzuhaToolkit Skill

中文 | [English](INSTALL-SKILL.en.md)

这份文档供收到 Release 文件夹的 Agent 阅读。Skill 已包含在安装包中，不需要从网络下载。

## 源目录

从当前 Release 根目录解析以下相对路径：

```text
payload\PmlTrigger.Yuzuha\skill
```

源目录必须同时包含：

```text
skill\
├─ SKILL.md
└─ references\
```

复制整个目录内容，不能只复制 `SKILL.md`，否则 Agent 会缺少 MCP、部署和修改工作流资料。

## DeepSeek Harness

优先安装到当前用户的发现目录：

```text
%USERPROFILE%\.dsh\skills\yuzuha-toolkit\
├─ SKILL.md
└─ references\
```

也可以在明确的 Harness 项目中安装为：

```text
<project-root>\.dsh\skills\yuzuha-toolkit\
```

不要把目录复制成 `yuzuha-toolkit\skill\SKILL.md`，也不要继续嵌套多层；
`SKILL.md` 必须直接位于 `yuzuha-toolkit` 下。

## Codex

安装脚本支持：

```powershell
.\Install-Yuzuha.ps1 -InstallCodexSkill
```

等价的用户级目标结构为：

```text
%USERPROFILE%\.codex\skills\yuzuha-toolkit\
├─ SKILL.md
└─ references\
```

## 更新策略

1. 安装前检查目标目录是否已有旧版或用户修改。
2. 首次安装可复制整个目录；已有版本不要无提示覆盖，先备份或比较差异。
3. MCP 注册与 Skill 安装彼此独立：MCP 能连通不代表 Agent 已加载 Skill。
4. 复制完成后重启 Harness，或新建会话以刷新 Skill 目录。
5. 新会话中确认 `yuzuha-toolkit` 出现在可用 Skill 列表，再进行 E3D 连接验证。

## 交给 Agent 的最短指令

> 阅读当前 Release 根目录的 `INSTALL-SKILL.zh-CN.md`，把包内完整
> `payload\PmlTrigger.Yuzuha\skill` 安装到你的用户级 Skill 发现目录。不要覆盖已有
> 修改；安装后检查 `yuzuha-toolkit\SKILL.md` 与 `references` 是否同级存在，并告诉我
> 是否需要重启或新建会话。
