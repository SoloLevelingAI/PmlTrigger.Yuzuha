# 契约元数据到组件的共同生成链 · 2026-11-01

本切片先细化契约再实现，补足“修改元数据后自动更新Web与FigCraft”，不再把手动build或浏览器内存导入称为实时生成。继续由canonical维护生效规范，展示元数据不能修改规范/实现状态或验证事实。

## 1. 可编辑源与边界

`docs/contracts/presentation.json`为展示作者源，canonical、capabilities、已审阅Script原文继续只读提取。验证事实与证据发布批准/路径移至独立人工审阅只读源`docs/contracts/evidence.json`，presentation及候选override出现evidence/verifications即拒绝，不能借直接编辑展示源改写排除项或结论。该源像canonical一样由人工审阅维护，不是防止拥有仓库权限的人修改全部原始证据的安全边界。Feature标题/职责/输入输出/副作用/失败/限制、Script展示标题及公开七字段覆盖、流程标题/步骤/连接、已实现的组件视图属性可编辑。公开七字段覆盖显式引用presentation和原文，不覆写内部原文。

文件编辑可新增/删除流程、步骤、连接及支持的展示映射；必须先验证唯一ID、完整引用、七字段及公开规则。编辑面板首期修改已有实体字段与完整单个流程，不原地修改身份。不允许修改canonical原ID/状态/规则、实现状态、来源路径、证据日期/结论/排除项。原13个业务Feature不能删除。并发以原始作者源SHA256作为sourceRevision；过期修改返回409，草稿保留，不自动覆盖或重试。

作者API的changes为 `{entityType,entityId,field,value}`，不接受任意JSON Pointer。允许project.summary/title；feature.title/responsibility/inputs/outputs/effects/failures/limitations；script.title及contract.input/precondition/method/effect/validation/receipt/recovery；flow.title/summary/steps/connections/featureIds；view.props的实际支持项。缺失实体或未知字段拒绝。候选必须通过完整提取、公开投影和共享模型校验，才能保存。

## 2. 共享组件模型

增加无DOM/网络依赖的 `render-model.js`，Node、Web及原生适配共同调用 `ContractAtlasRender.build(metadata,{metadataDigest})`。输出 `schema=contract-atlas-render/1`、schemaVersion、metadataDigest、稳定entityId/component的cards、flows、viewBindings及完整证据/来源。cards统一含title/summary、七字段或Feature sections、规范原ID/状态、实现范围、环境/日期/claim/exclusions、限制和格式化body/status/limit；适配器只做布局，不重新拼事实文本。

`views`从presentation读取（未提供时保留现默认），校验component与实体类型。variant/density/orientation等实际支持项必须影响两端布局；尚未实现的值必须明确拒绝，禁止静默接受。关键限制和证据排除项不得因展示props隐藏。Web本地导入/草稿也通过同一builder，标明来源，不称为已同步原生稿。

流程方向由已登记connections的from/to推导，同排及换排主路径应有可见方向标记，不能仅因步骤相邻便杜撰连接。next/uncertain/recovery以文字及样式区分，所有连接标签可见；关系清单保留跨行/恢复语义，不冒充运行轨迹。

候选按实际发布的pretty JSON加末尾换行计算UTF8预算：metadata最多2MiB、IR最多8MiB；构卡前累计重复展开的规范/证据引用最多2MiB。超过预算拒绝，不截断原事实或排除项。

## 3. 发布修订与自动构建

新增共同协调者，默认输出到隔离 `artifacts/contract-atlas-live`。监听展示源及其明确依赖、tokens；合并突发变更，单次构建串行执行。渲染器源码变化提示重启，不用旧模块缓存冒充新代码。构建输入和发布前均核对摘要；构建中又有新修改则旧结果作superseded，随后生成最新输入。

每次在新的staging目录中生成public metadata、render model、Web数据及真实FigCraft文件/结构结果/回执。只有元数据、共同模型和原生生成校验均通过，才把目录移到不可变 `revisions/<revisionId>`，最后原子替换current.json指针。manifest记录revisionId、metadataDigest、renderModelDigest、源摘要、工具阶段及产物path/sha256/bytes。Web与FigCraft必须使用相同模型摘要；不称跨来源文件和进程是全局事务。

新生成与缓存复用都在完整产物校验后、指针替换前复验源和渲染器摘要。单写者租约在停机时必须等待已受理保存及构建结束才释放，停止接纳新保存；租约文件存在时不自动抢占，异常退出需核对原PID再人工处理或选新输出目录。

源快照首读的作者/证据原始字节必须与随后采集的依赖摘要一致，否则作superseded；防止保存插入异步读取时形成“旧bytes、新hash”。manifest.sourceRevision必须等于inputs中的作者源摘要，也必须等于已发布metadata中source:presentation的摘要；消费者同样核对后者，避免旧原生稿错误完成新保存的等待。

来源无效、写入失败、工具缺失/失败、产物校验失败时不更新指针，lastgood继续可读。没有旧版本时明确未发布。原生稿只生成到协调者拥有的目录；不自动覆盖用户手工稿或修改已打开编辑器画布。FigCraft新文件自动生成，已打开的编辑器需自行重新载入/打开新版本；界面不能把“文件已生成”说成“画布已刷新”。

## 4. 本机服务与编辑API

保留静态离线入口；新增 `serve-contract-atlas.mjs --watch [--edit]`，仅绑定loopback。--watch自动构建共同版本，--edit显式启用作者写入。状态与通知只包含公开数据/非秘密摘要。

- GET `/api/state`：`{protocolVersion:1,instanceId,sequence,current:{revisionId,metadataDigest,renderModelDigest,manifestUrl}|null,generation:{phase,code?,message?},sourceRevision,generationEnabled,authoringEnabled,sessionToken}`。instanceId每次服务启动变化，sequence只在同实例内递增。
- GET `/api/events`：SSE `state`事件，data为同一状态；重连先GET state补漏。
- GET `/revisions/<id>/manifest.json`及manifest允许的产物：只读不可变发布目录，不服务staging/内部数据或任意路径。
- GET `/api/authoring`：`{sourceRevision,editable}`，只提供白名单作者字段，不返回源路径或证据写入口。
- POST `/api/preview`：`{expectedSourceRevision,changes}`，返回`{sourceRevision,candidateSourceRevision,metadata,renderModel,changes}`；不写源、不运行FigCraft，预览只能称草稿。
- POST `/api/save`：`{requestId,expectedSourceRevision,changes}`，明确保存作者源后返回202及`{sourceRevision,requestId,state:'accepted'}`，随后共同生成。保存成功与产物发布分开显示。相同requestId/摘要复用已记录事实，不重复写源；编号冲突拒绝。先保存非秘密受理回执，再写源；中断或响应不明按作者摘要/回执核对，不自动重发。
- POST `/api/rebuild`：显式重试共同生成，仍使用当前源，不复用失效候选；只读watch也允许，不要求启用作者写入。

POST需要同源Origin和会话token请求头、JSON预算、作者源并发复验；不接受外部网页跨源写入。写源先准备临时文件/备份并回验，不执行任何元数据命令。文件编辑器不共享本进程锁，发布前指纹检查负责发现变化；不承诺阻止任意外部编辑器竞争。

## 5. Web自动消费与草稿

HTTP watch模式订阅状态，只拉取current指向的已发布共同修订的manifest、metadata和render model，复验同源路径、摘要、公共schema与共同模型，再一次更新。当前构建失败时新连接仍可加载lastgood并显示失败。乱序/重复通知不覆盖新修订；服务重启后先GET state确认新instanceId，重置本实例sequence门禁。保留阅读/主题/实体/父级/筛选/流程、可恢复的展开/焦点/滚动；实体被删除时退到有效父级或project并解释。

未保存作者草稿或独立本地导入时暂停live覆盖，显示最新发布版本待处理；明确操作才能返回live。409保留草稿供比较/复制，不自动套用。SSE中断与构建失败显示stale/error，lastgood保留。界面常驻修订/摘要、构建阶段、原生文件下载及是否有未保存草稿；SVG标明live/import/draft来源与修订。

保存后的共同版本以manifest.sourceRevision匹配已受理作者源，而不是以旧预览metadataDigest匹配：tasks/证据等非作者依赖可能在预览后合法变化。bundle不得把state当前作者摘要冒充旧发布版本的作者摘要。用户可明确结束本地保存核对并返回已发布版本，说明仅丢弃本地待核对状态，不回滚源、不撤销构建、不重发保存；随后可重新加载当前作者基线继续修正。

## 6. 验收

实际修改Feature标题/说明、Script七字段和流程连接，证明无需手动build或刷新，Web卡片/流程和新FigCraft组件属性/连接同时更新，manifest双端摘要一致；views实际支持属性也要有前后差异。验证去抖/同输入不重建、构建中快速修改只发布最新、无效JSON/悬空引用/原规范状态写入/秘密泄漏拒绝、工具失败保留lastgood、旧sourceRevision冲突、草稿与本地导入不被live覆盖、SSE断线重连及选择状态保持。真实原生生成不能用mock替代；测试编辑使用隔离源副本并保留前后证据，不改真实规范或伪造验证事实。
