# PmlTrigger：AI 客户端接入说明 / AI client setup

PmlTrigger 用于在 AVEVA PDMS、AM、E3D 中查询元素、读取属性和执行 PML。两个 EXE 是通过标准输入/输出通信的 **MCP stdio 服务端**，不是需要手动双击的桌面程序。AI 客户端启动它们并通过 MCP 获取工具清单。

## 安装后必须重启

**安装、升级或修改 MCP 配置后，必须完全退出并重新启动使用它的 AI 客户端，再检查工具是否加载。仅新建聊天不等于重启。**

本机默认安装不需要修改 EVAR；不要为了注册 MCP 自动修改 EVAR。AVEVA Host 的环境配置是独立步骤。只有实际更改 Host 或 AVEVA 环境配置时，才需要完全重启 AVEVA。

## 两个服务的职责

| 服务 | 程序 | 用途 |
|---|---|---|
| YuzuhaToolkit | runtime/net10/YuzuhaToolkit.Mcp.exe | 发现及连接 AVEVA 会话、生成和执行 PML、返回查询结果 |
| YuzuhaToolkitKnowledge | runtime/net10/YuzuhaToolkit.Knowledge.exe | 检索本地项目、官方资料和经验知识库 |

读取当前元素时，发现会话并连接目标后，查阅 `YuzuhaReadCurrentElement` 的签名及参数，再通过执行侧现有工具读取结果。`YuzuhaReadCurrentElement` 是 PML 函数，不是独立 MCP 工具。不要把知识库来源注册与连接 AVEVA 会话混为一谈。

## 注册到 AI 客户端

现有安装脚本自动注册的是 Codex。其他客户端不会自动继承 Codex 配置，也不会扫描 EXE 或自动读取 Codex Skill。请在目标客户端的 MCP 设置中添加两个本地 stdio 服务，使用各程序的绝对路径，参数为空。客户端必须支持启动本地 stdio 进程。

支持 `mcpServers` JSON 的客户端可按下面结构合并配置；不要覆盖原有其他服务。将示例路径替换为实际安装目录。某些客户端使用不同配置结构，应使用其 MCP 设置页面。

```json
{
  "mcpServers": {
    "YuzuhaToolkit": {
      "command": "C:\\Users\\YOUR_USER\\AppData\\Local\\YuzuhaToolkit\\PmlTrigger.Yuzuha\\runtime\\net10\\YuzuhaToolkit.Mcp.exe",
      "args": []
    },
    "YuzuhaToolkitKnowledge": {
      "command": "C:\\Users\\YOUR_USER\\AppData\\Local\\YuzuhaToolkit\\PmlTrigger.Yuzuha\\runtime\\net10\\YuzuhaToolkit.Knowledge.exe",
      "args": []
    }
  }
}
```

## 重启后的验收

1. 在客户端工具列表确认两个服务均已加载；配置存在不等于握手成功。
2. 调用 `list_aveva_sessions`。零会话表示没有发现可见目标窗口，不等于 MCP 安装失败。
3. 选择返回的目标会话，确认 `TargetVerified=true`。未选择会话时出现 `E3D_TARGET_NOT_SELECTED` 是预期状态。
4. 用知识库搜索 `YuzuhaReadCurrentElement`，再测试“查询当前元素”。精确名称也找不到时检查项目索引；仅自然语言找不到时检查检索映射。

虚拟机使用时，本地 stdio MCP 和 AVEVA 应运行在同一台 Windows 环境。宿主机上的 MCP 不会通过现有本地会话发现和命名管道自动连接虚拟机里的 AVEVA。

排障时记录：AI 客户端名称、两个 EXE 的绝对路径、服务启动错误、实际调用的工具及返回值。不要仅用“AI 不知道这个工具”判断安装结果。

## English quick setup

These executables are local MCP **stdio servers** launched by the AI client. Register both absolute executable paths with empty arguments in each supported client; Codex registration and Skills are not shared with other clients. Merge entries without replacing unrelated servers. Default local MCP setup does not require EVAR changes.

**After installation, update, or MCP configuration changes, fully exit and restart the AI client. Opening another chat is not a restart.** Verify tools load, then call `list_aveva_sessions`. Keep the MCP process and AVEVA in the same Windows environment, including inside a VM. Restart AVEVA when its Host or environment configuration changes.
## AVEVA 环境定位规则 / Environment setup

通过注册表 `Get-ItemProperty` 或 `reg query` 定位安装，优先修改有效的 `Evar.INIT`；PDMS/AM 确认没有 INIT、只有 BAT 时才修改 `EVAR.BAT`。仅本机 MCP 注册不修改 EVAR。`-EvarBat` 接受 BAT 风格文件：`Evar.INIT` 本身即批处理语法，可直接传入，写入前自动备份，托管块尾置。

[完整步骤 / Full procedure](aveva-discovery.md)
