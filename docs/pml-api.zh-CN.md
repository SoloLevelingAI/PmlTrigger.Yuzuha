# Yuzuha Toolkit PML API

当前版本面向 AVEVA E3D 2.1、.NET Framework 4.8 和可信本机用户。

## 读取入口

```pml
!rows = !!YuzuhaReadCurrentElement(200, 3, '')
!rows = !!YuzuhaReadDbref(200, 3, '', '/SAMPLE-ZONE/SAMPLE-EQUIPMENT')
!rows = !!YuzuhaReadGlobal(200, 3, '', 'YUZUHA_SAMPLE')
```

`YuzuhaObjectWalker` 使用数量与深度预算遍历 CE、DBREF、PML Object 或 Array。
属性路径通过 RPC 时使用点号，例如 `Hposition.EAST` 或 `member.1`。

`run_pml_command_list` 的 `pmlCommand` 直接传上面的返回 ARRAY 表达式。不要传
`!!TEMP = ...` 或 `!!TEMP[1] = ...`，Host 会自行完成临时变量赋值和 `SIZE()` 校验。

## 文件执行

```pml
!!YuzuhaTriggerCommand.InitArgs('C:\path\to\macro.pmlmac')
!!YuzuhaTriggerCommand.execute('ExecuteFile')
!result = !!YuzuhaTriggerCommand.Query()
```

三行必须分开执行。该对象由
`PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd` 提供。

文件执行可能修改当前模型，只能在用户明确要求时调用。执行超时后禁止自动重试。

## Addin 与运行时

四个模块注册文件位于 `PMLUI/cat|des|DRA|iso/Addins/YuzuhaAddin`。
Bootstrap 从发布包的下列位置加载 Net48 宿主：

```text
runtime/net48/YuzuhaToolkit.PmlHost.Net48.dll
```

项目根目录的 `evar.example.txt` 仅包含占位符，真实本机路径不得提交。
