# PmlTrigger.Yuzuha 本机开发目录

中文 | [English](local-development.en.md)

## 唯一项目目录

`D:\PmlTrigger.Yuzuha` 是源码、Git、文档、Skill 和发布工作的唯一主目录：

```text
D:\PmlTrigger.Yuzuha\
├─ PMLUI\                         PML UI 源码
├─ PMLLIB\                        PML 源码
├─ src\                           .NET 源码
├─ skill\                         Skill 源码
├─ docs\                          项目文档
├─ artifacts\                     可删除的构建输出
├─ runtime\                       验证后提升的本机运行文件
└─ release\                       面向用户的安装包
```

不要把 `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha` 当作开发主目录；它是
用户安装/验收副本，覆盖安装时可以被替换。

## PMLUI 与 PMLLIB

当前 E3D 的 `evars.init` 指向 C 盘安装目录。开发阶段有两种方式：

1. **复制模式（默认推荐）：**从 D 盘源码复制到 C 盘安装副本。适合保持“用户实际
   安装效果”，也最容易发现打包遗漏。
2. **目录联接模式（快速迭代）：**让 C 盘的 `PMLUI`、`PMLLIB` Junction 指向 D 盘
   同名源码。只用于本机开发；运行 Release 安装器前必须明确解除或保留备份。

VS Code 应直接打开 `D:\PmlTrigger.Yuzuha`。不论采用哪种 E3D 部署方式，都不要在
C 盘安装副本里形成第二套长期维护的源码。

## MCP 与 runtime

MCP EXE 的位置与 E3D 加载 PML 的位置相互独立。开发机可以把 MCP 客户端注册到：

```text
D:\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe
```

这才叫“直接使用 D 盘开发版 MCP”。仅安装 Skill 不会注册 MCP；先用客户端的 MCP
列表确认实际命令路径。

不要让 `.csproj` 一次发布到 C、D 两个位置。推荐：

```text
src -> artifacts -> runtime -> C 盘本机部署 -> release
```

构建首先输出到 `artifacts`；验证后复制到 D 盘 `runtime`；最后通过单独的本机部署
步骤复制到 C 盘。覆盖 Net48 Host 前关闭 E3D，覆盖 MCP EXE 前停止对应 MCP 进程。

## 安装器与日常开发

- `Install-Yuzuha.ps1`：面向新机器、Release 验收和用户安装，负责备份、`evars.init`、
  MCP/Skill 可选注册等完整流程。
- 日常开发同步：只复制明确需要的 PMLUI、PMLLIB 或 runtime，不重复运行完整安装器。
- 发布前：必须再用完整安装器验证一次，避免开发同步掩盖安装包缺失。

Skill 的源码目录为 `D:\PmlTrigger.Yuzuha\skill`。正确安装结构只能有一层：

```text
%USERPROFILE%\.codex\skills\yuzuha-toolkit\
├─ SKILL.md
└─ references\
```

不得出现 `yuzuha-toolkit\skill\SKILL.md`，也不要把 runtime DLL/EXE 放进 Skill。
