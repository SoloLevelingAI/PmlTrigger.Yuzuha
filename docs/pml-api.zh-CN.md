# Yuzuha Toolkit PML API

当前版本面向 AM/PDMS NET35、E3D 2.1/3.1 NET48 和可信本机用户。

## 读取入口

```pml
!rows = !!YuzuhaReadCurrentElement(200, 3, '')
!rows = !!YuzuhaReadDbref(200, 3, '', '/SAMPLE-ZONE/SAMPLE-EQUIPMENT')
!rows = !!YuzuhaReadGlobal(200, 3, '', 'YUZUHA_SAMPLE')
```

`YuzuhaObjectWalker` 使用数量与深度预算遍历 CE、DBREF、PML Object 或 Array。
属性路径通过 RPC 时使用点号，例如 `Hposition.EAST` 或 `member.1`。

## 文件执行

```pml
!!YuzuhaExecuter.ExecuteFile('C:\path\to\macro.pmlmac')
!result = !!YuzuhaExecuter.Query()
```

文件执行可能修改当前模型，只能在用户明确要求时调用。执行超时后禁止自动重试。

## Addin 与运行时

四个模块注册文件位于 `PMLUI/cat|des|DRA|iso/Addins/YuzuhaAddin`。
Bootstrap 根据 EVAR 变量 `Yuzuha`（不带下划线）从下列位置加载宿主：

```text
runtime/profiles/<Profile>/<net35|net48>/YuzuhaToolkit.PmlHost.<Net35|Net48>.dll
```

当前模块由下列 PML 表达式取得并传给 Host：

```pml
!!YuzuhaModel = !!fmsys.FMINFO()[0].SPLIT()[3]
```

默认管道为 `yuzuha.pml.command.v1.pid-<AVEVA PID>`。Net10 MCP 启动时不连接：
`list_aveva_sessions` 只读发现可见 AVEVA 窗口的标题、PID、启动时间、产品和
项目；显式调用 `select_aveva_session` 后才连接所选 PID 的专属管道，并锁定 Host
报告的 `Design`、`Draft` 等模块。每次执行前都会重新核对身份。

项目根目录的 `evar.example.txt` 仅包含占位符，真实本机路径不得提交。
