# 契约元数据与组件图谱 · 2026-11-01

先规定契约，再生成实现。映射6.2/6.4，本切片服务项目理解与作品集，不替代业务GUI或全部Feature设计验收。canonical `docs/specification/contract.json` 保持权威，生成JSON不是第二份生效规范。

## 数据契约

使用 `project-contract-metadata/1`，`schemaVersion=1`。稳定ID使用显式映射，不能从标题生成；记录当前来源内容SHA256。输入为canonical、capabilities及总分总说明的已审阅字段，人工展示映射由独立presentation.json维护。生成产物可重建，不回写来源。规范状态、实现状态、证据范围三轴独立，禁止从已完成任务推断全产品实现或实机通过。

- project: `{id,title,summary,goalIds,featureIds}`。
- features[]: `{id,sourceId,title,responsibility,inputs[],outputs[],effects[],failures[],limitations[],scriptIds[],requirementIds[],implementation:{status,scope,sourceRefs[]},verificationIds[],audience}`；保留13个既有Feature，平台Ribbon/Database/migration等明确使用额外ID与来源。
- scripts[]: `{id,title,language,role,featureIds[],contract:{input,precondition,method,effect,validation,receipt,recovery},audience}`；七项各为 `{text,sourceRefs[]}`，缺字段报错，不捏造默认保证。
- requirements[]: `{id,originalId,authority,status,rule,acceptance,sourceRefs[],...}`；按canonical的Fields无损展开，保留原身份及状态。原G/B/T/S及其他表也无损保存在canonical字段中，不映射成已注册operation。
- edges[]: `{id,from,to,relation,basis,sourceRefs[]}`；仅已有实体，relation限定contains/uses/calls/exposes/implements/tracesTo/verifiedBy/dependsOn，basis区分explicit/static，不称为运行轨迹。
- verifications[]: `{id,scope,result,observedAt,environment,claim,exclusions[],evidenceIds[]}`；scope限定static/offline/ui/simulated-install/runtime，保留产品/版本及未验边界。
- evidence[]: `{id,kind,title,sourceRef,audience,publication}`；sources[]为 `{id,title,path?,sha256?,authority,audience}`。
- flows[]: `{id,title,summary,featureIds[],steps:[{id,title,summary,entityId}],connections:[{from,to,label,kind}]}`；步骤ID独立于实体ID，kind为next/recovery/uncertain，支持unknown支路。
- views[]为 `{id,component,entityId,props}`，仅支持展示配置；不得携带HTML、脚本或执行operation。顶层另含schema、snapshotDate、audience及diagnostics。

公开投影为白名单，保留规范/实现/证据结论与明确限制，移除原始日志、机器身份、绝对路径及未审阅图片。公开sources保留ID/标题/authority/摘要，省略内部path；引用不得悬空。不执行任何源文档中的命令。来源变化时 `--check` 必须失败，提示重新生成。

## 组件契约

目录 `docs/presentation/contract-atlas`。原生Web Components消费同一份JSON：卡片、流程、Feature总览、Script七字段、证据标记；help/portfolio只改变信息密度与布局，不能改变事实或隐藏关键未验范围。通过属性/数据设置复用，事件只返回选中ID。

默认打开即可离线预览，无CDN、无外部字体。按总体→Feature→Script逐层导航，支持搜索、筛选、键盘与移动端；空数据、无匹配、非法JSON提供可恢复反馈。本地导入上限2MiB，实体总数<=500，边<=1500、流程<=30、每流程步骤<=40，文本长度及嵌套有限；验证成功才替换数据。文本按纯文本渲染，拒绝脚本URL、危险链接和HTML执行。导出当前视图的自包含纯SVG，含状态/证据边界，无脚本、外链或foreignObject。

## 原生设计契约

同一公开元数据生成新 `docs/design/contract-atlas/contract-atlas.figcraft`；必须含真实component、instance、可编辑文字与变量绑定，不能以位图/伪后缀冒充。使用本机FigCraft CLI真实生成、结构检查、渲染并目检。Figma输出正规Plugin目录，支持读入相同元数据并创建组件/实例；未在真实Figma运行则明确记录此限制，mock通过不能替代实际验收。保留所有既有稿件，不触碰外部当前画布。

## 验收

稳定提取/来源漂移、重复ID/悬空引用、缺七字段、状态升级、公开泄漏、恶意导入必须可被测试发现。真实浏览器验证两种视图、层级/搜索/导入/导出/键盘及390/768/1440宽度，检查无横向溢出与重叠；保存截图与结果。FigCraft以原生组件/实例/变量及渲染结果验收；Figma执行状态单列。作品集默认本地，不发布到外部站点。

## 主题与阅读深度修订 · 2026-11-01

阅读视图与颜色主题 SHALL 为独立状态。help/portfolio决定叙事和技术组织；light/dark/system只决定语义颜色。2026-11-01后续阅读修订将披露程度独立为summary/full中心状态，见features/contract-reading-disclosure.md；切阅读视图不再重置展开偏好。切换任一状态 MUST 保留实体、筛选、显式流程及其他状态，不能将作品集硬编码成深色或指南硬编码成浅色。

- 主题默认system，支持独立选择light/dark，偏好存于本机 `pmltwin.contract-atlas.theme`。system实时响应系统变化；显式偏好不被系统覆盖。关闭重开、同源新标签同步；非法记录回system。存储不可用时允许本次切换并显示偏好未保存。
- head同步脚本在样式及内容绘制前设置html的实际主题，避免默认主题闪烁。Shadow组件继承同一主题变量；SVG冻结导出时的实际颜色，并注明主题。选择主题不重绘业务图，避免丢失焦点与细节展开状态。
- tokens v2仅包含themes.light/dark，light/dark角色完全一致，含background/surface/soft/text/muted/border/accent/warning/boundary；生成CSS与原生设计读取同一来源。装饰border与重要图元boundary分离，文字及交互/图形边界分别核验对比度。
- 指南与作品集均提供完整目录、七字段及来源入口，默认以摘要阅读；“全部展开”显示所有已登记详情，“全部折叠”恢复摘要，局部状态单独保留。指南强调字段/规范，作品集强调任务/交付；实现范围、限制和证据exclusions始终可见。不能靠颜色差异冒充阅读视图实现。
- 所有已登记流程都有明确选择入口，可自动跟随实体或显式固定某流程；显式流程进入URL状态，切阅读模式或主题不丢失。没有独立流程时说明未登记。
- FigCraft/Figma变量模式名为Light/Dark，样本覆盖Help/Portfolio×Light/Dark四组合，文本事实保持一致。真实Figma运行状态继续单独记账。

验收包含2阅读视图×2实际主题×390/768/1440宽度，以及system动态、首屏预置、持久化/跨标签/存储失败、主题与阅读/选择正交、Shadow与SVG实际颜色、文字与重要图元对比度；以真实浏览器和原生设计证据验证。
