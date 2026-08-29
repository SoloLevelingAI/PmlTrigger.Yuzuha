# Agent 使用 PmlTrigger.Yuzuha 前应了解的事项

中文 | [English](Agent.PmlTrigger.en.md)

## 1. 版本、组件与归属

本 Release 使用以下结构：

- `runtime/win-x64-nativeaot/YuzuhaToolkit.Mcp.exe`：.NET 10 Native AOT、stdio MCP。每个 Agent/MCP 客户端应启动自己的进程，不要求安装 .NET 10。
- `runtime/net48`：加载到 AVEVA E3D 进程中的 .NET Framework 4.8 PMLNet/RPC Host。
- `PMLUI`、`PMLLIB`：由同一个 E3D 环境共享，不要为每个 Agent 重复注册多份。
- MCP 与 Net48 Host 默认通过本机命名管道 `yuzuha.pml.command.v1` 通信。

`SKILL.md` 是 Agent 行为说明，普通 Markdown 是项目文档，两者不能替代 MCP 服务。Named Pipe 仅适合本机可信用户，不是权限隔离边界。

## 2. Agent 的标准工作流

1. 首先调用 `get_connection_status`。只有状态为 `Connected` 才执行需要 E3D Host 的工具。
2. 仅生成或讨论命令时使用 `generate_pml_call`，它不会触发 E3D。
3. 读取数组/对象图时使用 `run_pml_command_list`。
4. 用户明确要求执行后，才能调用 `run_pml_command` 或 `run_pml_command_list`。
5. 修改前读取并记录原状态；修改后重新读取目标验证结果。
6. 执行型调用发生超时后禁止自动重试，因为第一次调用可能已经改变 E3D 状态。
7. 始终保留并报告 `Success`、`Code`、`ErrorMessage`、`RequestId` 和实际执行的 `PmlCommand`。
8. 不得通过 `DESIGN` 或切换模块来尝试修复命令；模块切换可能重启项目并断开心跳。

## 3. 从 E3D 读取数据

`size` 是最大结果/遍历预算，不保证恰好返回相同数量的属性。`depth` 是最大 BFS 展开深度。先用较小的 `size` 和 `depth=2`，不足时再定向增加。`startStr` 使用点号分隔，例如 `member.1`、`hpos.origin`；不要使用交互命令行专用的 `@` 分隔方式。

### 3.1 当前元素

```pml
!!YuzuhaReadCurrentElement(!size is real, !depth is real, !startStr is string) is array
```

```pml
!!YuzuhaReadCurrentElement(20,2,'')
!!YuzuhaReadCurrentElement(20,3,'member')
!!YuzuhaReadCurrentElement(30,3,'hpos.origin')
```

并非所有元素都有 `HPOS` 或对应子属性。空结果不一定代表连接失败，应同时检查 RPC 的 `Success` 和 `ErrorMessage`。

### 3.2 指定名称、路径或 DBREF

```pml
!!YuzuhaReadDbref(!size is real, !depth is real, !startStr is string, !name is string) is array
```

`name` 可以是短名称、以 `/` 开头的完整名称，或以 `=` 开头的 DBREF：

```pml
!!YuzuhaReadDbref(20,3,'','100-FW-202')
!!YuzuhaReadDbref(20,3,'','/100-FW-202')
!!YuzuhaReadDbref(20,3,'member','=2013286668/56')
```

该函数会临时切换 CE 并尝试恢复原 CE。调用后仍应验证 CE，尤其是在异常返回时。

### 3.3 指定全局变量

```pml
!!YuzuhaReadGlobal(!size is real, !depth is real, !startStr is string, !globalVar is string) is array
```

`globalVar` 不带 `!!`，并且当前实现会动态解析变量名，只能使用受信任输入：

```pml
!!YuzuhaReadGlobal(20,3,'','YuzuhaRuntimePath')
```

对于 PMLOBJ、数组和其他全局对象，可以通过定向 `startStr` 减少无关遍历。

## 4. 执行命令

### 4.1 调用已有 Function

复杂任务优先封装成有明确参数和返回值的 PML Function。例如创建 BOX：

```pml
!!YuzuhaCreateBoxExample(!localAtStr is string, !name is string,
                         !xlen is real, !ylen is real, !zlen is real) is array

!!YuzuhaCreateBoxExample('ABC111','TestName2',50,50,50)
```

该示例会修改模型，只能在用户明确要求且确认目标 EQUI 后执行。

### 4.2 单行命令

本项目通过 `PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd` 提供
`YuzuhaTriggerCommand`。旧拼写不属于当前 API；如果对象不可用，应检查
`evars.init` 的 PMLLIB 搜索路径并完全重启 E3D。

```pml
!!YuzuhaTriggerCommand.InitArgs('/SITE-PIPING-AREA03')
!!YuzuhaTriggerCommand.execute('ExecuteCommand')
!!YuzuhaTriggerCommand.Query()
```

以上三行是三次独立调用，不能用逗号或分号合并。`$P /SITE/...` 只打印文本，不是
可靠的 CE 切换命令；定位后必须用读取函数重新确认 CE。

重命名示例：

```pml
!!YuzuhaTriggerCommand.InitArgs('name /AgentRenameNow')
!!YuzuhaTriggerCommand.execute('ExecuteCommand')
!!YuzuhaTriggerCommand.Query()
```

对象中的 DELETE/KILL 字符串检查只是演示性提示，不是安全沙箱，也不能替代用户授权和执行后验证。

### 4.3 多行文件

只有用户明确要求执行已有脚本时，才允许使用 `ExecuteFile`。文件必须位于用户确认的本机受控目录，并使用完整路径：

```pml
!!YuzuhaTriggerCommand.InitArgs('C:\Approved\demo.pmlmac')
!!YuzuhaTriggerCommand.execute('ExecuteFile')
!!YuzuhaTriggerCommand.Query()
```

Agent 不得自行把未知内容写入文件后立即执行，也不得在失败或超时后自动重试。

## 5. 演示函数：盘管

盘管是需要教给 Agent 的演示能力。它属于站点/私有扩展，不保证出现在纯开源环境中；演示前应确认函数已随目标站点 PMLLIB 加载。函数不存在时应报告不可用，禁止换用其他建模命令自行猜测或自动重试。

### 5.1 创建盘管

```pml
!!NewSpiral(!Owner is string, !startHei is real, !TotalHei is real,
            !pipeDiam is real, !pipePerTimes is real,
            !equiOutDiam is real) is array
```

- `Owner`：盘管所属设备名称或完整路径。
- `startHei`：起始高度。
- `TotalHei`：总高度。
- `pipeDiam`：管径。
- `pipePerTimes`：每圈上升量。
- `equiOutDiam`：设备外径/盘管展开参考直径。

```pml
!!NewSpiral('/AGENT1',-1500,1680,108,140,2824)
```

该调用会创建模型对象。执行前必须让用户确认 Owner 和所有尺寸，执行后读取返回数组及创建对象属性。
Owner 和路径必须保留最近一次读取结果的大小写；不要自行把 `/AGENT1` 改成 `/Agent1`。

### 5.2 旋转盘管

```pml
!!RotateMyspiral(!equi is string) is array
```

```pml
!!RotateMyspiral('$!!ce.name')
```

执行前必须确认当前 CE 确实是目标盘管或设备。更稳妥的方式是传入已验证的完整名称，而不是依赖会变化的 CE。

## 6. 成功与故障判定

- `get_connection_status = Connected` 只表示近期心跳连通，不表示某条 PML 一定成功。
- RPC `Success=false` 是业务失败；`PML RPC failed:` 是传输失败，两者都不能当作成功。
- 空数组可能是合法空结果，也可能是目标不存在；结合 `Success`、`Code` 和错误信息判断。
- 执行后必须重新查询目标状态。不要只根据命令文本、心跳或本地日志宣称操作成功。
- `run_pml_command_list` 的 `pmlCommand` 只能是返回 ARRAY 的表达式，不能传全局变量
  赋值语句；读取入口是 `YuzuhaReadCurrentElement`、`YuzuhaReadDbref` 和
  `YuzuhaReadGlobal`。
