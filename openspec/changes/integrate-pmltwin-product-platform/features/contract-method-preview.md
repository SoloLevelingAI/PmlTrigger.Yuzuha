# 契约到方法预览 · 2026-11-01

## 总体契约

沿用现有DocFX与`tools/Documentation/UiTrace.cs`的MSBuild编译输入/Roslyn语义分析，让Feature/Script契约可追到明确方法、签名、XML说明、源码快照和直接静态调用关系。生效规范、七字段和验证状态不变。方法预览不是代码执行、运行调用栈或覆盖率证明。

本切片先细化契约再实施。源码属于本机维护视图：独立internal方法目录与公开metadata/IR分开，但作为同一共同修订的受校验产物发布，禁止混载不同分析快照。FigCraft与公开SVG继续使用已有公开事实模型，本轮不自动向作品集导出原始C#或机器路径。

## Feature责任与范围

1. Roslyn生产者复用实际Compile/ReferencePath/DefineConstants输入，保留Assembly::DocumentationCommentId原身份及静态边种类。补方法签名、纯文本XML字段、完整partial声明位置、async标志和正文行范围。lambda使用源码位置身份，不能承诺编辑后稳定；生成/外部/SDK声明不公开正文。
2. 显式绑定配置`docs/contracts/method-bindings.json`将现有entityId映射到精确symbolId。不得按标题、短方法名或文件名猜测同名替代；重载、泛型和跨程序集身份分别保留。非C#或未纳入编译范围明确not-applicable/unavailable，不冒称已分析。
3. 方法目录提取和校验负责有界源码预览、真实xref解析与快照过期诊断。首次没有分析产物显示unavailable，不能启动Atlas时隐式运行全量Build-Documentation。提供明确捕获命令；捕获后watch自动发布。源码变化先标stale，重新捕获后恢复ready。
4. Web契约详情提供独立方法子面板，选择方法不改变Feature/Script、阅读/主题、筛选或任务流程。可查看直接callers/callees及边界，并跟随已知方法。方法选择和方向写入hash；返回、重连及新修订保留仍存在的选择，删除时清除并解释。
5. DocFX API站点由独立构建产生，首期实际覆盖Manager.Core/Workspace两项目；不能删除既有全量文档目录、启动DevHost或触发业务行为。精确成员链接从真实DocFX xrefmap/metadata解析，缺失、内部/private未发布或歧义则url=null并说明。不拼猜测的重载锚点。

## Script及数据契约

`method-bindings.json`仅含schemaVersion、analysis固定仓内输入路径与bindings。bindings为`{entityId,symbolIds,reason?}`；symbolIds是完整Assembly::DocID数组，空数组必须有不适用说明。原始分析graph、metadata和DocFX快照均在artifacts中；人工配置及路径需通过仓内realpath约束，不提供任意文件读取API。

共同方法目录`contract-method-catalog/1`的冻结形状：

```text
{schema, schemaVersion:1, audience:'internal', metadataDigest, renderModelDigest,
 status:'ready'|'unavailable'|'stale',
 analysis:{id,kind:'static-source-trace',commit,dirty,inputDigest,scope,omittedProjects,reason},
 methods:[{id,symbolId,assembly,docId,title,signature,summary,
   documentation:{summary,parameters:[{name,text}],returns,remarks},
   source:{path,startLine,endLine,sha256,body,generated,truncated}|null,
   declarations:[{path,startLine,endLine,sha256}],isAsync,isPartial,
   docfx:{uid,url,reason},boundaries:[{kind,expression,reason}]}],
 edges:[{id,from,to,kind,file,line,offset,expression,context}],
 bindings:[{entityId,methodIds,resolution:'resolved'|'unavailable'|'not-applicable',reason}],
 diagnostics:[string]}
```

id为`method:<sha256(symbolId)>`，不以展示标题生成身份。source.path仅允许明确仓内`src/`手写C#；正文为最多64KiB的有界预览，超限必须标truncated并保留原完整范围及文件摘要。XML与源码按纯文本渲染，不使用innerHTML、iframe、eval或执行按钮。catalog pretty UTF8最多8MiB、500方法、1500条有调用位置的边；边不因同名或同一目标合并。未知目标/无正文/未分析范围要有说明。

完整Roslyn分析输入是独立有界层：graph最多96MiB，完整编译输入receipt最多1024个仓内文件，手写正文来源最多512个。实际全图包含生成源码与XAML，不把这些混入手写预览，也不为了适应更小预算截断分析事实。方法目录只取显式绑定根的直接入/出边及关联方法；跟随邻居时未纳入的关系以scope边界说明，不能表示该方法无调用。

`contract-atlas-methods.mjs`提供`methodInputSnapshot(root)`返回`{fingerprint,inputs}`；以及`buildMethodCatalog({root,metadata,metadataDigest,renderModelDigest,inputSnapshot})`返回`{catalog,artifacts}`。artifacts为待复制的DocFX快照文件描述`{sourcePath,path,sha256,bytes,role:'docfx'}`，path仅位于本修订`docfx/`下。producer变化、配置、graph、DocFX输出及涉及源码哈希进入fingerprint；捕获期间/发布前变化必须被发现。

DocFX快照提供真实xref与页面/资源文件清单及原字节摘要；site来源限定同一仓库专用artifacts目录。所有链接指向manifest允许的同修订资产；缺页或哈希不符不发布坏链接。不把类型依赖图当作方法调用图。interface候选、virtual、delegate、callback和RPC对应必须保留原标签；跨进程边界不拼成进程内调用。

## 共同发布与恢复

协调者加入methodInputSnapshot指纹，revisionId同时绑定方法目录摘要。新修订包含唯一role=methodCatalog的`method-catalog.json`，其metadataDigest/renderModelDigest必须等于共同指针。无分析快照仍可发布明确unavailable的目录，旧公开图可正常阅读；已配置的损坏产物/失效绑定明确失败并保留lastgood。

源码发生变化但分析尚未重捕获时，方法目录返回stale并停止把旧正文当成当前源码展示；不自动全量编译。公开图与原验证事实不因stale升级或下降。分析捕获成功后，watch生成ready修订，浏览器一次应用其方法目录和公开图。客户端校验同源路径、字节数/SHA256、双摘要及内部目录形状；方法失败有局部错误/空态，不覆盖成其他方法或执行恢复动作。

## 验收与汇总

- 真实MSBuild/Roslyn捕获至少启动冻结/执行、会话或数组、Ribbon适配中的明确方法；涵盖非UI可达方法，未分析Host范围保留。
- 真实DocFX两项目API生成，至少一个精确方法链接及锚点实际可打开；private或缺失链接显示原因。
- 签名/XML/正文/行号/文件摘要与真实源码一致；重载/泛型/partial/async、候选边和外部无正文分别测试。
- 浏览器从契约进入方法、沿调用点切换并返回；hash/刷新/主题/筛选/流程保持；移动宽度、键盘与纯文本攻击输入验证。
- 源码变化标stale、重捕获变ready；错摘要/越界路径/错误绑定/缺快照安全失败，原图lastgood和草稿保护保留。

交付说明采用总体契约→Feature责任→Script输入/前置/方法/副作用/校验/回执/恢复→总结结构。静态关系与当前工程实机验证继续分开；不因方法已预览勾选原平台业务任务。
