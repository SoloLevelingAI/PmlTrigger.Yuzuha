# 文档导航、共享设计与作品集视觉契约

## 总体：目录找到任务，正文理解价值

PMLTwin文档将项目、Feature、内部Script、实现方法与证据串成可追溯的阅读链。左侧是完整项目目录，中央是当前页，右侧是本页大小标题。选中Script后先读当前契约，再按需浏览流程、同级卡片和规范；不再先穿过大幅展示区和卡片。

作品集先呈现任务目标与交付，契约与实现视图先解释核对路径。目标和交付来自同一已校验数据，Script明确说明这些是所属Feature的目标，不冒充脚本单独完成整项业务。语言/角色是辅助信息；规范状态、实现范围和验证证据仍分别陈述。

美观仍是独立要求。OpenSpec的`contract-document-navigation.md`明确要求主次构图、任务叙事、排版层级、对齐/留白及克制配色，另有20.6视觉复核任务。分颜色、结构通过、无溢出都不能代替作品集视觉完成度。代理目检与用户认可分别记录；没有由测试推断用户已经满意。

## Feature：两个目录各有用途

左目录始终列出18个Feature和14个Script，另有项目概览。共享Script只设一个主目录入口，其他关系保留在原契约。搜索覆盖全部项目；正文里的Feature/Script分类只筛卡片，不删除侧栏页面。

右目录按实际正文顺序列主节、当前5/7字段、原需求/来源、已选方法组和附录分类。选择闭合详情里的标题，只打开所需祖先并聚焦目标。手动滚动时只高亮当前位置，不抢焦点。目标身份基于原section/method ID，不按标题文字猜测。

桌面三栏，左右sticky并各自滚动，中间自然滚动。中宽保留左目录，本页目录收至正文前；手机将两种目录收为原生折叠入口。选择后收起相应目录，Esc回入口；低频导入/导出收在“页面工具”。页面本身不横向溢出，长表和源码各自在容器内滚动。

`section=`与现有hash参数共同保存。刷新、返回可恢复明确目标；新修订按稳定章节与相对位置恢复阅读。失效目标回正文标题。全部折叠将子锚点降为父节，避免刷新又把刚折起的分支打开。独立导入、草稿和共同版本分开维护章节状态。

## 内部Script：元数据与设计token生成两端

| 单元 | 责任 | 回执与边界 |
|---|---|---|
| app.js | 左目录、正文顺序、URL/来源namespace及目标登记 | 不执行业务操作，失效章节回退 |
| page-outline.js | 穿Shadow收集明确key、必要展开、聚焦、scrollspy | 清理旧观察器/RAF，重复身份显式拒绝 |
| tokens.json | 六类区块、两套主题、正文/标题/辅助层级 | 分类色不是运行状态 |
| build-contract-atlas-theme.mjs | 从同源token生成CSS变量 | `--check`拒绝生成漂移和非法样式 |
| 原生设计适配 | 同一IR和token生成可编辑组件及文档板 | 真实FigCraft结构/渲染验收；Figma宿主另列 |

六角色是概览、契约、流程、实现方法、证据、规范与来源。CSS使用`--atlas-section-<role>-accent/surface`；正文/卡片/辅助/标题等使用`--atlas-type-<name>-size/weight/line-height`。正文16px，卡片14px，辅助至少12px；标题用字号和字重区分。正文保持中性色，区块色用于浅底/细边/图例，警告仍有独立语义。

FigCraft的Document-Light/Dark使用真实launch-workflow七字段、完整目录和本页标题。改稿突出任务标题/所属目标，用细分隔组织正文，六色改为紧凑图例。保留可编辑原生文字和变量，不以截图冒充设计对象，不声称静态板拥有Web跳转/折叠。

## 汇总：功能验证与视觉复核分别留证

阅读路径是项目目录 → Feature/Script目标 → 当前细约 → 方法/规范 → 来源和证据。美观通过实际渲染分别检查首屏焦点、层级、对齐/留白、文字舒适度、配色与叙事，检视不足须回改。

```powershell
node tests/contract-atlas-outline-browser.mjs
node tests/contract-atlas-document-browser.mjs
node --test tests/contract-atlas-design-tokens.test.mjs tests/contract-atlas-design.test.mjs
node scripts/serve-contract-atlas.mjs --watch --edit --port 19439
# 服务就绪后，在另一个终端只读核对真实方法、DocFX和原生稿：
node tests/contract-atlas-document-live-browser.mjs
```

验证汇总及实际渲染证据：`docs/validation/contract-document-2026-11-01.json`。原平台未完成项保持原账，Figma真实宿主仍未验收。
