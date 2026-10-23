# PhaytonObjectWalker

`PMLLIB/Traversal/PhaytonObjectWalker.pmlobj` 是独立的新版本，已加入 `PMLLIB/pml.index`。现有 `YuzuhaReadCurrentElement`、`YuzuhaReadDbref`、`YuzuhaReadGlobal` 继续调用 `YuzuhaObjectWalker`，本次没有切换入口。

## 取值、格式化和遍历

`RegisterNode` 登记路径、父节点及深度；`ReadNode` 取得实际值、类型、来源及错误，并把值放入单项包装数组。`FormatResult` 只消费缓存，`CollectChildren` 从缓存登记下一层，`Traverse` 按广度优先顺序处理节点队列。

DBREF 属性普通读取失败时，`MatchLargeArr` 保留以下方式，直接返回提取数组：

```pml
!ob = !parentBox[1]
!eell = object array()
var !eell split $!lastfixArr of $!ob
return !eell
```

后续下标从该数组取值，不再求值失败的属性表达式。`NodeSources` 区分 `DIRECT` 和 `VAR SPLIT`；两种读取都失败时保留错误。提取项维持实际类型，本次 ELDATA 样本为 STRING，不自动转换为 REAL。

数组只格式化为 `<ARRAY> N Elements` 摘要，叶节点沿用 VAR 格式化。结果行为 `[depth] path<TYPE> value`；失败行带 `failedToGetValue`。错误判断只检查行首，数据中的同名字面文本仍是成功值。

## 入口与预算

`InitializeFromArray` 接受单项根包装数组，`InitArray[1]` 才是根值。也可以使用 `InitializeFromCurrentElement` 或 `InitializeFromGlobal`。初始化和每次 `Read` 都清空节点缓存；同一次读取复用已读值。

| 配置 | 行为 |
| --- | --- |
| `MaxArraySize` | 默认 500，向下取整，最少 1；限制全部返回行，包含根和起点摘要，也限制待输出队列 |
| `MaxBFSDepth` | 默认 3，向下取整，最少 0；从根深度 0 计算，达到上限后不展开 |
| `startStr` | 点分成员路径，支持数字、竖线或方括号下标；显式目标始终尝试读取，单行预算优先输出目标 |

行数预算限制输出和子节点登记，**不限制 VAR SPLIT 对单个属性的完整提取量**。这不是源数组分页或冻结快照协议。

示例应在 CE 已指向具有 ELDATA 的测试元素时使用：

```pml
!walker = object PhaytonObjectWalker()
!walker.InitializeFromCurrentElement()
!walker.startStr = 'ELDATA'
!walker.MaxArraySize = 12
!walker.MaxBFSDepth = 2
!rows = !walker.Read()
q var !rows
```

## XML 注释与验证

源码使用 `--` 注释前缀，内部采用 `summary`、`param`、`returns`、`value`、`remarks/para`、`example/code` XML 标签。构建通过 `scripts/Export-PmlXmlDocumentation.ps1` 解析并验证参数、返回声明及 XML，再插入 DocFX 的 [PML 源码页面](../../artifacts/docfx/navigation/pml/PMLLIB/Traversal/PhaytonObjectWalker.pmlobj.md)。PML 不走 C# 元数据扫描，不依赖 VSIX 才能展示注释。

36 个声明均有注释：1 个对象、17 个成员、18 个方法。[实测记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-phayton-object-walker.md)记录了 2410 项提取、2412 行格式化、逐项核对及定向下标读取。[内存数组回归宏](../../artifacts/docfx/navigation/repository/tests/live/phayton-object-walker/README.md)可独立复刻。
