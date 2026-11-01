# 契约图谱的原生生成适配器

Web、FigCraft与Figma共同使用 `docs/presentation/contract-atlas/render-model.js` 的 `ContractAtlasRender`。唯一事实文案在共同IR的 `cards[].text.body/status/limit`；本适配器只做布局、文字属性和原生变量绑定。`source/adapter-core.js` 的cards已变为薄映射，不独立拼七字段、规范状态、证据日期或限制。

## Node API与隔离输出

```js
import { buildDesign } from '../../../scripts/build-contract-atlas-design.mjs';
const result = await buildDesign({
  metadata, metadataDigest, renderModel, tokens,
  outputDir: '/absolute/new/staging/design',
  revisionId: 'revision-unique-id',
  figcraftCli: '/absolute/path/to/figcraft-cli.exe',
  render: true
});
```

API导入没有文件写入或CLI执行。metadata、IR、tokens为本次冻结快照；metadataDigest及renderModelDigest按 `JSON.stringify(value,null,2)+'\n'` 的UTF-8 SHA256计算。IR必须由共享builder生成并匹配当前metadata与digest。布局props在创建目录前校验。

`outputDir`必须是不存在的绝对目录；已有目录即使为空也拒绝，避免覆盖手工稿与lastgood。父目录须先存在。API只在该新目录写产物，不发布、不切换current指针、不刷新已打开的编辑器，也不覆盖仓库历史稿。watch协调者负责新staging、失效复验与最终发布。

所有FigCraft子进程使用异步spawn，构建期间Node事件循环仍可处理HTTP/SSE/第二次作者保存。每次调用有120秒超时与20MiB stdout/stderr总预算；版本检查10秒。不自动重放超时或失败命令。失败throw并尽量在本次新目录保留failure.json；旧修订完全不改。

返回 `{outputDir,metadataDigest,renderModelDigest,artifacts,native,...}`。artifact.path相对outputDir，附sha256/bytes/role；`.figcraft`有role=figcraft。build-receipt.json绑定双digest、tokenDigest、修订、CLI版本和结构结果。只在所有CLI回执ok、真实结构检查通过后，native.generated/validated/success才为true。`render:false`仅准备IR/命令/插件包，native为not-run，不能发布为原生成功。

## CLI

```powershell
node scripts/build-contract-atlas-design.mjs --input docs/contracts/project.public.json --render-model docs/contracts/render-model.json --output artifacts/my-new-design
node scripts/build-contract-atlas-design.mjs --check --output artifacts/my-new-design
node --test tests/contract-atlas-design.test.mjs
```

显式--output同样只接受新目录。省略--render-model时调用同一共享builder，省略--input时读取默认公开JSON。**默认无参数命令现明确输出新的 `artifacts/contract-atlas-design/<unique-id>` 并在stderr提示新位置，保留docs/design历史稿与手工修改；不会静默覆写旧位置。** stdout返回完整回执。--commands-only对应render:false，不是原生验收。--check核对指定既有回执中产物SHA，不会重建或改手工文件。

依赖Node.js与FigCraft CLI 0.6.0。默认工具路径为 `%LOCALAPPDATA%\Programs\FigCraft\versions\0.6.0\figcraft-cli.exe`，支持FIGCRAFT_CLI/API figcraftCli。工具缺失明确FIGCRAFT_UNAVAILABLE，不假装成功。中文使用本机Microsoft YaHei，换字体环境需重做视觉检查。

## 原生内容与props

输出包含原生组件主稿、全部Feature/Script/Evidence、项目总览、ViewBindings样本板、全部流程及四个独立Help/Portfolio × Light/Dark示例。每个实例Title/Body/Status/Limit逐字来自共享IR，保留metadataId及metadataDigest/renderModelDigest/revisionId。

支持variant=help|portfolio、density=compact|comfortable、orientation=horizontal|vertical。variant影响标题排版；density实际改变字号、padding和gap；orientation实际改变Body与Status/Limit的分栏/纵向位置。卡片高度按内容估计并取同一原生模板组的最大尺寸，不固定裁掉长文案。Flow同样应用density与orientation，horizontal每行最多四步，vertical单列；画板高度按行数与全部连接增长。40步骤/40连接有正尺寸、无重叠与画板内断言。不支持的值与隐藏状态/证据在生成前拒绝。

颜色来自tokens v2的全部同构light/dark角色；原有九角色值保持不变。新增overview/contract/flow/implementation/evidence/reference六个区块类别，各有label及两主题accent/surface，产生同名原生颜色变量。概览、契约和证据组件使用对应区块底色/边线；正文与标题仍用中性text，限制保留warning，类别色不代表验证结果。正文在全部区块底色上须达到4.5:1；非法颜色、缺角色或不足对比度在生成前拒绝。

typography提供body/card/meta/title/heading/subheading的size/weight/lineHeight；CSS生成`--atlas-type-<name>-size/weight/line-height`，区块为`--atlas-section-<role>-accent/surface`。原生生成独立数字变量；文档正文实际16px、目录14px、辅助至少12px，标题使用600字重。字号和文字可编辑，不宣称数值变量已自动绑定所有编辑器排版属性。

Document-Light/Dark两张静态参考板共享IR：左侧全部Feature/Script目录，中间真实script:launch-workflow七字段及状态/限制，右侧本页字段标题与六类区块示例。原文不复制到独立作者源，参考板不执行导航、折叠或捕获。原生校验核对两板、目录数量、每个七字段的原文字体及六类示例。PNG仅作预览：超长既有画板等比缩至最高12000px，完整可编辑文件不截断；文档板按1:1导出。

文档参考板的视觉层级以当前任务为主：轻品牌顶栏、所属Feature面包屑、32px任务标题及所属Feature目标；左目录区分Feature/Script并标出当前项，右侧为轻量字段目录和192×40紧凑图例。七字段使用中性段落、细分隔与24px章节间距，保留原始警告。12px主要圆角与8px节奏避免把分色框线当作作品集完成度。2026-11-01浅深真实PNG已按首屏焦点、层级、可读性、留白、配色及事实完整性逐项代理目检；状态为`agent-reviewed / human-not-reviewed`，不表示用户已经认可。

摘要用独立18px中性小标题组织：“任务目标”下保留所属Feature目标原文，不在原文重复拼接前缀；“适用边界”下保留原警告。首字段从528px开始，与边界段落留足间距；七字段原文和目录身份不变。原生门禁逐字比较目标/边界原文并核对实际小标题字号，Figma离线mock核对同样小标题；编辑器宿主仍为not-run。

## Figma：共享IR，真实执行not-run

每个新build的figma-plugin目录包含打包shared validator、shared render-model、布局适配及tokens快照的code.js、UI和manifest模板。插件读取同一公开metadata后调用同一builder，再将同一text映射到真正ComponentNode/实例/属性/变量，不生成伪.figma。离线mock逐卡核对IR事实及Light/Dark模式；**没有真实Figma导入/渲染通过证据**。

manifest不填杜撰ID。按Figma桌面“Create new plugin”取得真实插件ID，填入模板副本再导入。只新增页面与变量集合，不发布Library，不访问网络，不执行任何元数据命令。异常尽量清理本次新对象，但不宣称编辑器跨对象是原子事务。

依据官方[Manifest](https://developers.figma.com/docs/plugins/manifest/)、[组件属性](https://developers.figma.com/docs/plugins/working-with-component-properties/)、[创建组件](https://developers.figma.com/docs/plugins/api/properties/figma-createcomponent/)与[变量](https://developers.figma.com/docs/plugins/working-with-variables/)。真实Figma仍需核对字体回退、实例样式传播、文字覆盖与页面外内容保持。

## 验证范围

设计测试包含：API只写新dir；不匹配IR/不支持props拒绝；40-step布局；共享IR/Figma mock；以及有工具时真实生成两个独立修订，修改Feature标题/职责、Script算法、step标题、连接label及布局props，核对新原生组件随修订变化、双digest变化，缺工具失败不影响旧文件。无工具环境的真实CLI项明确skip，不被计为native通过。

真实修订测试证据可用CONTRACT_DESIGN_TEST_EVIDENCE指定输出目录。测试不修改展示作者源、canonical、既有设计或外部画布。回执表示文件已生成与结构通过，不能称正在打开的编辑器画布已刷新，也不证明业务GUI/产品或用户视觉认可完成。

## 原生流程连接契约

流程图中的方向只来自共享IR的connections，不从步骤数组索引推断。每条关系生成可编辑的原生矢量折线与箭头，并保留from/to/kind/label作为节点数据；图中编号与下方完整关系文案一一对应。next使用实线，uncertain使用虚线，recovery使用点线及独立回路；方向头落在真实目标端。同行连接经过卡片间隙，换行/回退连接经专用通道，不穿过步骤卡片正文。未登记连接的相邻卡片不添加箭头。

FigCraft与Figma复用flowLayout给出的步骤及路由几何。40步骤边界继续检查步骤/文案不重叠、所有尺寸和路由点在画板内；连接相互交叉不冒充逻辑节点。完整label不被短箭头间隙裁掉，图中编号可追到下方关系文本。渲染验收只代表关系可见与可编辑，不代表业务执行。
