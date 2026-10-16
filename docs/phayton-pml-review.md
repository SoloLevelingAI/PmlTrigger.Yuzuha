# PML 只读审查：VAR、数组与分页边界

审查基线：`04617bf572a637d63509762041108a011ecdc9f3`。
本阶段没有修改 `PMLLIB/`、`PMLUI/`，也没有向 AVEVA 发送任何 PML。
本机知识库搜索没有返回相关条目；以下结论来自本仓库当前源码。

## 源码已证实

- `PMLLIB/Traversal/YuzuhaObjectWalker.pmlobj` 的 `ResolveAttribute` 已使用
  `var !objStr var !AttArr` 获取显示文本。无需为了“支持 VAR”重写它。
- `Read`/`Traverse` 使用 `MaxArraySize`、`MaxBFSDepth` 控制遍历。预算限制的是
  BFS 遍历循环，结果还包含初始化行；不能把结果条数当作整个对象图的条数。
- `startStr.split('.')` 用于拆分属性路径，不是数组页码、offset 或续读游标。
  当前接口没有可验证的分页协议，重复同一预算调用不能证明拿全。
- `YuzuhaReadGlobal`、`YuzuhaReadCurrentElement`、`YuzuhaReadDbref` 均复用 walker；
  C# 不能恢复已被 walker 预算省略的数据。
- NET35/NET48 的 `GetRequireFromE3D.GetPmlBfsList` 先执行表达式并获取全局数组的
  `.SIZE()`，再逐项执行 `VAR !!YUZUHAPMLRPCITEM VAR !!<global>[index]`。
  它读取的是本次返回数组，而不是保证完整遍历所有源对象。数组越大，主线程上
  逐项调用的成本越高。当前实现使用固定临时全局名，后续优化应单独评估并发与清理。
- `pml.array.read` 保留原始未解析行和汇总，不声称 `Summary.total` 是源对象总量，
  也没有添加自动重试或自动循环执行 PML 的“拿全”逻辑。

## 可验证的下一步，需要单独授权才实施 PML 改动

用户提出的 `SPLIT 1 TO 1000` 是待验证方向。本次未取得对应 AVEVA 版本的官方语法
依据，也未在现场验证，不把它作为已确定可用的分页语法写入实现。

建议先在独立测试会话核对目标版本的 VAR/SPLIT 文档，再验证输出到字符串或数组的
具体形态；测试样本至少覆盖空数组、1、999、1000、1001、2500 项、稀疏索引、Unset、
嵌套数组、带引号/换行/中文的字符串。每页记录起止索引、总量、页长与未解析行，
拼接后核对无重复、无遗漏、顺序和类型保持一致。

分页还须区分“对已返回的字符串做切分”和“按源数组索引读页”。前者不能解除 walker
的遍历截断。续读过程中需固定源数组快照或检测变化，明确预算耗尽/真正结束，支持
显式取消及超时诊断。任何性能改动都应保留 Host 主线程、PID 身份检查、执行授权与
不重试策略。NET35/NET48、实际 AM/PDMS/E3D 版本应分别验证。

这些验证和后续 PML 修改尚未执行；不得据此宣称已经实现完整分页读取。
