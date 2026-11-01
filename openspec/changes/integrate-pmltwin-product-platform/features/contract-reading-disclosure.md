# 完整契约与作品集阅读 · 2026-11-01

## 总体契约

保留现有事实和三轴状态，把长页面组织为“摘要卡片 → 就地详情 → 完整规范/实现索引”。阅读视图help/portfolio、颜色主题、披露程度为三个独立维度。不能只减少字号、裁掉全文或隐藏实体来压缩页面。先细化本契约，再实现及验收。

当前数据已有18个Feature、14个Script、24条原需求及191条来源限定映射；此前作品集只列前6卡、总体详情只讲当前流程，构成可达性缺口。本次补齐已有事实的阅读入口，不把未实现功能或静态调用变成已验证能力。

## Feature与内容边界

- 总体目录提供全部Feature与Script的明确分类/数量及搜索入口；匹配项不再无说明地slice前6。可按类别筛选，但当前范围和隐藏数量必须明确，搜索能够找到并打开Script。总体详情保留自身sections，不以当前流程的部分Feature替代全部项目内容。
- 每张Feature/Script卡保留紧凑的标题、真实状态、职责摘要、内容数量和边界提示，提供卡内原生折叠详情及独立“进入此契约”导航。不得在导航button内部嵌套summary/button。
- 详情保留全部Feature字段或Script七字段、原需求原状态、实现scope、来源/摘要、证据环境/日期/exclusions。两阅读视图均能到达完整信息；避免在同一选中详情把五项摘要与完整七字段重复铺开。
- 关键限制、未知/过期/未实机状态、证据适用环境和exclusions继续显示，不以CSS行截断裁掉这些边界。较长的算法、逐条规范、源指纹、调用点列表及正文进入明确折叠框，摘要说明数量/范围，完整原文保留。
- 新增只读规范附录，展示现有canonical的G/B/T/S、screens/transitions/models与191条源限定映射，标明规范和候选映射不代表运行实现；字段/元组无损，不能只挑容易展示的列。
- 方法映射对每个Feature/Script明确为精确已绑定、未纳入分析范围或非C#不适用。只补真实图中存在的方法；Ribbon导入后端/Web pending不在当前编译图时说明范围，不能猜同名方法补齐。

## 全局与局部披露状态

中心状态为`{mode:'summary'|'full',scope,overrides}`。默认summary；URL使用`detail=summary|full`保存全局模式，非法值回summary。局部折叠按稳定key保留于本次会话，不因导航、搜索、主题或阅读视图变化丢失。

全局“全部展开”“全部折叠”是两个明确动作：修改mode并清除各scope的局部披露覆盖，当前内容和后续导航/实时修订均遵守；不修改草稿内容。局部操作形成“部分展开/自定义”状态，不假装全部折叠或全部展开。不能只在当前document.querySelectorAll('details')上改open，必须覆盖嵌套Shadow组件。

覆盖范围为内容区卡片、选中契约、来源、规范、流程说明、证据、已选方法的XML/位置/源码/调用列表及规范附录。不打开编辑器对话框、不执行捕获/部署、不自动选择或遍历全部方法。没有正文或不支持的事实仍显示原因。

状态key为`ownerId|section`；ownerId是稳定实体/流程/验证/方法ID，附录使用`reference`。卡内与选中详情使用不同section避免同名位置互相覆盖。live/static共用稳定实体状态；import/draft使用独立scope，不能串用旧事实的局部覆盖。新修订保留仍存在owner的状态，实体移除时清除失效覆盖。

组件model传入宿主代码提供的`disclosure:{isOpen(key)}`。原生details使用`data-disclosure-key`和同值`data-state-key`；用户toggle后发出`atlas-disclosure`事件，`detail:{key,open}`，bubbles/composed为true。展示数据本身不携带回调或执行内容。standalone无controller时提供安全摘要默认。

全局操作保留触发按钮焦点；关闭包含焦点的详情时转到对应summary。全部展开/折叠不清除实体、父Feature、方法、方向、筛选、显式流程、主题、草稿或读取来源。新revision保持滚动和可恢复焦点，不用旧DOM.open覆盖新的中心状态。

## Script及数据细化

共享IR保留原schema，增加只读`referenceIndex`与`coverage`投影，来源仍为已校验public metadata。referenceIndex提供完整sections的`{id,title,columns,rows,sourceRefs}`；rows与原元组/对象一一对应，未知额外字段也保留为明确列。191条映射展示source限定身份/owner/原status，不升级成已注册operation。

`coverage`记录Feature/Script/原需求/流程及映射数量，只代表目录覆盖，不叫测试覆盖率。方法绑定结果来自internal catalog，缺快照与不适用保持分别说明；公开IR不装入C#正文。native/Figma继续消费原事实投影，Web折叠是阅读交互，不宣称纸面画板自动获得交互。

新增`disclosure.js`无框架中心控制器，负责mode/namespace/局部覆盖/失效owner清理；app负责路由、工具栏和保持阅读状态；components/method-panel/reference组件负责语义化summary与完整详情。方法绑定配置允许空symbolIds显式标`resolution:'unavailable'|'not-applicable'`并附原因，不能把未纳入C#图的功能谎报为非C#。

## 验收与汇总

- 全目录18Feature/14Script可发现；Script搜索不只在侧栏有结果，卡片和导航范围一致。
- 全部展开后核对每张卡原始字段、14组七字段、24条要求、完整规范表及191映射；全部折叠仍有摘要和关键范围提示，压缩效果有实际前后高度/截图证据。
- 原生键盘Enter/Space、44px摘要点击区域、390/768/1440无页级横向溢出；全局动作在嵌套Shadow内真实打开/关闭，并作用于随后导航。
- 局部混合状态、hash刷新、视图/主题/筛选/流程/方法、实时更新、删除实体、独立导入/草稿及焦点恢复分别验证；不能只计数details标签。
- 真实服务接入后检查方法目录仍ready、精确DocFX链接可用、规范与验证事实未变；保留lastgood与源字节/摘要门禁。

交付仍按总体→Feature→Script→总结说明：作品集先看任务与交付，再按需下钻；完整性通过展开可达与原数据对照证明，不靠增加已实现标记证明。
