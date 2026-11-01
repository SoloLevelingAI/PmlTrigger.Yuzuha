# 只读规范附录组件

contract-reference消费 `{data:IR,disclosure,methodCatalog?}`；事实仅来自IR.referenceIndex.sections及coverage。每节保留id/title/columns/rows/sourceRefs，行数组或对象逐项呈现，嵌套值完整pretty JSON作为纯文本；未知对象字段和额外元组索引增加显式列，缺值标未提供，不丢弃字段或升级原状态。

附录入口始终说明目录数量与coverage的范围（不是测试覆盖率），搜索/分类显示匹配数、总数及隐藏数并可清空恢复全部。完整行始终保留，表格在内部横向滚动，窄屏不撑开页面。规范和191来源映射均不是运行实现或实机证据。

每节details稳定key为 `reference|<sectionId>`，data-disclosure-key与data-state-key同值；open只取宿主disclosure.isOpen(key)，无controller安全默认关闭，不根据help/portfolio自行打开。用户toggle发出 `atlas-disclosure`，detail={key,open}、bubbles/composed均true。初始化及宿主模型刷新不伪造用户toggle；按钮、搜索及表格不执行源内容，也不使用innerHTML/iframe。

filters与所有表格位于外层原生details（reference|index），标题、范围说明和coverage摘要始终可见。summary关闭外层，full由控制器打开外层及子层；移除后的旧details排队toggle不得跨新模型/namespace发事件。

方法绑定状态只展示传入目录的原状态与原因，不把unavailable变成not-applicable或resolved；该组件不加载源码、不选择/执行方法、不写外部系统。
