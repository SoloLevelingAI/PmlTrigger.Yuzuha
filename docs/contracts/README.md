# 项目契约元数据

`project-contract-metadata/1`是理解与作品集的展示投影，不是第二份生效规范，也不执行任何业务操作。生效要求仍来自`docs/specification/contract.json`；候选OpenSpec保持proposal身份。

## 生成及核对

在仓库根执行：

```powershell
node scripts/contract-atlas-metadata.mjs build
node scripts/contract-atlas-metadata.mjs check
node --test tests/contract-atlas-metadata.test.mjs
```

不需要额外Node依赖。`build`只读取仓内明确来源、写六份生成产物（含共同render model JSON及JS包装），不执行来源文档中的命令；`check`重新计算全部输出，任一来源内容或产物漂移均非零退出。外部路径和解析到仓外的链接拒绝读取。已有工作区改动不会被覆盖到来源。

- `presentation.json`是人工审阅的稳定ID、Feature映射、流程与公开展示摘要；不能从标题动态生成ID。证据范围和验证事实另由只读`evidence.json`维护。
- `project.internal.json`保留当前来源说明原文、内部来源相对路径和191条原始来源限定责任映射。
- `project.public.json`按白名单投影：保留24条canonical要求原状态及原表、13个既有业务Feature与5个明确平台Feature、14类Script七字段、3个流程、分层验证和限制。来源省略path，证据仅有已审阅结论/来源标识，不复制原日志或图片。
- `schema.json`是外部可用的Draft 2020-12结构schema，由共享验证器同一个schema对象导出。
- `../presentation/contract-atlas/atlas-data.js`定义`globalThis.CONTRACT_ATLAS_DATA`，默认只含公开投影，离线页面可直接使用。
- `render-model.json`及`../presentation/contract-atlas/atlas-render-model.js`是共同组件投影；后者定义`globalThis.CONTRACT_ATLAS_RENDER_MODEL`，其metadataDigest对应同次public JSON原始字节SHA256。

固定`snapshotDate=2026-11-01`是此次展示映射审阅日；每条verification的`observedAt`保留证据实际日期，当前历史证据为2026-10-31，不能用生成日期冒充新测试。来源SHA256按**原始字节**计算，包括BOM、CRLF/LF和末尾换行；不做换行规范化。迁移到不同checkout后换行变化也会使check失败，必须审阅后重新生成，不能关闭检查。

## 三轴状态与来源

`requirements[].status`是原canonical的verified/partial/planned；`features[].implementation`是原能力目录或明确切片范围；`verifications[]`按static/offline/ui/simulated-install/runtime分别表示具体环境、结果、排除范围和证据。三者不能互相升级。

原G/R/B/T/S、screens/transitions/models及字段注册表保存在`canonical`中；顶层requirements按Fields展开并保留originalId。191条Archon/Next/PMLTWIN映射的公开部分只保留源限定ID、责任owner和原状态，不把owner视为已实现Feature，也不把候选操作名当作已注册operation。当前图谱edges是明确结构关系或静态关系，不是实际运行轨迹。

七字段来自总分总说明中明确编号的14节，匹配失败、缺字段或重复字段立即报错。运行时原文中具体机器身份仅在internal保留，公开文本使用presentation.json中逐字段审阅摘要并引用该映射来源；不会静默替换原文含义。新增公开内容仍需人工审阅，自动敏感模式检查不能识别任意隐写。

## 共享验证器

普通浏览器脚本加载`docs/presentation/contract-atlas/metadata-validator.js`后：

```js
const result = globalThis.ContractAtlasValidation.validate(data);
// { valid: boolean, errors: string[] }
```

Node可直接import同一文件，Figma适配可原样bundle这个无依赖IIFE。验证失败时不要替换已有有效视图。函数不使用网络、DOM或eval；全量原生页面/设计工具的运行证明由各适配器分别交付。

JSON Schema负责结构；共享validator另核对跨实体语义和安全规则，二者都需要使用。外部工具仅通过JSON Schema不等于已经通过来源保真、引用闭合、公开隐私及运行时文本安全检查。自定义验证器支持当前导出schema使用的约束，不能当作任意第三方JSON Schema解释器。

验证规则包括：2MiB输入、最多500实体与1500条独立边、30个流程/每流程40步骤、最大JSON深度32及节点预算、文本预算、所有ID唯一、G/B/T/S及sourceRefs闭合、七字段完整、canonical值不变、flow节点可渲染、component与entity类型匹配、公开来源无path/无内部条目/无未审证据，以及危险URL/主动HTML/执行props/prototype键拒绝。文本仅可纯文本渲染；验证通过也不授权任何operation。

公开投影不自动发布，当前所有成果仍在本仓。运行时外观证据与UI离线夹具分别保留，未点击宏、未迁移活动Database及未适配CAF等限制仍须显示。

## 可编辑作者源与共同组件投影

证据事实独立存于人工审阅的只读源`docs/contracts/evidence.json`，结构为`{schemaVersion:1,evidence:[],verifications:[]}`。这里保留既有证据ID、原始报告路径、验证日期、结论、环境及排除范围；它不是展示作者源的一部分，不通过编辑面板或作者候选写入。`source:evidence`记录该ledger的原始字节SHA256，每份报告仍保留独立来源及摘要。更新证据须独立人工审阅后修改ledger，不能借展示字段编辑升级运行时结论。

`presentation.json`及`presentationOverride`不得包含`evidence`或`verifications`，即使内容与ledger相同也明确拒绝，不静默忽略。CLI文件编辑与API候选都采用这条门禁；作者源只引用既有ID，不能迁入可编辑副本覆盖claim、日期或exclusions。共同协调者同时监视ledger及其明确报告来源，但自动生成不等于重新执行这些历史验证。

`scripts/lib/contract-atlas-authoring.mjs`提供纯函数：

```js
editable(presentation); // [{entityType, entityId, field, label, value}]
applyChanges(presentation, changes); // 新候选，不改原对象或磁盘
```

changes每项严格为`{entityType,entityId,field,value}`。允许project标题/摘要、Feature展示字段、Script标题和七字段公开覆盖、单个flow的说明/步骤/连接/Feature引用、已有view的props。原ID、canonical状态与规则、实现状态、来源路径和验证事实均不可修改。Script无公开覆盖时editable值为null，表示继承；编辑器可用当前public metadata显示有效文本，但保存修改只写presentation.publicOverrides，不写内部原文。

`generate(root,{presentationOverride})`完整生成候选并校验公开安全与共同模型。候选的`source:presentation.sha256`严格对应`JSON.stringify(candidate,null,2)+'\n'`的UTF8字节，供外层sourceRevision/CAS使用；此函数不保存、不运行FigCraft。删除或修改flow步骤后有悬空连接会拒绝；13个既有Feature不得删除。文件直接编辑可配置完整presentation.views，未提供则生成同ID的默认绑定。

无DOM/网络IIFE `docs/presentation/contract-atlas/render-model.js`定义：

```js
ContractAtlasRender.build(metadata, {metadataDigest});
ContractAtlasRender.validate(ir, metadata, {metadataDigest}); // {valid,errors}
ContractAtlasRender.resolveProps(props, {variant:'help'});
```

IR的`schema=contract-atlas-render/1`。cards按稳定entityId统一包含ProjectOverview、FeatureCard、ScriptContractCard及EvidenceBadge；sections保留全部七字段/来源，requirements保留原ID/状态，implementation保留范围，verifications保留环境/日期/claim/exclusions并补共同environmentText；text.body/status/limit是唯一事实文本拼装。Web、SVG、FigCraft、Figma只布局，不重新拼事实。flows保留稳定步骤及next/recovery/uncertain连接；viewBindings是作者声明的真实绑定。validate通过同源重建逐字段比较，伪造body、删除排除项或更换摘要均拒绝。

metadataDigest允许null，明确表示独立本地导入/草稿未绑定发布版本；已发布共同模型必须使用64位小写SHA256并由外层核验实际metadata字节。模型本身不能证明文件已发布或编辑器画布已刷新。

当前支持variant=help/portfolio、density=compact/comfortable、orientation=horizontal/vertical。props只保留作者显式覆盖，缺variant跟随阅读view，其余由resolveProps给出默认comfortable/horizontal。两个渲染器必须让这些值影响组织/尺寸/排列。showStatus/showEvidence仅接受true；false及未知属性拒绝，关键限制/证据不能隐藏。重复同component/entityId绑定有歧义，拒绝。尚无消费者支持的BoundaryNotice绑定明确拒绝，不假装已生效。

作者源写入、并发CAS、watch去抖、真实FigCraft构建、lastgood及原子发布指针由共同协调者负责，不能把本文的静态build或纯候选函数称为自动实时发布。运行`npm run contracts:watch`启用真实共同生成和作者编辑；操作及总分总责任说明见[契约到组件的自动生成链](../reference/contract-generation-pipeline.zh-CN.md)，细化契约见`openspec/changes/integrate-pmltwin-product-platform/features/contract-generation-pipeline.md`。

共享builder按实际发布的pretty UTF8字节同步约束metadata最多2MiB、IR最多8MiB，并在构卡前限制重复规范/证据展开最多2MiB；即便紧凑JSON能通过，实际传输或展开超限也拒绝候选，避免保存后生成失败。不会通过截断七字段或排除项适应预算。
## 完整阅读索引与方法覆盖契约

共享IR增加`referenceIndex.sections`，每节为`{id,title,columns,rows,sourceRefs}`。columns是字符串列名；rows逐行复制原始tuple或object，不填补、截断或改写原状态。未知object键加入明确列，超出声明列数的tuple列名为`$extra:N`（从0起）。单元格数组/对象必须按完整JSON纯文本呈现；缺省单元格与明确null不同。context节保留注册表和其他顶层字段；来源映射context保留policy/counts等，191条mapping原行独立展示。索引仅为规范/来源责任目录，不证明实现、注册或运行验收。

节ID固定为`reference:context`、`reference:goals`、`reference:requirements`、`reference:bindings`、`reference:tests`、`reference:screens`、`reference:transitions`、`reference:models`、`reference:source-mappings`及`reference:source-mapping-context`。`coverage`仅记录features/scripts/requirements/flows/sourceMappings五项目录数量。原Project sections与事实不变。

`method-bindings.json`为全部18个Feature及14个Script逐个登记：非空symbolIds必须逐个命中真实捕获图；空数组必须带`resolution:'unavailable'|'not-applicable'`与真实原因。未纳入当前C#编译图、未完成业务与非C#入口分别说明。补绑定不升级实现或验证状态，不扩大当前只读目录预算，也不发布源码到公开IR。
