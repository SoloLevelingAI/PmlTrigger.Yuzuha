# PhaytonObjectWalker 回归

## manual:phayton-object-walker

`smoke.pmlmac` 是已在 2026-10-23 E3D 2.1 会话执行通过的内存数组回归宏。默认 `testLargeAttribute=false`，覆盖稀疏下标、嵌套、空数组、行数/深度预算、目标优先、格式显示深度、纯表达式、缓存重新初始化、错误节点和错误标记字面数据。成功时输出 PASS，失败时输出 `!failures`。

先确认 `PMLLIB/Traversal/PhaytonObjectWalker.pmlobj` 位于目标会话可见的自定义 PMLLIB，再在原生命令入口执行 `pml reload object PhaytonObjectWalker`，通过现有宏执行器运行本宏。读取 PASS/FAIL 及原生错误，不以复制或派发成功替代实际结果。

默认宏只使用局部变量，不选择元素，不修改模型。可选 ELDATA 分支须先在自有测试模型选好 CE，再显式启用；该分支本次未执行。完整 2410 项实测使用独立基线与逐项断言，详见[实测记录](../../../docs/validation/2026-10-23-phayton-object-walker.md)。公开夹具不包含用户工程或固定 DBREF。
