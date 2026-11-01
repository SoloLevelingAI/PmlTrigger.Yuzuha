# PMLTWIN Contract Atlas

直接打开 [index.html](index.html)，即可通过 `file://` 离线使用。不需要服务、构建、CDN、外部字体或工程环境。旧版 [21 页契约演示](../contracts/index.html) 保持不变。

这是公开元数据的阅读工具，不是业务 GUI。数据与共享组件模型分别生成至 `atlas-data.js` / `atlas-render-model.js`；canonical 规范仍是 `docs/specification/contract.json`。`render-model.js` 是 Web、SVG、原生适配共同调用的无 DOM 事实 builder，适配器只布局其 sections、body/status/limit、规范原状态和证据，不各自拼另一份事实。实现状态、规范状态和验证范围分别显示，不从任务完成或图表颜色推断实机通过。

## 修改元数据后自动生成两端

从仓库根启动本机共同生成服务：

```powershell
node scripts/serve-contract-atlas.mjs --watch --port 19439
# 明确需要在页面修改作者源时，再加 --edit：
node scripts/serve-contract-atlas.mjs --watch --edit --port 19439
```

打开终端给出的 loopback URL。`--watch` 监视明确的元数据依赖，串行构建、验证 Web 与真实 FigCraft 共同修订；只读 watch 可以显式“重新生成”，不会因此获得编辑作者源的权限。`file://` 或没有 `--watch` 的静态服务仍是快照，状态栏会明确说明，不假装实时连接。

状态栏显示数据来源、构建阶段、revision 与 metadata/render-model 双摘要。共同发布后 SSE 自动通知，浏览器读取 immutable manifest 和两份 JSON，复验同源路径、字节数/SHA256、作者源摘要、原生 generated/validated/success 严格布尔及共享模型，再一次更新。构建失败、坏产物或断线保留 lastgood；服务重启由 instanceId 重置序列门禁并通过 GET 补漏。FigCraft 链接只指向当前已应用共同修订；生成新文件不表示已打开的原生编辑器画布自动刷新。

自动更新保留实体/父级、阅读与主题、筛选、固定流程、可恢复的折叠、焦点及滚动。原实体被删除时回到仍存在的父级或总体。独立 JSON 导入和未保存作者草稿暂停实时覆盖；“返回最新共同版本”是显式动作，不会覆盖正在编辑的草稿。

`--edit` 后“编辑展示字段”先读取服务端白名单。Feature 字段、Script 七字段、流程步骤/连接和支持的 view props 都可选择编辑；数组/对象使用 JSON 文本。界面不提供规范状态、来源路径、验证结论等只读字段。

验证与证据来自独立、人工审阅的 `docs/contracts/evidence.json` ledger，不属于页面作者字段；展示字段修改不能增加实机通过结论。

1. 修改值后“校验并预览草稿”：只更新当前 Web，不写作者源或生成原生文件。
2. “明确保存并生成共同版本”：带作者源基线摘要与唯一请求编号；202 只表示源已受理，界面等待 **已发布 manifest.sourceRevision 匹配保存源摘要** 才结束等待。期间其他非作者依赖更新可以产生新的模型摘要，不会因此永久卡住。
3. 409 保留草稿供查看/复制，不自动覆盖或重发。保存响应不明时显示原编号，仅“核对保存状态”，不自动再保存。
4. 若已保存但原生工具失败，或已有其他新作者修订，可显式“结束本地核对，返回已发布版本”。二次确认仅结束本地等待和预览，不回滚作者源、不取消服务器构建、不重发请求。原编号仍可核对，重新打开面板会读取当前作者修订继续修正。

完整来源/发布/恢复用法见 [共同生成链路说明](../../reference/contract-generation-pipeline.zh-CN.md)。FigCraft 真实生成与结构验收有独立证据；Figma Plugin 尚未在真实 Figma 宿主运行，Node 验证不替代该宿主验收。

## 阅读与交互

### 文档布局与作品集

默认先读任务地图，再进入资料分支：项目 → 目标类别 → Feature → 已登记流程步骤。当前步骤有原始stepId、交付及前后/未知/恢复入口；契约、实现方法、证据与来源、完整目录通过正文分支切换，返回保留原位置。没有登记流程的Feature不按目录推测步骤。Web位置是阅读位置，不是实际执行进度；产品动作仍在Desktop中明确操作。完整用法见[任务地图与动线](../../reference/task-map-navigation.zh-CN.md)。

`npm run contracts:reading`与`contracts:tasks`运行当前任务地图/分支阅读验收。旧`tests/contract-atlas-reading-browser.mjs`保留历史全页排版假设，本轮未适配、未运行，不列入通过项；当前摘要/完整内容/方法与实时状态由task、document及真实reading-live/document-live验证。

摘要现在以“任务目标、所属任务交付、输入/职责摘要、实现方式/范围、适用边界、详细细约”等中性小标题分组；原七字段与来源及旧URL不变。Dashboard可用性与真实桌面试用范围见[会话绿灯说明](../../reference/dashboard-session-health.zh-CN.md)，不能把网页/视觉完成等同于整个产品完善。

左侧项目目录渐进展开，中间显示当前任务或所选资料分支，右侧为当前分支的大小标题。旧section链接会先选相应分支，再按稳定key展开必要详情、滚动并聚焦；scrollspy只更新当前位置。URL的branch/flow/step/section与实体、方法和筛选共同保存，全部折叠会把子锚点降到父节，刷新仍折叠。桌面两侧sticky且独立滚动；中宽收起本页目录，手机两个目录用原生折叠入口，页面工具为次级菜单。

作品集先显示所属任务目标及交付，契约视图强调逐项核对；真实状态和边界保持独立。颜色仅标记区块类别，标题正文以字号/字重区分。六角色及排版来自tokens.json，同时生成CSS和FigCraft变量/静态文档参考板。美观是独立视觉复核，颜色一致、无溢出或测试通过均不等同于用户视觉认可。完整契约、设计及使用说明见[文档导航与共享设计](../../reference/contract-document-navigation.zh-CN.md)。

### 从契约查看方法

本机共同生成页的“实现方法”面板读取独立 internal 方法目录，与公开 metadata/IR 分开。Feature/Script 仅按显式绑定定位方法；点击方法可看签名、XML 说明、源码文件摘要和行范围、手写正文预览，以及直接调用方/下游的每个静态调用位置。接口候选、动态分派、回调及 RPC 对应保留边界，不是运行调用栈或覆盖证明。方法和方向保存到 hash，切主题、阅读模式或浏览静态关系不改变契约筛选和任务流程。

目录字节摘要必须同时匹配共同指针与 manifest；目录还须匹配当前 metadata/IR 双摘要。DocFX 链接仅使用本修订清单允许的真实成员页面；缺失或 private 成员说明原因，不猜测锚点。签名、XML、源码与表达式均按纯文本展示，无执行入口。原始源码不进入公开 SVG 或 FigCraft。

`file://` 不读取内部方法文件，显示不可用；独立导入和作者草稿不沿用旧方法绑定。没有捕获快照时明确 unavailable；源码改变后显示 stale 并隐藏旧正文。运行维护者提供的显式捕获命令后，watch 会发布新快照；浏览器不会自行启动全量 MSBuild。坏目录保留已应用的整体图谱并报告错误。

显式捕获：`npm run contracts:methods:capture`，依次运行 TraceDesktop 和独立 DocFX 快照捕获；完成后 watch 自动发布。方法面板浏览器专项：`npm run contracts:methods:browser`。隔离数据覆盖纯文本输入、键盘选择、静态调用导航、hash/筛选保持、移动布局、stale/草稿边界和摘要/链接门禁；不替代真实 Roslyn/DocFX 捕获验收。

- 阅读视图与外观主题是两个独立维度。默认作品集阅读视图，外观默认跟随系统，也可独立选择浅色或深色；选择项目指南不会改变颜色主题。
- 两种阅读视图都能查看完整目录与原文。卡片先显示标题、职责摘要、真实状态、字段数量与关键边界；“展开完整契约”就地查看字段、原需求、来源和验证说明，“进入此契约”独立导航，不在按钮内嵌套折叠控件。
- “全部折叠 / 全部展开”统一控制嵌套卡片、契约、流程说明、规范附录与已选方法。默认摘要；局部操作后显示自定义状态，全局动作清除局部覆盖。`detail=summary|full`保存全局程度，`catalog=features|scripts|all`保存总体目录分类；两者独立于阅读视图和外观。
- 总体目录完整列出18个Feature与14个Script，可分类查看；搜索跨类别。附录的10节352行保留全部原始元组/字段和191条源码限定映射，可搜索、分类并在表内横向滚动。数字表示目录数量，不表示测试覆盖率。完整使用路径见[阅读说明](../../reference/contract-reading.zh-CN.md)。
- 总体 → Feature → Script，面包屑返回上层；搜索名称、ID、职责与关联原需求，按实现状态筛选。未匹配时可清空筛选恢复。
- 完整Feature字段或Script七字段各保留一份，按需展开；项目详情保留项目自身字段。两种阅读视图均提供完整来源入口。实现范围、限制、证据的环境、时间和排除项持续显示，不以折叠或行截断隐藏。方法签名、静态分析范围和过期/未知边界持续显示，XML、定位、已有源码和每方向调用位置分组展开。
- 流程实线表示已登记主路径，虚线/文字表示不确定或恢复支路。顺序与关系只是静态契约描述，不是运行轨迹。没有独立流程或证据时明确写“未登记”，不补造保证。
- 图中 Tab/方向键移动，Enter 选择；选择后焦点到层级标题。说明窗口使用原生 dialog，Esc 关闭并回到触发按钮。状态有文字标签，不只依赖颜色；支持 reduced-motion。
- “任务流程”可独立浏览全部已登记流程，也可跟随当前实体；显式选定后，切阅读视图、主题或实体都保留该流程，跨 Feature 浏览时会说明上下文。URL hash 保存视图、实体、父 Feature、显式流程、关键词和状态，重新打开可恢复。仅用白名单字段解释 hash，不将其当 HTML 或代码执行。

外观由 head 中同步加载的 `theme.js` 管理，先于页面样式解析存储偏好与系统配色，避免以阅读模式猜主题。显式偏好保存到本机存储并跨同源页面同步；跟随系统时响应系统配色变化。存储不可用时控件仍可切换，提示“仅本次有效”，不妨碍阅读。`ContractAtlasTheme.getState()/setPreference()` 与 `atlas-theme-change` 独立于图谱选择状态。Shadow DOM 组件继承同一主题变量，SVG读取当前解析后的实际颜色与主题名称。

`tokens.json` v2只按light/dark维护同一组语义角色，阅读视图不出现在颜色模式名中。修改token后运行 `node scripts/build-contract-atlas-theme.mjs`（或 `npm run contracts:build`）生成 `theme-tokens.css`；`node scripts/build-contract-atlas-theme.mjs --check`拒绝颜色源码与生成CSS漂移。装饰分隔线使用border，重要控件/流程轮廓使用更高对比的boundary。打印固定采用同源浅色配色，SVG保留当前实际主题。

## 可复用组件

`components.js` 注册开放 Shadow DOM 的原生组件。CSS 变量由宿主继承，不依赖框架。先加载 validator、共享 builder 与生成 IR，再设置 `.model`；所有模型文本用 `textContent` 渲染。所有内容组件接受可选 `disclosure:{isOpen(key)}`，key为 `ownerId|section`。原生折叠控件同时带 `data-disclosure-key`/`data-state-key`，用户操作发出 `atlas-disclosure`（`detail:{key,open}`，bubbles/composed）；程序初始设置不会冒充用户覆盖。独立使用时默认摘要，不随help模式强行打开。卡内与选中详情使用不同key，嵌套卡片接收同一披露接口。组件发出 `atlas-select`（`detail: {id}`，bubbles/composed）或 `atlas-more`，不执行元数据中的命令。实际 props 的 density 影响留白，orientation 影响横/纵排列，variant 影响阅读组织；状态/证据不能通过 props 隐藏。

| 组件 | model |
|---|---|
| `task-map` | `{map:ir.taskMap,entityId,featureId,flowId,stepId,branch}`，只消费共享任务事实；选择发出 `atlas-task-navigate`，不执行产品入口 |
| `contract-card` | `{...irCard,id:irCard.entityId,status:irCard.implementation?.status,index,selected,mode,data:ir,disclosure?}` |
| `feature-map` | `{items:[cardModel],selectedId,hiddenCount,mode,data:ir,disclosure?}` |
| `contract-flow` | `{flow:ir.flows[n],selectedId,mode,disclosure?}`，显示共享步骤及全部连接标签 |
| `contract-detail` | `{card:irCard,kind:'project'|'feature'|'script',data:ir,mode,relatedCards,flow?,disclosure?}` |
| `evidence-strip` | `{verifications:irCard.verifications,data:ir,mode,disclosure?}`，显示共享环境文本与全部排除项 |
| `contract-reference` | `{data:ir,disclosure?,methodCatalog?}`，只读完整规范附录；可选方法目录仅来自本机共同修订 |
| `method-panel` | `{catalog,entityId,methodId,direction,docfxUrls,origin,disclosure?}`，本机内部只读方法预览，草稿/导入不复用绑定 |

`contract-atlas` 为演示页面的协调宿主，`app.js` 是依赖全局 document ID 的**单实例页面控制器**，不是一页可放多个完整 Atlas 的封装。可独立复用的是表中的内容组件及其 `.model`/选择事件API；同页可创建多个这些独立组件。metadata 的 `views[]` 是已验证的展示声明，本页不把它当动态布局引擎或任意组件执行入口。`disclosure.js`的Controller维护全局模式与稳定key覆盖；组件仅询问`model.disclosure.isOpen(key)`和报告原生toggle，不改元数据。

正文入口使用 `#branch-navigation [data-branch]`，固定为 `task/contract/implementation/evidence/catalog`；`ContractAtlas.getState()`提供 `branch`、`stepId`及原有实体/流程/方法/披露状态。同一Script出现在多个步骤时保留独立stepId，没有明确位置时不选第一个。地图节点的“对应契约交付”来自关联契约，不表示该步已经产出结果。

新主路径浏览器验收为 `npm run contracts:tasks`：12个项目首屏组合、6个完整步骤/前后步首屏组合，以及分支往返、旧URL、多流程、重复Script、修订焦点和删除回退。旧静态/主题/文档测试显式进入相应资料分支后继续验证安全导入、原文字与来源、SVG、键盘、对比度和折叠。任务地图首屏与兼容资料布局是不同验收，不能用隐藏DOM的数量代替可见入口。

`tokens.json` 提供设计工具可复用的两套语义颜色、圆角、间距和字体；`style.css` 与组件使用对应 `--atlas-*` 变量。公开来源只展示已审阅的 ID、标题、authority 与 SHA256，不自动生成内部路径或外部链接。

```html
<script src="theme.js"></script>
<link rel="stylesheet" href="theme-tokens.css">
<link rel="stylesheet" href="style.css">
<contract-card id="example"></contract-card>
<script src="atlas-data.js"></script>
<script src="metadata-validator.js"></script>
<script src="render-model.js"></script>
<script src="atlas-render-model.js"></script>
<script src="components.js"></script>
<script>
  const data = globalThis.CONTRACT_ATLAS_DATA;
  if (!ContractAtlasValidation.validate(data).valid) throw Error('invalid metadata');
  const ir = globalThis.CONTRACT_ATLAS_RENDER_MODEL;
  if (!ContractAtlasRender.validate(ir, data, {metadataDigest: ir.metadataDigest}).valid)
    throw Error('invalid shared model');
  const card = ir.cards.find(item => item.entityId === 'feature:ribbon');
  document.querySelector('#example').model = {
    ...card, id: card.entityId, status: card.implementation.status,
    mode: 'help', index: 0, selected: false, data: ir
  };
  document.querySelector('#example').addEventListener('atlas-select', event => {
    console.log(event.detail.id); // 宿主自行决定导航；组件不执行业务。
  });
</script>
```

## 本地数据与纯 SVG

导入只读取用户选择的 JSON 文件，上限 2 MiB。共享 `metadata-validator.js` 检查版本、形状、预算、唯一 ID、引用、公开性和危险内容；实体上限 500、关系 1500、流程 30、每流程 40 步。当前公开演示只接受 `audience=public`。失败保留原图并显示原因；内部数据不会被自动投影或公开。空 Feature/Script 数据有可恢复空态。

导入和草稿同样调用共享 builder，不绕过 IR。它们只在本地展示，不覆盖发布指针，也不声称原生文件同步；SVG注明 static/live/import/draft、修订与摘要。

导出保存当前层级和筛选下的自包含 SVG（总体包含当前分类和筛选下的全部匹配契约）。它嵌入颜色、文字、快照日期、关联规范的原 ID/原状态、实现范围、来源指纹，以及证据的产品/版本/模块和未涵盖范围，不包含脚本、事件处理器、外部资源、`foreignObject` 或工程执行功能。图是静态说明，不是运行截图或新的验收结论。

## 验证

从仓库根运行：

```powershell
node tests/contract-atlas-browser.mjs
# 任务地图、步骤上下文、资料分支与旧链接：
node tests/contract-atlas-task-map-browser.mjs
# 消费者异常/编辑恢复测试：原生生成器明确使用测试替身
node tests/contract-atlas-live-consumer.test.mjs
# 真正的watch + Chromium + FigCraft CLI共同生成验收，必须安装真实CLI
node tests/contract-atlas-live-browser.mjs
node tests/contract-atlas-theme-browser.mjs
```

浏览器验证使用仓库根锁定的 Playwright 依赖。新电脑在仓库根准备：

```powershell
npm ci
npm run contracts:browsers:install
npm run contracts:browser
```

第二步等价于 `npx playwright install chromium`；下载与锁定 Playwright 版本匹配的受管 Chromium，不固定本机缓存目录或浏览器 revision。测试不会自动下载。已有浏览器可通过 `CONTRACT_ATLAS_CHROMIUM_EXE` 指定完整可执行文件；也可显式设置 `CONTRACT_ATLAS_BROWSER_CHANNEL=msedge` 使用已安装 Edge。未安装受管浏览器且未指定覆盖时，测试给出安装命令并失败，不伪装通过。全部契约浏览器测试共用 `tests/support/contract-browser.cjs`，不再读取被忽略的 `artifacts/**/node_modules` 或 `CONTRACT_ATLAS_PLAYWRIGHT_MODULE`。

真实浏览器在 `file://` 验证两模式 × 390/768/1440 宽度的总体、Feature、Script 共 18 个布局；另验证钻取、筛选、空态、键盘/焦点、hash、恶意/超大导入和纯 SVG。导出明确核对 E3D、2.1、DESIGN。消费者测试覆盖摘要/原生 false 门禁、迟到响应、import/draft 保护、409、未知保存不重发、accepted 后原生失败的显式本地恢复及依赖变化。它的 native test double 不作为原生验收。

真实共同生成测试独立调用 FigCraft CLI，修改隔离作者源、等待真实 watch/SSE、比较 Web 与原生组件/连接/props 和双摘要，并验证工具失败、显式恢复及服务重启。结果位于 `artifacts/contract-atlas-live-evidence/`；消费者结果位于 `artifacts/contract-atlas-live-ui/`，静态页面结果位于 `artifacts/contract-atlas/`。这些不产生新的 AVEVA 实机结论，也不修改外部当前设计画布。

组件披露专项：`node tests/contract-atlas-disclosure-components.mjs`。独立夹具在390/768/1440与两阅读视图逐原文核对32卡、14组七字段，并检查嵌套Shadow披露、无程序toggle回流、原生键盘与44px摘要、项目字段及方法分组。页面中央控制器的全局/局部状态另由阅读浏览器专项验收。
