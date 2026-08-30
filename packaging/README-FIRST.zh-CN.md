# PmlTrigger.Yuzuha Native AOT 复制安装包

中文 | [English](README-FIRST.en.md)

这是一个未压缩的 `v0.1.0-preview.6` Release 目录。它包含：

- .NET 10 Native AOT MCP 单文件；目标机不需要安装 .NET 10；
- AVEVA 内加载的 Net48 Host 及其可再分发依赖；
- PMLUI、PMLLIB、Agent Skill 和操作文档；
- 复制安装及 E3D `evars.init` 配置脚本。

如果由 Agent/DeepSeek Harness 负责安装，第一步让它阅读
[`INSTALL-SKILL.zh-CN.md`](INSTALL-SKILL.zh-CN.md)，该文档会告诉 Agent 在哪里找到
完整 Skill，以及复制到哪个发现目录。

不包含 AVEVA、PMLNet 或其他 AVEVA 专有程序集。用户必须已经安装并获得许可使用 AVEVA E3D 2.1/.NET Framework 4.8 环境。

## 近一键安装

关闭 E3D，以 PowerShell 运行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

默认复制到：

```text
%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha
```

脚本会自动寻找 E3D 的 `evars.init`，修改前创建带时间戳的备份，并将安装目录中的 `PMLUI`、`PMLLIB` 追加进去。非标准安装可指定：

```powershell
.\Install-Yuzuha.ps1 -EvarsInitPath 'D:\AVEVA\Everything3D2.10\evars.init' `
  -RegisterCodex -InstallCodexSkill
```

如只想复制而暂不修改 E3D：

```powershell
.\Install-Yuzuha.ps1 -SkipE3DConfiguration
```

已有安装默认不会被覆盖。使用 `-Force` 更新时，脚本会先把完整旧安装移动到同级
`backup` 目录，再复制全新 payload；Codex Skill 也会先备份再替换，不会产生
`yuzuha-toolkit\skill\SKILL.md` 这种错误嵌套。

## 安装后验证

1. 重启 E3D。
2. 在 E3D 中确认 `!!YuzuhaRpcHost.GetRpcServerStatus()` 返回 `RUNNING`。
3. 在 Agent 中调用无副作用的 `get_connection_status`，确认状态为 `Connected`。
4. 先执行只读查询；不要用创建 BOX 或盘管来测试连接。
5. 新会话中确认 Agent 能看到 `yuzuha-toolkit` Skill，再让它读取连接状态。

详细 Agent 规则见安装目录中的 `docs\Agent.PmlTrigger.zh-CN.md`；英文版为
`docs\Agent.PmlTrigger.en.md`。

## 发布前自检

```powershell
.\Test-YuzuhaRelease.ps1
```

校验值见 `SHA256SUMS.txt`。PDB 未包含在交付包中。
